using Sops.Drugs.Normalization;
using Xunit;

namespace Sops.Drugs.Tests
{
    public class BarcodeTests
    {
        [Theory]
        [InlineData("6221000003295", true)]   // Valid EAN-13
        [InlineData("6221000003295.0", true)] // Float decimal notation
        [InlineData("01234565", true)]        // Valid EAN-8 (0123456 -> sum: 6*3+5*1+4*3+3*1+2*3+1*1+0=18+5+12+3+6+1=45 -> (10-(45%10))%10 = 5)
        [InlineData("6221000003294", false)]  // Wrong check digit
        [InlineData("12345", false)]          // Invalid length
        [InlineData("abcdef1234567", false)]  // Non digits
        [InlineData("", false)]               // Empty
        [InlineData(null, false)]             // Null
        public void ValidateCheckDigit_VariousBarcodes_ReturnsExpectedResult(string? barcode, bool expectedValid)
        {
            bool actual = BarcodeValidator.ValidateCheckDigit(barcode);
            Assert.Equal(expectedValid, actual);
        }

        [Fact]
        public void Normalize_ValidBarcodeWithSpacesAndDecimal_NormalizesProperly()
        {
            string raw = "  6221000003295.0  ";
            string? normalized = BarcodeValidator.Normalize(raw);
            Assert.Equal("6221000003295", normalized);
        }
    }
}
