using Sops.Drugs.Normalization;
using Xunit;

namespace Sops.Drugs.Tests
{
    public class NormalizationTests
    {
        [Theory]
        [InlineData("PANADOL EXTRA TAB", "PANADOL EXTRA")]
        [InlineData("AUGMENTIN 1GM TABLETS", "AUGMENTIN")]
        [InlineData("CONGESTAL 20 TAB", "CONGESTAL")]
        [InlineData("CATAFLAM 50MG 20 TAB", "CATAFLAM")]
        [InlineData("BRUFEN 400 MG 30 TAB", "BRUFEN")]
        [InlineData("LANTUS 100IU/ML SOLOSTAR", "LANTUS")]
        public void ExtractBrandName_CommonDrugs_ExtractsCleanBrand(string input, string expected)
        {
            string actual = DrugNameNormalizer.ExtractBrandName(input);
            Assert.Equal(expected, actual);
        }

        [Fact]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1508:Avoid dead code", Justification = "Test assertion")]
        public void NormalizeArabic_AlefVariations_NormalizedToBareAlef()
        {
            string input = "أحمد إبراهيم آمال ٱبن";
            string expected = "احمد ابراهيم امال ابن";
            string actual = DrugNameNormalizer.NormalizeArabic(input);
            Assert.Equal(expected, actual);
        }

        [Fact]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1508:Avoid dead code", Justification = "Test assertion")]
        public void NormalizeArabic_TehMarbuta_NormalizedToHeh()
        {
            string input = "أقراص فوارة كبسولة معلقة";
            string expected = "اقراص فواره كبسوله معلقه";
            string actual = DrugNameNormalizer.NormalizeArabic(input);
            Assert.Equal(expected, actual);
        }

        [Fact]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1508:Avoid dead code", Justification = "Test assertion")]
        public void NormalizeArabic_YehAndAlefMaksura_NormalizedToYeh()
        {
            string input = "دواء مستشفى مصطفى داء";
            string expected = "دواء مستشفي مصطفي داء";
            string actual = DrugNameNormalizer.NormalizeArabic(input);
            Assert.Equal(expected, actual);
        }

        [Fact]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1508:Avoid dead code", Justification = "Test assertion")]
        public void NormalizeArabic_DiacriticsAndTashkeel_Removed()
        {
            // بَانَادُولْ إِكْسْتْرَا - Panadol Extra with full tashkeel
            string input = "بَانَادُولْ إِكْسْتْرَا";
            string actual = DrugNameNormalizer.NormalizeArabic(input);
            Assert.Equal("بانادول اكسترا", actual);
        }

        [Fact]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1508:Avoid dead code", Justification = "Test assertion")]
        public void NormalizeArabic_Tatweel_Removed()
        {
            // باااانادول with tatweel: بــــانادول
            string input = "بـــانادول";
            string actual = DrugNameNormalizer.NormalizeArabic(input);
            Assert.Equal("بانادول", actual);
        }

        [Fact]
        public void NormalizeArabic_NullOrEmpty_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, DrugNameNormalizer.NormalizeArabic(null!));
            Assert.Equal(string.Empty, DrugNameNormalizer.NormalizeArabic("   "));
        }

        [Theory]
        [InlineData("PANADOL   EXTRA", "PANADOL EXTRA")]
        [InlineData("augmentin  625 mg", "AUGMENTIN 625 MG")]
        public void NormalizeEnglish_FormatsProperly(string input, string expected)
        {
            string actual = DrugNameNormalizer.NormalizeEnglish(input);
            Assert.Equal(expected, actual);
        }
    }
}
