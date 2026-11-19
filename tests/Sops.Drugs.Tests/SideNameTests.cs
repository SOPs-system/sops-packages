using System.Collections.Generic;
using Sops.Drugs.Models;
using Sops.Drugs.SideName;
using Xunit;

namespace Sops.Drugs.Tests
{
    public class SideNameTests
    {
        [Theory]
        [InlineData("500 mg", true)]
        [InlineData("500 mg / 20 tablet", true)]
        [InlineData("1 g", true)]
        [InlineData("100 mcg", true)]
        [InlineData("120 ml", true)]
        [InlineData("20 tablet", true)]
        [InlineData("30 capsule", true)]
        [InlineData("500 mg / 125 mg / 20 tablet", true)]
        [InlineData("invalid", false)]           // No number
        [InlineData("", false)]                  // Empty
        [InlineData("   ", false)]              // Whitespace
        public void IsValid_CommonSideNames_ValidatesAccordingToGrammar(string input, bool expected)
        {
            bool actual = SideNameValidator.IsValid(input);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Format_SingleIngredientTablet_ProducesValidSideName()
        {
            var ingredients = new List<DrugIngredient>
            {
                new DrugIngredient { Name = "Paracetamol", Concentration = 500, Unit = ConcentrationUnit.MG }
            };

            string sideName = SideNameFormatter.Format(ingredients, DrugDosageForm.TABLET, 20);

            Assert.Equal("500 mg / 20 tablets", sideName);
            Assert.True(SideNameValidator.IsValid(sideName));
        }

        [Fact]
        public void Format_CombinationTablet_ProducesValidSideName()
        {
            var ingredients = new List<DrugIngredient>
            {
                new DrugIngredient { Name = "Amoxicillin", Concentration = 500, Unit = ConcentrationUnit.MG },
                new DrugIngredient { Name = "Clavulanic Acid", Concentration = 125, Unit = ConcentrationUnit.MG }
            };

            string sideName = SideNameFormatter.Format(ingredients, DrugDosageForm.TABLET, 14);

            Assert.Equal("500 mg / 125 mg / 14 tablets", sideName);
            Assert.True(SideNameValidator.IsValid(sideName));
        }

        [Fact]
        public void Normalize_SlashesAndSpaces_StandardizesFormat()
        {
            string raw = "500mg/20tabs";
            string normalized = SideNameValidator.Normalize(raw);
            Assert.Contains("/", normalized);
        }
    }
}
