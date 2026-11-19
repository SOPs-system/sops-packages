using System.Collections.Generic;
using System.Linq;
using Sops.Device.Fingerprinting;
using Sops.Device.Models;
using Xunit;

namespace Sops.Device.Tests
{
    public class DeviceChangeDetectorTests
    {
        [Fact]
        public void Compare_IdenticalFingerprints_ReturnsExactMatch()
        {
            var hashes = new Dictionary<string, string>
            {
                { "MachineGuid", "hash_mg" },
                { "BaseBoardProduct", "hash_bb" },
                { "SystemDrive", "hash_sd" }
            };

            var baseline = new DeviceFingerprint("combined_123", hashes, hashes.Keys.ToList(), 1);
            var current = new DeviceFingerprint("combined_123", hashes, hashes.Keys.ToList(), 1);

            var comparison = DeviceChangeDetector.Compare(baseline, current);

            Assert.False(comparison.HasChanged);
            Assert.Equal(1.0, comparison.SimilarityScore);
            Assert.Equal(DeviceComparisonConfidence.ExactMatch, comparison.Confidence);
            Assert.Empty(comparison.ChangedSignals);
            Assert.Empty(comparison.RemovedSignals);
            Assert.Empty(comparison.AddedSignals);
            Assert.Equal(3, comparison.MatchedSignals.Count);
        }

        [Fact]
        public void Compare_AddingUsbEthernetAdapter_DoesNotTriggerSignificantDrift()
        {
            // Baseline system with motherboard, disk, and built-in ethernet
            var baselineHashes = new Dictionary<string, string>
            {
                { "BaseBoardProduct", "hash_motherboard" },
                { "MachineGuid", "hash_mg" },
                { "SystemDrive", "hash_disk" },
                { "NetworkMac", "mac_builtin" }
            };

            // User plugs in a USB Ethernet dongle (NetworkMac_1 added)
            var currentHashes = new Dictionary<string, string>
            {
                { "BaseBoardProduct", "hash_motherboard" },
                { "MachineGuid", "hash_mg" },
                { "SystemDrive", "hash_disk" },
                { "NetworkMac", "mac_builtin" },
                { "NetworkMac_1", "mac_usb_dongle" }
            };

            var baseline = new DeviceFingerprint("hash_base", baselineHashes, baselineHashes.Keys.ToList(), 1);
            var current = new DeviceFingerprint("hash_curr", currentHashes, currentHashes.Keys.ToList(), 1);

            var comparison = DeviceChangeDetector.Compare(baseline, current);

            // Adding a network adapter should preserve HighConfidence / acceptable tolerance
            Assert.True(comparison.SimilarityScore >= 0.85, $"Similarity score {comparison.SimilarityScore} fell below 0.85 upon adding a USB adapter.");
            Assert.Equal(DeviceComparisonConfidence.HighConfidence, comparison.Confidence);
            Assert.Contains("NetworkMac_1", comparison.AddedSignals);
            Assert.Empty(comparison.ChangedSignals);
        }

        [Fact]
        public void Compare_ReplacingNetworkCard_MaintainsHighConfidence()
        {
            var baselineHashes = new Dictionary<string, string>
            {
                { "BaseBoardProduct", "hash_mb" },
                { "MachineGuid", "hash_mg" },
                { "SystemDrive", "hash_sd" },
                { "NetworkMac", "mac_old" }
            };

            var currentHashes = new Dictionary<string, string>
            {
                { "BaseBoardProduct", "hash_mb" },
                { "MachineGuid", "hash_mg" },
                { "SystemDrive", "hash_sd" },
                { "NetworkMac", "mac_new" } // only MAC changed
            };

            var baseline = new DeviceFingerprint("hash_base", baselineHashes, baselineHashes.Keys.ToList(), 1);
            var current = new DeviceFingerprint("hash_curr", currentHashes, currentHashes.Keys.ToList(), 1);

            var comparison = DeviceChangeDetector.Compare(baseline, current);

            Assert.True(comparison.HasChanged);
            Assert.True(comparison.SimilarityScore >= 0.80);
            Assert.Equal(DeviceComparisonConfidence.HighConfidence, comparison.Confidence);
            Assert.Single(comparison.ChangedSignals);
            Assert.Contains("NetworkMac", comparison.ChangedSignals);
            Assert.Contains("Network adapter modification detected", comparison.Assessment);
        }

        [Fact]
        public void Compare_ReplacingMotherboard_DetectsSignificantChange()
        {
            var baselineHashes = new Dictionary<string, string>
            {
                { "BaseBoardProduct", "hash_mb_old" },
                { "MachineGuid", "hash_mg_old" },
                { "SystemDrive", "hash_sd" }
            };

            var currentHashes = new Dictionary<string, string>
            {
                { "BaseBoardProduct", "hash_mb_new" },
                { "MachineGuid", "hash_mg_new" },
                { "SystemDrive", "hash_sd" }
            };

            var baseline = new DeviceFingerprint("base", baselineHashes, baselineHashes.Keys.ToList(), 1);
            var current = new DeviceFingerprint("curr", currentHashes, currentHashes.Keys.ToList(), 1);

            var comparison = DeviceChangeDetector.Compare(baseline, current);

            Assert.True(comparison.HasChanged);
            Assert.True(comparison.SimilarityScore < 0.50);
            Assert.Equal(DeviceComparisonConfidence.SignificantChange, comparison.Confidence);
            Assert.Contains("BaseBoardProduct", comparison.ChangedSignals);
            Assert.Contains("MachineGuid", comparison.ChangedSignals);
        }

        [Fact]
        public void Compare_UnavailableSignalInLiveEvaluation_DoesNotPenalizeAsHardwareRemoval()
        {
            var baselineHashes = new Dictionary<string, string>
            {
                { "BaseBoardProduct", "hash_mb" },
                { "MachineGuid", "hash_mg" },
                { "SystemDrive", "hash_sd" },
                { "OptionalSensor", "sensor_val" }
            };

            var currentHashes = new Dictionary<string, string>
            {
                { "BaseBoardProduct", "hash_mb" },
                { "MachineGuid", "hash_mg" },
                { "SystemDrive", "hash_sd" }
            };

            // Live signals show OptionalSensor failed due to AccessDenied
            var liveSignals = new List<DeviceSignal>
            {
                DeviceSignal.Available("BaseBoardProduct", "BIOS", "mb", "hash_mb"),
                DeviceSignal.Available("MachineGuid", "Reg", "mg", "hash_mg"),
                DeviceSignal.Available("SystemDrive", "Disk", "sd", "hash_sd"),
                DeviceSignal.Unavailable("OptionalSensor", "Driver", SignalStatus.AccessDenied, "Security policy blocked sensor reading")
            };

            var baseline = new DeviceFingerprint("base", baselineHashes, baselineHashes.Keys.ToList(), 1);
            var current = new DeviceFingerprint("curr", currentHashes, currentHashes.Keys.ToList(), 1);

            var comparison = DeviceChangeDetector.Compare(baseline, current, evaluatedSignals: liveSignals);

            Assert.Contains("OptionalSensor", comparison.InaccessibleSignals);
            Assert.DoesNotContain("OptionalSensor", comparison.RemovedSignals);
            // Because it was recognized as inaccessible rather than physically removed, core anchors give 100%
            Assert.Equal(1.0, comparison.SimilarityScore);
            Assert.Equal(DeviceComparisonConfidence.ExactMatch, comparison.Confidence);
        }

        [Fact]
        public void Compare_TotalHardwareChange_ReturnsDifferentDevice()
        {
            var baselineHashes = new Dictionary<string, string>
            {
                { "MachineGuid", "mg_old" },
                { "SystemDrive", "sd_old" },
                { "BaseBoardProduct", "bb_old" }
            };

            var currentHashes = new Dictionary<string, string>
            {
                { "MachineGuid", "mg_new" },
                { "SystemDrive", "sd_new" },
                { "BaseBoardProduct", "bb_new" }
            };

            var baseline = new DeviceFingerprint("base", baselineHashes, baselineHashes.Keys.ToList(), 1);
            var current = new DeviceFingerprint("curr", currentHashes, currentHashes.Keys.ToList(), 1);

            var comparison = DeviceChangeDetector.Compare(baseline, current);

            Assert.True(comparison.HasChanged);
            Assert.Equal(0.0, comparison.SimilarityScore);
            Assert.Equal(DeviceComparisonConfidence.DifferentDevice, comparison.Confidence);
            Assert.Equal(3, comparison.ChangedSignals.Count);
        }
    }
}
