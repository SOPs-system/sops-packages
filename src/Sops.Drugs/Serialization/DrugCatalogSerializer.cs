using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sops.Drugs.Models;

namespace Sops.Drugs.Serialization
{
    /// <summary>
    /// Handles serialization and deserialization of the drug catalog into standard SOPs JSON formats.
    /// Preserves UTF-8 Arabic characters without unicode escape sequences.
    /// </summary>
    public static class DrugCatalogSerializer
    {
        private static readonly JsonSerializerOptions StandardOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Serializes the canonical <see cref="DrugCatalog"/> to JSON stream.
        /// </summary>
        public static void SerializeCatalog(DrugCatalog catalog, Stream stream)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Indented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }))
            {
                JsonSerializer.Serialize(writer, catalog, StandardOptions);
            }
        }

        /// <summary>
        /// Serializes the canonical <see cref="DrugCatalog"/> to a JSON file.
        /// </summary>
        public static void SerializeCatalogToFile(DrugCatalog catalog, string filePath)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                SerializeCatalog(catalog, fs);
            }
        }

        /// <summary>
        /// Deserializes a canonical <see cref="DrugCatalog"/> from JSON stream.
        /// </summary>
        public static DrugCatalog DeserializeCatalog(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            return JsonSerializer.Deserialize<DrugCatalog>(stream, StandardOptions) ?? new DrugCatalog();
        }

        /// <summary>
        /// Deserializes a canonical <see cref="DrugCatalog"/> from a JSON file.
        /// </summary>
        public static DrugCatalog DeserializeCatalogFromFile(string filePath)
        {
            using (var fs = File.OpenRead(filePath))
            {
                return DeserializeCatalog(fs);
            }
        }

        /// <summary>
        /// Serializes drugs and categories into the snake_case database schema format required by Sop.Server.
        /// </summary>
        /// <param name="catalog">The drug catalog.</param>
        /// <param name="stream">Output stream.</param>
        /// <param name="limit">Optional maximum number of drugs to export.</param>
        public static void SerializeServerDbExport(DrugCatalog catalog, Stream stream, int? limit = null)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            var drugsToExport = limit.HasValue ? catalog.Drugs.Take(limit.Value).ToList() : catalog.Drugs;

            var serverDrugs = drugsToExport.Select(d => new ServerDrugExportRecord
            {
                Id = d.Id,
                Sku = d.Sku,
                NameEn = d.NameEn,
                NameAr = d.NameAr,
                GenericNamesJson = d.GenericNames,
                SideName = d.SideName,
                Company = d.Company,
                Barcode = d.Barcode,
                DosageForm = d.DosageForm.ToString(),
                StorageType = d.StorageType.ToString(),
                CategoryId = d.CategoryId,
                ActiveIngredientsJson = d.ActiveIngredients.Select(a => new ServerActiveIngredientRecord
                {
                    Name = a.Name,
                    Concentration = a.Concentration,
                    Unit = a.Unit.ToString()
                }).ToList(),
                PackagesJson = d.Packages.Select(p => new ServerPackageRecord
                {
                    Name = p.Name,
                    Type = p.Type,
                    AmountInPack = p.AmountInPack
                }).ToList(),
                StockUnitAmount = $"0:0/{Math.Max(1, d.Units)}",
                RequiresRx = d.RequiresRx,
                Price = d.PricePiastres,
                CostPrice = 0,
                AllowUnitSale = d.AllowUnitSale,
                TaxRate = null,
                LowStockThreshold = "0",
                TotalSales = 0,
                TotalPurchases = 0
            }).ToList();

            var serverCategories = catalog.Categories.Select(c => new ServerCategoryExportRecord
            {
                Id = c.Id,
                NameEn = c.NameEn,
                NameAr = c.NameAr,
                ParentId = c.ParentId
            }).ToList();

            var exportWrapper = new ServerDbExportWrapper
            {
                Drugs = serverDrugs,
                Categories = serverCategories,
                ExportTimestampUtc = DateTimeOffset.UtcNow,
                TotalDrugs = serverDrugs.Count,
                TotalCategories = serverCategories.Count
            };

            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Indented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }))
            {
                JsonSerializer.Serialize(writer, exportWrapper, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
            }
        }

        /// <summary>
        /// Serializes drugs and categories into the snake_case database schema format to a file.
        /// </summary>
        public static void SerializeServerDbExportToFile(DrugCatalog catalog, string filePath, int? limit = null)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                SerializeServerDbExport(catalog, fs, limit);
            }
        }
    }

    /// <summary>
    /// Wrapper for Sop.Server database export.
    /// </summary>
    public class ServerDbExportWrapper
    {
        /// <summary>Export timestamp in UTC.</summary>
        [JsonPropertyName("export_timestamp_utc")]
        public DateTimeOffset ExportTimestampUtc { get; set; }

        /// <summary>Total drugs in this export.</summary>
        [JsonPropertyName("total_drugs")]
        public int TotalDrugs { get; set; }

        /// <summary>Total categories in this export.</summary>
        [JsonPropertyName("total_categories")]
        public int TotalCategories { get; set; }

        /// <summary>Exported categories list.</summary>
        [JsonPropertyName("categories")]
        public List<ServerCategoryExportRecord> Categories { get; set; } = new List<ServerCategoryExportRecord>();

        /// <summary>Exported drugs list.</summary>
        [JsonPropertyName("drugs")]
        public List<ServerDrugExportRecord> Drugs { get; set; } = new List<ServerDrugExportRecord>();
    }

    /// <summary>
    /// Snake-case drug record matching Sop.Server drug table columns.
    /// </summary>
    public class ServerDrugExportRecord
    {
        /// <summary>Unique drug identifier.</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>Business SKU code.</summary>
        [JsonPropertyName("sku")]
        public string Sku { get; set; } = string.Empty;

        /// <summary>English trade name.</summary>
        [JsonPropertyName("name_en")]
        public string NameEn { get; set; } = string.Empty;

        /// <summary>Arabic trade name.</summary>
        [JsonPropertyName("name_ar")]
        public string? NameAr { get; set; }

        /// <summary>Scientific generic names list.</summary>
        [JsonPropertyName("generic_names_json")]
        public List<string> GenericNamesJson { get; set; } = new List<string>();

        /// <summary>Side name specification.</summary>
        [JsonPropertyName("side_name")]
        public string? SideName { get; set; }

        /// <summary>Manufacturing company.</summary>
        [JsonPropertyName("company")]
        public string? Company { get; set; }

        /// <summary>Barcode string.</summary>
        [JsonPropertyName("barcode")]
        public string? Barcode { get; set; }

        /// <summary>Dosage form string.</summary>
        [JsonPropertyName("dosage_form")]
        public string DosageForm { get; set; } = "TABLET";

        /// <summary>Storage type string.</summary>
        [JsonPropertyName("storage_type")]
        public string StorageType { get; set; } = "NORMAL";

        /// <summary>Referenced category ID.</summary>
        [JsonPropertyName("category_id")]
        public string? CategoryId { get; set; }

        /// <summary>Active ingredients list.</summary>
        [JsonPropertyName("active_ingredients_json")]
        public List<ServerActiveIngredientRecord> ActiveIngredientsJson { get; set; } = new List<ServerActiveIngredientRecord>();

        /// <summary>Packages list.</summary>
        [JsonPropertyName("packages_json")]
        public List<ServerPackageRecord> PackagesJson { get; set; } = new List<ServerPackageRecord>();

        /// <summary>Stock unit amount string.</summary>
        [JsonPropertyName("stock_unit_amount")]
        public string StockUnitAmount { get; set; } = "0:0/1";

        /// <summary>Requires prescription flag.</summary>
        [JsonPropertyName("requires_rx")]
        public bool RequiresRx { get; set; }

        /// <summary>Retail price in piastres.</summary>
        [JsonPropertyName("price")]
        public long Price { get; set; }

        /// <summary>Cost price in piastres.</summary>
        [JsonPropertyName("cost_price")]
        public long CostPrice { get; set; }

        /// <summary>Allow unit sale flag.</summary>
        [JsonPropertyName("allow_unit_sale")]
        public bool AllowUnitSale { get; set; }

        /// <summary>Tax rate percentage.</summary>
        [JsonPropertyName("tax_rate")]
        public double? TaxRate { get; set; }

        /// <summary>Low stock alert threshold.</summary>
        [JsonPropertyName("low_stock_threshold")]
        public string LowStockThreshold { get; set; } = "0";

        /// <summary>Completed sales count.</summary>
        [JsonPropertyName("total_sales")]
        public int TotalSales { get; set; }

        /// <summary>Completed purchases count.</summary>
        [JsonPropertyName("total_purchases")]
        public int TotalPurchases { get; set; }
    }

    /// <summary>
    /// Active ingredient record in database JSON export.
    /// </summary>
    public class ServerActiveIngredientRecord
    {
        /// <summary>Ingredient name.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Concentration strength numeric value.</summary>
        [JsonPropertyName("concentration")]
        public double Concentration { get; set; }

        /// <summary>Concentration measurement unit.</summary>
        [JsonPropertyName("unit")]
        public string Unit { get; set; } = "MG";
    }

    /// <summary>
    /// Package tier record in database JSON export.
    /// </summary>
    public class ServerPackageRecord
    {
        /// <summary>Package name.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "Box";

        /// <summary>Package tier type.</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "UNIT";

        /// <summary>Amount contained in pack.</summary>
        [JsonPropertyName("amount_in_pack")]
        public int AmountInPack { get; set; } = 1;
    }

    /// <summary>
    /// Category record in database JSON export.
    /// </summary>
    public class ServerCategoryExportRecord
    {
        /// <summary>Category unique identifier.</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>English category name.</summary>
        [JsonPropertyName("name_en")]
        public string NameEn { get; set; } = string.Empty;

        /// <summary>Arabic category name.</summary>
        [JsonPropertyName("name_ar")]
        public string? NameAr { get; set; }

        /// <summary>Parent category ID.</summary>
        [JsonPropertyName("parent_id")]
        public string? ParentId { get; set; }
    }
}
