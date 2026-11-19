using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sops.DataLayer.Authentication;
using Sops.DataLayer.Authorization;
using Sops.DataLayer.Models.Authentication;
using Sops.DataLayer.Models.Responses;
using Xunit;

namespace Sops.DataLayer.Tests
{
    public class AuthenticationAndSessionTests
    {
        [Fact]
        public async Task SecureCredentialStore_Saves_And_Retrieves_Token()
        {
            var store = new SecureCredentialStore();
            string key = "pos_session_token";
            string secretToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.secret";

            await store.SaveTokenAsync(key, secretToken);
            var retrieved = await store.GetTokenAsync(key);

            Assert.Equal(secretToken, retrieved);

            await store.DeleteTokenAsync(key);
            var afterDelete = await store.GetTokenAsync(key);
            Assert.Null(afterDelete);
        }

        [Fact]
        public void SessionManager_Maintains_Session_State_And_Permissions()
        {
            var store = new SecureCredentialStore();
            var manager = new SessionManager(store);

            var loginResponse = new LoginResponse
            {
                AccessToken = "tok_123",
                RefreshToken = "ref_456",
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(8),
                Permissions = new List<string> { "pos.sales.create", "pos.inventory.view" },
                Roles = new List<string> { "Cashier" },
                Session = new SessionInfo
                {
                    SessionId = "sess_001",
                    StaffId = "staff_99",
                    Username = "cashier1",
                    ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(8)
                }
            };

            manager.SetSession(loginResponse);

            Assert.True(manager.IsAuthenticated);
            Assert.False(manager.IsExpired);
            Assert.Equal("tok_123", manager.CurrentAccessToken);
            Assert.Equal("cashier1", manager.CurrentSession?.Username);
            Assert.Contains("pos.sales.create", manager.Permissions);

            manager.ClearSession();
            Assert.False(manager.IsAuthenticated);
            Assert.True(manager.IsExpired);
            Assert.Null(manager.CurrentSession);
        }

        [Fact]
        public void PermissionClient_Correctly_Evaluates_Permissions()
        {
            var store = new SecureCredentialStore();
            var manager = new SessionManager(store);
            var permClient = new PermissionClient(manager);

            manager.SetSession(new LoginResponse
            {
                AccessToken = "tok_123",
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
                Permissions = new List<string> { "pos.sales.create", "role:Pharmacist" },
                Session = new SessionInfo { ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1) }
            });

            Assert.True(permClient.HasPermission("pos.sales.create"));
            Assert.False(permClient.HasPermission("admin.settings.manage"));
            Assert.True(permClient.IsInRole("Pharmacist"));
            Assert.False(permClient.IsInRole("Administrator"));
        }

        [Fact]
        public async Task TokenRefreshCoordinator_SingleFlight_Prevents_Thundering_Herd()
        {
            var store = new SecureCredentialStore();
            var manager = new SessionManager(store);
            await store.SaveTokenAsync("current_refresh_token", "valid_refresh_token");

            int refreshExecutionCount = 0;
            Func<string, CancellationToken, Task<DataResult<LoginResponse>>> refreshNetworkCall = async (token, ct) =>
            {
                Interlocked.Increment(ref refreshExecutionCount);
                await Task.Delay(100, ct); // Simulate network latency

                return DataResult<LoginResponse>.Success(new LoginResponse
                {
                    AccessToken = "new_access_token",
                    ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
                    Session = new SessionInfo { Username = "user1", ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1) }
                });
            };

            var coordinator = new TokenRefreshCoordinator(manager, store, refreshNetworkCall);

            // Trigger 10 concurrent requests to refresh token simultaneously
            var tasks = new Task<DataResult<LoginResponse>>[10];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(() => coordinator.RefreshTokenAsync());
            }

            var results = await Task.WhenAll(tasks);

            // Assert that only ONE actual network refresh call was made across all 10 requests
            Assert.Equal(1, refreshExecutionCount);
            foreach (var res in results)
            {
                Assert.True(res.IsSuccess);
                Assert.Equal("new_access_token", res.Data?.AccessToken);
            }
        }
    }
}
