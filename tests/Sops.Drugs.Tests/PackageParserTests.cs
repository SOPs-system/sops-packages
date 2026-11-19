using Sops.Drugs.Models;
using Sops.Drugs.Normalization;
using Xunit;

namespace Sops.Drugs.Tests
{
    public class PackageParserTests
    {
        [Theory]
        [InlineData("PANADOL EXTRA 2 STRIPS OF 12 TABS", 24, 2, 12)]
        [InlineData("AUGMENTIN 1GM 2 STRIPS X 7 TABLETS", 14, 2, 7)]
        [InlineData("CONGESTAL 2 STRIPS * 10 TABS", 20, 2, 10)]
        public void Parse_StripsOfUnitsPattern_ReturnsExactCalculatedUnits(
            string tradeName, int expectedTotal, int expectedStrips, int expectedPerStrip)
        {
            var (total, strips, perStrip) = PackageParser.Parse(tradeName, null, DrugDosageForm.TABLET);
            Assert.Equal(expectedTotal, total);
            Assert.Equal(expectedStrips, strips);
            Assert.Equal(expectedPerStrip, perStrip);
        }

        [Theory]
        [InlineData("CATAFLAM 50MG 20 TABLETS", 20, 2, 10)]
        [InlineData("BRUFEN 400MG 30 TABLETS", 30, 3, 10)]
        [InlineData("PANADOL ADVANCE 24 TABS", 24, 1, 24)]
        public void Parse_SimpleUnitCounts_ReturnsExpectedUnits(
            string tradeName, int expectedTotal, int expectedStrips, int expectedPerStrip)
        {
            var (total, strips, perStrip) = PackageParser.Parse(tradeName, null, DrugDosageForm.TABLET);
            Assert.Equal(expectedTotal, total);
            Assert.Equal(expectedStrips, strips);
            Assert.Equal(expectedPerStrip, perStrip);
        }

        [Fact]
        public void Parse_ExplicitUnitsFromDataset_UsedWhenPresent()
        {
            var (total, strips, perStrip) = PackageParser.Parse("ASPOCID", "30", DrugDosageForm.TABLET);
            Assert.Equal(30, total);
            Assert.Equal(3, strips);
            Assert.Equal(10, perStrip);
        }

        [Fact]
        public void Parse_LiquidsAndOintments_DefaultsToOneUnit()
        {
            var (total, strips, perStrip) = PackageParser.Parse("CONGESTAL SYRUP 120ML", null, DrugDosageForm.SYRUP);
            Assert.Equal(1, total);
            Assert.Equal(1, strips);
            Assert.Equal(1, perStrip);
        }
    }
}
