using System;
using System.Collections.Generic;
using System.Text;
using Sops.Device.Fingerprinting;
using Sops.Device.Models;
using Xunit;

namespace Sops.Device.Tests
{
    public class DeviceFingerprintPrivacyTests
    {
        [Fact]
        public void ComputeHmacSha256_ValidInputs_ProducesDeterministicHex()
        {
            byte[] key = Encoding.UTF8.GetBytes("pharmacy-tenant-secret-key-32bytes");
            string normalizedValue = "00:1A:2B:3C:4D:5E";

            string hash1 = DeviceSignalNormalizer.ComputeHmacSha256(normalizedValue, key);
            string hash2 = DeviceSignalNormalizer.ComputeHmacSha256(normalizedValue, key);

            Assert.False(string.IsNullOrEmpty(hash1));
            Assert.Equal(64, hash1.Length);
            Assert.Equal(hash1, hash2);
        }

        [Fact]
        public void ComputeHmacSha256_DifferentKeys_ProduceDifferentHashes()
        {
            // Tenant isolation test: the same physical MAC address must produce distinct hashes for different tenants
            byte[] tenantKeyA = Encoding.UTF8.GetBytes("tenant-a-secret-key-000000000000");
            byte[] tenantKeyB = Encoding.UTF8.GetBytes("tenant-b-secret-key-000000000000");
            string mac = "00:1A:2B:3C:4D:5E";

            string hashA = DeviceSignalNormalizer.ComputeHmacSha256(mac, tenantKeyA);
            string hashB = DeviceSignalNormalizer.ComputeHmacSha256(mac, tenantKeyB);

            Assert.NotEqual(hashA, hashB);
        }

        [Fact]
        public void ComputeHmacSha256_EmptyOrNullKey_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => DeviceSignalNormalizer.ComputeHmacSha256("value", Array.Empty<byte>()));
            Assert.Throws<ArgumentException>(() => DeviceSignalNormalizer.ComputeHmacSha256("value", null!));
        }

        [Fact]
        public void GenerateKeyed_WithTenantSecret_GeneratesVersion2KeyedFingerprint()
        {
            byte[] secretKey = Encoding.UTF8.GetBytes("my-super-secret-tenant-key-1234");
            var signals = new List<DeviceSignal>
            {
                DeviceSignal.Available("MachineGuid", "Registry", "e8f3b2a1-0000-0000-0000-000000000001", "unkeyed_placeholder_hash"),
                DeviceSignal.Available("BaseBoardProduct", "Registry", "OptiPlex 7090", "unkeyed_placeholder_hash")
            };

            var fp = DeviceFingerprintGenerator.GenerateKeyed(signals, secretKey, scope: "tenant-pharmacy-1", keyScope: KeyScope.Tenant);

            Assert.Equal(2, fp.FingerprintVersion);
            Assert.Equal(FingerprintAlgorithm.HmacSha256Keyed, fp.Algorithm);
            Assert.Equal(KeyScope.Tenant, fp.KeyScope);
            Assert.True(fp.IsKeyed);
            Assert.Equal("tenant-pharmacy-1", fp.Scope);
            Assert.Equal(2, fp.IncludedSignals.Count);
            Assert.False(string.IsNullOrEmpty(fp.CombinedHash));
            Assert.Equal(64, fp.CombinedHash.Length);

            // Re-generation produces exact match
            var fp2 = DeviceFingerprintGenerator.GenerateKeyed(signals, secretKey, scope: "tenant-pharmacy-1", keyScope: KeyScope.Tenant);
            Assert.Equal(fp.CombinedHash, fp2.CombinedHash);
        }

        [Fact]
        public void Version1_UnkeyedSha256_BackwardCompatibilityPreserved()
        {
            var signals = new List<DeviceSignal>
            {
                DeviceSignal.Available("MachineGuid", "Registry", "e8f3b2a1-0000-0000-0000-000000000001", "mockhash1"),
                DeviceSignal.Available("BaseBoardProduct", "Registry", "OptiPlex 7090", "mockhash2")
            };

            var fpV1 = DeviceFingerprintGenerator.Generate(signals, version: 1, salt: "sops:device:v1");

            Assert.Equal(1, fpV1.FingerprintVersion);
            Assert.Equal(FingerprintAlgorithm.Sha256Unkeyed, fpV1.Algorithm);
            Assert.False(fpV1.IsKeyed);
            Assert.Equal("sops:device:v1", fpV1.Scope);
            Assert.False(string.IsNullOrEmpty(fpV1.CombinedHash));
        }

        [Fact]
        public void Migration_FromV1ToV2_PreservesHardwareContinuity()
        {
            string salt = "sops:device:v1";
            string guidVal = "e8f3b2a1-0000-0000-0000-000000000001";
            string boardVal = "OptiPlex 7090";

            var signals = new List<DeviceSignal>
            {
                DeviceSignal.Available("MachineGuid", "Registry", guidVal, DeviceSignalNormalizer.ComputeSha256(guidVal, salt)),
                DeviceSignal.Available("BaseBoardProduct", "Registry", boardVal, DeviceSignalNormalizer.ComputeSha256(boardVal, salt))
            };

            // Previous was v1 unkeyed
            var prevV1 = DeviceFingerprintGenerator.Generate(signals, version: 1, salt: salt);

            // Upgraded to v2 keyed HMAC
            byte[] newKey = Encoding.UTF8.GetBytes("new-upgraded-tenant-secret-key-32");
            var currentV2 = DeviceFingerprintGenerator.GenerateKeyed(signals, newKey, scope: "tenant-1", keyScope: KeyScope.Tenant);

            // Comparison passing live signals verifies raw signal match across algorithm boundary
            var comparison = DeviceChangeDetector.Compare(prevV1, currentV2, maxNetworkCap: 2.0, evaluatedSignals: signals);

            Assert.False(comparison.HasChanged);
            Assert.True(comparison.SimilarityScore >= 0.99);
            Assert.Equal(DeviceComparisonConfidence.ExactMatch, comparison.Confidence);
        }

        [Fact]
        public void Migration_WithoutLiveSignals_ReturnsIndeterminate()
        {
            var signals = new List<DeviceSignal>
            {
                DeviceSignal.Available("MachineGuid", "Registry", "val1", "hash1"),
                DeviceSignal.Available("BaseBoardProduct", "Registry", "val2", "hash2")
            };

            var prevV1 = DeviceFingerprintGenerator.Generate(signals, version: 1, salt: "sops:device:v1");
            byte[] newKey = Encoding.UTF8.GetBytes("new-secret-key-32-bytes-long-1234");
            var currentV2 = DeviceFingerprintGenerator.GenerateKeyed(signals, newKey, scope: "tenant-1");

            // Calling Compare without evaluatedSignals must return Indeterminate
            var comparison = DeviceChangeDetector.Compare(prevV1, currentV2, maxNetworkCap: 2.0, evaluatedSignals: null);

            Assert.True(comparison.HasChanged);
            Assert.Equal(DeviceComparisonConfidence.Indeterminate, comparison.Confidence);
            Assert.Contains("differing cryptographic configurations", comparison.Assessment, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void DifferingScopesOrKeys_WithoutLiveSignals_ReturnsIndeterminate()
        {
            var signals = new List<DeviceSignal>
            {
                DeviceSignal.Available("MachineGuid", "Registry", "val1", "hash1"),
                DeviceSignal.Available("BaseBoardProduct", "Registry", "val2", "hash2")
            };

            byte[] keyTenantA = Encoding.UTF8.GetBytes("tenant-a-secret-key-32-bytes-long");
            byte[] keyTenantB = Encoding.UTF8.GetBytes("tenant-b-secret-key-32-bytes-long");

            var fpTenantA = DeviceFingerprintGenerator.GenerateKeyed(signals, keyTenantA, scope: "tenant-a", keyScope: KeyScope.Tenant);
            var fpTenantB = DeviceFingerprintGenerator.GenerateKeyed(signals, keyTenantB, scope: "tenant-b", keyScope: KeyScope.Tenant);

            // Comparing fingerprints of differing tenants without raw signals returns Indeterminate
            var comparison = DeviceChangeDetector.Compare(fpTenantA, fpTenantB, maxNetworkCap: 2.0, evaluatedSignals: null);

            Assert.True(comparison.HasChanged);
            Assert.Equal(DeviceComparisonConfidence.Indeterminate, comparison.Confidence);
            Assert.Contains("differing cryptographic configurations", comparison.Assessment, StringComparison.OrdinalIgnoreCase);
        }
    }
}
