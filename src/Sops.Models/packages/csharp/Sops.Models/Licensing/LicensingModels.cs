namespace Sops.Models.Licensing;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Validation status of a branch or workstation license.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LicenseStatus
{
    /// <summary>Fully valid and active.</summary>
    Valid,
    /// <summary>Expired but operating within offline grace period.</summary>
    GracePeriod,
    /// <summary>License expired; operation restricted.</summary>
    Expired,
    /// <summary>License suspended by administration.</summary>
    Suspended,
    /// <summary>License revoked.</summary>
    Revoked,
    /// <summary>Digital signature verification failed.</summary>
    InvalidSignature,
    /// <summary>Hardware signature drift exceeds allowed threshold.</summary>
    DeviceMismatch
}

/// <summary>
/// Standardized license claims and entitlements encoded within signed certificates.
/// </summary>
public sealed record LicenseClaims
{
    /// <summary>
    /// Gets the unique license identifier.
    /// </summary>
    [JsonPropertyName("license_id")]
    public Guid LicenseId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the licensed tenant ID.
    /// </summary>
    [JsonPropertyName("tenant_id")]
    public Guid TenantId { get; init; }

    /// <summary>
    /// Gets the subscription tier name.
    /// </summary>
    [JsonPropertyName("tier")]
    public string Tier { get; init; } = "Professional";

    /// <summary>
    /// Gets the license issuance timestamp.
    /// </summary>
    [JsonPropertyName("issued_at_utc")]
    public DateTimeOffset IssuedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the license expiration timestamp.
    /// </summary>
    [JsonPropertyName("expires_at_utc")]
    public DateTimeOffset ExpiresAtUtc { get; init; }

    /// <summary>
    /// Gets the maximum allowed branches under this license.
    /// </summary>
    [JsonPropertyName("max_branches")]
    public int MaxBranches { get; init; } = 1;

    /// <summary>
    /// Gets the maximum concurrent workstation seats allowed per branch.
    /// </summary>
    [JsonPropertyName("max_seats_per_branch")]
    public int MaxSeatsPerBranch { get; init; } = 3;

    /// <summary>
    /// Gets the list of enabled feature flags.
    /// </summary>
    [JsonPropertyName("enabled_features")]
    public IReadOnlyList<string> EnabledFeatures { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Outcome of a device or workstation license activation attempt.
/// </summary>
public sealed record ActivationResult
{
    /// <summary>
    /// Gets whether activation succeeded.
    /// </summary>
    [JsonPropertyName("is_successful")]
    public bool IsSuccessful { get; init; }

    /// <summary>
    /// Gets the outcome status code string.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Gets the raw signed license certificate string on success.
    /// </summary>
    [JsonPropertyName("certificate_raw")]
    public string? CertificateRaw { get; init; }

    /// <summary>
    /// Gets the diagnostic error message on failure.
    /// </summary>
    [JsonPropertyName("failure_reason")]
    public string? FailureReason { get; init; }
}
