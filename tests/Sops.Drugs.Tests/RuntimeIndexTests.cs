using System.Collections.Generic;
using Sops.Drugs.Models;
using Sops.Drugs.Runtime;
using Xunit;

namespace Sops.Drugs.Tests
{
    public class RuntimeIndexTests
    {
        private DrugCatalog CreateSampleCatalog()
        {
            var catalog = new DrugCatalog();
            catalog.Categories.Add(new DrugCategory("CAT-0001", "Analgesics"));
            catalog.Categories.Add(new DrugCategory("CAT-0002", "Antibiotics"));

            catalog.Drugs.Add(new Drug
            {
                Id = "id-panadol",
                Sku = "DRG-000001",
                NameEn = "Panadol Extra",
                NameAr = "بانادول إكسترا",
                DosageForm = DrugDosageForm.TABLET,
                Barcode = "6221000003295",
                CategoryId = "CAT-0001",
                CategoryName = "Analgesics",
                GenericNames = new List<string> { "Paracetamol", "Caffeine" }
            });

            catalog.Drugs.Add(new Drug
            {
                Id = "id-congestal",
                Sku = "DRG-000002",
                NameEn = "Congestal",
                NameAr = "كونجيستال أقراص",
                DosageForm = DrugDosageForm.TABLET,
                Barcode = "6221000003296",
                CategoryId = "CAT-0001",
                CategoryName = "Analgesics",
                GenericNames = new List<string> { "Paracetamol", "Pseudoephedrine", "Chlorpheniramine" }
            });

            catalog.Drugs.Add(new Drug
            {
                Id = "id-augmentin",
                Sku = "DRG-000003",
                NameEn = "Augmentin 1gm",
                NameAr = "أوجمنتين",
                DosageForm = DrugDosageForm.TABLET,
                Barcode = "6221000003297",
                CategoryId = "CAT-0002",
                CategoryName = "Antibiotics",
                GenericNames = new List<string> { "Amoxicillin", "Clavulanic Acid" }
            });

            return catalog;
        }

        [Fact]
        public void GetByIdAndSku_ExistingKeys_ReturnsCorrectDrug()
        {
            var catalog = CreateSampleCatalog();
            var index = new DrugCatalogIndex(catalog);

            var byId = index.GetById("id-panadol");
            Assert.NotNull(byId);
            Assert.Equal("Panadol Extra", byId!.NameEn);

            var bySku = index.GetBySku("DRG-000002");
            Assert.NotNull(bySku);
            Assert.Equal("Congestal", bySku!.NameEn);
        }

        [Fact]
        public void FindByBarcode_ExistingBarcode_ReturnsCorrectDrug()
        {
            var catalog = CreateSampleCatalog();
            var index = new DrugCatalogIndex(catalog);

            var drug = index.FindByBarcode("6221000003295");
            Assert.NotNull(drug);
            Assert.Equal("Panadol Extra", drug!.NameEn);
        }

        [Fact]
        public void FindByCategory_CategoryWithMultipleDrugs_ReturnsAllMatchingDrugs()
        {
            var catalog = CreateSampleCatalog();
            var index = new DrugCatalogIndex(catalog);

            var analgesics = index.FindByCategory("CAT-0001");
            Assert.Equal(2, analgesics.Count);

            var antibiotics = index.FindByCategory("CAT-0002");
            Assert.Single(antibiotics);
            Assert.Equal("Augmentin 1gm", antibiotics[0].NameEn);
        }

        [Fact]
        public void FindByIngredient_GenericName_FindsAllProductsContainingIt()
        {
            var catalog = CreateSampleCatalog();
            var index = new DrugCatalogIndex(catalog);

            // Both Panadol and Congestal contain Paracetamol
            var paracetamolDrugs = index.FindByIngredient("Paracetamol");
            Assert.Equal(2, paracetamolDrugs.Count);

            var amoxicillinDrugs = index.FindByIngredient("Amoxicillin");
            Assert.Single(amoxicillinDrugs);
            Assert.Equal("Augmentin 1gm", amoxicillinDrugs[0].NameEn);
        }

        [Theory]
        [InlineData("Panadol", "Panadol Extra")]
        [InlineData("بانادول", "Panadol Extra")]
        [InlineData("كونجيستال", "Congestal")]
        [InlineData("Augmentin", "Augmentin 1gm")]
        public void Search_BilingualQueries_FindsExpectedDrug(string query, string expectedName)
        {
            var catalog = CreateSampleCatalog();
            var index = new DrugCatalogIndex(catalog);

            var results = index.Search(query);
            Assert.NotEmpty(results);
            Assert.Equal(expectedName, results[0].NameEn);
        }
    }
}
