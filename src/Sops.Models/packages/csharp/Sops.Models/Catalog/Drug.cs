namespace Sops.Models.Catalog;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Sops.Models.Common;

/// <summary>
/// Master pharmaceutical drug specification contract.
/// Globally authoritative for trade names, dosage form, packaging hierarchy, and government-regulated prices.
/// </summary>
public sealed record Drug
{
    /// <summary>
    /// Gets the globally unique drug identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the unique Stock Keeping Unit (SKU) code.
    /// </summary>
    [JsonPropertyName("sku")]
    public string Sku { get; init; } = string.Empty;

    /// <summary>
    /// Gets the primary EAN-13 or GS1 barcode for scanning.
    /// In the SOPs domain model, barcodes belong strictly to the Drug entity.
    /// </summary>
    [JsonPropertyName("barcode")]
    public string Barcode { get; init; } = string.Empty;

    /// <summary>
    /// Gets the official trade name in English / Latin.
    /// </summary>
    [JsonPropertyName("name_en")]
    public string NameEn { get; init; } = string.Empty;

    /// <summary>
    /// Gets the official trade name in Arabic.
    /// </summary>
    [JsonPropertyName("name_ar")]
    public string NameAr { get; init; } = string.Empty;

    /// <summary>
    /// Gets the generic / scientific International Nonproprietary Name (INN).
    /// </summary>
    [JsonPropertyName("generic_name")]
    public string GenericName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional reference to the master category.
    /// </summary>
    [JsonPropertyName("category_id")]
    public Guid? CategoryId { get; init; }

    /// <summary>
    /// Gets the pharmaceutical dosage form.
    /// </summary>
    [JsonPropertyName("dosage_form")]
    public DrugDosageForm DosageForm { get; init; } = DrugDosageForm.Tablet;

    /// <summary>
    /// Gets the authoritative official public price in minor units with currency.
    /// </summary>
    [JsonPropertyName("official_price")]
    public Money OfficialPrice { get; init; }

    /// <summary>
    /// Gets whether this drug is subject to mandatory governmental price regulation.
    /// </summary>
    [JsonPropertyName("is_price_regulated")]
    public bool IsPriceRegulated { get; init; } = true;

    /// <summary>
    /// Gets whether dispensing requires a licensed medical prescription.
    /// </summary>
    [JsonPropertyName("requires_prescription")]
    public bool RequiresPrescription { get; init; }

    /// <summary>
    /// Gets the required storage environment.
    /// </summary>
    [JsonPropertyName("storage_type")]
    public StorageType StorageType { get; init; } = StorageType.RoomTemperature;

    /// <summary>
    /// Gets the embedded packaging tiers with positive integer conversion factors.
    /// </summary>
    [JsonPropertyName("packages")]
    public IReadOnlyList<DrugPackage> Packages { get; init; } = Array.Empty<DrugPackage>();

    /// <summary>
    /// Gets the active chemical ingredients and strengths.
    /// </summary>
    [JsonPropertyName("active_ingredients")]
    public IReadOnlyList<ActiveIngredient> ActiveIngredients { get; init; } = Array.Empty<ActiveIngredient>();

    /// <summary>
    /// Gets the current regulatory and commercial status.
    /// </summary>
    [JsonPropertyName("status")]
    public DrugStatus Status { get; init; } = DrugStatus.Active;

    /// <summary>
    /// Gets the monotonically increasing catalog revision counter.
    /// </summary>
    [JsonPropertyName("catalog_revision")]
    public long CatalogRevision { get; init; } = 1;

    /// <summary>
    /// Validates domain invariants on the drug contract.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Sku))
        {
            throw new InvalidOperationException("Drug SKU cannot be null or empty.");
        }

        if (string.IsNullOrWhiteSpace(Barcode))
        {
            throw new InvalidOperationException("Drug barcode cannot be null or empty.");
        }

        if (OfficialPrice.AmountMinorUnits < 0)
        {
            throw new InvalidOperationException("Official public price cannot be negative.");
        }

        if (Packages == null || Packages.Count == 0)
        {
            throw new InvalidOperationException("A Drug must have at least one defined packaging tier.");
        }

        foreach (var pkg in Packages)
        {
            if (pkg.ConversionFactorToBase < 1)
            {
                throw new InvalidOperationException($"Package tier '{pkg.TierName}' must have a conversion factor >= 1.");
            }
        }
    }
}
