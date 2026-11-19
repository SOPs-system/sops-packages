namespace Sops.Models.Catalog;

using System;
using System.Text.Json.Serialization;
using Sops.Models.Common;

/// <summary>
/// Therapeutic or commercial classification category for catalogue items.
/// </summary>
public sealed record DrugCategory
{
    /// <summary>
    /// Gets the unique category identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the optional parent category identifier for hierarchical trees.
    /// </summary>
    [JsonPropertyName("parent_id")]
    public Guid? ParentId { get; init; }

    /// <summary>
    /// Gets the category code (e.g., "ANTIBIOTICS", "CARDIO").
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// Gets the English / Latin category name.
    /// </summary>
    [JsonPropertyName("name_en")]
    public string NameEn { get; init; } = string.Empty;

    /// <summary>
    /// Gets the Arabic category name.
    /// </summary>
    [JsonPropertyName("name_ar")]
    public string NameAr { get; init; } = string.Empty;

    /// <summary>
    /// Gets an optional description of the category.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

/// <summary>
/// Pharmaceutical supplier, wholesaler, or manufacturer distributor.
/// </summary>
public sealed record Distributor
{
    /// <summary>
    /// Gets the unique distributor identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the official company or commercial trade name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the commercial tax registration number.
    /// </summary>
    [JsonPropertyName("tax_number")]
    public string? TaxNumber { get; init; }

    /// <summary>
    /// Gets the primary contact phone number.
    /// </summary>
    [JsonPropertyName("phone")]
    public string Phone { get; init; } = string.Empty;

    /// <summary>
    /// Gets the contact email address.
    /// </summary>
    [JsonPropertyName("email")]
    public string? Email { get; init; }

    /// <summary>
    /// Gets the physical corporate headquarters or warehouse address.
    /// </summary>
    [JsonPropertyName("address")]
    public Address? Address { get; init; }

    /// <summary>
    /// Gets whether this distributor account is currently active.
    /// </summary>
    [JsonPropertyName("is_active")]
    public bool IsActive { get; init; } = true;
}

/// <summary>
/// High-speed search projection for rapid point-of-sale drug queries.
/// </summary>
public sealed record DrugSearchResult
{
    /// <summary>
    /// Gets the master drug ID.
    /// </summary>
    [JsonPropertyName("drug_id")]
    public Guid DrugId { get; init; }

    /// <summary>
    /// Gets the SKU code.
    /// </summary>
    [JsonPropertyName("sku")]
    public string Sku { get; init; } = string.Empty;

    /// <summary>
    /// Gets the scannable primary barcode.
    /// </summary>
    [JsonPropertyName("barcode")]
    public string Barcode { get; init; } = string.Empty;

    /// <summary>
    /// Gets the English trade name.
    /// </summary>
    [JsonPropertyName("name_en")]
    public string NameEn { get; init; } = string.Empty;

    /// <summary>
    /// Gets the Arabic trade name.
    /// </summary>
    [JsonPropertyName("name_ar")]
    public string NameAr { get; init; } = string.Empty;

    /// <summary>
    /// Gets the dosage form string representation.
    /// </summary>
    [JsonPropertyName("dosage_form")]
    public string DosageForm { get; init; } = string.Empty;

    /// <summary>
    /// Gets the official price.
    /// </summary>
    [JsonPropertyName("official_price")]
    public Money OfficialPrice { get; init; }

    /// <summary>
    /// Gets whether inventory is currently available at the local branch.
    /// </summary>
    [JsonPropertyName("is_in_stock")]
    public bool IsInStock { get; init; }

    /// <summary>
    /// Gets the available count in base units.
    /// </summary>
    [JsonPropertyName("available_base_units")]
    public decimal AvailableBaseUnits { get; init; }

    /// <summary>
    /// Gets the physical shelf or drawer location label.
    /// </summary>
    [JsonPropertyName("shelf_location")]
    public string? ShelfLocation { get; init; }
}
