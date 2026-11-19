using System.Collections.Generic;
using Sops.Drugs.Matching;
using Sops.Drugs.Models;
using Xunit;

namespace Sops.Drugs.Tests
{
    public class MatchingTests
    {
        [Fact]
        public void Merge_ExactMatch_CombinesFieldsAndTracksBothSources()
        {
            var matcher = new DrugMatcher();

            var s1 = new List<Drug>
            {
                new Drug
                {
                    NameEn = "Panadol Extra",
                    NameAr = "بانادول إكسترا",
                    DosageForm = DrugDosageForm.TABLET,
                    Units = 24,
                    Price = 50.0m,
                    PricePiastres = 5000,
                    Barcode = "6221000003295",
                    BarcodeValid = true,
                    Company = "GSK"
                }
            };

            var s2 = new List<Drug>
            {
                new Drug
                {
                    NameEn = "Panadol Extra",
                    NameAr = "بانادول اكسترا",
                    DosageForm = DrugDosageForm.TABLET,
                    Units = 24,
                    Price = 50.0m,
                    PricePiastres = 5000,
                    CategoryName = "Analgesics & Antipyretics",
                    Route = "Oral"
                }
            };

            var catalog = matcher.Merge(s1, s2, out var reviewItems, out var barcodeConflicts);

            Assert.Single(catalog.Drugs);
            var drug = catalog.Drugs[0];

            Assert.Equal("Panadol Extra", drug.NameEn);
            Assert.Equal(2, drug.Sources.Count);
            Assert.Contains("eg-drugs", drug.Sources);
            Assert.Contains("egyptian-drug-database", drug.Sources);
            Assert.Equal("Analgesics & Antipyretics", drug.CategoryName);
            Assert.Equal("Oral", drug.Route);
            Assert.Empty(reviewItems);
            Assert.Empty(barcodeConflicts);
        }

        [Fact]
        public void Merge_PriceDiscrepancy_RecordsReviewItemAndFlag()
        {
            var matcher = new DrugMatcher();

            var s1 = new List<Drug>
            {
                new Drug
                {
                    NameEn = "Augmentin 1gm",
                    DosageForm = DrugDosageForm.TABLET,
                    Units = 14,
                    Price = 120.0m,
                    PricePiastres = 12000
                }
            };

            var s2 = new List<Drug>
            {
                new Drug
                {
                    NameEn = "Augmentin 1gm",
                    DosageForm = DrugDosageForm.TABLET,
                    Units = 14,
                    Price = 90.0m,
                    PricePiastres = 9000
                }
            };

            var catalog = matcher.Merge(s1, s2, out var reviewItems, out _);

            Assert.Single(catalog.Drugs);
            Assert.Single(reviewItems);
            Assert.Equal("PriceDiscrepancy", reviewItems[0].IssueType);
            Assert.Contains("PRICE_CONFLICT", catalog.Drugs[0].ReviewFlags[0]);
        }

        [Fact]
        public void Merge_DuplicateBarcodeAcrossDifferentProducts_LogsBarcodeConflict()
        {
            var matcher = new DrugMatcher();

            var s1 = new List<Drug>
            {
                new Drug
                {
                    NameEn = "Drug A",
                    DosageForm = DrugDosageForm.TABLET,
                    Units = 10,
                    Barcode = "6221000003295"
                },
                new Drug
                {
                    NameEn = "Drug B",
                    DosageForm = DrugDosageForm.TABLET,
                    Units = 20,
                    Barcode = "6221000003295" // Same barcode on different product!
                }
            };

            var s2 = new List<Drug>();

            var catalog = matcher.Merge(s1, s2, out _, out var barcodeConflicts);

            Assert.Equal(2, catalog.Drugs.Count);
            Assert.Single(barcodeConflicts);
            Assert.Equal("6221000003295", barcodeConflicts[0].Barcode);
            Assert.Equal("Drug A", barcodeConflicts[0].PrimaryDrugName);
            Assert.Equal("Drug B", barcodeConflicts[0].ConflictingDrugName);
        }
    }
}
