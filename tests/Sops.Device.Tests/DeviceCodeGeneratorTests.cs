using System;
using Sops.Device.Identity;
using Sops.Device.Models;
using Xunit;

namespace Sops.Device.Tests
{
    public class DeviceCodeGeneratorTests
    {
        private readonly DeviceCodeGenerator _generator = new DeviceCodeGenerator();

        [Fact]
        public void GenerateCode_WithValidInput_ReturnsFormattedCode()
        {
            string installId = Guid.NewGuid().ToString("D");
            string code = _generator.GenerateCode(installId, DevicePlatform.Windows, "SOP");

            Assert.NotNull(code);
            Assert.StartsWith("SOP-WIN-", code);
            Assert.True(_generator.ValidateCode(code, out string? error), $"Validation failed: {error}");
        }

        [Fact]
        public void GenerateCode_WithPlatform_IncludesPlatformAbbreviation()
        {
            string installId = Guid.NewGuid().ToString("D");
            string code = _generator.GenerateCode(installId, DevicePlatform.Linux, "SOP");

            Assert.StartsWith("SOP-LNX-", code);
            Assert.True(_generator.ValidateCode(code, out _));
        }

        [Fact]
        public void GenerateCode_UsesCrockfordBase32CharsetExclusively()
        {
            // Crockford charset: 0123456789ABCDEFGHJKMNPQRSTVWXYZ
            // Disallows: I, L, O, U
            var invalidChars = new[] { 'I', 'L', 'O', 'U' };

            for (int i = 0; i < 50; i++)
            {
                string installId = Guid.NewGuid().ToString("D");
                string code = _generator.GenerateCode(installId, DevicePlatform.Windows, "SOP");

                // Format is SOP-WIN-XXXX-XXXX
                // The Crockford encoded payload starts after the second hyphen:
                string[] parts = code.Split('-');
                Assert.True(parts.Length >= 4);
                string payload = string.Concat(parts[2], parts[3]);

                foreach (char ch in invalidChars)
                {
                    Assert.DoesNotContain(ch, payload);
                }
            }
        }

        [Fact]
        public void ValidateCode_WithValidChecksum_ReturnsTrue()
        {
            string installId = Guid.NewGuid().ToString("D");
            string code = _generator.GenerateCode(installId, DevicePlatform.MacOS, "SOP");
            bool isValid = _generator.ValidateCode(code, out int version, out string? error);

            Assert.True(isValid);
            Assert.Equal(1, version);
            Assert.Null(error);
        }

        [Fact]
        public void GenerateCode_IsDeterministicForSameInstallId()
        {
            string installId = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";
            string code1 = _generator.GenerateCode(installId, DevicePlatform.Windows, "SOP");
            string code2 = _generator.GenerateCode(installId, DevicePlatform.Windows, "SOP");

            Assert.Equal(code1, code2);
        }

        [Fact]
        public void ValidateCode_WithTamperedCharacter_ReturnsFalse()
        {
            string installId = Guid.NewGuid().ToString("D");
            string code = _generator.GenerateCode(installId, DevicePlatform.Windows, "SOP");
            
            // Alter a character in the body
            char[] chars = code.ToCharArray();
            int idx = chars.Length - 2; // change character before checksum
            chars[idx] = chars[idx] == 'A' ? 'B' : 'A';
            string tampered = new string(chars);

            bool isValid = _generator.ValidateCode(tampered, out string? error);
            Assert.False(isValid);
            Assert.Contains("checksum", error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void NormalizeCode_ConvertsAmbiguousCharacters()
        {
            // 'i' or 'l' -> '1', 'o' -> '0', lowercase to uppercase
            string raw = "sop-win-oil1-2345";
            string normalized = DeviceCodeGenerator.NormalizeCode(raw);

            Assert.StartsWith("SOP-WIN-", normalized);
            string payload = normalized.Substring("SOP-WIN-".Length);

            Assert.DoesNotContain('o', payload);
            Assert.DoesNotContain('O', payload);
            Assert.DoesNotContain('i', payload);
            Assert.DoesNotContain('I', payload);
            Assert.DoesNotContain('l', payload);
            Assert.DoesNotContain('L', payload);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("INVALID")]
        [InlineData("SOP-1")]
        public void ValidateCode_InvalidFormat_ReturnsFalse(string invalidCode)
        {
            bool isValid = _generator.ValidateCode(invalidCode, out string? error);
            Assert.False(isValid);
            Assert.NotNull(error);
        }
    }
}
