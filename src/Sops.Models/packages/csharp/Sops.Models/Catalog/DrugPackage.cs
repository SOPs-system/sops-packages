namespace Sops.Models.Catalog;

using System;
using System.Text.Json.Serialization;

/// <summary>
/// Value object embedded inside Drug defining a packaging level (e.g. Box, Strip, Unit) and integer conversion factor to base units.
/// Does NOT have an independent barcode or database table.
/// </summary>
public sealed record DrugPackage
{
    /// <summary>
    /// Gets the packaging tier name (e.g. "Box", "Strip", "Vial", "Unit").
    /// </summary>
    [JsonPropertyName("tier_name")]
    public string TierName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the positive integer factor indicating how many base units are contained in this package tier.
    /// Strictly positive integer (&gt;= 1).
    /// </summary>
    [JsonPropertyName("conversion_factor_to_base")]
    public int ConversionFactorToBase { get; init; } = 1;

    /// <summary>
    /// Gets whether this tier is the default dispensing unit at POS checkout.
    /// </summary>
    [JsonPropertyName("is_default_dispensing_unit")]
    public bool IsDefaultDispensingUnit { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DrugPackage"/> class.
    /// </summary>
    public DrugPackage() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DrugPackage"/> class with parameters.
    /// </summary>
    public DrugPackage(string tierName, int conversionFactorToBase, bool isDefaultDispensingUnit = false)
    {
        if (string.IsNullOrWhiteSpace(tierName))
        {
            throw new ArgumentException("Tier name cannot be null or whitespace.", nameof(tierName));
        }

        if (conversionFactorToBase < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(conversionFactorToBase), "Conversion factor must be a positive integer greater than or equal to 1.");
        }

        TierName = tierName;
        ConversionFactorToBase = conversionFactorToBase;
        IsDefaultDispensingUnit = isDefaultDispensingUnit;
    }
}

/// <summary>
/// Active pharmaceutical ingredient and strength.
/// </summary>
public sealed record ActiveIngredient
{
    /// <summary>
    /// Gets the official substance name (e.g. "Paracetamol", "Amoxicillin").
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the concentration or strength (e.g. "500 mg", "125 mg / 5 ml").
    /// </summary>
    [JsonPropertyName("strength")]
    public string Strength { get; init; } = string.Empty;
}
