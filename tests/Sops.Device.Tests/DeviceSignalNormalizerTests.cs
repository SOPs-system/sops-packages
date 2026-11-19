using Sops.Device.Fingerprinting;
using Xunit;

namespace Sops.Device.Tests
{
    public class DeviceSignalNormalizerTests
    {
        [Fact]
        public void NormalizeAndHash_ValidSignal_ComputesExpectedSha256()
        {
            string raw = "  ABC-123-def  ";
            bool isValid = DeviceSignalNormalizer.IsValidSignal("TestSignal", raw, out string? reason);
            Assert.True(isValid);
            Assert.Null(reason);

            string normalized = DeviceSignalNormalizer.Normalize("TestSignal", raw);
            Assert.Equal("ABC-123-def", normalized);

            string hash = DeviceSignalNormalizer.ComputeSha256(normalized);
            Assert.NotEmpty(hash);
            Assert.Equal(64, hash.Length); // 64 hex chars = SHA-256
        }

        [Fact]
        public void ComputeSha256_WithApplicationSalt_ProducesDifferentScopedHashes()
        {
            string signal = "00:1A:2B:3C:4D:5E";
            string unsalted = DeviceSignalNormalizer.ComputeSha256(signal);
            string tenantA = DeviceSignalNormalizer.ComputeSha256(signal, "tenant-a");
            string tenantB = DeviceSignalNormalizer.ComputeSha256(signal, "tenant-b");

            Assert.NotEqual(unsalted, tenantA);
            Assert.NotEqual(tenantA, tenantB);
            Assert.Equal(64, tenantA.Length);
            Assert.Equal(64, tenantB.Length);
        }

        [Theory]
        [InlineData("To be filled by O.E.M.")]
        [InlineData("to be filled by o.e.m.")]
        [InlineData("Default string")]
        [InlineData("System Serial Number")]
        [InlineData("None")]
        [InlineData("Not Specified")]
        [InlineData("00000000-0000-0000-0000-000000000000")]
        [InlineData("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")]
        [InlineData("123456789")]
        [InlineData("0123456789")]
        [InlineData("VMware-42 1a 2b")]
        [InlineData("VirtualBox-12345")]
        [InlineData("qemu-standard")]
        public void IsValidSignal_OEMPlaceholder_IsRejected(string placeholder)
        {
            bool isValid = DeviceSignalNormalizer.IsValidSignal("BiosSerial", placeholder, out string? reason);

            Assert.False(isValid);
            Assert.NotNull(reason);
        }

        [Theory]
        [InlineData("00:00:00:00:00:00")]
        [InlineData("FF:FF:FF:FF:FF:FF")]
        [InlineData("01:00:5E:00:00:01")] // Multicast IPv4
        [InlineData("33:33:00:00:00:01")] // Multicast IPv6
        [InlineData("03:00:00:00:00:01")] // I/G bit set (multicast)
        [InlineData("000000000000")]
        public void IsValidSignal_InvalidOrMulticastMacAddress_IsRejected(string invalidMac)
        {
            bool isValid = DeviceSignalNormalizer.IsValidSignal("NetworkMac", invalidMac, out string? reason);

            Assert.False(isValid);
            Assert.NotNull(reason);
        }

        [Theory]
        [InlineData("00:1A:2B:3C:4D:5E")] // Unicast (00 is even)
        [InlineData("0A-00-27-00-00-16")] // Unicast (0A is even)
        [InlineData("001122334455")]       // Unicast (00 is even)
        public void IsValidSignal_ValidMacAddress_IsAccepted(string validMac)
        {
            bool isValid = DeviceSignalNormalizer.IsValidSignal("NetworkMac", validMac, out string? reason);

            Assert.True(isValid);
            Assert.Null(reason);

            string normalized = DeviceSignalNormalizer.Normalize("NetworkMac", validMac);
            Assert.Contains(":", normalized);
            Assert.Equal(17, normalized.Length); // XX:XX:XX:XX:XX:XX
        }
    }
}
