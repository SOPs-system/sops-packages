namespace Sops.Models.Common;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Paginated envelope containing query results and total record count.
/// </summary>
/// <typeparam name="T">Type of items contained in the page.</typeparam>
public sealed record PageResult<T>
{
    /// <summary>
    /// Gets the current 1-based page index.
    /// </summary>
    [JsonPropertyName("page_number")]
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// Gets the requested page size.
    /// </summary>
    [JsonPropertyName("page_size")]
    public int PageSize { get; init; } = 50;

    /// <summary>
    /// Gets the total count of records matching the query.
    /// </summary>
    [JsonPropertyName("total_count")]
    public long TotalCount { get; init; }

    /// <summary>
    /// Gets the calculated total number of pages.
    /// </summary>
    [JsonPropertyName("total_pages")]
    public int TotalPages { get; init; }

    /// <summary>
    /// Gets the list of items on the current page.
    /// </summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    /// <summary>
    /// Creates an empty page result.
    /// </summary>
    public static PageResult<T> Empty(int pageNumber = 1, int pageSize = 50) => new()
    {
        PageNumber = pageNumber,
        PageSize = pageSize,
        TotalCount = 0,
        TotalPages = 0,
        Items = Array.Empty<T>()
    };

    /// <summary>
    /// Factory method to construct a page result and calculate total pages.
    /// </summary>
    public static PageResult<T> Create(IReadOnlyList<T> items, long totalCount, int pageNumber, int pageSize)
    {
        int totalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 0;
        return new PageResult<T>
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Items = items ?? Array.Empty<T>()
        };
    }
}
