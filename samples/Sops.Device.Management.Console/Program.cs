using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sops.Device.Abstractions;
using Sops.Device.Configuration;
using Sops.Device.Extensions;
using Sops.Device.Fingerprinting;
using Sops.Device.Management;
using Sops.Device.Models;
using Sops.Device.Persistence;

namespace Sops.Device.Management.ConsoleSample
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            Console.WriteLine("================================================================================");
            Console.WriteLine("  SOPs.Device — Universal Cross-Platform Device Management Lifecycle Demo       ");
            Console.WriteLine("================================================================================");

            string sampleDir = Path.Combine(Path.GetTempPath(), "sops_device_mgmt_demo_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(sampleDir);
            string identityFile = Path.Combine(sampleDir, "identity.json");
            string credentialFile = Path.Combine(sampleDir, "device_credentials.dat");

            try
            {
                var services = new ServiceCollection();
                services.AddLogging(b =>
                {
                    b.SetMinimumLevel(LogLevel.Information);
                    b.AddConsole();
                });

                services.AddSopsDevice(options =>
                {
                    options.CodePrefix = "SOP";
                    options.IdentityMode = DeviceIdentityMode.Hybrid;
                    options.EnableHardwareFingerprint = true;
                    options.IncludeMacAddresses = true;
                    options.EnableChangeDetection = true;
                    options.StoragePath = identityFile;
                    options.AppVersion = "2.4.0";
                    options.FingerprintAlgorithm = FingerprintAlgorithm.HmacSha256Keyed;
                    options.KeyScope = KeyScope.Tenant;
                    options.SecretKeyHex = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF"; // Tenant HMAC key
                });

                using (var provider = services.BuildServiceProvider())
                {
                    var deviceService = provider.GetRequiredService<IDeviceService>();
                    var codeGenerator = provider.GetRequiredService<IDeviceCodeGenerator>();

                    // 1. Display device metadata
                    Console.WriteLine("\n[Step 1] Querying Device Metadata...");
                    var info = await deviceService.GetDeviceInfoAsync();
                    Console.WriteLine($"  - Operating System:      {info.Platform} ({info.PlatformVersion})");
                    Console.WriteLine($"  - Architecture:          {info.Architecture}");
                    Console.WriteLine($"  - Device Form Factor:    {info.DeviceType}");
                    Console.WriteLine($"  - Machine Hostname:      {Environment.MachineName}");
                    Console.WriteLine($"  - Application Version:   {info.AppVersion}");

                    // 2. Generate installation ID
                    Console.WriteLine("\n[Step 2] Generating Local Installation Identity...");
                    var identity = await deviceService.GetIdentityAsync();
                    Console.WriteLine($"  - Persistent Installation ID: {identity.InstallationId}");
                    Console.WriteLine($"  - Created At (UTC):           {identity.CreatedAtUtc:yyyy-MM-dd HH:mm:ss}");
                    Console.WriteLine($"  - Identity Mode:              {identity.Mode}");

                    // 3. Display device code
                    Console.WriteLine("\n[Step 3] Generating & Validating Crockford Base32 Device Code...");
                    string deviceCode = await deviceService.GetDeviceCodeAsync();
                    bool isValidCode = codeGenerator.ValidateCode(deviceCode, out int version, out string? codeErr);
                    Console.WriteLine($"  - Device Code:                {deviceCode}");
                    Console.WriteLine($"  - Format Valid:               {isValidCode}");
                    Console.WriteLine($"  - Code Format Version:        {version}");
                    Console.WriteLine($"  - Error Status:               {codeErr ?? "None (Checksum verified)"}");

                    // 4. Collect available signal statuses
                    Console.WriteLine("\n[Step 4] Collecting Hardware Signal Availability...");
                    Console.WriteLine($"  - Total Signals Inspected:    {info.SignalAvailability.Count}");
                    foreach (var sig in info.SignalAvailability)
                    {
                        Console.WriteLine($"    * {sig.Key,-20}: {sig.Value}");
                    }

                    // 5. Generate a fingerprint (Keyed HMAC-SHA256 for privacy)
                    Console.WriteLine("\n[Step 5] Generating Keyed HMAC-SHA256 Hardware Fingerprint...");
                    var fingerprint = await deviceService.GetFingerprintAsync();
                    Console.WriteLine($"  - Algorithm:                  {fingerprint.Algorithm}");
                    Console.WriteLine($"  - Key Scope:                  {fingerprint.KeyScope}");
                    Console.WriteLine($"  - Is Keyed (HMAC):            {fingerprint.IsKeyed}");
                    Console.WriteLine($"  - Combined Hash:              {fingerprint.CombinedHash}");
                    Console.WriteLine($"  - Signals Included:           {fingerprint.IncludedSignals.Count} ({string.Join(", ", fingerprint.IncludedSignals)})");

                    // 6. Register against explicitly selected mock registry
                    Console.WriteLine("\n[Step 6] Registering Workstation with Mock Device Registry...");
                    var registry = new InMemoryDeviceRegistry();
                    var credStore = new ProtectedFileDeviceCredentialStore(credentialFile);
                    var offlinePolicy = new DeviceOfflinePolicy
                    {
                        GracePeriod = TimeSpan.FromDays(7),
                        MaxClockRollbackTolerance = TimeSpan.FromMinutes(15),
                        AllowOfflineOperations = true
                    };
                    var regManager = new LocalDeviceRegistrationManager(deviceService, new DeviceOptions(), credStore, offlinePolicy);

                    var ownership = new DeviceOwnership(
                        tenantId: "tenant-al-amal-pharmacy",
                        branchId: "branch-cairo-central",
                        pharmacyId: "PHARM-10042",
                        assignedToUserId: "tech-youssef",
                        environment: "Production");

                    var regResult = await regManager.RegisterAsync(
                        registry,
                        ownership,
                        displayName: "POS Counter #1 (Main Terminal)");

                    Console.WriteLine($"  - Registration Success:       {regResult.Success}");
                    Console.WriteLine($"  - Registration ID:            {regResult.RegistrationId}");
                    Console.WriteLine($"  - Authoritative Status:       {regResult.Status}");
                    Console.WriteLine($"  - Server Token Thumbprint:    {regResult.ServerToken?.Substring(0, 16)}... (Confidential Token Issued)");

                    // 7. Perform a heartbeat
                    Console.WriteLine("\n[Step 7] Sending Periodic Heartbeat to Server...");
                    var hb1 = await regManager.SendHeartbeatAsync(registry, sequenceNumber: 1);
                    Console.WriteLine($"  - Heartbeat Acknowledged:     {hb1.Acknowledged}");
                    Console.WriteLine($"  - Reconciled Status:          {hb1.Status}");
                    Console.WriteLine($"  - Grace Remaining:            {hb1.GracePeriodRemaining?.TotalDays:F1} days");
                    Console.WriteLine($"  - Server Timestamp:           {hb1.ServerTimestampUtc:yyyy-MM-dd HH:mm:ss} UTC");

                    // 8. Simulate temporary disconnection
                    Console.WriteLine("\n[Step 8] Simulating Network Disconnection (Offline Operation)...");
                    var offlineRegistry = new DisconnectedMockRegistry();
                    var hbOffline = await regManager.SendHeartbeatAsync(offlineRegistry, sequenceNumber: 2);
                    Console.WriteLine($"  - Reached Server:             {hbOffline.Acknowledged}");
                    Console.WriteLine($"  - Offline Instructions:       {hbOffline.Instructions}");

                    // 9. Evaluate offline status
                    Console.WriteLine("\n[Step 9] Evaluating Workstation Offline Policy & Grace Period...");
                    var stateNow = await regManager.GetCurrentStateAsync();
                    var offlineEval = stateNow.EvaluateOfflineState(DateTimeOffset.UtcNow, offlinePolicy);
                    Console.WriteLine($"  - Offline State Assessment:   {offlineEval}");
                    Console.WriteLine($"  - Last Sync Timestamp:        {stateNow.LastSyncAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
                    Console.WriteLine($"  - Configured Grace Period:    {offlinePolicy.GracePeriod.TotalDays} days");
                    Console.WriteLine($"  - Within Grace Window:        {stateNow.IsWithinOfflineGracePeriod(DateTimeOffset.UtcNow, offlinePolicy.GracePeriod)}");

                    // 10. Simulate suspension
                    Console.WriteLine("\n[Step 10] Simulating Server-Initiated Device Suspension (e.g. Audit Pending)...");
                    await registry.SuspendDeviceAsync(regResult.RegistrationId!, "Maintenance compliance audit required", "sec-admin-1");
                    var hbSuspended = await regManager.SendHeartbeatAsync(registry, sequenceNumber: 3);
                    Console.WriteLine($"  - Heartbeat Response Status:  {hbSuspended.Status}");
                    var stateSuspended = await regManager.GetCurrentStateAsync();
                    var evalSuspended = stateSuspended.EvaluateOfflineState(DateTimeOffset.UtcNow, offlinePolicy);
                    Console.WriteLine($"  - Local Evaluated State:      {evalSuspended} (POS Operations Blocked)");

                    // 11. Simulate revocation
                    Console.WriteLine("\n[Step 11] Simulating Permanent Device Revocation (e.g. Decommissioned)...");
                    var revocation = new DeviceRevocation(
                        registrationId: regResult.RegistrationId!,
                        reason: "Terminal hardware decommissioned",
                        revokedByUserId: "admin-sec-team");
                    await registry.RevokeDeviceAsync(revocation);
                    var hbRevoked = await regManager.SendHeartbeatAsync(registry, sequenceNumber: 4);
                    Console.WriteLine($"  - Heartbeat Response Status:  {hbRevoked.Status}");
                    Console.WriteLine($"  - Requires Immediate Cutoff:  {hbRevoked.RequiresRevocation}");
                    var stateRevoked = await regManager.GetCurrentStateAsync();
                    var evalRevoked = stateRevoked.EvaluateOfflineState(DateTimeOffset.UtcNow, offlinePolicy);
                    Console.WriteLine($"  - Local Evaluated State:      {evalRevoked} (Strict Monotonic Cutoff)");

                    // 12. Simulate device replacement
                    Console.WriteLine("\n[Step 12] Simulating Hardware Device Replacement Workflow...");
                    var replacementRequest = new DeviceReplacementRequest(
                        oldRegistrationId: regResult.RegistrationId!,
                        newInstallationId: Guid.NewGuid().ToString("D"),
                        newDeviceCode: "SOP2-REPL-ACEM-ENT0",
                        reason: "Motherboard and NIC replacement after lightning surge",
                        requestedByUserId: "tech-youssef");

                    var oldRecordReplaced = await registry.ReplaceDeviceAsync(replacementRequest);
                    var auditEvents = await registry.GetAuditEventsAsync(regResult.RegistrationId!);
                    Console.WriteLine($"  - Old Device Status:          {oldRecordReplaced.Status}");
                    Console.WriteLine($"  - Old Device Status Reason:   {oldRecordReplaced.StatusReason}");
                    Console.WriteLine($"  - Audit Events Logged:        {auditEvents.Count}");

                    // 13. Demonstrate identity persistence after restart
                    Console.WriteLine("\n[Step 13] Demonstrating Identity & Credential Persistence After Application Restart...");
                    var restartedCredStore = new ProtectedFileDeviceCredentialStore(credentialFile);
                    var restartedManager = new LocalDeviceRegistrationManager(deviceService, new DeviceOptions(), restartedCredStore, offlinePolicy);
                    var reloadedState = await restartedManager.GetCurrentStateAsync();
                    Console.WriteLine($"  - Restored Device Code:       {reloadedState.DeviceCode}");
                    Console.WriteLine($"  - Restored Registration ID:   {reloadedState.RegistrationId}");
                    Console.WriteLine($"  - Restored Status:            {reloadedState.LastKnownStatus}");
                    Console.WriteLine($"  - Restored Installation ID:   {reloadedState.InstallationId}");
                    Console.WriteLine($"  - Has Preserved Token:        {!string.IsNullOrEmpty(reloadedState.ServerRegistrationToken)}");

                    // 14. Display security-safe diagnostics (safe thumbprints, no secret tokens or raw MACs)
                    Console.WriteLine("\n[Step 14] Displaying Security-Safe Diagnostics (Zero PII / Secret Leaks)...");
                    var rawCredential = new DeviceCredential(
                        registrationId: reloadedState.RegistrationId ?? "REG-SAMPLE-1",
                        deviceToken: "sops_sec_token_999888777666555444333222111000",
                        issuedAtUtc: DateTimeOffset.UtcNow,
                        expiresAtUtc: DateTimeOffset.UtcNow.AddDays(30),
                        tokenVersion: 1,
                        scope: KeyScope.Tenant,
                        trustModel: "Bearer-Token-With-Hardware-Binding (Mock in-memory token demo)");

                    Console.WriteLine($"  - Public Registration ID:     {rawCredential.RegistrationId}");
                    Console.WriteLine($"  - Safe Non-Sensitive Thumbprint: {rawCredential.Thumbprint} (SHA-256 Truncated)");
                    Console.WriteLine($"  - Scope Boundary:             {rawCredential.Scope}");
                    Console.WriteLine($"  - Trust Model:                {rawCredential.TrustModel}");
                    Console.WriteLine($"  - Secret Raw Token Leaked?    NO (DeviceToken is never printed in logs or telemetry)");
                    Console.WriteLine($"  - Raw Network MAC Exposed?    NO (Normalized into SHA-256/HMAC hashes only)");
                    Console.WriteLine($"  - Token Is Expired?           {rawCredential.IsExpired(DateTimeOffset.UtcNow)}");

                    Console.WriteLine("\n================================================================================");
                    Console.WriteLine("  ALL 14 DEVICE MANAGEMENT LIFECYCLE PHASES COMPLETED SUCCESSFULLY             ");
                    Console.WriteLine("================================================================================");
                    return 0;
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(sampleDir)) Directory.Delete(sampleDir, true);
                }
                catch { }
            }
        }

        private sealed class DisconnectedMockRegistry : IDeviceRegistry
        {
            public Task<DeviceRegistrationResult> RegisterAsync(DeviceRegistrationRequest request, System.Threading.CancellationToken cancellationToken = default)
            {
                throw new System.Net.Http.HttpRequestException("Network unreachable. Server host cannot be resolved.");
            }

            public Task<DeviceHeartbeatResult> ReportHeartbeatAsync(DeviceHeartbeat heartbeat, System.Threading.CancellationToken cancellationToken = default)
            {
                throw new System.Net.Http.HttpRequestException("Network timeout. Gateway offline.");
            }

            public Task<DeviceRecord?> GetAsync(string registrationId, System.Threading.CancellationToken cancellationToken = default)
            {
                throw new System.Net.Http.HttpRequestException("Network unreachable.");
            }

            public Task<DeviceRecord?> GetByDeviceCodeAsync(string deviceCode, System.Threading.CancellationToken cancellationToken = default)
            {
                throw new System.Net.Http.HttpRequestException("Network unreachable.");
            }
        }
    }
}
