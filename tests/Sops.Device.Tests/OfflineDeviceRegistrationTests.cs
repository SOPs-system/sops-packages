using System;
using System.Threading;
using System.Threading.Tasks;
using Sops.Device.Abstractions;
using Sops.Device.Configuration;
using Sops.Device.Management;
using Sops.Device.Models;
using Xunit;

namespace Sops.Device.Tests
{
    public class OfflineDeviceRegistrationTests
    {
        private class MockDeviceService : IDeviceService
        {
            public Task<DeviceInfo> GetDeviceInfoAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(new DeviceInfo("SOP-WIN-TEST-0001-K", "inst-0001", DevicePlatform.Windows, "Win11", "x64", "Desktop", 2, new System.Collections.Generic.Dictionary<string, SignalStatus>()));

            public Task<DeviceIdentity> GetIdentityAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(new DeviceIdentity("inst-0001", "SOP-WIN-TEST-0001-K", DeviceIdentityMode.Hybrid, DevicePlatform.Windows));

            public Task<string> GetDeviceCodeAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult("SOP-WIN-TEST-0001-K");

            public Task<DeviceFingerprint> GetFingerprintAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(DeviceFingerprint.Empty());

            public Task<DeviceIdentityComparison> CompareAsync(DeviceFingerprint previous, CancellationToken cancellationToken = default) =>
                Task.FromResult(DeviceIdentityComparison.ExactMatch(Array.Empty<string>()));
        }

        [Fact]
        public void LocalDeviceRegistration_GracePeriod_EvaluatedCorrectly()
        {
            var now = DateTimeOffset.UtcNow;
            var reg = new LocalDeviceRegistration(
                "inst-0001", "SOP-WIN-TEST-0001-K", "REG-01", DeviceStatus.Active, "TOKEN", lastSyncAtUtc: now.AddDays(-5));

            // Within 14 days grace
            Assert.True(reg.IsWithinOfflineGracePeriod(now));

            // Lapsed beyond 14 days
            Assert.False(reg.IsWithinOfflineGracePeriod(now.AddDays(15)));

            // Non-active device has no offline grace
            var revokedReg = new LocalDeviceRegistration(
                "inst-0001", "SOP-WIN-TEST-0001-K", "REG-01", DeviceStatus.Revoked, "TOKEN", lastSyncAtUtc: now);
            Assert.False(revokedReg.IsWithinOfflineGracePeriod(now));
        }

        [Fact]
        public async Task LocalDeviceRegistrationManager_FullFlow_HandlesOfflineAndRevocation()
        {
            var registry = new InMemoryDeviceRegistry();
            var manager = new LocalDeviceRegistrationManager(new MockDeviceService(), new DeviceOptions());

            // 1. Initial local state is unregistered
            var initial = await manager.GetCurrentStateAsync();
            Assert.Equal(DeviceStatus.Unregistered, initial.LastKnownStatus);

            // 2. Register
            var regResult = await manager.RegisterAsync(registry, new DeviceOwnership("tenant-1", "branch-1"));
            Assert.True(regResult.Success);

            var registeredState = await manager.GetCurrentStateAsync();
            Assert.Equal(DeviceStatus.Active, registeredState.LastKnownStatus);
            Assert.False(string.IsNullOrEmpty(registeredState.RegistrationId));

            // 3. Heartbeat online
            var hbResult = await manager.SendHeartbeatAsync(registry, sequenceNumber: 1);
            Assert.True(hbResult.Acknowledged);
            Assert.Equal(DeviceStatus.Active, hbResult.Status);

            // 4. Server revokes device
            await registry.RevokeDeviceAsync(new DeviceRevocation(registeredState.RegistrationId!, "Audit fail", "admin"));

            // Next heartbeat receives revocation and updates local state
            var hbRevoked = await manager.SendHeartbeatAsync(registry, sequenceNumber: 2);
            Assert.Equal(DeviceStatus.Revoked, hbRevoked.Status);

            var postRevocation = await manager.GetCurrentStateAsync();
            Assert.Equal(DeviceStatus.Revoked, postRevocation.LastKnownStatus);
        }

        [Fact]
        public void EvaluateOfflineState_CustomPolicy_HonorsCustomGraceAndClockRollback()
        {
            var now = DateTimeOffset.UtcNow;
            var reg = new LocalDeviceRegistration(
                "inst-0001", "SOP-WIN-TEST-0001-K", "REG-01", DeviceStatus.Active, "VALID_TOKEN", lastSyncAtUtc: now.AddDays(-2));

            // Custom policy with 1 day grace
            var strictPolicy = DeviceOfflinePolicy.Custom(TimeSpan.FromDays(1));
            var state = reg.EvaluateOfflineState(now, strictPolicy);
            Assert.Equal(DeviceOfflineState.OfflineGraceExpired, state);

            // Policy with 7 days grace
            var normalPolicy = DeviceOfflinePolicy.Custom(TimeSpan.FromDays(7));
            var stateNormal = reg.EvaluateOfflineState(now, normalPolicy);
            Assert.Equal(DeviceOfflineState.WithinGracePeriod, stateNormal);

            // Clock rollback simulation (current time is earlier than last sync by 5 days)
            var rollbackState = reg.EvaluateOfflineState(now.AddDays(-5), normalPolicy);
            Assert.Equal(DeviceOfflineState.ClockRollbackDetected, rollbackState);
        }

        [Fact]
        public void EvaluateOfflineState_ExplicitOnlineAndDisconnected_ReturnsCorrectSemantics()
        {
            var now = DateTimeOffset.UtcNow;
            var reg = new LocalDeviceRegistration(
                "inst-0001", "SOP-WIN-TEST-0001-K", "REG-01", DeviceStatus.Active, "VALID_TOKEN",
                lastSyncAtUtc: now.AddMinutes(-5), isConnectedToServer: true);

            var policy = DeviceOfflinePolicy.Custom(TimeSpan.FromDays(7));

            // Connected and verified within 15 minutes -> ActiveOnline
            Assert.Equal(DeviceOfflineState.ActiveOnline, reg.EvaluateOfflineState(now, policy));

            // Disconnected with valid grace -> WithinGracePeriod
            var disconnectedReg = reg.WithConnectionState(isConnected: false);
            Assert.Equal(DeviceOfflineState.WithinGracePeriod, disconnectedReg.EvaluateOfflineState(now, policy));

            // Disconnected after grace period expired -> OfflineGraceExpired
            Assert.Equal(DeviceOfflineState.OfflineGraceExpired, disconnectedReg.EvaluateOfflineState(now.AddDays(8), policy));

            // Explicit server suspension -> Suspended
            var suspendedReg = new LocalDeviceRegistration(
                "inst-0001", "SOP-WIN-TEST-0001-K", "REG-01", DeviceStatus.Suspended, "VALID_TOKEN",
                lastSyncAtUtc: now, isConnectedToServer: true);
            Assert.Equal(DeviceOfflineState.Suspended, suspendedReg.EvaluateOfflineState(now, policy));

            // Explicit server revocation -> Revoked
            var revokedReg = new LocalDeviceRegistration(
                "inst-0001", "SOP-WIN-TEST-0001-K", "REG-01", DeviceStatus.Revoked, "VALID_TOKEN",
                lastSyncAtUtc: now, isConnectedToServer: true);
            Assert.Equal(DeviceOfflineState.Revoked, revokedReg.EvaluateOfflineState(now, policy));

            // Missing enrollment -> NeverRegistered
            var unregisteredReg = LocalDeviceRegistration.Unregistered("inst-0001", "SOP-WIN-TEST-0001-K");
            Assert.Equal(DeviceOfflineState.NeverRegistered, unregisteredReg.EvaluateOfflineState(now, policy));

            // Invalid credentials -> CredentialsInvalid
            var invalidCredsReg = new LocalDeviceRegistration(
                "inst-0001", "SOP-WIN-TEST-0001-K", "REG-01", DeviceStatus.Active, serverRegistrationToken: null,
                lastSyncAtUtc: now, isConnectedToServer: true);
            Assert.Equal(DeviceOfflineState.CredentialsInvalid, invalidCredsReg.EvaluateOfflineState(now, policy));

            // Significant clock rollback -> ClockRollbackDetected
            Assert.Equal(DeviceOfflineState.ClockRollbackDetected, reg.EvaluateOfflineState(now.AddDays(-1), policy));
        }

        [Fact]
        public void MonotonicRevocation_CannotBeOverwrittenByOlderActiveSnapshot()
        {
            var now = DateTimeOffset.UtcNow;
            var regRevoked = new LocalDeviceRegistration(
                "inst-0001", "SOP-WIN-TEST-0001-K", "REG-01", DeviceStatus.Revoked, "TOKEN", lastSyncAtUtc: now);

            // Stale server packet or older snapshot reporting Active must not undo revocation
            var updated = regRevoked.WithSync(now.AddMinutes(1), DeviceStatus.Active);
            Assert.Equal(DeviceStatus.Revoked, updated.LastKnownStatus);
        }

        [Fact]
        public async Task LocalDeviceRegistrationManager_WithCredentialStore_PersistsAndLoadsState()
        {
            var credStore = new Persistence.InMemoryDeviceCredentialStore();
            var registry = new InMemoryDeviceRegistry();
            var manager = new LocalDeviceRegistrationManager(new MockDeviceService(), new DeviceOptions(), credStore);

            await manager.RegisterAsync(registry, new DeviceOwnership("tenant-test"));

            var stored = await credStore.ReadRegistrationAsync();
            Assert.NotNull(stored);
            Assert.Equal(DeviceStatus.Active, stored!.LastKnownStatus);
            Assert.False(string.IsNullOrEmpty(stored.ServerRegistrationToken));

            // Create a fresh manager instance sharing the same credential store (simulating app restart)
            var restartManager = new LocalDeviceRegistrationManager(new MockDeviceService(), new DeviceOptions(), credStore);
            var restoredState = await restartManager.GetCurrentStateAsync();

            Assert.Equal(DeviceStatus.Active, restoredState.LastKnownStatus);
            Assert.Equal(stored.RegistrationId, restoredState.RegistrationId);
        }
    }
}
