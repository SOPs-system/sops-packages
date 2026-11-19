using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sops.DataLayer.Authentication;
using Sops.DataLayer.Configuration;
using Sops.DataLayer.Endpoints;
using Sops.DataLayer.Events;
using Sops.DataLayer.Models.Authentication;
using Sops.DataLayer.Models.Events;
using Sops.DataLayer.Models.Requests;
using Sops.DataLayer.Models.Responses;
using Sops.DataLayer.Transport;
using Xunit;

namespace Sops.DataLayer.Tests
{
    public class ServerContractIntegrationTests
    {
        // Models matching SOPs Server contracts (D:\SOPs\server)
        public class ServerPagedResult<T>
        {
            public List<T> Items { get; set; } = new List<T>();
            public int TotalCount { get; set; }
            public int Page { get; set; }
            public int PageSize { get; set; }
        }

        public class ServerDrug
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Barcode { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public int Stock { get; set; }
        }

        public class ServerTransaction
        {
            public string Id { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public decimal Total { get; set; }
            public string? TerminalId { get; set; }
            public string? ShiftId { get; set; }
        }

        public class ServerShift
        {
            public string Id { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public decimal OpeningBalance { get; set; }
        }

        [Fact]
        public async Task RpcTransport_Drug_GetPage_Contract_Serializes_And_Deserializes()
        {
            string capturedBody = string.Empty;
            var handler = new TestHttpHandler(async (req, ct) =>
            {
                if (req.Content != null)
                {
                    capturedBody = await req.Content.ReadAsStringAsync();
                }
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"result\":{\"items\":[{\"id\":\"d-101\",\"name\":\"Panadol Extra 500mg\",\"barcode\":\"62210001\",\"price\":45.5,\"stock\":120}],\"totalCount\":1,\"page\":1,\"pageSize\":20}}",
                        Encoding.UTF8,
                        "application/json")
                };
                return response;
            });

            var httpClient = new HttpClient(handler);
            var store = new SecureCredentialStore("test_contract_entropy");
            var sessionMgr = new SessionManager(store);
            var transport = new RpcTransport(httpClient, sessionMgr);

            var endpoint = new EndpointDefinition(
                "drug.getpage",
                BackendTarget.LocalServer,
                TransportMode.Rpc,
                "POST",
                "/rpc/Drug/GetPage",
                rpcService: "Drug",
                rpcMethod: "GetPage");

            var requestBody = new { filter = "panadol", page = 1, pageSize = 20 };
            var request = new DataRequest("drug.getpage", requestBody, context: new RequestContext());
            var uri = new Uri("http://127.0.0.1:5000/rpc/Drug/GetPage");

            var result = await transport.SendAsync<ServerPagedResult<ServerDrug>>(endpoint, request, uri, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(1, result.Data.TotalCount);
            Assert.Single(result.Data.Items);
            Assert.Equal("Panadol Extra 500mg", result.Data.Items[0].Name);
            Assert.Equal(45.5m, result.Data.Items[0].Price);
            Assert.Contains("\"filter\":\"panadol\"", capturedBody);
        }

        [Fact]
        public async Task RpcTransport_Drug_FindByBarcode_Contract_Serializes_And_Deserializes()
        {
            var handler = new TestHttpHandler((req, ct) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"result\":{\"id\":\"d-202\",\"name\":\"Amoxicillin 500mg\",\"barcode\":\"62299999\",\"price\":60.0,\"stock\":50}}",
                        Encoding.UTF8,
                        "application/json")
                };
                return Task.FromResult(response);
            });

            var httpClient = new HttpClient(handler);
            var store = new SecureCredentialStore("test_contract_entropy");
            var sessionMgr = new SessionManager(store);
            var transport = new RpcTransport(httpClient, sessionMgr);

            var endpoint = new EndpointDefinition(
                "drug.findbybarcode",
                BackendTarget.LocalServer,
                TransportMode.Rpc,
                "POST",
                "/rpc/Drug/FindByBarcode",
                rpcService: "Drug",
                rpcMethod: "FindByBarcode");

            var request = new DataRequest("drug.findbybarcode", new { barcode = "62299999" }, context: new RequestContext());
            var uri = new Uri("http://127.0.0.1:5000/rpc/Drug/FindByBarcode");

            var result = await transport.SendAsync<ServerDrug>(endpoint, request, uri, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal("d-202", result.Data.Id);
            Assert.Equal("62299999", result.Data.Barcode);
            Assert.Equal(60.0m, result.Data.Price);
        }

        [Fact]
        public async Task RpcTransport_Transaction_CreateSaleDraft_And_Complete_Contract()
        {
            var handler = new TestHttpHandler((req, ct) =>
            {
                string json;
                if (req.RequestUri != null && req.RequestUri.AbsolutePath.Contains("CreateSaleDraft"))
                {
                    json = "{\"result\":{\"id\":\"tx-777\",\"status\":\"Draft\",\"total\":0.0,\"terminalId\":\"T-01\",\"shiftId\":\"S-01\"}}";
                }
                else
                {
                    json = "{\"result\":{\"id\":\"tx-777\",\"status\":\"Completed\",\"total\":150.75,\"terminalId\":\"T-01\",\"shiftId\":\"S-01\"}}";
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });
            });

            var httpClient = new HttpClient(handler);
            var store = new SecureCredentialStore("test_contract_entropy");
            var sessionMgr = new SessionManager(store);
            var transport = new RpcTransport(httpClient, sessionMgr);

            // Draft
            var draftEndpoint = new EndpointDefinition("sales.createdraft", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/Transaction/CreateSaleDraft");
            var draftResult = await transport.SendAsync<ServerTransaction>(
                draftEndpoint,
                new DataRequest("sales.createdraft", new { patientId = "P-1", reason = "OTC", shiftId = "S-01", terminalId = "T-01" }, context: new RequestContext()),
                new Uri("http://127.0.0.1:5000/rpc/Transaction/CreateSaleDraft"),
                CancellationToken.None);

            Assert.True(draftResult.IsSuccess);
            Assert.Equal("Draft", draftResult.Data?.Status);

            // Complete
            var completeEndpoint = new EndpointDefinition(
                "sales.complete",
                BackendTarget.LocalServer,
                TransportMode.Rpc,
                "POST",
                "/rpc/Transaction/Complete",
                isFinancialMutation: true,
                requiresIdempotencyKey: true);

            var completeResult = await transport.SendAsync<ServerTransaction>(
                completeEndpoint,
                new DataRequest("sales.complete", new { txId = "tx-777" }, context: new RequestContext(idempotencyKey: "idemp-001")),
                new Uri("http://127.0.0.1:5000/rpc/Transaction/Complete"),
                CancellationToken.None);

            Assert.True(completeResult.IsSuccess);
            Assert.Equal("Completed", completeResult.Data?.Status);
            Assert.Equal(150.75m, completeResult.Data?.Total);
        }

        [Fact]
        public async Task RpcTransport_Shift_OpenShift_Contract()
        {
            var handler = new TestHttpHandler((req, ct) =>
            {
                var json = "{\"result\":{\"id\":\"shift-88\",\"status\":\"Open\",\"openingBalance\":500.0}}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });
            });

            var httpClient = new HttpClient(handler);
            var store = new SecureCredentialStore("test_contract_entropy");
            var sessionMgr = new SessionManager(store);
            var transport = new RpcTransport(httpClient, sessionMgr);

            var endpoint = new EndpointDefinition("shift.openshift", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/Shift/OpenShift");
            var result = await transport.SendAsync<ServerShift>(
                endpoint,
                new DataRequest("shift.openshift", new { openingBalance = 500.0 }, context: new RequestContext()),
                new Uri("http://127.0.0.1:5000/rpc/Shift/OpenShift"),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal("shift-88", result.Data.Id);
            Assert.Equal(500.0m, result.Data.OpeningBalance);
        }

        [Fact]
        public async Task RpcTransport_Parses_Real_Server_Error_Envelope()
        {
            var handler = new TestHttpHandler((req, ct) =>
            {
                // Server returns 404 with {"error": {"code": "not_found", "message": "Drug not found"}}
                var json = "{\"error\":{\"code\":\"not_found\",\"message\":\"Drug with barcode 999999 was not found in catalog.\"}}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });
            });

            var httpClient = new HttpClient(handler);
            var store = new SecureCredentialStore("test_contract_entropy");
            var sessionMgr = new SessionManager(store);
            var transport = new RpcTransport(httpClient, sessionMgr);

            var endpoint = new EndpointDefinition("drug.findbybarcode", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/Drug/FindByBarcode");
            var result = await transport.SendAsync<ServerDrug>(
                endpoint,
                new DataRequest("drug.findbybarcode", new { barcode = "999999" }, context: new RequestContext()),
                new Uri("http://127.0.0.1:5000/rpc/Drug/FindByBarcode"),
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(DataOutcome.NotFound, result.Outcome);
            Assert.Equal("not_found", result.Error?.Code);
            Assert.Contains("999999 was not found", result.Error?.Message);
        }

        [Fact]
        public async Task Live_Loopback_Http_RPC_And_SSE_Server_EndToEnd_Verification()
        {
            var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            string baseUri = $"http://127.0.0.1:{port}/";

            var cts = new CancellationTokenSource();

            // Background TCP HTTP server handling both RPC and SSE
            var serverTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    System.Net.Sockets.TcpClient client;
                    try
                    {
                        client = await listener.AcceptTcpClientAsync();
                    }
                    catch
                    {
                        break;
                    }

                    _ = Task.Run(async () =>
                    {
                        var utf8NoBom = new UTF8Encoding(false);
                        using (client)
                        using (var stream = client.GetStream())
                        using (var reader = new StreamReader(stream, utf8NoBom, false, 1024, leaveOpen: true))
                        using (var writer = new StreamWriter(stream, utf8NoBom, 1024, leaveOpen: true))
                        {
                            string? requestLine = await reader.ReadLineAsync();
                            if (string.IsNullOrEmpty(requestLine)) return;

                            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            string? headerLine;
                            while (!string.IsNullOrEmpty(headerLine = await reader.ReadLineAsync()))
                            {
                                int colon = headerLine.IndexOf(':');
                                if (colon > 0)
                                {
                                    headers[headerLine.Substring(0, colon).Trim()] = headerLine.Substring(colon + 1).Trim();
                                }
                            }

                            if (requestLine.StartsWith("POST /rpc/Drug/FindByBarcode", StringComparison.OrdinalIgnoreCase))
                            {
                                // Validate Authorization header
                                headers.TryGetValue("Authorization", out var authHeader);
                                bool authorized = !string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ");

                                if (!authorized)
                                {
                                    byte[] unauthBody = Encoding.UTF8.GetBytes("{\"error\":{\"code\":\"unauthorized\",\"message\":\"Token missing\"}}");
                                    await writer.WriteAsync($"HTTP/1.1 401 Unauthorized\r\nContent-Type: application/json\r\nContent-Length: {unauthBody.Length}\r\nConnection: close\r\n\r\n");
                                    await writer.FlushAsync();
                                    await stream.WriteAsync(unauthBody, 0, unauthBody.Length);
                                    return;
                                }

                                byte[] resBody = Encoding.UTF8.GetBytes("{\"result\":{\"id\":\"live-drug-1\",\"name\":\"Panadol Extra\",\"barcode\":\"622000001\",\"price\":45.0,\"stock\":10}}");
                                await writer.WriteAsync($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {resBody.Length}\r\nConnection: close\r\n\r\n");
                                await writer.FlushAsync();
                                await stream.WriteAsync(resBody, 0, resBody.Length);
                            }
                            else if (requestLine.StartsWith("GET /api/live", StringComparison.OrdinalIgnoreCase))
                            {
                                await writer.WriteAsync("HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n");
                                await writer.FlushAsync();

                                await writer.WriteAsync("retry: 1000\n\n");
                                await writer.FlushAsync();

                                await writer.WriteAsync("event: ready\ndata: {\"status\":\"online\",\"version\":\"1.0.0\"}\n\n");
                                await writer.FlushAsync();

                                await writer.WriteAsync(": ping\n\n");
                                await writer.FlushAsync();

                                await writer.WriteAsync("event: snapshot\ndata: {\"topic\":\"kpis\",\"activeUsers\":3}\n\n");
                                await writer.FlushAsync();
                            }
                            else
                            {
                                await writer.WriteAsync("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                                await writer.FlushAsync();
                            }
                        }
                    });
                }
            });

            try
            {
                // 1. Verify Live RPC call over HTTP with real Bearer Token
                var store = new SecureCredentialStore("test_entropy");
                var sessionMgr = new SessionManager(store);
                var session = new SessionInfo
                {
                    SessionId = "sess-01",
                    StaffId = "user-1",
                    Username = "user@sops.eg",
                    TerminalId = "T-01",
                    ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(8)
                };
                sessionMgr.SetSession(new LoginResponse
                {
                    AccessToken = "valid-local-token-xyz",
                    Session = session,
                    Roles = new[] { "Pharmacist" }
                });

                using (var httpHandler = new HttpClientHandler { UseProxy = false })
                using (var httpClient = new HttpClient(httpHandler))
                {
                    var transport = new RpcTransport(httpClient, sessionMgr);
                    var endpoint = new EndpointDefinition(
                        "drug.findbybarcode",
                        BackendTarget.LocalServer,
                        TransportMode.Rpc,
                        "POST",
                        "/rpc/Drug/FindByBarcode",
                        requiresAuthentication: true);

                    var rpcUri = new Uri($"{baseUri}rpc/Drug/FindByBarcode");
                    var request = new DataRequest("drug.findbybarcode", new { barcode = "622000001" }, context: new RequestContext());

                    var rpcResult = await transport.SendAsync<ServerDrug>(endpoint, request, rpcUri, CancellationToken.None);

                    if (!rpcResult.IsSuccess)
                    {
                        throw new Exception($"RPC Failed: Outcome={rpcResult.Outcome}, Code={rpcResult.Error?.Code}, Msg={rpcResult.Error?.Message}, Status={rpcResult.Metadata?.StatusCode}");
                    }
                    Assert.True(rpcResult.IsSuccess);
                    Assert.NotNull(rpcResult.Data);
                    Assert.Equal("live-drug-1", rpcResult.Data!.Id);
                    Assert.Equal("Panadol Extra", rpcResult.Data.Name);
                    Assert.Equal(45.0m, rpcResult.Data.Price);
                    Assert.NotNull(rpcResult.Metadata);
                    Assert.Equal(DataSourceOrigin.LocalServer, rpcResult.Metadata!.Source);
                    Assert.Equal(200, rpcResult.Metadata.StatusCode);
                }

                // 2. Verify Live SSE `/api/live` stream parsing
                using (var sseHttpHandler = new HttpClientHandler { UseProxy = false })
                using (var httpClient = new HttpClient(sseHttpHandler))
                {
                    var eventBus = new DataLayerEventBus();
                    var registry = new EndpointRegistry();
                    var options = new DataLayerOptions { LocalServerAddress = new Uri(baseUri) };
                    var resolver = new EndpointResolver(options);

                    var receivedEvents = new List<EventEnvelope>();
                    var sseClient = new LivePollingClient(httpClient, resolver, registry, eventBus, options);
                    sseClient.EventReceived += (s, e) =>
                    {
                        lock (receivedEvents)
                        {
                            receivedEvents.Add(e);
                        }
                    };

                    await sseClient.ConnectAsync();

                    // Allow SSE event delivery
                    await Task.Delay(500);

                    await sseClient.DisconnectAsync();

                    lock (receivedEvents)
                    {
                        Assert.NotEmpty(receivedEvents);
                        Assert.Contains(receivedEvents, e => e.EventType == "ready" && e.Payload is System.Text.Json.JsonElement je && je.GetProperty("status").GetString() == "online");
                        Assert.Contains(receivedEvents, e => e.EventType == "snapshot" && e.Payload is System.Text.Json.JsonElement je && je.GetProperty("topic").GetString() == "kpis");
                    }
                }
            }
            finally
            {
                cts.Cancel();
                listener.Stop();
                try { await serverTask; } catch { }
            }
        }

        private sealed class TestHttpHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

            public TestHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return _handler(request, cancellationToken);
            }
        }
    }
}
