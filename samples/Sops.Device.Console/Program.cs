using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sops.Device.Abstractions;
using Sops.Device.Configuration;
using Sops.Device.Extensions;
using Sops.Device.Models;

namespace Sops.Device.ConsoleSample
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            Console.WriteLine("======================================================================");
            Console.WriteLine("  SOPs.Device — Universal Cross-Platform Device Identity SDK Demo     ");
            Console.WriteLine("======================================================================");

            // Configure DI container
            string tempStorage = Path.Combine(Path.GetTempPath(), "sops_device_sample_" + Guid.NewGuid().ToString("N"));
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
                options.StoragePath = Path.Combine(tempStorage, "identity.json");
                options.AppVersion = "2.4.0";
            });

            using (var provider = services.BuildServiceProvider())
            {
                var deviceService = provider.GetRequiredService<IDeviceService>();
                var codeGenerator = provider.GetRequiredService<IDeviceCodeGenerator>();

                // 1. Get Device Info and Code
                Console.WriteLine("\n[1] Querying Device Metadata...");
                var info = await deviceService.GetDeviceInfoAsync();
                string code = await deviceService.GetDeviceCodeAsync();
                Console.WriteLine($"    Platform:        {info.Platform} ({info.PlatformVersion})");
                Console.WriteLine($"    Architecture:    {info.Architecture}");
                Console.WriteLine($"    Device Type:     {info.DeviceType}");
                Console.WriteLine($"    Device Code:     {code}");
                Console.WriteLine($"    Installation ID: {info.InstallationId}");
                Console.WriteLine($"    Signals Evaluated: {info.SignalAvailability.Count}");
                foreach (var sig in info.SignalAvailability)
                {
                    Console.WriteLine($"      - {sig.Key}: {sig.Value}");
                }

                // 2. Validate Generated Device Code
                Console.WriteLine("\n[2] Validating Device Code Format & Checksum...");
                bool isValid = codeGenerator.ValidateCode(code, out int codeVersion, out string? error);
                Console.WriteLine($"    Code '{code}' Valid: {isValid} (Format Version: {codeVersion}, Error: {error ?? "none"})");
                if (!isValid) return 1;

                // 3. Get Full Device Identity
                Console.WriteLine("\n[3] Querying Device Identity...");
                var identity = await deviceService.GetIdentityAsync();
                Console.WriteLine($"    Mode:            {identity.Mode}");
                Console.WriteLine($"    Created At:      {identity.CreatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine($"    Last Validated:  {identity.LastValidatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");

                // 4. Compute Current Hardware Fingerprint
                Console.WriteLine("\n[4] Computing Hardware Fingerprint...");
                var fingerprint = await deviceService.GetFingerprintAsync();
                Console.WriteLine($"    Fingerprint Ver: {fingerprint.FingerprintVersion}");
                Console.WriteLine($"    Combined Hash:   {fingerprint.CombinedHash}");
                Console.WriteLine($"    Included Signals ({fingerprint.IncludedSignals.Count}): {string.Join(", ", fingerprint.IncludedSignals)}");

                // 5. Compare with Previous Fingerprint (Exact Match Test)
                Console.WriteLine("\n[5] Comparing Current Fingerprint against Baseline (Self-Check)...");
                var selfComparison = await deviceService.CompareAsync(fingerprint);
                Console.WriteLine($"    Has Changed:     {selfComparison.HasChanged}");
                Console.WriteLine($"    Similarity:      {selfComparison.SimilarityScore:P1}");
                Console.WriteLine($"    Confidence:      {selfComparison.Confidence}");
                Console.WriteLine($"    Assessment:      {selfComparison.Assessment}");

                // 6. Simulate Hardware Drift (e.g. NIC upgrade)
                Console.WriteLine("\n[6] Simulating Hardware Modification (Single NIC replacement)...");
                var modifiedHashes = new System.Collections.Generic.Dictionary<string, string>(fingerprint.SignalHashes);
                if (modifiedHashes.ContainsKey("NetworkMac"))
                {
                    modifiedHashes["NetworkMac"] = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
                }
                else
                {
                    modifiedHashes["NetworkMac"] = "new_adapter_hash";
                }

                var simulatedFp = new DeviceFingerprint("simulated_hash", modifiedHashes, fingerprint.IncludedSignals, fingerprint.FingerprintVersion);
                var driftComparison = await deviceService.CompareAsync(simulatedFp);
                Console.WriteLine($"    Has Changed:     {driftComparison.HasChanged}");
                Console.WriteLine($"    Similarity:      {driftComparison.SimilarityScore:P1}");
                Console.WriteLine($"    Confidence:      {driftComparison.Confidence}");
                Console.WriteLine($"    Matched Signals: {string.Join(", ", driftComparison.MatchedSignals)}");
                Console.WriteLine($"    Changed Signals: {string.Join(", ", driftComparison.ChangedSignals)}");
                Console.WriteLine($"    Assessment:      {driftComparison.Assessment}");

                // 7. Demonstrate Device Management & Registration
                Console.WriteLine("\n[7] Testing Universal Device Management & Registration Lifecycle...");
                var registry = new Sops.Device.Management.InMemoryDeviceRegistry();
                var regManager = new Sops.Device.Management.LocalDeviceRegistrationManager(deviceService, new DeviceOptions());

                var regResult = await regManager.RegisterAsync(
                    registry,
                    new Sops.Device.Management.DeviceOwnership("pharmacy-chain-1", "branch-downtown", "PHARM-902", "pos-tech-1"),
                    displayName: "POS Counter 1");
                Console.WriteLine($"    Registered:      {regResult.Success} (Status: {regResult.Status})");
                Console.WriteLine($"    Registration ID: {regResult.RegistrationId}");
                Console.WriteLine($"    Server Token:    {regResult.ServerToken?.Substring(0, 16)}...");

                var hbResult = await regManager.SendHeartbeatAsync(registry, sequenceNumber: 1);
                Console.WriteLine($"    Heartbeat Ping:  Acknowledged={hbResult.Acknowledged}, Status={hbResult.Status}");
                var localState = await regManager.GetCurrentStateAsync();
                Console.WriteLine($"    Offline Grace:   Active={localState.IsWithinOfflineGracePeriod(DateTimeOffset.UtcNow)} (LastSync: {localState.LastSyncAtUtc:HH:mm:ss})");

                // Cleanup temporary storage
                try
                {
                    if (Directory.Exists(tempStorage)) Directory.Delete(tempStorage, true);
                }
                catch { }

                Console.WriteLine("\n======================================================================");
                Console.WriteLine("  ALL DEVICE SDK CHECKS COMPLETED SUCCESSFULLY                        ");
                Console.WriteLine("======================================================================");
                return 0;
            }
        }
    }
}
