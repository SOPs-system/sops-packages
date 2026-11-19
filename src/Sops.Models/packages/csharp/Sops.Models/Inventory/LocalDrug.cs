namespace Sops.Models.Inventory;

using System;
using System.Text.Json.Serialization;
using Sops.Models.Common;

/// <summary>
/// Branch-specific operational profile, local purchasing cost, and inventory limits for a catalogue drug.
/// Authoritative on local branch costs and custom shelf location.
/// </summary>
public sealed record LocalDrug
{
    /// <summary>
    /// Gets the unique local drug record identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the reference to the authoritative global drug catalogue item.
    /// </summary>
    [JsonPropertyName("global_drug_id")]
    public Guid GlobalDrugId { get; init; }

    /// <summary>
    /// Gets the local branch pharmacy identifier.
    /// </summary>
    [JsonPropertyName("pharmacy_id")]
    public Guid PharmacyId { get; init; }

    /// <summary>
    /// Gets the weighted average local purchase cost in minor units.
    /// Local operational costs do NOT overwrite official public prices.
    /// </summary>
    [JsonPropertyName("local_cost_price")]
    public Money LocalCostPrice { get; init; }

    /// <summary>
    /// Gets optional custom local selling price (must not exceed official price if regulated).
    /// </summary>
    [JsonPropertyName("custom_selling_price")]
    public Money? CustomSellingPrice { get; init; }

    /// <summary>
    /// Gets the optional reference to the physical shelf location.
    /// </summary>
    [JsonPropertyName("storage_location_id")]
    public Guid? StorageLocationId { get; init; }

    /// <summary>
    /// Gets the low-stock warning threshold.
    /// </summary>
    [JsonPropertyName("minimum_stock_threshold")]
    public int MinimumStockThreshold { get; init; } = 5;

    /// <summary>
    /// Gets the maximum shelf capacity threshold.
    /// </summary>
    [JsonPropertyName("maximum_stock_threshold")]
    public int MaximumStockThreshold { get; init; } = 100;

    /// <summary>
    /// Gets whether this pharmacy currently stocks and dispenses this item.
    /// </summary>
    [JsonPropertyName("is_active_locally")]
    public bool IsActiveLocally { get; init; } = true;
}

/// <summary>
/// Physical pharmacy location within the store (aisle, shelf, drawer, refrigerator).
/// </summary>
public sealed record StorageLocation
{
    /// <summary>
    /// Gets the location ID.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the owning pharmacy ID.
    /// </summary>
    [JsonPropertyName("pharmacy_id")]
    public Guid PharmacyId { get; init; }

    /// <summary>
    /// Gets the friendly shelf label (e.g. "Aisle 3 - Shelf B").
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the zone classification (e.g. "Front Counter", "Cold Storage").
    /// </summary>
    [JsonPropertyName("zone")]
    public string Zone { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether this location is refrigerated or temperature-controlled.
    /// </summary>
    [JsonPropertyName("is_temperature_controlled")]
    public bool IsTemperatureControlled { get; init; }
}
