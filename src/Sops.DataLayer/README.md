# Sops.DataLayer

`Sops.DataLayer` is the official client communication and data-access SDK for **Smart Operating Pharmacies (SOPs)** desktop applications. It serves as the single strongly typed, secure, asynchronous, offline-aware boundary connecting the SOPs WPF Desktop POS to the local pharmacy server (`http://127.0.0.1:5000` / legacy `8714`) and the global cloud backend (`https://api.sops.pharmacy`).

---

## Architecture Overview

```
+---------------------------------------------------------------------------------+
|                               SOPs WPF Desktop POS                              |
|                (Views, ViewModels, AsyncCommands, RequestScopes)                |
+---------------------------------------------------------------------------------+
                                         |
                                         v
+---------------------------------------------------------------------------------+
|                                  ISopsDataLayer                                 |
|             (Drugs, Patients, Inventory, Sales, Shifts, Licensing, Sync)        |
+---------------------------------------------------------------------------------+
                                         |
                                         v
+---------------------------------------------------------------------------------+
|                              IRequestPipeline Engine                            |
|  [CorrelationMiddleware] -> [ValidationMiddleware] -> [AuthenticationMiddleware]|
|               -> [ResilienceMiddleware] -> [OfflineReadCoordinator]             |
+---------------------------------------------------------------------------------+
                    |                                           |
                    v                                           v
+---------------------------------------+   +-------------------------------------+
|             HttpTransport             |   |             RpcTransport            |
|     (REST / JSON / ETag / RetryAfter) |   |  (Route RPC + JSON-RPC 2.0 Envelopes|
+---------------------------------------+   +-------------------------------------+
                    |                                           |
                    v                                           v
+---------------------------------------+   +-------------------------------------+
|         Local Pharmacy Server         |   |         Global Cloud Backend        |
|       (http://127.0.0.1:5000/8714)    |   |     (https://api.sops.pharmacy)     |
+---------------------------------------+   +-------------------------------------+
```

---

## Key Hardened Features

### 1. Multi-Targeting & TLS Negotiation (`netstandard2.0;net48`)
- Compiled against both `netstandard2.0` (for portable tooling, unit tests, and cross-platform services) and `net48` (for native WPF integration on Windows 7 SP1 through Windows 11).
- Conditional framework references for `net48`: `PresentationCore`, `PresentationFramework`, `WindowsBase`, `System.Xaml`, and `System.Security`.
- **TLS 1.2 Enforcement & Operating System Prerequisites**:
  - In .NET Framework 4.8, `ServicePointManager.SecurityProtocol` defaults to `SystemDefault` on modern Windows (10/11), delegating secure negotiation to the OS SCHANNEL provider.
  - Windows 7 SP1 requires Windows Update **KB3140245** (or Easy Fix 51044) to enable TLS 1.2 in SCHANNEL.
  - `TlsConfiguration.EnsureTls12Supported()` ensures TLS 1.2 (`(SecurityProtocolType)3072`) is enabled for HTTPS calls without forcing TLS 1.3 (which is not universally supported on Windows 7) and explicitly forbids obsolete protocols (SSL 3.0, TLS 1.0, TLS 1.1). Certificate validation is never bypassed.
- Safe dispatcher fallback using `SynchronizationContext` when compiled outside WPF.

### 2. Dependency Graph & Reuse Matrix
Consumes shared client packages without duplication:
- **`Sops.Configuration`**: Unified options binding (`DataLayerOptions`) and discovery.
- **`Sops.Logging`**: Structured diagnostics with sensitive data redaction.
- **`Sops.Cache` & `Sops.Cache.Memory`**: In-process memory caching and cache-aside coordination.
- **`Sops.Drugs`**: Canonical Egyptian drug catalog models, barcoding, and search contracts.
- **`Sops.Licensing`**: Cryptographic license verification, hardware fingerprinting, and device tokens.
- **`Sops.Payments`**: Payment contracts and transaction status abstractions. Server payment gateway implementations (Paymob secret keys and webhooks) are strictly managed on the pharmacy server/backend and not exposed in the desktop client.

*Security Constraint: Server-only packages (`Sops.Cache.Redis`, `Sops.Database`, `Sops.Mail`, `Sops.WhatsApp`) and direct database engine access (SQLCipher/PostgreSQL) are strictly excluded from the desktop client SDK.*

### 3. Dual Transport Engine
- **`HttpTransport`**: Manages standard REST HTTP requests with header injection, Bearer token attachment from `ISessionManager`, ETag extraction, and HTTP 429 `Retry-After` parsing.
- **`RpcTransport`**: Directly targets the local pharmacy server's RPC dispatcher (`Sop.Server.Hosting.Rpc`). Supports both single-gateway JSON-RPC 2.0 (`POST /rpc` with `{ "jsonrpc": "2.0", "method": ..., "params": ..., "id": ... }`) and route-based RPC (`POST /rpc/{service}/{method}`), with full support for JSON-RPC 2.0 error objects (`{ "error": { "code": ..., "message": ... } }`).
- **`TransportFactory`**: Selects the transport dynamically according to `EndpointDefinition.Transport`.

### 4. Endpoints & Route Resolution
- **Local Server Architecture**: The local pharmacy server operates over HTTP on port `5000` (primary) with configurable fallback to legacy port `8714`. Local services (`DrugService`, `TransactionService`, `PatientService`, etc.) are exposed via RPC over HTTP and real-time SSE (`/api/live`), not REST endpoints.
- **SSRF Prevention & LAN Support**:
  - Authorizes loopback addresses (`127.0.0.1`, `localhost`, `::1`) and private LAN IPv4 address spaces (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`, `169.254.0.0/16`) for local POS deployments.
  - Verifies that resolved URI host and port match the base target host and port to block authority injection (`@evil.com` or `//evil.com`).
  - Rejects user credentials (`UserInfo`) in endpoint URIs.
- **Route Traversal Protection**: Rejects directory traversal (`..`) and encoded path traversal delimiters (`%2f`, `%5c`, `%2e%2e`, `\` or `/`) in route parameters.
- Dynamic route template parameter substitution: `/api/v1/drugs/barcode/{barcode}` substitutes `{barcode}` safely with URL encoding and appends remaining query parameters.

### 5. Authentication, Single-Flight Refresh & DPAPI Storage
- **`SecureCredentialStore`**: Native Windows DPAPI (`CryptProtectData`/`CryptUnprotectData` via `crypt32.dll`) using `CurrentUser` scope and `CRYPTPROTECT_UI_FORBIDDEN`. Encrypted blobs are persisted to disk atomically via temporary file replacement.
- **Selective Win32 Error Recovery**: Distinguishes permanent data corruption (Win32 errors `13` Invalid Data, `0x80092004` NTE_BAD_KEY, `0x80092005` NTE_BAD_DATA) from temporary profile errors (`5` Access Denied, `1326` Logon Failure). Permanently corrupted credential files are purged to allow clean re-login, while temporary profile lock errors throw `DpapiException` to prevent catastrophic credential deletion when roaming profiles or domain accounts are momentarily locked.
- **Strict Token Separation**: Local tokens (`LocalAccessToken`) and Cloud tokens (`CloudAccessToken`) are segregated in `ISessionManager` and dispatched exclusively to their respective targets (`BackendTarget.LocalServer` vs `BackendTarget.GlobalApi`). Local credentials never leak to cloud APIs, and cloud tokens never leak to local endpoints.
- **Single-Flight Refresh Stampede Prevention (`TokenRefreshCoordinator`)**: Uses an asynchronous lock (`SemaphoreSlim`) to guarantee that concurrent expired requests await a single network refresh task, preventing thundering herds against the server.
- **Automatic 401 Interception**: Transparently catches `401 Unauthorized`, refreshes the token, and retries the in-flight request once.

### 6. Financial Safety, Idempotency & Cache Isolation
- **Strict Tenancy Isolation**: Cache keys partition data by Pharmacy, Branch, and User scope:
  `sops:dl:p:{pharmacyId}:b:{branchId}:u:{userScope}:{endpointId}`.
- **Prohibited Offline Mutations**: Financial sales, payments, and stock adjustments (`sales.complete`, `sales.addpayment`, `inventory.adjust`) are strictly forbidden offline and fail fast with `DataOutcome.ServerUnavailable`. They are never queued implicitly in client memory.
- **Unknown Outcomes on Network Timeout**: Timeouts or socket drops during financial writes do not imply failure; they return `DataOutcome.UnknownOutcome`. Financial retries preserve the original `IdempotencyKey` to prevent duplicate transaction creation, and transactions must be authoritatively reconciled against the server.
- **Automatic Invalidation**: Successful financial and catalog mutations automatically invalidate associated cached entries (`drugs.*`, `inventory.*`, `catalog.*`).
- **Stale Fallback**: Permitted read queries (`drugs.search`, `categories.list`) fall back to cached data when the server is unreachable, tagging responses with `Metadata.IsStale = true`.

### 7. Real-Time Events (Server-Sent Events)
- **`LivePollingClient`**: Consumes `text/event-stream` over `/api/live` with automatic reconnection, jittered exponential backoff, retry directive support (`retry: <ms>`), and heartbeat handling (`: ping`).
- **Snapshot-Driven Synchronization**: The local server streams state snapshots and does not maintain a persistent historical replay log. Therefore, event reconnection triggers cache invalidation and authoritative re-reads for sensitive state rather than assuming zero-data-loss replay.
- **W3C SSE Parser Compliance**: Fully implements multiline `data:` concatenation with newline delimiters, leading space stripping, comment line (`:`) rejection, and event dispatch on empty line boundaries.
- **Deduplication**: Maintains a 256-entry ring buffer of recent event IDs to discard duplicate events.
- **In-Process Pub/Sub (`DataLayerEventBus`)**: Thread-safe event bus with exception isolation, UI thread dispatching via `IUiDispatcher`, and session-scoped subscription cleanup via `ClearSubscriptions()`.

### 8. WPF Desktop MVVM Integration
- **`ObservableRequestState<T>`**: INotifyPropertyChanged container covering all 10 required UI states:
  - `Idle`
  - `Loading`
  - `Refreshing`
  - `Success`
  - `Empty` (null or empty collection)
  - `Stale` (cached data served during offline/degraded mode)
  - `Offline` (server or network unavailable)
  - `Syncing` (pending synchronization)
  - `Failed` (business error)
  - `Cancelled`
- **Search Query Race Prevention**: `BeginRequest()` returns a monotonically increasing sequence token. When an asynchronous query completes, `SetResult(result, sequenceToken)` drops outdated search results from slower earlier keystrokes, ensuring older queries never overwrite newer ones.
- **`AsyncCommand`**: Reentrancy-safe asynchronous `ICommand` implementation with automatic `CanExecute` toggling and cancellation support.
- **`ViewModelRequestScope`**: Scopes request cancellation and event subscriptions to the ViewModel lifecycle, releasing all subscriptions and in-flight HTTP requests upon disposal.

---

## Installation & Dependency Injection

```csharp
using Microsoft.Extensions.DependencyInjection;
using Sops.DataLayer.DependencyInjection;
using Sops.DataLayer.Configuration;

var services = new ServiceCollection();

// Register prerequisite logging and memory caching
services.AddSingleton<Sops.Logging.ILogger, ConsoleLogger>();
services.AddSingleton<Sops.Cache.ICacheService, Sops.Cache.Memory.MemoryCacheService>();

// Register Sops.DataLayer with desktop options
services.AddSopsDataLayer(options =>
{
    options.LocalServerAddress = new Uri("http://127.0.0.1:5000");
    options.GlobalApiAddress = new Uri("https://api.sops.pharmacy");
    options.TerminalId = "POS-TERMINAL-01";
    options.PharmacyId = "PHARM-EGY-CAI-042";
    options.BranchId = "BRANCH-DOWNTOWN";
    options.RequestTimeoutSeconds = 30;
    options.EnableCaching = true;
});

var provider = services.BuildServiceProvider();
var dataLayer = provider.GetRequiredService<ISopsDataLayer>();
```

---

## WPF ViewModel Usage Example

```csharp
public class PosDrugSearchViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ISopsDataLayer _dataLayer;
    private readonly ViewModelRequestScope _scope = new ViewModelRequestScope();

    public ObservableRequestState<DataPage<DrugDto>> SearchState { get; } = new ObservableRequestState<DataPage<DrugDto>>();
    public ICommand SearchCommand { get; }

    public PosDrugSearchViewModel(ISopsDataLayer dataLayer)
    {
        _dataLayer = dataLayer;

        SearchCommand = new AsyncCommand(async (param, token) =>
        {
            string query = param?.ToString() ?? string.Empty;
            
            // BeginRequest returns a unique sequence token to prevent race conditions
            long sequenceToken = SearchState.BeginRequest(isRefreshing: false);

            using var linkedCts = _scope.CreateLinkedSource(token);
            var result = await _dataLayer.Drugs.SearchAsync(query, page: 1, pageSize: 25, cancellationToken: linkedCts.Token);

            // Applies result only if it matches the latest sequence token
            SearchState.SetResult(result, sequenceToken);
        });

        // Register event subscriptions that auto-dispose when the screen closes
        var token = _dataLayer.EventBus.SubscribeOnUi<EventEnvelope>(OnLiveEventReceived);
        _scope.RegisterSubscription(token);
    }

    private void OnLiveEventReceived(EventEnvelope evt)
    {
        if (evt.EventType == "inventory.stock_updated")
        {
            // Update UI indicators
        }
    }

    public void Dispose()
    {
        _scope.Dispose(); // Cancels pending requests and disposes subscriptions
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
```

---

## Verification & Test Results

- Unit & Integration Test Project: [tests/Sops.DataLayer.Tests](file:///D:/SOPs-system/sops-packages/tests/Sops.DataLayer.Tests/Sops.DataLayer.Tests.csproj)
- `Sops.DataLayer.Tests`: **53 / 53 tests passed (100%)**
- Solution-wide test run: **605 / 605 tests passed (100%) across all 12 test projects**
- Sample consumer [samples/WpfNet48Consumer](file:///D:/SOPs-system/sops-packages/samples/WpfNet48Consumer/WpfNet48Consumer.csproj) builds and executes cleanly against .NET Framework 4.8 with true WPF Application lifecycle, STA Dispatcher, XAML data-binding, and all 10 request states.
- Packaged NuGet artifact: `artifacts/Sops.DataLayer.0.1.0.nupkg`.
- Dependency Vulnerability Auditing: **Active (`NuGetAudit=true`)**. Local cache audited with 0 vulnerabilities; remote advisory index queries recorded as incomplete due to offline network isolation.
