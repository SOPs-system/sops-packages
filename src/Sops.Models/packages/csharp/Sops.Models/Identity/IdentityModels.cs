namespace Sops.Models.Identity;

using System;
using System.Text.Json.Serialization;

/// <summary>
/// Safe user profile projection for general API exchange without exposing password hashes, salts, or session tokens.
/// </summary>
public sealed record UserSummary
{
    /// <summary>
    /// Gets the unique user ID.
    /// </summary>
    [JsonPropertyName("user_id")]
    public Guid UserId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the login username.
    /// </summary>
    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Gets the user's full display name.
    /// </summary>
    [JsonPropertyName("full_name")]
    public string FullName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the contact email address.
    /// </summary>
    [JsonPropertyName("email")]
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional contact phone number.
    /// </summary>
    [JsonPropertyName("phone")]
    public string? Phone { get; init; }

    /// <summary>
    /// Gets whether this user account is currently active.
    /// </summary>
    [JsonPropertyName("is_active")]
    public bool IsActive { get; init; } = true;
}

/// <summary>
/// Pharmacy staff profile including professional credentials and branch assignment.
/// </summary>
public sealed record StaffSummary
{
    /// <summary>
    /// Gets the user ID.
    /// </summary>
    [JsonPropertyName("user_id")]
    public Guid UserId { get; init; }

    /// <summary>
    /// Gets the staff member's full name.
    /// </summary>
    [JsonPropertyName("full_name")]
    public string FullName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the assigned operational role (e.g. "Cashier", "HeadPharmacist").
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    /// <summary>
    /// Gets the assigned physical branch ID.
    /// </summary>
    [JsonPropertyName("branch_id")]
    public Guid BranchId { get; init; }

    /// <summary>
    /// Gets whether this staff member is a licensed pharmacist.
    /// </summary>
    [JsonPropertyName("is_pharmacist")]
    public bool IsPharmacist { get; init; }

    /// <summary>
    /// Gets the official pharmacy syndicate or license number.
    /// </summary>
    [JsonPropertyName("license_number")]
    public string? LicenseNumber { get; init; }
}

/// <summary>
/// Workstation login session summary.
/// </summary>
public sealed record SessionSummary
{
    /// <summary>
    /// Gets the session ID.
    /// </summary>
    [JsonPropertyName("session_id")]
    public Guid SessionId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the logged-in user ID.
    /// </summary>
    [JsonPropertyName("user_id")]
    public Guid UserId { get; init; }

    /// <summary>
    /// Gets the username.
    /// </summary>
    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Gets the terminal ID where the session is active.
    /// </summary>
    [JsonPropertyName("terminal_id")]
    public Guid TerminalId { get; init; }

    /// <summary>
    /// Gets the branch ID.
    /// </summary>
    [JsonPropertyName("branch_id")]
    public Guid BranchId { get; init; }

    /// <summary>
    /// Gets the session start timestamp.
    /// </summary>
    [JsonPropertyName("logged_in_at_utc")]
    public DateTimeOffset LoggedInAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the last activity timestamp.
    /// </summary>
    [JsonPropertyName("last_activity_utc")]
    public DateTimeOffset LastActivityUtc { get; init; } = DateTimeOffset.UtcNow;
}
