using Sops.Drugs.Models;
using Sops.Drugs.Normalization;
using Xunit;

namespace Sops.Drugs.Tests
{
    public class StorageClassificationTests
    {
        [Theory]
        [InlineData("LANTUS 100IU/ML SOLOSTAR", "INSULIN GLARGINE", null, StorageType.REFRIGERATOR)]
        [InlineData("MIXTARD 30 HM 100IU/ML", "INSULIN HUMAN", null, StorageType.REFRIGERATOR)]
        [InlineData("NOVORAPID 100 U/ML FLEXPEN", "INSULIN ASPART", null, StorageType.REFRIGERATOR)]
        [InlineData("HUMALOG 100 IU/ML KWIKPEN", "INSULIN LISPRO", null, StorageType.REFRIGERATOR)]
        [InlineData("TOUJEO 300 U/ML SOLOSTAR", "INSULIN GLARGINE", null, StorageType.REFRIGERATOR)]
        [InlineData("HERCEPTIN 440MG VIAL", "TRASTUZUMAB", null, StorageType.REFRIGERATOR)]
        [InlineData("HUMIRA 40MG/0.8ML SYRINGE", "ADALIMUMAB", null, StorageType.REFRIGERATOR)]
        [InlineData("HEPATITIS B VACCINE 20MCG", "HEPATITIS B SURFACE ANTIGEN", null, StorageType.REFRIGERATOR)]
        [InlineData("INFLUVAC TETRA VACCINE", "INFLUENZA VACCINE", null, StorageType.REFRIGERATOR)]
        [InlineData("TEST DRUG", null, "Store between 2-8°C in refrigerator", StorageType.REFRIGERATOR)]
        [InlineData("دواء تجريبي", null, "يحفظ في الثلاجة", StorageType.REFRIGERATOR)]
        [InlineData("PANADOL EXTRA 24 TAB", "PARACETAMOL, CAFFEINE", null, StorageType.NORMAL)]
        [InlineData("AUGMENTIN 1GM 14 TAB", "AMOXICILLIN, CLAVULANIC ACID", null, StorageType.NORMAL)]
        [InlineData("CONGESTAL 20 TAB", "PARACETAMOL, PSEUDOEPHEDRINE, CHLORPHENIRAMINE", null, StorageType.NORMAL)]
        [InlineData("CATAFLAM 50MG 20 TAB", "DICLOFENAC POTASSIUM", null, StorageType.NORMAL)]
        public void Classify_VariousDrugs_CorrectlyIdentifiesStorageCondition(
            string name, string? active, string? description, StorageType expected)
        {
            var actual = StorageClassifier.Classify(name, active, description);
            Assert.Equal(expected, actual);
        }
    }
}
