using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Sops.Drugs.Models;
using Sops.Drugs.Serialization;
using Xunit;

namespace Sops.Drugs.Tests
{
    public class SerializationTests
    {
        [Fact]
        public void SerializeCatalog_PreservesArabicCharactersWithoutEscapeSequences()
        {
            var catalog = new DrugCatalog();
            catalog.Drugs.Add(new Drug
            {
                Id = "test-123",
                Sku = "DRG-000001",
                NameEn = "Panadol Extra",
                NameAr = "بانادول إكسترا",
                DosageForm = DrugDosageForm.TABLET,
                Units = 24,
                Price = 50.0m,
                PricePiastres = 5000
            });

            using var ms = new MemoryStream();
            DrugCatalogSerializer.SerializeCatalog(catalog, ms);
            string json = Encoding.UTF8.GetString(ms.ToArray());

            Assert.Contains("بانادول إكسترا", json);
            Assert.DoesNotContain("\\u0628", json); // Does NOT contain escaped unicode
        }

        [Fact]
        public void Roundtrip_CanonicalCatalog_PreservesAllFields()
        {
            var original = new DrugCatalog
            {
                Manifest = new DrugCatalogManifest
                {
                    BuildTimestampUtc = DateTimeOffset.Parse("2026-11-18T00:00:00Z"),
                    TotalDrugs = 1,
                    TotalCategories = 1
                }
            };

            original.Categories.Add(new DrugCategory("CAT-0001", "Analgesics"));
            original.Drugs.Add(new Drug
            {
                Id = "drg-guid-001",
                Sku = "DRG-000001",
                NameEn = "Cataflam 50mg",
                NameAr = "كاتافلام 50 مجم",
                DosageForm = DrugDosageForm.TABLET,
                StorageType = StorageType.NORMAL,
                Units = 20,
                Price = 65.5m,
                PricePiastres = 6550,
                Barcode = "6221000003295",
                BarcodeValid = true,
                ActiveIngredients = new List<DrugIngredient>
                {
                    new DrugIngredient { Name = "Diclofenac Potassium", Concentration = 50, Unit = ConcentrationUnit.MG }
                },
                Packages = new List<DrugPackageInfo>
                {
                    new DrugPackageInfo("Box", "UNIT", 20)
                }
            });

            using var ms = new MemoryStream();
            DrugCatalogSerializer.SerializeCatalog(original, ms);
            ms.Position = 0;

            var deserialized = DrugCatalogSerializer.DeserializeCatalog(ms);

            Assert.Single(deserialized.Drugs);
            Assert.Single(deserialized.Categories);

            var drug = deserialized.Drugs[0];
            Assert.Equal("drg-guid-001", drug.Id);
            Assert.Equal("DRG-000001", drug.Sku);
            Assert.Equal("Cataflam 50mg", drug.NameEn);
            Assert.Equal("كاتافلام 50 مجم", drug.NameAr);
            Assert.Equal(65.5m, drug.Price);
            Assert.Equal(6550, drug.PricePiastres);
            Assert.Equal("6221000003295", drug.Barcode);
            Assert.Single(drug.ActiveIngredients);
            Assert.Equal("Diclofenac Potassium", drug.ActiveIngredients[0].Name);
        }

        [Fact]
        public void SerializeServerDbExport_ExportsSnakeCaseSchemaMatchingServer()
        {
            var catalog = new DrugCatalog();
            catalog.Categories.Add(new DrugCategory("CAT-0001", "Antibiotics"));
            catalog.Drugs.Add(new Drug
            {
                Id = "drg-db-001",
                Sku = "DRG-000001",
                NameEn = "Augmentin 1gm",
                NameAr = "أوجمنتين 1 جم",
                SideName = "1 g / 14 tablet",
                Company = "GSK",
                DosageForm = DrugDosageForm.TABLET,
                StorageType = StorageType.NORMAL,
                Price = 120.0m,
                PricePiastres = 12000,
                Units = 14,
                RequiresRx = true,
                CategoryId = "CAT-0001"
            });

            using var ms = new MemoryStream();
            DrugCatalogSerializer.SerializeServerDbExport(catalog, ms);
            string json = Encoding.UTF8.GetString(ms.ToArray());

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("drugs", out var drugsElem));
            Assert.True(root.TryGetProperty("categories", out var catsElem));
            Assert.Equal(1, drugsElem.GetArrayLength());
            Assert.Equal(1, catsElem.GetArrayLength());

            var firstDrug = drugsElem[0];
            Assert.Equal("drg-db-001", firstDrug.GetProperty("id").GetString());
            Assert.Equal("DRG-000001", firstDrug.GetProperty("sku").GetString());
            Assert.Equal("Augmentin 1gm", firstDrug.GetProperty("name_en").GetString());
            Assert.Equal("1 g / 14 tablet", firstDrug.GetProperty("side_name").GetString());
            Assert.Equal(12000, firstDrug.GetProperty("price").GetInt64());
            Assert.True(firstDrug.GetProperty("requires_rx").GetBoolean());
        }
    }
}
