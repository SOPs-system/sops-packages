using System.Collections.Generic;
using Sops.Device.Fingerprinting;
using Sops.Device.Models;
using Xunit;

namespace Sops.Device.Tests
{
    public class DeviceFingerprintGeneratorTests
    {
        [Fact]
        public void Generate_CanonicalSorting_ProducesIdenticalHashRegardlessOfOrder()
        {
            var sig1 = DeviceSignal.Available("MachineGuid", "Registry", "val1", "hash1");
            var sig2 = DeviceSignal.Available("BaseBoardProduct", "BIOS", "val2", "hash2");
            var sig3 = DeviceSignal.Available("SystemDrive", "Disk", "val3", "hash3");

            var listA = new List<DeviceSignal> { sig1, sig2, sig3 };
            var listB = new List<DeviceSignal> { sig3, sig1, sig2 };
            var listC = new List<DeviceSignal> { sig2, sig3, sig1 };

            var fpA = DeviceFingerprintGenerator.Generate(listA, salt: "sops-salt");
            var fpB = DeviceFingerprintGenerator.Generate(listB, salt: "sops-salt");
            var fpC = DeviceFingerprintGenerator.Generate(listC, salt: "sops-salt");

            Assert.Equal(fpA.CombinedHash, fpB.CombinedHash);
            Assert.Equal(fpA.CombinedHash, fpC.CombinedHash);
            Assert.Equal(3, fpA.IncludedSignals.Count);
            Assert.Equal("sops-salt", fpA.Scope);
        }

        [Fact]
        public void Generate_WithDifferentSalts_GeneratesDifferentCombinedHashes()
        {
            var sig1 = DeviceSignal.Available("MachineGuid", "Registry", "val1", "hash1");
            var signals = new List<DeviceSignal> { sig1 };

            var fp1 = DeviceFingerprintGenerator.Generate(signals, salt: "tenant-1");
            var fp2 = DeviceFingerprintGenerator.Generate(signals, salt: "tenant-2");

            Assert.NotEqual(fp1.CombinedHash, fp2.CombinedHash);
            Assert.Equal("tenant-1", fp1.Scope);
            Assert.Equal("tenant-2", fp2.Scope);
        }

        [Fact]
        public void Generate_ExcludesRejectedOrUnavailableSignals()
        {
            var available = DeviceSignal.Available("ValidSig", "Src", "val1", "hash1");
            var rejected = DeviceSignal.Rejected("BadSig", "Src", "OEM placeholder");
            var unavailable = DeviceSignal.Unavailable("MissingSig", "Src", SignalStatus.Unavailable, "Not found");

            var fp = DeviceFingerprintGenerator.Generate(new[] { available, rejected, unavailable });

            Assert.Single(fp.IncludedSignals);
            Assert.Contains("ValidSig", fp.IncludedSignals);
            Assert.DoesNotContain("BadSig", fp.IncludedSignals);
            Assert.DoesNotContain("MissingSig", fp.IncludedSignals);
        }

        [Fact]
        public void Generate_EmptySignals_GeneratesEmptyFingerprintSafely()
        {
            var fp = DeviceFingerprintGenerator.Generate(new List<DeviceSignal>());

            Assert.NotNull(fp);
            Assert.Equal(1, fp.FingerprintVersion);
            Assert.Empty(fp.IncludedSignals);
            Assert.Empty(fp.CombinedHash);
        }
    }
}
