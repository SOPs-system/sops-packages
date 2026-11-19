using System;
using System.Linq;
using System.Threading.Tasks;
using Sops.Device.Management;
using Sops.Device.Models;
using Xunit;

namespace Sops.Device.Tests
{
    public class DeviceManagementLifecycleTests
    {
        [Fact]
        public async Task RegisterAsync_ValidRequest_EstablishesActiveDevice()
        {
            var registry = new InMemoryDeviceRegistry();
            var ownership = new DeviceOwnership("tenant-alpha", "branch-main", "PHARM-100", "operator-1");
            var metadata = new DeviceMetadata(hostname: "POS-DESK-01", appVersion: "1.0.0");

            var request = new DeviceRegistrationRequest(
                idempotencyToken: "idemp-001",
                deviceCode: "SOP-WIN-742G-K92P-X",
                installationId: "00000000-0000-0000-0000-000000000001",
                displayName: "Front Desk POS 1",
                platform: DevicePlatform.Windows,
                ownership: ownership,
                metadata: metadata);

            var result = await registry.RegisterAsync(request);

            Assert.True(result.Success);
            Assert.Equal(DeviceStatus.Active, result.Status);
            Assert.False(string.IsNullOrWhiteSpace(result.RegistrationId));
            Assert.False(string.IsNullOrWhiteSpace(result.ServerToken));
            Assert.NotNull(result.Record);
            Assert.Equal("Front Desk POS 1", result.Record!.DisplayName);
            Assert.Equal("tenant-alpha", result.Record.Ownership.TenantId);

            // Audit log check
            var audits = await registry.GetAuditEventsAsync(result.RegistrationId!);
            Assert.NotEmpty(audits);
            Assert.Contains(audits, a => a.EventType == "Registered");
        }

        [Fact]
        public async Task RegisterAsync_SameIdempotencyToken_ReturnsCachedResult()
        {
            var registry = new InMemoryDeviceRegistry();
            var ownership = new DeviceOwnership("tenant-alpha");
            var request = new DeviceRegistrationRequest(
                idempotencyToken: "idemp-retry-1",
                deviceCode: "SOP-WIN-742G-K92P-X",
                installationId: "00000000-0000-0000-0000-000000000001",
                displayName: "Front Desk POS 1",
                platform: DevicePlatform.Windows,
                ownership: ownership);

            var firstCall = await registry.RegisterAsync(request);
            var secondCall = await registry.RegisterAsync(request);

            Assert.True(secondCall.Success);
            Assert.Equal(firstCall.RegistrationId, secondCall.RegistrationId);
            Assert.Equal(firstCall.ServerToken, secondCall.ServerToken);
        }

        [Fact]
        public async Task RegisterAsync_SameTokenWithConflictingPayload_ReturnsIdempotencyConflict()
        {
            var registry = new InMemoryDeviceRegistry();
            var ownershipA = new DeviceOwnership("tenant-alpha");
            var requestA = new DeviceRegistrationRequest(
                idempotencyToken: "idemp-clash",
                deviceCode: "SOP-WIN-742G-K92P-X",
                installationId: "00000000-0000-0000-0000-000000000001",
                displayName: "Front Desk POS 1",
                platform: DevicePlatform.Windows,
                ownership: ownershipA);

            await registry.RegisterAsync(requestA);

            // Same token, differing installation and name
            var requestB = new DeviceRegistrationRequest(
                idempotencyToken: "idemp-clash",
                deviceCode: "SOP-WIN-742G-K92P-X",
                installationId: "99999999-9999-9999-9999-999999999999",
                displayName: "Back Office POS",
                platform: DevicePlatform.Windows,
                ownership: ownershipA);

            var resultB = await registry.RegisterAsync(requestB);

            Assert.False(resultB.Success);
            Assert.Equal("IDEMPOTENCY_CONFLICT", resultB.ErrorCode);
        }

        [Fact]
        public async Task RegisterAsync_DeviceCodeCollisionFromDifferentInstallation_Rejected()
        {
            var registry = new InMemoryDeviceRegistry();
            var ownership = new DeviceOwnership("tenant-alpha");

            // Installation 1 registers code SOP-WIN-742G-K92P-X
            var request1 = new DeviceRegistrationRequest(
                idempotencyToken: "idemp-dev1",
                deviceCode: "SOP-WIN-742G-K92P-X",
                installationId: "11111111-1111-1111-1111-111111111111",
                displayName: "POS 1",
                platform: DevicePlatform.Windows,
                ownership: ownership);
            await registry.RegisterAsync(request1);

            // Installation 2 presents the same code
            var request2 = new DeviceRegistrationRequest(
                idempotencyToken: "idemp-dev2",
                deviceCode: "SOP-WIN-742G-K92P-X",
                installationId: "22222222-2222-2222-2222-222222222222",
                displayName: "POS 2",
                platform: DevicePlatform.Windows,
                ownership: ownership);
            var result2 = await registry.RegisterAsync(request2);

            Assert.False(result2.Success);
            Assert.Equal("DEVICE_CODE_COLLISION", result2.ErrorCode);
        }

        [Fact]
        public async Task ReportHeartbeatAsync_ActiveDevice_UpdatesTimestamp()
        {
            var registry = new InMemoryDeviceRegistry();
            var ownership = new DeviceOwnership("tenant-alpha");
            var reg = await registry.RegisterAsync(new DeviceRegistrationRequest(
                "token-hb", "SOP-WIN-AAAA-BBBB-C", "00000000-0000-0000-0000-000000000001", "POS 1", DevicePlatform.Windows, ownership));

            var heartbeat = new DeviceHeartbeat(reg.RegistrationId!, "SOP-WIN-AAAA-BBBB-C", "00000000-0000-0000-0000-000000000001", 1, "1.0.1", "ProcessingOrders");
            var hbResult = await registry.ReportHeartbeatAsync(heartbeat);

            Assert.True(hbResult.Acknowledged);
            Assert.Equal(DeviceStatus.Active, hbResult.Status);
            Assert.False(hbResult.RequiresRevocation);

            var record = await registry.GetAsync(reg.RegistrationId!);
            Assert.NotNull(record!.LastHeartbeatUtc);
            Assert.Equal("1.0.1", record.Metadata.AppVersion);
        }

        [Fact]
        public async Task AdministrativeOperations_SuspendReactivateRevokeReplace_FullLifecycle()
        {
            var registry = new InMemoryDeviceRegistry();
            var ownership = new DeviceOwnership("tenant-alpha", "branch-1");
            var reg = await registry.RegisterAsync(new DeviceRegistrationRequest(
                "token-admin", "SOP-WIN-1111-2222-K", "00000000-0000-0000-0000-000000000001", "Counter POS", DevicePlatform.Windows, ownership));

            string regId = reg.RegistrationId!;

            // 1. Rename
            var renamed = await registry.RenameDeviceAsync(regId, "Counter POS Renovated", "admin-user");
            Assert.Equal("Counter POS Renovated", renamed.DisplayName);

            // 2. Reassign
            var reassigned = await registry.ReassignDeviceAsync(regId, new DeviceOwnership("tenant-alpha", "branch-2"), "admin-user");
            Assert.Equal("branch-2", reassigned.Ownership.BranchId);

            // 3. Suspend
            var suspended = await registry.SuspendDeviceAsync(regId, "Suspicious activity detected", "security-officer");
            Assert.Equal(DeviceStatus.Suspended, suspended.Status);

            // Heartbeat during suspension instructs client to pause operations
            var hbSuspended = await registry.ReportHeartbeatAsync(new DeviceHeartbeat(regId, "SOP-WIN-1111-2222-K", "00000000-0000-0000-0000-000000000001"));
            Assert.Equal(DeviceStatus.Suspended, hbSuspended.Status);
            Assert.True(hbSuspended.RequiresRevocation);

            // 4. Reactivate
            var reactivated = await registry.ReactivateDeviceAsync(regId, "Security review completed", "security-officer");
            Assert.Equal(DeviceStatus.Active, reactivated.Status);

            // 5. Replace
            var replaced = await registry.ReplaceDeviceAsync(new DeviceReplacementRequest(
                regId, "00000000-0000-0000-0000-000000000002", "SOP-WIN-3333-4444-M", "Hardware motherboard failure", "tech-1"));
            Assert.Equal(DeviceStatus.Replaced, replaced.Status);

            // 6. Revoke
            var revoked = await registry.RevokeDeviceAsync(new DeviceRevocation(regId, "Permanently retired", "admin-user"));
            Assert.Equal(DeviceStatus.Revoked, revoked.Status);

            // Reactivating a revoked device must throw
            await Assert.ThrowsAsync<InvalidOperationException>(() => registry.ReactivateDeviceAsync(regId, "Reopen", "admin-user"));

            // Verify comprehensive audit history
            var auditEvents = await registry.GetAuditEventsAsync(regId);
            Assert.Contains(auditEvents, a => a.EventType == "Registered");
            Assert.Contains(auditEvents, a => a.EventType == "Renamed");
            Assert.Contains(auditEvents, a => a.EventType == "Reassigned");
            Assert.Contains(auditEvents, a => a.EventType == "Suspended");
            Assert.Contains(auditEvents, a => a.EventType == "Reactivated");
            Assert.Contains(auditEvents, a => a.EventType == "Replaced");
            Assert.Contains(auditEvents, a => a.EventType == "Revoked");
        }
    }
}
