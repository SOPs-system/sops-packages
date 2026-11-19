using System;
using System.Collections.Generic;

namespace Sops.Drugs.Models
{
    /// <summary>
    /// Represents a master pharmaceutical drug item in the SOPs catalog.
    /// </summary>
    public class Drug
    {
        /// <summary>
        /// Gets or sets the stable unique drug identifier (UUID or deterministic hash).
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the business SKU identifier with "DRG" prefix.
        /// </summary>
        public string Sku { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the commercial trade name in English.
        /// </summary>
        public string NameEn { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the commercial trade name or phonetic search alias in Arabic.
        /// </summary>
        public string? NameAr { get; set; }

        /// <summary>
        /// Gets or sets the list of active ingredient scientific/generic names.
        /// </summary>
        public List<string> GenericNames { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the structured list of active ingredients with strength and measurement unit.
        /// </summary>
        public List<DrugIngredient> ActiveIngredients { get; set; } = new List<DrugIngredient>();

        /// <summary>
        /// Gets or sets the validated SideName string representing dosage, strength, and pack specification.
        /// </summary>
        public string? SideName { get; set; }

        /// <summary>
        /// Gets or sets the dosage form (e.g. TABLET, CAPSULE, SYRUP).
        /// </summary>
        public DrugDosageForm DosageForm { get; set; } = DrugDosageForm.TABLET;

        /// <summary>
        /// Gets or sets the storage condition classification (NORMAL, REFRIGERATOR, FREEZER, UNKNOWN).
        /// </summary>
        public StorageType StorageType { get; set; } = StorageType.NORMAL;

        /// <summary>
        /// Gets or sets the retail price in Egyptian Pounds (EGP).
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// Gets or sets the retail price in integer piastres (1 EGP = 100 piastres).
        /// </summary>
        public long PricePiastres { get; set; }

        /// <summary>
        /// Gets or sets the previous retail price in Egyptian Pounds, if tracked.
        /// </summary>
        public decimal? OldPrice { get; set; }

        /// <summary>
        /// Gets or sets the previous retail price in integer piastres, if tracked.
        /// </summary>
        public long? OldPricePiastres { get; set; }

        /// <summary>
        /// Gets or sets the primary EAN/UPC/GTIN barcode string.
        /// </summary>
        public string? Barcode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the check digit of the barcode is valid.
        /// </summary>
        public bool BarcodeValid { get; set; }

        /// <summary>
        /// Gets or sets the pharmaceutical manufacturer or marketing company name.
        /// </summary>
        public string? Company { get; set; }

        /// <summary>
        /// Gets or sets the foreign key referencing <see cref="DrugCategory.Id"/>.
        /// </summary>
        public string? CategoryId { get; set; }

        /// <summary>
        /// Gets or sets the category name.
        /// </summary>
        public string? CategoryName { get; set; }

        /// <summary>
        /// Gets or sets the number of base units in one pack (e.g. 20 tablets, 100 ml).
        /// </summary>
        public int Units { get; set; } = 1;

        /// <summary>
        /// Gets or sets the list of packaging tiers and conversion ratios.
        /// </summary>
        public List<DrugPackageInfo> Packages { get; set; } = new List<DrugPackageInfo>();

        /// <summary>
        /// Gets or sets the route of administration (e.g. "Oral.Solid", "Oral.Liquid", "Topical").
        /// </summary>
        public string? Route { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this medication requires a medical prescription.
        /// </summary>
        public bool RequiresRx { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether single unit sales (e.g. per strip) are permitted.
        /// </summary>
        public bool AllowUnitSale { get; set; } = true;

        /// <summary>
        /// Gets or sets the description or therapeutic indication of the drug.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the medical uses text in Arabic.
        /// </summary>
        public string? Uses { get; set; }

        /// <summary>
        /// Gets or sets the list of upstream source datasets contributing to this record.
        /// </summary>
        public List<string> Sources { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets any review or audit flags raised during catalog merging.
        /// </summary>
        public List<string> ReviewFlags { get; set; } = new List<string>();

        /// <inheritdoc />
        public override string ToString() => $"{NameEn} - {SideName ?? DosageForm.ToString()} ({Price:F2} EGP)";
    }
}
