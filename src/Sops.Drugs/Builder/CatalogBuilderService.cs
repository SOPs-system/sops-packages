using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using Sops.Drugs.Csv;
using Sops.Drugs.Matching;
using Sops.Drugs.Models;
using Sops.Drugs.Normalization;
using Sops.Drugs.Runtime;
using Sops.Drugs.Serialization;
using Sops.Drugs.SideName;

namespace Sops.Drugs.Builder
{
    /// <summary>
    /// Coordinates parsing of source CSV files, drug matching/merging, SideName formatting,
    /// and export of all JSON catalogs and CSV reports.
    /// </summary>
    public class CatalogBuilderService
    {
        /// <summary>
        /// Builds the complete catalog from source CSV files in dataDir and outputs artifacts to outDir.
        /// </summary>
        public DrugCatalog BuildCatalog(
            string dataDir,
            string outDir,
            SourceProvenance? s1Prov = null,
            SourceProvenance? s2Prov = null)
        {
            if (!Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            string s1Path = Path.Combine(dataDir, "eg_drugs.csv");
            string s2Path = Path.Combine(dataDir, "egyptian-drugs.csv");

            if (!File.Exists(s1Path))
            {
                throw new FileNotFoundException($"Source 1 CSV not found at: {s1Path}. Run fetch first.");
            }
            if (!File.Exists(s2Path))
            {
                throw new FileNotFoundException($"Source 2 CSV not found at: {s2Path}. Run fetch first.");
            }

            Console.WriteLine("--> Parsing Source 1 (mahmoudfalous/eg-drugs)...");
            var source1Drugs = ParseFalousEgDrugs(s1Path);
            Console.WriteLine($"    Parsed {source1Drugs.Count:N0} records from Source 1.");

            Console.WriteLine("--> Parsing Source 2 (karem505/egyptian-drug-database)...");
            var source2Drugs = ParseKaremEgyptianDrugs(s2Path);
            Console.WriteLine($"    Parsed {source2Drugs.Count:N0} records from Source 2.");

            Console.WriteLine("--> Matching and merging datasets...");
            var matcher = new DrugMatcher();
            var catalog = matcher.Merge(source1Drugs, source2Drugs, out var reviewItems, out var barcodeConflicts);

            // Populate source provenance
            if (s1Prov != null)
            {
                s1Prov.RecordCount = source1Drugs.Count;
                catalog.Manifest.Sources.Add(s1Prov);
            }
            if (s2Prov != null)
            {
                s2Prov.RecordCount = source2Drugs.Count;
                catalog.Manifest.Sources.Add(s2Prov);
            }

            Console.WriteLine($"    Total Merged Drugs: {catalog.Drugs.Count:N0}");
            Console.WriteLine($"    Total Categories:   {catalog.Categories.Count:N0}");
            Console.WriteLine($"    Matched Products:   {catalog.Manifest.MatchedCount:N0}");
            Console.WriteLine($"    Unmatched Products: {catalog.Manifest.UnmatchedCount:N0}");
            Console.WriteLine($"    Barcode Conflicts:  {catalog.Manifest.BarcodeConflictCount:N0}");
            Console.WriteLine($"    Price Conflicts:    {catalog.Manifest.PriceDiscrepancyCount:N0}");
            Console.WriteLine($"    Refrigerated Drugs: {catalog.Manifest.RefrigeratedCount:N0}");

            Console.WriteLine("--> Exporting JSON catalogs and CSV reports...");
            ExportArtifacts(catalog, outDir, reviewItems, barcodeConflicts);

            return catalog;
        }

        private static List<Drug> ParseFalousEgDrugs(string filePath)
        {
            var list = new List<Drug>();

            using (var fs = File.OpenRead(filePath))
            using (var csv = new SopsCsvReader(fs))
            {
                foreach (var rec in csv.ReadRecords())
                {
                    rec.TryGetValue("name", out var nameEn);
                    rec.TryGetValue("arabic", out var nameAr);
                    rec.TryGetValue("active", out var active);
                    rec.TryGetValue("company", out var company);
                    rec.TryGetValue("price", out var priceStr);
                    rec.TryGetValue("oldprice", out var oldPriceStr);
                    rec.TryGetValue("barcode", out var barcode);
                    rec.TryGetValue("units", out var unitsStr);
                    rec.TryGetValue("description", out var desc);
                    rec.TryGetValue("uses", out var uses);

                    if (string.IsNullOrWhiteSpace(nameEn)) continue;

                    var dosageForm = DosageFormParser.Parse(nameEn);
                    var (units, strips, perStrip) = PackageParser.Parse(nameEn, unitsStr, dosageForm);
                    var ingredients = StrengthParser.ParseIngredients(active, nameEn);
                    var storage = StorageClassifier.Classify(nameEn, active, desc);

                    decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price);
                    long pricePiastres = (long)Math.Round(price * 100m, MidpointRounding.AwayFromZero);

                    decimal? oldPrice = null;
                    long? oldPricePiastres = null;
                    if (decimal.TryParse(oldPriceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal op))
                    {
                        oldPrice = op;
                        oldPricePiastres = (long)Math.Round(op * 100m, MidpointRounding.AwayFromZero);
                    }

                    string? normBarcode = BarcodeValidator.Normalize(barcode);
                    bool barcodeValid = normBarcode != null && BarcodeValidator.ValidateCheckDigit(normBarcode);

                    var drug = new Drug
                    {
                        NameEn = DrugNameNormalizer.NormalizeEnglish(nameEn),
                        NameAr = DrugNameNormalizer.NormalizeArabic(nameAr),
                        GenericNames = ingredients.Select(i => i.Name).Distinct().ToList(),
                        ActiveIngredients = ingredients,
                        DosageForm = dosageForm,
                        StorageType = storage,
                        Price = price,
                        PricePiastres = pricePiastres,
                        OldPrice = oldPrice,
                        OldPricePiastres = oldPricePiastres,
                        Barcode = normBarcode,
                        BarcodeValid = barcodeValid,
                        Company = company?.Trim(),
                        Units = units,
                        CategoryName = desc?.Trim(),
                        Description = desc?.Trim(),
                        Uses = uses?.Trim(),
                        AllowUnitSale = (dosageForm == DrugDosageForm.TABLET || dosageForm == DrugDosageForm.TABLET_FC || dosageForm == DrugDosageForm.CAPSULE)
                    };

                    drug.Sources.Add("eg-drugs");
                    list.Add(drug);
                }
            }

            return list;
        }

        private static List<Drug> ParseKaremEgyptianDrugs(string filePath)
        {
            var list = new List<Drug>();

            using (var fs = File.OpenRead(filePath))
            using (var csv = new SopsCsvReader(fs))
            {
                foreach (var rec in csv.ReadRecords())
                {
                    rec.TryGetValue("commercial_name_en", out var nameEn);
                    rec.TryGetValue("commercial_name_ar", out var nameAr);
                    rec.TryGetValue("scientific_name", out var active);
                    rec.TryGetValue("manufacturer", out var company);
                    rec.TryGetValue("drug_class", out var drugClass);
                    rec.TryGetValue("route", out var route);
                    rec.TryGetValue("price_egp", out var priceStr);

                    if (string.IsNullOrWhiteSpace(nameEn)) continue;

                    var dosageForm = DosageFormParser.Parse(nameEn);
                    var (units, strips, perStrip) = PackageParser.Parse(nameEn, null, dosageForm);
                    var ingredients = StrengthParser.ParseIngredients(active, nameEn);
                    var storage = StorageClassifier.Classify(nameEn, active, route);

                    decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price);
                    long pricePiastres = (long)Math.Round(price * 100m, MidpointRounding.AwayFromZero);

                    var drug = new Drug
                    {
                        NameEn = DrugNameNormalizer.NormalizeEnglish(nameEn),
                        NameAr = DrugNameNormalizer.NormalizeArabic(nameAr),
                        GenericNames = ingredients.Select(i => i.Name).Distinct().ToList(),
                        ActiveIngredients = ingredients,
                        DosageForm = dosageForm,
                        StorageType = storage,
                        Price = price,
                        PricePiastres = pricePiastres,
                        Company = company?.Trim(),
                        Units = units,
                        Route = route?.Trim(),
                        CategoryName = drugClass?.Trim(),
                        Description = drugClass?.Trim(),
                        AllowUnitSale = (dosageForm == DrugDosageForm.TABLET || dosageForm == DrugDosageForm.TABLET_FC || dosageForm == DrugDosageForm.CAPSULE)
                    };

                    drug.Sources.Add("egyptian-drug-database");
                    list.Add(drug);
                }
            }

            return list;
        }

        private static void ExportArtifacts(
            DrugCatalog catalog,
            string outDir,
            List<DrugReviewItem> reviewItems,
            List<BarcodeConflictItem> barcodeConflicts)
        {
            // 1. sops-drugs-import.json (canonical JSON)
            string importJsonPath = Path.Combine(outDir, "sops-drugs-import.json");
            DrugCatalogSerializer.SerializeCatalogToFile(catalog, importJsonPath);
            Console.WriteLine($"    Created: {importJsonPath}");

            // 2. sop-db-export.json (snake_case database format)
            string dbExportPath = Path.Combine(outDir, "sop-db-export.json");
            DrugCatalogSerializer.SerializeServerDbExportToFile(catalog, dbExportPath);
            Console.WriteLine($"    Created: {dbExportPath}");

            // 3. sop-db-export-SAMPLE-50.json (50-sample export)
            string sample50Path = Path.Combine(outDir, "sop-db-export-SAMPLE-50.json");
            DrugCatalogSerializer.SerializeServerDbExportToFile(catalog, sample50Path, limit: 50);
            Console.WriteLine($"    Created: {sample50Path}");

            // 4. sops-drugs-review.csv
            string reviewCsvPath = Path.Combine(outDir, "sops-drugs-review.csv");
            using (var fs = File.Create(reviewCsvPath))
            using (var writer = new SopsCsvWriter(fs))
            {
                writer.WriteRow("DrugName", "IssueType", "Details", "Source1Value", "Source2Value");
                foreach (var item in reviewItems)
                {
                    writer.WriteRow(item.DrugName, item.IssueType, item.Details, item.Source1Value, item.Source2Value);
                }
            }
            Console.WriteLine($"    Created: {reviewCsvPath} ({reviewItems.Count} entries)");

            // 5. sops-drugs-barcode-conflicts.csv
            string barcodeConflictsPath = Path.Combine(outDir, "sops-drugs-barcode-conflicts.csv");
            using (var fs = File.Create(barcodeConflictsPath))
            using (var writer = new SopsCsvWriter(fs))
            {
                writer.WriteRow("Barcode", "PrimaryDrugName", "PrimaryDrugSku", "ConflictingDrugName", "ConflictingDrugSku");
                foreach (var conflict in barcodeConflicts)
                {
                    writer.WriteRow(conflict.Barcode, conflict.PrimaryDrugName, conflict.PrimaryDrugSku, conflict.ConflictingDrugName, conflict.ConflictingDrugSku);
                }
            }
            Console.WriteLine($"    Created: {barcodeConflictsPath} ({barcodeConflicts.Count} entries)");

            // 6. sop-drugs-refrigerated.csv
            string refrigeratedPath = Path.Combine(outDir, "sop-drugs-refrigerated.csv");
            var refrigeratedDrugs = catalog.Drugs.Where(d => d.StorageType == StorageType.REFRIGERATOR).ToList();
            using (var fs = File.Create(refrigeratedPath))
            using (var writer = new SopsCsvWriter(fs))
            {
                writer.WriteRow("Sku", "NameEn", "NameAr", "ActiveIngredients", "StorageType", "PriceEgp");
                foreach (var drug in refrigeratedDrugs)
                {
                    string active = string.Join("; ", drug.ActiveIngredients.Select(a => a.ToString()));
                    writer.WriteRow(drug.Sku, drug.NameEn, drug.NameAr, active, drug.StorageType.ToString(), drug.Price.ToString("F2", CultureInfo.InvariantCulture));
                }
            }
            Console.WriteLine($"    Created: {refrigeratedPath} ({refrigeratedDrugs.Count} entries)");

            // 7. sops-drugs-build-manifest.json
            string manifestPath = Path.Combine(outDir, "sops-drugs-build-manifest.json");
            using (var fs = File.Create(manifestPath))
            {
                JsonSerializer.Serialize(fs, catalog.Manifest, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
            }
            Console.WriteLine($"    Created: {manifestPath}");

            // 8. sops-drugs-validation-report.json
            string validationReportPath = Path.Combine(outDir, "sops-drugs-validation-report.json");
            var index = new DrugCatalogIndex(catalog);
            var validationIssues = index.ValidateCatalog();

            var validationReport = new
            {
                timestamp_utc = DateTimeOffset.UtcNow,
                total_drugs = catalog.Drugs.Count,
                total_categories = catalog.Categories.Count,
                errors = validationIssues.Count(i => i.Severity == "ERROR"),
                warnings = validationIssues.Count(i => i.Severity == "WARNING"),
                info = validationIssues.Count(i => i.Severity == "INFO"),
                side_name_valid_pct = (catalog.Drugs.Count(d => !string.IsNullOrEmpty(d.SideName) && SideNameValidator.IsValid(d.SideName)) / (double)Math.Max(1, catalog.Drugs.Count)) * 100.0,
                sample_issues = validationIssues.Take(50).Select(i => new { severity = i.Severity, description = i.Description, context = i.Context })
            };

            using (var fs = File.Create(validationReportPath))
            {
                JsonSerializer.Serialize(fs, validationReport, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
            }
            Console.WriteLine($"    Created: {validationReportPath}");
        }
    }
}
