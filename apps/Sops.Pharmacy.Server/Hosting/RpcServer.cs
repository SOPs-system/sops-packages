using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.Pharmacy.Server.Configuration;
using Sops.Pharmacy.Server.Domain.Auth;
using Sops.Pharmacy.Server.Domain.Catalog;
using Sops.Pharmacy.Server.Domain.Devices;
using Sops.Pharmacy.Server.Domain.Inventory;
using Sops.Pharmacy.Server.Domain.Sales;
using Sops.Pharmacy.Server.Domain.Shifts;
using Sops.Pharmacy.Server.Persistence;

namespace Sops.Pharmacy.Server.Hosting
{
    public class RpcServer : IDisposable
    {
        private readonly PharmacyServerSettings _settings;
        private readonly SqlCipherDatabaseProvider _dbProvider;
        private readonly IDrugCatalogService _drugCatalog;
        private readonly IInventoryService _inventory;
        private readonly ISalesService _sales;
        private readonly IShiftService _shifts;
        private readonly IDeviceService _devices;
        private readonly IAuthService _auth;
        private readonly SqlCipherOutboxStore _outboxStore;
        private readonly SqlCipherInboxStore _inboxStore;
        private readonly ILogger<RpcServer>? _logger;

        private TcpListener? _tcpListener;
        private CancellationTokenSource? _cts;
        private Task? _listenerTask;
        private readonly DateTime _startTime = DateTime.UtcNow;
        private bool _disposed;

        public bool IsRunning => _tcpListener != null;
        public TimeSpan Uptime => DateTime.UtcNow - _startTime;

        public RpcServer(
            PharmacyServerSettings settings,
            SqlCipherDatabaseProvider dbProvider,
            IDrugCatalogService drugCatalog,
            IInventoryService inventory,
            ISalesService sales,
            IShiftService shifts,
            IDeviceService devices,
            IAuthService auth,
            SqlCipherOutboxStore outboxStore,
            SqlCipherInboxStore inboxStore,
            ILogger<RpcServer>? logger = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _dbProvider = dbProvider ?? throw new ArgumentNullException(nameof(dbProvider));
            _drugCatalog = drugCatalog ?? throw new ArgumentNullException(nameof(drugCatalog));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _sales = sales ?? throw new ArgumentNullException(nameof(sales));
            _shifts = shifts ?? throw new ArgumentNullException(nameof(shifts));
            _devices = devices ?? throw new ArgumentNullException(nameof(devices));
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
            _inboxStore = inboxStore ?? throw new ArgumentNullException(nameof(inboxStore));
            _logger = logger;
        }

        public void Start()
        {
            if (IsRunning) return;

            _cts = new CancellationTokenSource();
            bool started = false;
            var ip = _settings.ListenOnAllInterfaces ? IPAddress.Any : IPAddress.Loopback;

            // Attempt configured port, falling back to adjacent ports if reserved by the operating system
            for (int offset = 0; offset <= 3; offset++)
            {
                int candidatePort = _settings.Port + offset;
                try
                {
                    _tcpListener = new TcpListener(ip, candidatePort);
                    _tcpListener.Start();
                    _settings.Port = candidatePort;
                    started = true;
                    _logger?.LogInformation("RPC Server listening on {Ip}:{Port} (PID: {Pid})", ip, candidatePort, System.Diagnostics.Process.GetCurrentProcess().Id);
                    _listenerTask = Task.Run(() => AcceptTcpClientsAsync(_cts.Token));
                    break;
                }
                catch (SocketException ex)
                {
                    _logger?.LogWarning("Binding to {Ip}:{Port} failed ({Error}). Attempting fallback...", ip, candidatePort, ex.Message);
                    try { _tcpListener?.Stop(); } catch { }
                    _tcpListener = null;

                    // Also try loopback specifically if 0.0.0.0 had permission restrictions
                    if (_settings.ListenOnAllInterfaces)
                    {
                        try
                        {
                            _tcpListener = new TcpListener(IPAddress.Loopback, candidatePort);
                            _tcpListener.Start();
                            _settings.Port = candidatePort;
                            started = true;
                            _logger?.LogInformation("RPC Server listening on {Ip}:{Port} (PID: {Pid})", IPAddress.Loopback, candidatePort, System.Diagnostics.Process.GetCurrentProcess().Id);
                            _listenerTask = Task.Run(() => AcceptTcpClientsAsync(_cts.Token));
                            break;
                        }
                        catch
                        {
                            try { _tcpListener?.Stop(); } catch { }
                            _tcpListener = null;
                        }
                    }
                }
            }

            if (!started)
            {
                var err = $"Could not start RPC TCP listener on port {_settings.Port} or adjacent ports.";
                _logger?.LogError(err);
                throw new InvalidOperationException(err);
            }
        }

        public void Stop()
        {
            if (!IsRunning) return;

            try
            {
                _cts?.Cancel();
                _tcpListener?.Stop();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error while stopping RPC server.");
            }
            finally
            {
                _tcpListener = null;
            }
        }

        private async Task AcceptTcpClientsAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _tcpListener != null)
            {
                try
                {
                    var client = await _tcpListener.AcceptTcpClientAsync().ConfigureAwait(false);
                    _ = Task.Run(() => ProcessHttpClientAsync(client, ct));
                }
                catch when (ct.IsCancellationRequested || _tcpListener == null)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error accepting RPC TCP client.");
                }
            }
        }

        private async Task ProcessHttpClientAsync(TcpClient client, CancellationToken ct)
        {
            using (client)
            using (var stream = client.GetStream())
            {
                try
                {
                    var reader = new StreamReader(stream, Encoding.UTF8);
                    var requestLine = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(requestLine)) return;

                    var parts = requestLine.Split(' ');
                    if (parts.Length < 2) return;

                    var method = parts[0].ToUpperInvariant();
                    var urlPath = parts[1];
                    var queryIndex = urlPath.IndexOf('?');
                    var path = (queryIndex >= 0 ? urlPath.Substring(0, queryIndex) : urlPath).ToLowerInvariant();

                    var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    string? line;
                    while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync().ConfigureAwait(false)))
                    {
                        var colIdx = line.IndexOf(':');
                        if (colIdx > 0)
                        {
                            var hName = line.Substring(0, colIdx).Trim();
                            var hVal = line.Substring(colIdx + 1).Trim();
                            headers[hName] = hVal;
                        }
                    }

                    if (method == "OPTIONS")
                    {
                        await WriteResponseRawAsync(stream, 204, "No Content", "{}").ConfigureAwait(false);
                        return;
                    }

                    string body = string.Empty;
                    if (headers.TryGetValue("Content-Length", out var clStr) && int.TryParse(clStr, out var contentLength) && contentLength > 0)
                    {
                        var buffer = new char[contentLength];
                        int readTotal = 0;
                        while (readTotal < contentLength)
                        {
                            int read = await reader.ReadAsync(buffer, readTotal, contentLength - readTotal).ConfigureAwait(false);
                            if (read <= 0) break;
                            readTotal += read;
                        }
                        body = new string(buffer, 0, readTotal);
                    }

                    var clientIp = ((IPEndPoint?)client.Client.RemoteEndPoint)?.Address.ToString() ?? "127.0.0.1";
                    await RouteRequestAsync(stream, method, path, headers, body, clientIp, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error processing HTTP client request.");
                }
            }
        }

        private async Task RouteRequestAsync(
            NetworkStream stream,
            string method,
            string path,
            Dictionary<string, string> headers,
            string body,
            string clientIp,
            CancellationToken ct)
        {
            // 1. Health & Info
            if (path == "/health" || path == "/rpc/health")
            {
                var dbHealth = await _dbProvider.CheckHealthAsync(ct).ConfigureAwait(false);
                var healthStatus = new
                {
                    status = dbHealth.IsHealthy ? "Healthy" : "Unhealthy",
                    database = new { isHealthy = dbHealth.IsHealthy, description = dbHealth.Description, latencyMs = dbHealth.Latency.TotalMilliseconds },
                    uptime = Uptime.ToString(@"d\.hh\:mm\:ss"),
                    port = _settings.Port,
                    mode = _settings.OperatingMode,
                    pharmacyId = _settings.PharmacyId,
                    timestamp = DateTime.UtcNow
                };
                await WriteJsonResponseAsync(stream, dbHealth.IsHealthy ? 200 : 503, dbHealth.IsHealthy ? "OK" : "Service Unavailable", healthStatus).ConfigureAwait(false);
                return;
            }

            if (path == "/rpc/info")
            {
                await WriteJsonResponseAsync(stream, 200, "OK", new
                {
                    isSuccess = true,
                    data = new
                    {
                        serverName = "SOPs Pharmacy Local Server",
                        version = "1.0.0",
                        operatingMode = _settings.OperatingMode,
                        port = _settings.Port,
                        pharmacyId = _settings.PharmacyId,
                        branchName = _settings.BranchName,
                        databaseEngine = "SQLCipher 4.x (AES-256)",
                        uptime = Uptime.ToString()
                    }
                }).ConfigureAwait(false);
                return;
            }

            using var doc = !string.IsNullOrWhiteSpace(body) ? JsonDocument.Parse(body) : null;
            var root = doc?.RootElement;

            // 2. Auth Routes
            if (path == "/rpc/auth/login")
            {
                var username = root?.TryGetProperty("username", out var u) == true ? u.GetString() ?? "" : "";
                var password = root?.TryGetProperty("password", out var p) == true ? p.GetString() ?? "" : "";
                var result = await _auth.LoginAsync(username, password, clientIp, ct).ConfigureAwait(false);

                if (result.Success)
                {
                    await WriteJsonResponseAsync(stream, 200, "OK", new { isSuccess = true, data = result }).ConfigureAwait(false);
                }
                else
                {
                    await WriteJsonResponseAsync(stream, 401, "Unauthorized", new { isSuccess = false, error = new { code = "INVALID_CREDENTIALS", message = result.ErrorMessage } }).ConfigureAwait(false);
                }
                return;
            }

            if (path == "/rpc/auth/logout")
            {
                await WriteJsonResponseAsync(stream, 200, "OK", new { isSuccess = true, data = new { message = "Logged out successfully" } }).ConfigureAwait(false);
                return;
            }

            if (path == "/api/auth/me")
            {
                headers.TryGetValue("Authorization", out var authHeader);
                var token = authHeader?.StartsWith("Bearer ") == true ? authHeader.Substring(7).Trim() : "";
                var user = await _auth.ValidateTokenAsync(token, ct).ConfigureAwait(false);

                if (user != null)
                {
                    await WriteJsonResponseAsync(stream, 200, "OK", new { isSuccess = true, data = user }).ConfigureAwait(false);
                }
                else
                {
                    await WriteJsonResponseAsync(stream, 401, "Unauthorized", new { isSuccess = false, error = new { code = "UNAUTHORIZED", message = "Invalid or expired session token." } }).ConfigureAwait(false);
                }
                return;
            }

            // 3. Drug Catalog Routes
            if (path == "/rpc/drugs/searchdrugs")
            {
                var query = root?.TryGetProperty("query", out var q) == true ? q.GetString() : null;
                var limit = root?.TryGetProperty("limit", out var l) == true ? l.GetInt32() : 50;
                var list = await _drugCatalog.SearchDrugsAsync(query, limit, ct).ConfigureAwait(false);
                await WriteJsonResponseAsync(stream, 200, "OK", new { isSuccess = true, data = list }).ConfigureAwait(false);
                return;
            }

            if (path == "/rpc/drugs/getdrugbyid")
            {
                var id = root?.TryGetProperty("id", out var i) == true ? i.GetString() ?? "" : "";
                var drug = await _drugCatalog.GetByIdAsync(id, ct).ConfigureAwait(false);
                if (drug != null)
                    await WriteJsonResponseAsync(stream, 200, "OK", new { isSuccess = true, data = drug }).ConfigureAwait(false);
                else
                    await WriteJsonResponseAsync(stream, 404, "Not Found", new { isSuccess = false, error = new { code = "DRUG_NOT_FOUND", message = $"Drug '{id}' was not found." } }).ConfigureAwait(false);
                return;
            }

            if (path == "/rpc/drugs/findbybarcode")
            {
                var barcode = root?.TryGetProperty("barcode", out var b) == true ? b.GetString() ?? "" : "";
                var drug = await _drugCatalog.FindByBarcodeAsync(barcode, ct).ConfigureAwait(false);
                if (drug != null)
                    await WriteJsonResponseAsync(stream, 200, "OK", new { isSuccess = true, data = drug }).ConfigureAwait(false);
                else
                    await WriteJsonResponseAsync(stream, 404, "Not Found", new { isSuccess = false, error = new { code = "DRUG_NOT_FOUND", message = $"Barcode '{barcode}' was not found." } }).ConfigureAwait(false);
                return;
            }

            if (path == "/rpc/drugs/findbysku")
            {
                var sku = root?.TryGetProperty("sku", out var s) == true ? s.GetString() ?? "" : "";
                var drug = await _drugCatalog.FindBySkuAsync(sku, ct).ConfigureAwait(false);
                if (drug != null)
                    await WriteJsonResponseAsync(stream, 200, "OK", new { isSuccess = true, data = drug }).ConfigureAwait(false);
                else
                    await WriteJsonResponseAsync(stream, 404, "Not Found", new { isSuccess = false, error = new { code = "DRUG_NOT_FOUND", message = $"SKU '{sku}' was not found." } }).ConfigureAwait(false);
                return;
            }

            // 4. Inventory Routes
            if (path == "/rpc/inventory/getstocksummary")
            {
                var summary = await _inventory.GetStockSummaryAsync(ct).ConfigureAwait(false);
                await WriteJsonResponseAsync(stream, 200, "OK", new { isSuccess = true, data = summary }).ConfigureAwait(false);
                return;
            }

            if (path == "/rpc/inventory/getlotsfordrug")
            {
                var drugId = root?.TryGetProperty("drugId", out var di) == true ? di.GetString() ?? "" : "";
                var lots = await _inventory.GetLotsForDrugAsync(drugId, ct).ConfigureAwait(false);
                await WriteJsonResponseAsync(stream, 200, "OK", new { isSuccess = true, data = lots }).ConfigureAwait(false);
                return;
            }

            if (path == "/rpc/inventory/adjustlot")
            {
                var lotId = root?.TryGetProperty("lotId", out var li) == true ? li.GetString() ?? "" : "";
                var newQty = root?.TryGetProperty("newQuantity", out var nq) == true ? nq.GetDecimal() : 0m;
                var reason = root?.TryGetProperty("reason", out var rs) == true ? rs.GetString() ?? "Manual adjustment" : "Manual adjustment";
                var updated = await _inventory.AdjustLotAsync(lotId, newQty, reason, null, ct).ConfigureAwait(false);
                var ok = updated != null;
                await WriteJsonResponseAsync(stream, ok ? 200 : 400, ok ? "OK" : "Bad Request", new { isSuccess = ok, data = updated }).ConfigureAwait(false);
                return;
            }

            // 5. Sales & Shifts
            if (path == "/rpc/sales/createsaledraft")
            {
                var patientId = root?.TryGetProperty("patientId", out var pi) == true ? pi.GetString() : null;
                var reason = root?.TryGetProperty("reason", out var rs) == true ? rs.GetString() : null;
                var draft = await _sales.CreateDraftAsync(patientId, reason).ConfigureAwait(false);
                await WriteJsonResponseAsync(stream, 200, "OK", new { isSuccess = true, data = draft }).ConfigureAwait(false);
                return;
            }

            if (path == "/rpc/sales/addsaleline")
            {
                var draftId = root?.TryGetProperty("draftId", out var did) == true ? did.GetString() ?? "" : "";
                var drugId = root?.TryGetProperty("drugId", out var di) == true ? di.GetString() ?? "" : "";
                var qty = root?.TryGetProperty("quantity", out var q) == true ? q.GetDecimal() : 1m;

                var draft = await _sales.AddLineAsync(draftId, drugId, qty, 1, ct).ConfigureAwait(false);
                await WriteJsonResponseAsync(stream, 200, "OK", new { isSuccess = true, data = draft }).ConfigureAwait(false);
                return;
            }

            if (path == "/rpc/sales/addpayment")
            {
                var draftId = root?.TryGetProperty("draftId", out var did) == true ? did.GetString() ?? "" : "";
                var methodCode = root?.TryGetProperty("method", out var m) == true ? m.GetString() ?? "Cash" : "Cash";
                var amountMinor = root?.TryGetProperty("amountMinor", out var a) == true ? a.GetInt64() : 0L;

                var draft = await _sales.AddPaymentAsync(draftId, methodCode, amountMinor).ConfigureAwait(false);
                await WriteJsonResponseAsync(stream, 200, "OK", new { isSuccess = true, data = draft }).ConfigureAwait(false);
                return;
            }

            if (path == "/rpc/sales/completetransaction")
            {
                var draftId = root?.TryGetProperty("draftId", out var did) == true ? did.GetString() ?? "" : "";
                var terminalId = root?.TryGetProperty("terminalId", out var ti) == true ? ti.GetString() ?? "POS-01" : "POS-01";
                var cashierId = root?.TryGetProperty("cashierUserId", out var cu) == true ? cu.GetString() ?? "cashier1" : "cashier1";
                var shiftId = root?.TryGetProperty("shiftId", out var si) == true ? si.GetString() ?? "shift_01" : "shift_01";
                var idempotencyKey = root?.TryGetProperty("idempotencyKey", out var ik) == true ? ik.GetString() : null;

                var tx = await _sales.CompleteTransactionAsync(draftId, _settings.PharmacyId, terminalId, cashierId, shiftId, idempotencyKey, ct).ConfigureAwait(false);
                await WriteJsonResponseAsync(stream, 200, "OK", new { isSuccess = true, data = tx }).ConfigureAwait(false);
                return;
            }

            if (path == "/rpc/sales/listtransactions")
            {
                var list = await _sales.ListRecentTransactionsAsync(0, 100, ct).ConfigureAwait(false);
                await WriteJsonResponseAsync(stream, 200, "OK", new { isSuccess = true, data = list }).ConfigureAwait(false);
                return;
            }

            if (path == "/rpc/shifts/openshift")
            {
                var termId = root?.TryGetProperty("terminalId", out var ti) == true ? ti.GetString() ?? "POS-01" : "POS-01";
                var cashierId = root?.TryGetProperty("cashierUserId", out var cu) == true ? cu.GetString() ?? "cashier1" : "cashier1";
                var openingCash = root?.TryGetProperty("openingCashMinor", out var oc) == true ? oc.GetInt64() : 50000L;

                var shift = await _shifts.OpenShiftAsync(_settings.PharmacyId, termId, cashierId, openingCash, ct).ConfigureAwait(false);
                await WriteJsonResponseAsync(stream, 200, "OK", new { isSuccess = true, data = shift }).ConfigureAwait(false);
                return;
            }

            if (path == "/rpc/shifts/closeshift")
            {
                var shiftId = root?.TryGetProperty("shiftId", out var si) == true ? si.GetString() ?? "" : "";
                var closingCash = root?.TryGetProperty("closingCashMinor", out var cc) == true ? cc.GetInt64() : 0L;

                var shift = await _shifts.CloseShiftAsync(shiftId, closingCash, ct).ConfigureAwait(false);
                await WriteJsonResponseAsync(stream, 200, "OK", new { isSuccess = true, data = shift }).ConfigureAwait(false);
                return;
            }

            // 6. Generic POS Support
            if (path == "/rpc/categories/listcategories")
            {
                var categories = new[]
                {
                    new { id = "cat_analgesics", name = "Analgesics & Antipyretics", nameAr = "مسكنات وخافضات حرارة" },
                    new { id = "cat_antibiotics", name = "Antibiotics & Antivirals", nameAr = "مضادات حيوية وفيروسية" },
                    new { id = "cat_respiratory", name = "Respiratory & Allergy", nameAr = "أدوية تنفسية وحساسية" },
                    new { id = "cat_gastro", name = "Gastrointestinal", nameAr = "أدوية الجهاز الهضمي" },
                    new { id = "cat_vitamins", name = "Vitamins & Supplements", nameAr = "فيتامينات ومكملات غذائية" }
                };
                await WriteJsonResponseAsync(stream, 200, "OK", new { isSuccess = true, data = categories }).ConfigureAwait(false);
                return;
            }

            if (path == "/rpc/warehouses/listwarehouses")
            {
                var warehouses = new[]
                {
                    new { id = "loc_dispensary", name = "Main Dispensary Front", isPrimary = true },
                    new { id = "loc_backstore", name = "Back Storeroom", isPrimary = false },
                    new { id = "loc_fridge", name = "Biologicals Refrigerator (2-8°C)", isPrimary = false }
                };
                await WriteJsonResponseAsync(stream, 200, "OK", new { isSuccess = true, data = warehouses }).ConfigureAwait(false);
                return;
            }

            if (path == "/rpc/settings/getsettings")
            {
                await WriteJsonResponseAsync(stream, 200, "OK", new
                {
                    isSuccess = true,
                    data = new
                    {
                        pharmacyId = _settings.PharmacyId,
                        branchName = _settings.BranchName,
                        currency = "EGP",
                        taxRate = 0.0m,
                        autoBackupHours = _settings.BackupIntervalHours
                    }
                }).ConfigureAwait(false);
                return;
            }

            // 7. Sync Endpoints
            if (path == "/rpc/sync/status")
            {
                var pendingOutbox = await _outboxStore.GetPendingCountAsync(ct).ConfigureAwait(false);
                var pendingInbox = await _inboxStore.GetPendingCountAsync(ct).ConfigureAwait(false);
                await WriteJsonResponseAsync(stream, 200, "OK", new
                {
                    isSuccess = true,
                    data = new
                    {
                        status = "Healthy",
                        pendingOutboxCount = pendingOutbox,
                        pendingInboxCount = pendingInbox,
                        lastSyncAt = DateTime.UtcNow.AddMinutes(-2),
                        isCloudReachable = false
                    }
                }).ConfigureAwait(false);
                return;
            }

            // 8. Management Console Endpoints
            if (path == "/rpc/management/status")
            {
                var proc = System.Diagnostics.Process.GetCurrentProcess();
                var pendingOutbox = await _outboxStore.GetPendingCountAsync(ct).ConfigureAwait(false);
                var pendingInbox = await _inboxStore.GetPendingCountAsync(ct).ConfigureAwait(false);
                var terminals = await _devices.GetRegisteredTerminalsAsync(ct).ConfigureAwait(false);
                var dbFile = new FileInfo(_settings.DatabasePath);

                await WriteJsonResponseAsync(stream, 200, "OK", new
                {
                    isSuccess = true,
                    data = new
                    {
                        serverName = "SOPs Pharmacy Local Server",
                        version = "1.0.0",
                        uptime = Uptime.ToString(@"d\.hh\:mm\:ss"),
                        uptimeTotalSeconds = Uptime.TotalSeconds,
                        activeTerminalsCount = terminals.Count,
                        databaseSizeBytes = dbFile.Exists ? dbFile.Length : 0,
                        databasePath = _settings.DatabasePath,
                        memoryUsageMb = proc.WorkingSet64 / (1024 * 1024),
                        port = _settings.Port,
                        pendingOutbox = pendingOutbox,
                        pendingInbox = pendingInbox
                    }
                }).ConfigureAwait(false);
                return;
            }

            if (path == "/rpc/management/terminals")
            {
                var terminals = await _devices.GetRegisteredTerminalsAsync(ct).ConfigureAwait(false);
                await WriteJsonResponseAsync(stream, 200, "OK", new { isSuccess = true, data = terminals }).ConfigureAwait(false);
                return;
            }

            if (path == "/rpc/management/terminals/approve")
            {
                var id = root?.TryGetProperty("id", out var i) == true ? i.GetString() ?? "" : "";
                var ok = await _devices.ApproveTerminalAsync(id, ct).ConfigureAwait(false);
                await WriteJsonResponseAsync(stream, 200, "OK", new { isSuccess = ok }).ConfigureAwait(false);
                return;
            }

            if (path == "/rpc/management/terminals/revoke")
            {
                var id = root?.TryGetProperty("id", out var i) == true ? i.GetString() ?? "" : "";
                var ok = await _devices.RevokeTerminalAsync(id, ct).ConfigureAwait(false);
                await WriteJsonResponseAsync(stream, 200, "OK", new { isSuccess = ok }).ConfigureAwait(false);
                return;
            }

            if (path == "/rpc/management/logs")
            {
                var logs = await _auth.GetRecentAuditEventsAsync(100, ct).ConfigureAwait(false);
                await WriteJsonResponseAsync(stream, 200, "OK", new { isSuccess = true, data = logs }).ConfigureAwait(false);
                return;
            }

            if (path == "/rpc/management/users")
            {
                var users = await _auth.ListUsersAsync(ct).ConfigureAwait(false);
                await WriteJsonResponseAsync(stream, 200, "OK", new { isSuccess = true, data = users }).ConfigureAwait(false);
                return;
            }

            if (path == "/rpc/management/backup/history")
            {
                var backupDir = _settings.BackupDirectory;
                var list = new List<object>();
                if (Directory.Exists(backupDir))
                {
                    foreach (var file in Directory.GetFiles(backupDir, "*.bak"))
                    {
                        var fi = new FileInfo(file);
                        list.Add(new
                        {
                            fileName = fi.Name,
                            fullPath = fi.FullName,
                            sizeBytes = fi.Length,
                            createdAt = fi.CreationTimeUtc
                        });
                    }
                }
                await WriteJsonResponseAsync(stream, 200, "OK", new { isSuccess = true, data = list }).ConfigureAwait(false);
                return;
            }

            // 404 Fallback
            await WriteJsonResponseAsync(stream, 404, "Not Found", new
            {
                isSuccess = false,
                error = new { code = "NOT_FOUND", message = $"Endpoint '{path}' not found on SOPs Pharmacy Local Server." }
            }).ConfigureAwait(false);
        }

        private static async Task WriteJsonResponseAsync(NetworkStream stream, int statusCode, string statusText, object data)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
            var json = JsonSerializer.Serialize(data, options);
            await WriteResponseRawAsync(stream, statusCode, statusText, json).ConfigureAwait(false);
        }

        private static async Task WriteResponseRawAsync(NetworkStream stream, int statusCode, string statusText, string json)
        {
            var bodyBytes = Encoding.UTF8.GetBytes(json);
            var header = $"HTTP/1.1 {statusCode} {statusText}\r\n" +
                         "Content-Type: application/json; charset=utf-8\r\n" +
                         $"Content-Length: {bodyBytes.Length}\r\n" +
                         "Access-Control-Allow-Origin: *\r\n" +
                         "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
                         "Access-Control-Allow-Headers: Content-Type, Authorization, X-Correlation-ID\r\n" +
                         "Connection: close\r\n\r\n";

            var headerBytes = Encoding.ASCII.GetBytes(header);
            await stream.WriteAsync(headerBytes, 0, headerBytes.Length).ConfigureAwait(false);
            if (bodyBytes.Length > 0)
            {
                await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length).ConfigureAwait(false);
            }
            await stream.FlushAsync().ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _cts?.Dispose();
        }
    }
}
