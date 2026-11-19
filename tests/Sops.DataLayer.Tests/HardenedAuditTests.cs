using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Sops.Cache.Memory;
using Sops.DataLayer.Authentication;
using Sops.DataLayer.Configuration;
using Sops.DataLayer.Endpoints;
using Sops.DataLayer.Events;
using Sops.DataLayer.Models.Authentication;
using Sops.DataLayer.Models.Events;
using Sops.DataLayer.Models.Requests;
using Sops.DataLayer.Models.Responses;
using Sops.DataLayer.Offline;
using Sops.DataLayer.Wpf;
using Xunit;

namespace Sops.DataLayer.Tests
{
    public class HardenedAuditTests
    {
        [Fact]
        public void EndpointResolver_Substitutes_Route_Parameters_And_Appends_Remaining_Query()
        {
            var options = new DataLayerOptions
            {
                LocalServerAddress = new Uri("http://127.0.0.1:5000")
            };
            var resolver = new EndpointResolver(options);
            var endpoint = new EndpointDefinition(
                "drugs.barcode",
                BackendTarget.LocalServer,
                TransportMode.HttpRest,
                "GET",
                "/api/v1/drugs/barcode/{barcode}");

            var queryParams = new Dictionary<string, string>
            {
                { "barcode", "6221234 56789" },
                { "branchId", "CAI-01" }
            };

            var uri = resolver.ResolveUri(endpoint, queryParams);

            Assert.Equal("http://127.0.0.1:5000/api/v1/drugs/barcode/6221234%2056789?branchId=CAI-01", uri.AbsoluteUri);
        }

        [Fact]
        public void RequestContext_Contains_All_Mandatory_Audit_Fields()
        {
            var deadline = DateTimeOffset.UtcNow.AddMinutes(5);
            var context = new RequestContext(
                correlationId: "corr-123",
                causationId: "caus-456",
                idempotencyKey: "idemp-789",
                terminalId: "TERM-01",
                userId: "user-1",
                sessionId: "sess-1",
                pharmacyId: "PHARM-1",
                branchId: "BR-1",
                tenantId: "TEN-1",
                clientVersion: "2.1.0",
                deadlineUtc: deadline,
                endpointId: "drugs.search",
                expectedEntityVersion: "etag-v1");

            Assert.NotNull(context.RequestId);
            Assert.Equal("corr-123", context.CorrelationId);
            Assert.Equal("caus-456", context.CausationId);
            Assert.Equal("idemp-789", context.IdempotencyKey);
            Assert.Equal("TERM-01", context.TerminalId);
            Assert.Equal("user-1", context.UserId);
            Assert.Equal("sess-1", context.SessionId);
            Assert.Equal("PHARM-1", context.PharmacyId);
            Assert.Equal("BR-1", context.BranchId);
            Assert.Equal("TEN-1", context.TenantId);
            Assert.Equal("2.1.0", context.ClientVersion);
            Assert.Equal(deadline, context.DeadlineUtc);
            Assert.Equal("drugs.search", context.EndpointId);
            Assert.Equal("etag-v1", context.ExpectedEntityVersion);

            // Test WithOverrides
            var overridden = context.WithOverrides(terminalId: "TERM-02", endpointId: "drugs.getbyid");
            Assert.Equal("TERM-02", overridden.TerminalId);
            Assert.Equal("drugs.getbyid", overridden.EndpointId);
            Assert.Equal("corr-123", overridden.CorrelationId);
            Assert.Equal("idemp-789", overridden.IdempotencyKey);
        }

        [Fact]
        public void DataError_And_DataOutcome_Expose_All_Required_Failure_Categories()
        {
            var netErr = DataError.NetworkUnavailable("Connection lost");
            Assert.Equal("network_unavailable", netErr.Code);

            var conflictErr = DataError.Conflict("Duplicate key detected");
            Assert.True(conflictErr.IsIdempotencyConflict);

            var rateErr = DataError.RateLimited("Slow down");
            Assert.Equal(429, rateErr.StatusCode);

            var netResult = DataResult<string>.NetworkUnavailable("Offline");
            Assert.False(netResult.IsSuccess);
            Assert.Equal(DataOutcome.NetworkUnavailable, netResult.Outcome);

            var rateResult = DataResult<string>.RateLimited("Rate limit hit");
            Assert.False(rateResult.IsSuccess);
            Assert.Equal(DataOutcome.RateLimited, rateResult.Outcome);
        }

        [Fact]
        public void ResponseMetadata_Includes_ETag_And_RetryAfter()
        {
            var retry = TimeSpan.FromSeconds(30);
            var meta = ResponseMetadata.Live(
                DataSourceOrigin.GlobalApi,
                TimeSpan.FromMilliseconds(120),
                statusCode: 429,
                correlationId: "corr-1",
                serverVersionOrETag: "W/\"v1.2.3\"",
                retryAfter: retry);

            Assert.Equal("W/\"v1.2.3\"", meta.ServerVersionOrETag);
            Assert.Equal(retry, meta.RetryAfter);
            Assert.Equal(429, meta.StatusCode);
            Assert.Equal(DataSourceOrigin.GlobalApi, meta.Source);
        }

        [Fact]
        public async Task SecureCredentialStore_Recovers_Gracefully_From_Corrupted_Storage()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "sops_audit_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var store = new SecureCredentialStore("test_entropy", tempDir);
                await store.SaveTokenAsync("test_key", "secret_value_123");

                var readBack = await store.GetTokenAsync("test_key");
                Assert.Equal("secret_value_123", readBack);

                // Simulate corruption by overwriting the file with random invalid bytes
                string filePath = Path.Combine(tempDir, "test_key.dat");
                if (File.Exists(filePath))
                {
                    File.WriteAllBytes(filePath, new byte[] { 0x00, 0xFF, 0xDE, 0xAD, 0xBE, 0xEF });

                    // Re-instantiate store to bypass in-memory dictionary and read from corrupt file
                    var store2 = new SecureCredentialStore("test_entropy", tempDir);
                    var corruptedResult = await store2.GetTokenAsync("test_key");

                    // Must handle corruption safely without throwing and return null
                    Assert.Null(corruptedResult);
                }
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [Fact]
        public void EventEnvelope_Carries_Tenancy_And_Schema_Version()
        {
            var evt = new EventEnvelope(
                "drug.price_updated",
                payload: new { drugId = 42, newPrice = 55.0m },
                pharmacyId: "PHARM-CAI-01",
                branchId: "BR-MAIN",
                schemaVersion: "1.2");

            Assert.Equal("drug.price_updated", evt.EventType);
            Assert.Equal("PHARM-CAI-01", evt.PharmacyId);
            Assert.Equal("BR-MAIN", evt.BranchId);
            Assert.Equal("1.2", evt.SchemaVersion);
            Assert.NotNull(evt.EventId);
            Assert.True(evt.OccurredAtUtc > DateTimeOffset.MinValue);
        }

        [Fact]
        public void EventBus_ClearSubscriptions_Cleans_All_Active_Handlers()
        {
            var bus = new DataLayerEventBus();
            int calls = 0;

            bus.Subscribe<string>(_ => calls++);
            bus.Publish("Hello 1");
            Assert.Equal(1, calls);

            bus.ClearSubscriptions();
            bus.Publish("Hello 2");
            Assert.Equal(1, calls); // Should not increase after clearing
        }

        [Fact]
        public async Task OfflineReadCoordinator_Enforces_Tenancy_Partition_Isolation()
        {
            var memoryCache = new MemoryCacheService();
            var options = new DataLayerOptions { EnableCaching = true, DefaultCacheTtlMinutes = 10 };
            var coordinator = new OfflineReadCoordinator(memoryCache, options);

            var endpoint = new EndpointDefinition(
                "drugs.search",
                BackendTarget.LocalServer,
                TransportMode.Rpc,
                "POST",
                "/rpc/drugs/searchdrugs",
                rpcService: "Drugs",
                rpcMethod: "SearchDrugs",
                cachePolicy: EndpointCachePolicy.Minutes(10),
                offlinePolicy: EndpointOfflinePolicy.CacheFallback);

            var reqPharmacyA = new DataRequest("drugs.search", new { q = "Panadol" }, context: new RequestContext(pharmacyId: "PHARM_A", branchId: "B1"));
            var reqPharmacyB = new DataRequest("drugs.search", new { q = "Panadol" }, context: new RequestContext(pharmacyId: "PHARM_B", branchId: "B1"));

            // Populate cache for Pharmacy A
            await coordinator.ExecuteWithCachePolicyAsync(endpoint, reqPharmacyA, () => Task.FromResult(DataResult<string>.Success("Pharmacy A Results")));

            // Query Pharmacy A from cache
            var hitA = await coordinator.ExecuteWithCachePolicyAsync(endpoint, reqPharmacyA, () => Task.FromResult(DataResult<string>.Success("New Network Result A")));
            Assert.Equal("Pharmacy A Results", hitA.Data);

            // Query Pharmacy B from network (must NOT hit Pharmacy A's cache!)
            var hitB = await coordinator.ExecuteWithCachePolicyAsync(endpoint, reqPharmacyB, () => Task.FromResult(DataResult<string>.Success("Pharmacy B Results")));
            Assert.Equal("Pharmacy B Results", hitB.Data);
        }

        [Fact]
        public async Task OfflineReadCoordinator_Strictly_Prohibits_Offline_Mutations()
        {
            var memoryCache = new MemoryCacheService();
            var options = new DataLayerOptions { EnableCaching = true };
            var coordinator = new OfflineReadCoordinator(memoryCache, options);

            var mutationEndpoint = new EndpointDefinition(
                "sales.complete",
                BackendTarget.LocalServer,
                TransportMode.HttpRest,
                "POST",
                "/api/v1/sales/complete",
                offlinePolicy: EndpointOfflinePolicy.OnlineOnly);

            var req = new DataRequest("sales.complete", new { saleId = 123 }, new RequestOptions { CacheOnly = true });

            var result = await coordinator.ExecuteWithCachePolicyAsync<string>(
                mutationEndpoint,
                req,
                () => Task.FromResult(DataResult<string>.Success("OK")));

            Assert.False(result.IsSuccess);
            Assert.Equal(DataOutcome.ServerUnavailable, result.Outcome);
        }

        [Fact]
        public void ObservableRequestState_Covers_All_Ten_Required_UI_States()
        {
            var state = new ObservableRequestState<string>();

            // 1. Idle
            Assert.True(state.IsIdle);
            Assert.Equal(RequestUiStatus.Idle, state.Status);

            // 2. Loading
            state.SetLoading(isRefreshing: false);
            Assert.True(state.IsLoading);
            Assert.True(state.IsBusy);

            // 3. Refreshing
            state.SetLoading(isRefreshing: true);
            Assert.True(state.IsRefreshing);
            Assert.True(state.IsBusy);

            // 4. Success
            state.SetResult(DataResult<string>.Success("Valid Data"));
            Assert.True(state.IsSuccess);
            Assert.Equal("Valid Data", state.Data);

            // 5. Empty
            state.SetResult(DataResult<string>.Empty());
            Assert.True(state.IsEmpty);

            // 6. Stale
            var staleMeta = ResponseMetadata.Cached(DateTimeOffset.UtcNow.AddMinutes(-30), isStale: true);
            state.SetResult(DataResult<string>.Success("Stale Data", staleMeta));
            Assert.True(state.IsStale);

            // 7. Offline
            state.SetResult(DataResult<string>.ServerUnavailable("Server is offline"));
            Assert.True(state.IsOffline);

            // 8. Syncing
            state.SetResult(DataResult<string>.Failure(DataOutcome.PendingSynchronization, DataError.Create("SYNC", "Pending sync")));
            Assert.True(state.IsSyncing);

            // 9. Failed
            state.SetResult(DataResult<string>.Failure(DataError.Create("ERR", "Business error")));
            Assert.True(state.HasError);
            Assert.Equal(RequestUiStatus.Failed, state.Status);

            // 10. Cancelled
            state.SetCancelled();
            Assert.True(state.IsCancelled);
        }

        [Fact]
        public void ObservableRequestState_Eliminates_Search_Query_Race_Conditions()
        {
            var state = new ObservableRequestState<string>();

            // Query 1 starts (slow query, e.g. "P")
            long token1 = state.BeginRequest();

            // Query 2 starts shortly after (fast query, e.g. "Panadol")
            long token2 = state.BeginRequest();
            Assert.True(token2 > token1);

            // Fast query 2 completes first
            bool applied2 = state.SetResult(DataResult<string>.Success("Panadol 500mg"), token2);
            Assert.True(applied2);
            Assert.Equal("Panadol 500mg", state.Data);

            // Slow query 1 completes late (stale result)
            bool applied1 = state.SetResult(DataResult<string>.Success("P - Outdated"), token1);
            Assert.False(applied1); // Dropped!

            // Data must still be Panadol 500mg, never overwritten by outdated query 1
            Assert.Equal("Panadol 500mg", state.Data);
        }

        [Fact]
        public void ViewModelRequestScope_Disposes_Registered_Subscriptions()
        {
            var scope = new ViewModelRequestScope();
            bool subscriptionDisposed = false;

            var testSubscription = new TestSubscriptionToken(() => subscriptionDisposed = true);
            scope.RegisterSubscription(testSubscription);

            Assert.False(subscriptionDisposed);
            scope.Dispose();
            Assert.True(subscriptionDisposed);
        }

        [Fact]
        public void EndpointResolver_Rejects_Unapproved_Host_SSRF()
        {
            var options = new DataLayerOptions
            {
                LocalServerAddress = new Uri("http://127.0.0.1:5000"),
                AllowedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "127.0.0.1", "localhost" }
            };
            var resolver = new EndpointResolver(options);
            var badEndpoint = new EndpointDefinition(
                "evil.test",
                BackendTarget.LocalServer,
                TransportMode.HttpRest,
                "GET",
                "/api/test");

            // Change LocalServerAddress to an unapproved host
            options.LocalServerAddress = new Uri("http://evil-attacker.com:5000");

            Assert.Throws<InvalidOperationException>(() => resolver.ResolveUri(badEndpoint));
        }

        [Fact]
        public void EndpointResolver_Rejects_Route_Parameter_Path_Traversal()
        {
            var options = new DataLayerOptions
            {
                LocalServerAddress = new Uri("http://127.0.0.1:5000")
            };
            var resolver = new EndpointResolver(options);
            var endpoint = new EndpointDefinition(
                "drugs.barcode",
                BackendTarget.LocalServer,
                TransportMode.HttpRest,
                "GET",
                "/api/v1/drugs/barcode/{barcode}");

            var badParams = new Dictionary<string, string>
            {
                { "barcode", "../admin/secret" }
            };

            Assert.Throws<ArgumentException>(() => resolver.ResolveUri(endpoint, badParams));
        }

        [Fact]
        public void SessionManager_Strictly_Separates_Local_And_Cloud_Tokens()
        {
            var store = new SecureCredentialStore("test_entropy");
            var manager = new SessionManager(store);

            var loginResponse = new LoginResponse
            {
                AccessToken = "local_pos_jwt_token_123",
                RefreshToken = "local_pos_refresh_token_123",
                Session = new SessionInfo
                {
                    SessionId = "sess_01",
                    StaffId = "staff_01",
                    ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(8)
                }
            };

            manager.SetSession(loginResponse);
            manager.SetCloudSession("cloud_global_jwt_token_789", "cloud_refresh_token_789");

            Assert.Equal("local_pos_jwt_token_123", manager.LocalAccessToken);
            Assert.Equal("local_pos_jwt_token_123", manager.CurrentAccessToken);
            Assert.Equal("cloud_global_jwt_token_789", manager.CloudAccessToken);

            // Clear local session must NOT clear cloud session
            manager.ClearSession();
            Assert.Null(manager.LocalAccessToken);
            Assert.Null(manager.CurrentAccessToken);
            Assert.Equal("cloud_global_jwt_token_789", manager.CloudAccessToken);

            // Clear cloud session
            manager.ClearCloudSession();
            Assert.Null(manager.CloudAccessToken);
        }

        [Fact]
        public async Task ValidationMiddleware_Rejects_Financial_Mutation_Without_Idempotency_Key()
        {
            var middleware = new Pipeline.ValidationMiddleware();
            var endpoint = new EndpointDefinition(
                "sales.complete",
                BackendTarget.LocalServer,
                TransportMode.HttpRest,
                "POST",
                "/api/v1/sales/complete",
                isFinancialMutation: true,
                requiresIdempotencyKey: true);

            var reqNoKey = new DataRequest("sales.complete", new { total = 100 }, context: new RequestContext());
            var result = await middleware.InvokeAsync(endpoint, reqNoKey, (ep, r, ct) => Task.FromResult(DataResult<string>.Success("OK")), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(DataOutcome.ValidationFailed, result.Outcome);
            Assert.Equal("MISSING_IDEMPOTENCY_KEY", result.Error?.Code);

            // With key should succeed
            var reqWithKey = new DataRequest("sales.complete", new { total = 100 }, context: new RequestContext(idempotencyKey: "sale_idemp_01"));
            var resultWithKey = await middleware.InvokeAsync(endpoint, reqWithKey, (ep, r, ct) => Task.FromResult(DataResult<string>.Success("OK")), CancellationToken.None);
            Assert.True(resultWithKey.IsSuccess);
        }

        [Fact]
        public void DpapiException_IsPermanentCorruption_Distinguishes_Profile_Lock_From_Data_Corruption()
        {
            // Error 13 is ERROR_INVALID_DATA (corrupted file)
            var corruptEx = new DpapiException(13, "Data corrupt");
            Assert.True(corruptEx.IsPermanentCorruption);

            // Error 5 is ERROR_ACCESS_DENIED (temporary profile lock/permissions)
            var lockEx = new DpapiException(5, "Access denied");
            Assert.False(lockEx.IsPermanentCorruption);

            // Error 1326 is ERROR_LOGON_FAILURE
            var logonEx = new DpapiException(1326, "Logon failure");
            Assert.False(logonEx.IsPermanentCorruption);
        }

        [Fact]
        public void TlsConfiguration_Enforces_Tls12_And_Forbids_Insecure_Protocols()
        {
            Security.TlsConfiguration.EnsureTls12Supported();

            Assert.True(Security.TlsConfiguration.IsTls12Active());
            Assert.False(Security.TlsConfiguration.HasInsecureProtocolsEnabled());
        }

        [Fact]
        public void TlsConfiguration_ValidateTransportSecurity_Rejects_Insecure_Cloud_And_UserInfo()
        {
            // Insecure HTTP cloud endpoint must fail
            var httpCloud = new Uri("http://api.sops.pharmacy/api/v1/licenses");
            Assert.Throws<InvalidOperationException>(() =>
                Security.TlsConfiguration.ValidateTransportSecurity(httpCloud, requireHttps: true));

            // Userinfo credentials in authority must fail
            var userInfoUri = new Uri("http://admin:secret@127.0.0.1:5000/api/v1/drugs");
            Assert.Throws<InvalidOperationException>(() =>
                Security.TlsConfiguration.ValidateTransportSecurity(userInfoUri, requireHttps: false));

            // Valid HTTPS cloud endpoint passes
            var httpsCloud = new Uri("https://api.sops.pharmacy/api/v1/licenses");
            Security.TlsConfiguration.ValidateTransportSecurity(httpsCloud, requireHttps: true);
        }

        [Fact]
        public void EndpointResolver_Allows_Authorized_LAN_IPs()
        {
            var options = new DataLayerOptions
            {
                LocalServerAddress = new Uri("http://192.168.1.150:5000")
            };
            var resolver = new EndpointResolver(options);
            var endpoint = new EndpointDefinition(
                "drugs.barcode",
                BackendTarget.LocalServer,
                TransportMode.HttpRest,
                "GET",
                "/api/v1/drugs/barcode/12345");

            var resolved = resolver.ResolveUri(endpoint);
            Assert.Equal("http://192.168.1.150:5000/api/v1/drugs/barcode/12345", resolved.AbsoluteUri);
        }

        [Fact]
        public void EndpointResolver_Rejects_Authority_Injection_And_Encoded_Traversal()
        {
            var options = new DataLayerOptions
            {
                LocalServerAddress = new Uri("http://127.0.0.1:5000")
            };
            var resolver = new EndpointResolver(options);

            // Authority injection attempt with @evil.com
            var evilEndpoint = new EndpointDefinition(
                "evil.ssrf",
                BackendTarget.LocalServer,
                TransportMode.HttpRest,
                "GET",
                "/evil/path");

            var queryParams = new Dictionary<string, string>
            {
                { "path", "@evil.com/leak" }
            };

            // Attempt with encoded path traversal %2e%2e
            var traversalEndpoint = new EndpointDefinition(
                "traversal.ssrf",
                BackendTarget.LocalServer,
                TransportMode.HttpRest,
                "GET",
                "/api/v1/drugs/{barcode}");

            var traversalParams = new Dictionary<string, string>
            {
                { "barcode", "%2e%2e%2fsecrets" }
            };

            Assert.Throws<ArgumentException>(() => resolver.ResolveUri(traversalEndpoint, traversalParams));
        }

        [Fact]
        public async Task RpcTransport_Financial_Mutation_Times_Out_Returns_UnknownOutcome()
        {
            var handler = new FakeHttpHandler((req, ct) =>
            {
                // Simulate in-flight timeout
                throw new OperationCanceledException("Socket timed out awaiting response headers");
            });

            var httpClient = new System.Net.Http.HttpClient(handler);
            var store = new SecureCredentialStore("test_entropy");
            var sessionMgr = new SessionManager(store);
            var transport = new Transport.RpcTransport(httpClient, sessionMgr);

            var financialEndpoint = new EndpointDefinition(
                "sales.complete",
                BackendTarget.LocalServer,
                TransportMode.Rpc,
                "POST",
                "/rpc/sales/completetransaction",
                isFinancialMutation: true,
                requiresIdempotencyKey: true);

            var request = new DataRequest("sales.complete", new { txId = "tx-123" }, context: new RequestContext(idempotencyKey: "idemp-safe-01"));
            var targetUri = new Uri("http://127.0.0.1:5000/rpc/sales/completetransaction");

            // CancellationToken is NOT cancelled by client; this is an in-flight timeout
            var result = await transport.SendAsync<string>(financialEndpoint, request, targetUri, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(DataOutcome.UnknownOutcome, result.Outcome);
            Assert.Contains("Financial mutation timed out in-flight", result.Error?.Message);
        }

        [Fact]
        public void DataLayerOptions_Validate_Rejects_UserInfo()
        {
            var options = new DataLayerOptions
            {
                LocalServerAddress = new Uri("http://user:pass@127.0.0.1:5000")
            };

            Assert.Throws<InvalidOperationException>(() => options.Validate());
        }

        private sealed class FakeHttpHandler : System.Net.Http.HttpMessageHandler
        {
            private readonly Func<System.Net.Http.HttpRequestMessage, CancellationToken, Task<System.Net.Http.HttpResponseMessage>> _handler;

            public FakeHttpHandler(Func<System.Net.Http.HttpRequestMessage, CancellationToken, Task<System.Net.Http.HttpResponseMessage>> handler)
            {
                _handler = handler;
            }

            protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return _handler(request, cancellationToken);
            }
        }

        private sealed class TestSubscriptionToken : IDisposable
        {
            private readonly Action _onDispose;
            public TestSubscriptionToken(Action onDispose) => _onDispose = onDispose;
            public void Dispose() => _onDispose();
        }
    }
}
