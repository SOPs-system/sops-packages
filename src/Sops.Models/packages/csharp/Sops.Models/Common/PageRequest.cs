namespace Sops.Models.Common;

using System.Text.Json.Serialization;

/// <summary>
/// Standard pagination and sorting request parameters for list endpoints.
/// </summary>
public sealed record PageRequest
{
    /// <summary>
    /// Gets the 1-based page index.
    /// </summary>
    [JsonPropertyName("page_number")]
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// Gets the number of items per page (maximum 500).
    /// </summary>
    [JsonPropertyName("page_size")]
    public int PageSize { get; init; } = 50;

    /// <summary>
    /// Gets the optional search query term.
    /// </summary>
    [JsonPropertyName("search_term")]
    public string? SearchTerm { get; init; }

    /// <summary>
    /// Gets the optional field name to sort by.
    /// </summary>
    [JsonPropertyName("sort_by")]
    public string? SortBy { get; init; }

    /// <summary>
    /// Gets whether to sort in descending order.
    /// </summary>
    [JsonPropertyName("sort_descending")]
    public bool SortDescending { get; init; }
}
