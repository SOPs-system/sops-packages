namespace Sops.Models.Common;

using System.Text.Json.Serialization;

/// <summary>
/// Physical street address value object for pharmacies, branches, warehouses, and distributors.
/// </summary>
public sealed record Address
{
    /// <summary>
    /// Gets the primary street address or building number.
    /// </summary>
    [JsonPropertyName("street_line1")]
    public string StreetLine1 { get; init; } = string.Empty;

    /// <summary>
    /// Gets optional secondary address info (floor, suite, apartment, landmark).
    /// </summary>
    [JsonPropertyName("street_line2")]
    public string? StreetLine2 { get; init; }

    /// <summary>
    /// Gets the city or municipality (e.g., "Cairo", "Alexandria").
    /// </summary>
    [JsonPropertyName("city")]
    public string City { get; init; } = string.Empty;

    /// <summary>
    /// Gets the state or governorate (e.g., "Giza", "Cairo").
    /// </summary>
    [JsonPropertyName("state_or_governorate")]
    public string StateOrGovernorate { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional postal code.
    /// </summary>
    [JsonPropertyName("postal_code")]
    public string? PostalCode { get; init; }

    /// <summary>
    /// Gets the two-letter ISO 3166-1 alpha-2 uppercase country code (e.g., "EG", "SA").
    /// </summary>
    [JsonPropertyName("country_code")]
    public string CountryCode { get; init; } = "EG";
}
