namespace Sops.Models.Common;

using System;
using System.Text.Json.Serialization;

/// <summary>
/// Universal provenance and concurrency metadata for persistent records.
/// </summary>
public sealed record AuditMetadata
{
    /// <summary>
    /// Gets the UTC creation timestamp.
    /// </summary>
    [JsonPropertyName("created_at_utc")]
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the user ID who created the record.
    /// </summary>
    [JsonPropertyName("created_by_user_id")]
    public Guid? CreatedByUserId { get; init; }

    /// <summary>
    /// Gets the optional UTC last-modified timestamp.
    /// </summary>
    [JsonPropertyName("updated_at_utc")]
    public DateTimeOffset? UpdatedAtUtc { get; init; }

    /// <summary>
    /// Gets the user ID who last modified the record.
    /// </summary>
    [JsonPropertyName("updated_by_user_id")]
    public Guid? UpdatedByUserId { get; init; }

    /// <summary>
    /// Gets the optimistic concurrency version sequence number.
    /// </summary>
    [JsonPropertyName("version")]
    public long Version { get; init; } = 1;
}
