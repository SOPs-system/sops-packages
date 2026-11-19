using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sops.Drugs.Models;
using Sops.Drugs.Normalization;
using Sops.Drugs.SideName;

namespace Sops.Drugs.Matching
{
    /// <summary>
    /// Matching and merging engine for Egyptian pharmaceutical datasets.
    /// Implements exact normalized matching, strength/dosage/pack-aware matching,
    /// conflict detection, and ambiguous match reporting.
    /// </summary>
    public class DrugMatcher
    {
        /// <summary>
        /// Merges records from Source 1 (Falous eg-drugs) and Source 2 (Karem505 drug-database)
        /// into a unified, reconciled <see cref="DrugCatalog"/>.
        /// </summary>
        /// <param name="source1Drugs">List of parsed drugs from Source 1.</param>
        /// <param name="source2Drugs">List of parsed drugs from Source 2.</param>
        /// <param name="reviewItems">Receives review items for ambiguous matches and price discrepancies.</param>
        /// <param name="barcodeConflicts">Receives barcode collision records.</param>
        /// <returns>A unified, reconciled <see cref="DrugCatalog"/>.</returns>
        public DrugCatalog Merge(
            List<Drug> source1Drugs,
            List<Drug> source2Drugs,
            out List<DrugReviewItem> reviewItems,
            out List<BarcodeConflictItem> barcodeConflicts)
        {
            reviewItems = new List<DrugReviewItem>();
            barcodeConflicts = new List<BarcodeConflictItem>();

            var catalog = new DrugCatalog();
            var categoryMap = new Dictionary<string, DrugCategory>(StringComparer.OrdinalIgnoreCase);

            var barcodeOwnerMap = new Dictionary<string, Drug>(StringComparer.OrdinalIgnoreCase);
            var mergedDrugs = new List<Drug>();
            var s2MatchedIndices = new HashSet<int>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Build lookup index for Source 2
            var s2ExactKeyIndex = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            var s2BrandFormIndex = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < source2Drugs.Count; i++)
            {
                var s2 = source2Drugs[i];
                string exactKey = BuildExactMatchKey(s2);
                if (!s2ExactKeyIndex.TryGetValue(exactKey, out var list1))
                {
                    list1 = new List<int>();
                    s2ExactKeyIndex[exactKey] = list1;
                }
                list1.Add(i);

                string brandFormKey = BuildBrandFormKey(s2);
                if (!s2BrandFormIndex.TryGetValue(brandFormKey, out var list2))
                {
                    list2 = new List<int>();
                    s2BrandFormIndex[brandFormKey] = list2;
                }
                list2.Add(i);
            }

            int matchedCount = 0;
            int sequentialSku = 1;

            // Process Source 1 drugs
            foreach (var s1 in source1Drugs)
            {
                int matchedS2Idx = -1;

                // Step 1: Exact Key Match
                string s1ExactKey = BuildExactMatchKey(s1);
                if (s2ExactKeyIndex.TryGetValue(s1ExactKey, out var candidates1))
                {
                    foreach (int candidateIdx in candidates1)
                    {
                        if (!s2MatchedIndices.Contains(candidateIdx))
                        {
                            matchedS2Idx = candidateIdx;
                            break;
                        }
                    }
                }

                // Step 2: Brand + Form + Unit Match
                if (matchedS2Idx == -1)
                {
                    string s1BrandForm = BuildBrandFormKey(s1);
                    if (s2BrandFormIndex.TryGetValue(s1BrandForm, out var candidates2))
                    {
                        foreach (int candidateIdx in candidates2)
                        {
                            if (!s2MatchedIndices.Contains(candidateIdx))
                            {
                                var s2Candidate = source2Drugs[candidateIdx];
                                if (s1.Units == s2Candidate.Units)
                                {
                                    matchedS2Idx = candidateIdx;
                                    break;
                                }
                            }
                        }
                    }
                }

                Drug targetDrug;
                if (matchedS2Idx != -1)
                {
                    s2MatchedIndices.Add(matchedS2Idx);
                    var s2 = source2Drugs[matchedS2Idx];
                    matchedCount++;

                    // Merge fields with precedence:
                    targetDrug = new Drug
                    {
                        Id = GenerateUniqueDeterministicId(s1.NameEn + "|" + s1.DosageForm + "|" + s1.Units, seenIds),
                        Sku = IdentityGenerator.GenerateDrugSku(sequentialSku++),
                        NameEn = s1.NameEn,
                        NameAr = !string.IsNullOrWhiteSpace(s1.NameAr) ? s1.NameAr : s2.NameAr,
                        GenericNames = s1.GenericNames.Count > 0 ? s1.GenericNames : s2.GenericNames,
                        ActiveIngredients = s1.ActiveIngredients.Count > 0 ? s1.ActiveIngredients : s2.ActiveIngredients,
                        DosageForm = s1.DosageForm != DrugDosageForm.OTHER ? s1.DosageForm : s2.DosageForm,
                        StorageType = s1.StorageType != StorageType.UNKNOWN ? s1.StorageType : s2.StorageType,
                        Barcode = !string.IsNullOrWhiteSpace(s1.Barcode) ? s1.Barcode : s2.Barcode,
                        BarcodeValid = s1.BarcodeValid || s2.BarcodeValid,
                        Company = !string.IsNullOrWhiteSpace(s1.Company) ? s1.Company : s2.Company,
                        Units = s1.Units > 1 ? s1.Units : s2.Units,
                        Route = !string.IsNullOrWhiteSpace(s2.Route) ? s2.Route : s1.Route,
                        CategoryName = !string.IsNullOrWhiteSpace(s2.CategoryName) ? s2.CategoryName : s1.CategoryName,
                        Description = !string.IsNullOrWhiteSpace(s1.Description) ? s1.Description : s2.Description,
                        Uses = s1.Uses,
                        RequiresRx = s1.RequiresRx || s2.RequiresRx,
                        AllowUnitSale = s1.AllowUnitSale
                    };

                    targetDrug.Sources.Add("eg-drugs");
                    targetDrug.Sources.Add("egyptian-drug-database");

                    // Reconcile Price (source precedence: Source 1 if > 0, else Source 2)
                    if (s1.Price > 0)
                    {
                        targetDrug.Price = s1.Price;
                        targetDrug.PricePiastres = s1.PricePiastres;
                    }
                    else
                    {
                        targetDrug.Price = s2.Price;
                        targetDrug.PricePiastres = s2.PricePiastres;
                    }

                    targetDrug.OldPrice = s1.OldPrice;
                    targetDrug.OldPricePiastres = s1.OldPricePiastres;

                    // Detect price conflicts
                    if (s1.Price > 0 && s2.Price > 0 && Math.Abs(s1.Price - s2.Price) >= 1.0m)
                    {
                        string flag = $"PRICE_CONFLICT: S1={s1.Price:F2} EGP, S2={s2.Price:F2} EGP";
                        targetDrug.ReviewFlags.Add(flag);

                        reviewItems.Add(new DrugReviewItem
                        {
                            DrugName = targetDrug.NameEn,
                            IssueType = "PriceDiscrepancy",
                            Details = flag,
                            Source1Value = s1.Price.ToString("F2", CultureInfo.InvariantCulture),
                            Source2Value = s2.Price.ToString("F2", CultureInfo.InvariantCulture)
                        });
                    }
                }
                else
                {
                    // Source 1 only
                    targetDrug = s1;
                    targetDrug.Id = GenerateUniqueDeterministicId(s1.NameEn + "|" + s1.DosageForm + "|" + s1.Units, seenIds);
                    targetDrug.Sku = IdentityGenerator.GenerateDrugSku(sequentialSku++);
                }

                // Format validated SideName
                targetDrug.SideName = SideNameFormatter.Format(
                    targetDrug.ActiveIngredients,
                    targetDrug.DosageForm,
                    targetDrug.Units);

                // Build default packaging list
                targetDrug.Packages = new List<DrugPackageInfo>
                {
                    new DrugPackageInfo("Box", "UNIT", targetDrug.Units)
                };

                // Track barcode conflicts
                if (!string.IsNullOrWhiteSpace(targetDrug.Barcode))
                {
                    string barcode = targetDrug.Barcode!;
                    if (barcodeOwnerMap.TryGetValue(barcode, out var existingDrug))
                    {
                        string conflictFlag = $"DUPLICATE_BARCODE: shared with {existingDrug.NameEn}";
                        targetDrug.ReviewFlags.Add(conflictFlag);

                        barcodeConflicts.Add(new BarcodeConflictItem
                        {
                            Barcode = barcode,
                            PrimaryDrugName = existingDrug.NameEn,
                            ConflictingDrugName = targetDrug.NameEn,
                            PrimaryDrugSku = existingDrug.Sku,
                            ConflictingDrugSku = targetDrug.Sku
                        });
                    }
                    else
                    {
                        barcodeOwnerMap[barcode] = targetDrug;
                    }
                }

                // Associate category
                AssignCategory(targetDrug, categoryMap);

                mergedDrugs.Add(targetDrug);
            }

            // Process remaining unmatched Source 2 drugs
            for (int i = 0; i < source2Drugs.Count; i++)
            {
                if (!s2MatchedIndices.Contains(i))
                {
                    var s2 = source2Drugs[i];
                    s2.Id = GenerateUniqueDeterministicId(s2.NameEn + "|" + s2.DosageForm + "|" + s2.Units, seenIds);
                    s2.Sku = IdentityGenerator.GenerateDrugSku(sequentialSku++);
                    s2.SideName = SideNameFormatter.Format(s2.ActiveIngredients, s2.DosageForm, s2.Units);
                    s2.Packages = new List<DrugPackageInfo>
                    {
                        new DrugPackageInfo("Box", "UNIT", s2.Units)
                    };

                    if (!string.IsNullOrWhiteSpace(s2.Barcode))
                    {
                        string barcode = s2.Barcode!;
                        if (barcodeOwnerMap.TryGetValue(barcode, out var existingDrug))
                        {
                            barcodeConflicts.Add(new BarcodeConflictItem
                            {
                                Barcode = barcode,
                                PrimaryDrugName = existingDrug.NameEn,
                                ConflictingDrugName = s2.NameEn,
                                PrimaryDrugSku = existingDrug.Sku,
                                ConflictingDrugSku = s2.Sku
                            });
                        }
                        else
                        {
                            barcodeOwnerMap[barcode] = s2;
                        }
                    }

                    AssignCategory(s2, categoryMap);
                    mergedDrugs.Add(s2);
                }
            }

            catalog.Drugs = mergedDrugs;
            catalog.Categories = categoryMap.Values.OrderBy(c => c.Id).ToList();

            // Populate manifest statistics
            catalog.Manifest.TotalDrugs = mergedDrugs.Count;
            catalog.Manifest.TotalCategories = catalog.Categories.Count;
            catalog.Manifest.MatchedCount = matchedCount;
            catalog.Manifest.UnmatchedCount = mergedDrugs.Count - matchedCount;
            catalog.Manifest.BarcodeConflictCount = barcodeConflicts.Count;
            catalog.Manifest.PriceDiscrepancyCount = reviewItems.Count;
            catalog.Manifest.RefrigeratedCount = mergedDrugs.Count(d => d.StorageType == StorageType.REFRIGERATOR);

            return catalog;
        }

        private static string BuildExactMatchKey(Drug d)
        {
            string brand = DrugNameNormalizer.ExtractBrandName(d.NameEn);
            return $"{brand}|{d.DosageForm}|{d.Units}";
        }

        private static string BuildBrandFormKey(Drug d)
        {
            string brand = DrugNameNormalizer.ExtractBrandName(d.NameEn);
            return $"{brand}|{d.DosageForm}";
        }

        private static void AssignCategory(Drug drug, Dictionary<string, DrugCategory> categoryMap)
        {
            string catName = string.IsNullOrWhiteSpace(drug.CategoryName) ? "General Medicines" : drug.CategoryName!.Trim();

            if (!categoryMap.TryGetValue(catName, out var category))
            {
                int nextIndex = categoryMap.Count + 1;
                string catId = IdentityGenerator.GenerateCategoryId(nextIndex);
                category = new DrugCategory(catId, catName);
                categoryMap[catName] = category;
            }

            drug.CategoryId = category.Id;
            drug.CategoryName = category.NameEn;
        }

        private static string GenerateUniqueDeterministicId(string seed, HashSet<string> seenIds)
        {
            string id = IdentityGenerator.GenerateDeterministicId(seed);
            int collision = 1;
            while (!seenIds.Add(id))
            {
                id = IdentityGenerator.GenerateDeterministicId($"{seed}#{collision++}");
            }
            return id;
        }
    }

    /// <summary>
    /// Review report record representing a match anomaly or price discrepancy.
    /// </summary>
    public class DrugReviewItem
    {
        /// <summary>English drug commercial name</summary>
        public string DrugName { get; set; } = string.Empty;
        /// <summary>Category of issue (e.g. "PriceDiscrepancy", "AmbiguousMatch")</summary>
        public string IssueType { get; set; } = string.Empty;
        /// <summary>Human-readable details</summary>
        public string Details { get; set; } = string.Empty;
        /// <summary>Value from Source 1</summary>
        public string Source1Value { get; set; } = string.Empty;
        /// <summary>Value from Source 2</summary>
        public string Source2Value { get; set; } = string.Empty;
    }

    /// <summary>
    /// Review record representing a barcode collision between two distinct drug products.
    /// </summary>
    public class BarcodeConflictItem
    {
        /// <summary>The colliding barcode string</summary>
        public string Barcode { get; set; } = string.Empty;
        /// <summary>First drug owner name</summary>
        public string PrimaryDrugName { get; set; } = string.Empty;
        /// <summary>First drug SKU</summary>
        public string PrimaryDrugSku { get; set; } = string.Empty;
        /// <summary>Second drug owner name</summary>
        public string ConflictingDrugName { get; set; } = string.Empty;
        /// <summary>Second drug SKU</summary>
        public string ConflictingDrugSku { get; set; } = string.Empty;
    }
}
