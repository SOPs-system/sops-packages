using Sops.Drugs.Models;
using Sops.Drugs.Normalization;
using Xunit;

namespace Sops.Drugs.Tests
{
    public class StrengthAndDosageFormTests
    {
        [Theory]
        [InlineData("PANADOL EXTRA 500 MG TAB", DrugDosageForm.TABLET)]
        [InlineData("AUGMENTIN 1GM F.C. TABLET", DrugDosageForm.TABLET_FC)]
        [InlineData("VOLTAREN 75MG E.C. TAB", DrugDosageForm.TABLET_EC)]
        [InlineData("CATASPARK EFF. TABLET", DrugDosageForm.TABLET_EFF)]
        [InlineData("CEFOBID 1G VIAL", DrugDosageForm.VIAL)]
        [InlineData("CATAFLAM 75MG AMP", DrugDosageForm.AMPOULE)]
        [InlineData("CONGESTAL SYRUP 120ML", DrugDosageForm.SYRUP)]
        [InlineData("PANADOL SUSPENSION 100ML", DrugDosageForm.SUSPENSION)]
        [InlineData("FUCIDIN 2% CREAM 15GM", DrugDosageForm.CREAM)]
        [InlineData("GARAMYCIN OINTMENT 15G", DrugDosageForm.OINTMENT)]
        [InlineData("VOLTAREN EMULGEL 50G", DrugDosageForm.EMULGEL)]
        [InlineData("TEARS NATURALE EYE DROPS", DrugDosageForm.DROPS)]
        [InlineData("DOLPHIN 25MG SUPP", DrugDosageForm.SUPPOSITORY)]
        [InlineData("VENTOLIN INHALER 100MCG", DrugDosageForm.INHALER)]
        [InlineData("OTRIVIN ADULT NASAL SPRAY", DrugDosageForm.SPRAY)]
        [InlineData("C-RETARD 500MG CAPSULES", DrugDosageForm.CAPSULE)]
        [InlineData("SMEXTA SACHET 3GM", DrugDosageForm.SACHET)]
        public void ParseDosageForm_VariousText_ExtractsExpectedForm(string input, DrugDosageForm expected)
        {
            var actual = DosageFormParser.Parse(input);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("MG", ConcentrationUnit.MG)]
        [InlineData("mg", ConcentrationUnit.MG)]
        [InlineData("G", ConcentrationUnit.G)]
        [InlineData("gm", ConcentrationUnit.G)]
        [InlineData("MCG", ConcentrationUnit.MCG)]
        [InlineData("ug", ConcentrationUnit.MCG)]
        [InlineData("µg", ConcentrationUnit.MCG)]
        [InlineData("ML", ConcentrationUnit.ML)]
        [InlineData("IU", ConcentrationUnit.IU)]
        [InlineData("%", ConcentrationUnit.PERCENT)]
        public void ParseUnit_StandardUnits_MappedCorrectly(string unitStr, ConcentrationUnit expected)
        {
            var actual = StrengthParser.ParseUnit(unitStr);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ParseIngredients_SingleIngredientWithStrength_ExtractsProperly()
        {
            string raw = "PARACETAMOL 500 MG";
            var ingredients = StrengthParser.ParseIngredients(raw);

            Assert.Single(ingredients);
            Assert.Equal("Paracetamol", ingredients[0].Name);
            Assert.Equal(500.0, ingredients[0].Concentration);
            Assert.Equal(ConcentrationUnit.MG, ingredients[0].Unit);
        }

        [Fact]
        public void ParseIngredients_CombinationIngredients_ExtractsAllComponents()
        {
            string raw = "AMOXICILLIN 500 MG + CLAVULANIC ACID 125 MG";
            var ingredients = StrengthParser.ParseIngredients(raw);

            Assert.Equal(2, ingredients.Count);
            Assert.Equal("Amoxicillin", ingredients[0].Name);
            Assert.Equal(500.0, ingredients[0].Concentration);
            Assert.Equal(ConcentrationUnit.MG, ingredients[0].Unit);

            Assert.Equal("Clavulanic Acid", ingredients[1].Name);
            Assert.Equal(125.0, ingredients[1].Concentration);
            Assert.Equal(ConcentrationUnit.MG, ingredients[1].Unit);
        }

        [Fact]
        public void ParseIngredients_PercentageConcentration_ParsedCorrectly()
        {
            string raw = "FUSIDIC ACID 2%";
            var ingredients = StrengthParser.ParseIngredients(raw);

            Assert.Single(ingredients);
            Assert.Equal("Fusidic Acid", ingredients[0].Name);
            Assert.Equal(2.0, ingredients[0].Concentration);
            Assert.Equal(ConcentrationUnit.PERCENT, ingredients[0].Unit);
        }
    }
}
