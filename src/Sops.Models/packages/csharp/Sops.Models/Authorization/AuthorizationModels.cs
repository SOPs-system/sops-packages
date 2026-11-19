namespace Sops.Models.Authorization;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Organizational role definition grouping a collection of permissions.
/// </summary>
public sealed record RoleDefinition
{
    /// <summary>
    /// Gets the unique role ID.
    /// </summary>
    [JsonPropertyName("role_id")]
    public Guid RoleId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the role name (e.g. "Pharmacist", "Cashier", "BranchManager").
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets an optional role description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Gets the list of permission codes assigned to this role.
    /// </summary>
    [JsonPropertyName("permissions")]
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Granular permission code in the system.
/// </summary>
public sealed record PermissionDefinition
{
    /// <summary>
    /// Gets the permission code (e.g. "pos.sales.execute", "inventory.transfer.approve").
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// Gets the human-readable display name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the system module (e.g. "POS", "Inventory", "Patients", "Admin").
    /// </summary>
    [JsonPropertyName("module")]
    public string Module { get; init; } = string.Empty;

    /// <summary>
    /// Gets an optional description of the permission.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

/// <summary>
/// Result of an authorization evaluation check.
/// </summary>
public sealed record AuthorizationDecision
{
    /// <summary>
    /// Gets whether the action is permitted.
    /// </summary>
    [JsonPropertyName("is_authorized")]
    public bool IsAuthorized { get; init; }

    /// <summary>
    /// Gets the checked permission code.
    /// </summary>
    [JsonPropertyName("permission_code")]
    public string PermissionCode { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional reason for denial.
    /// </summary>
    [JsonPropertyName("denial_reason")]
    public string? DenialReason { get; init; }

    /// <summary>
    /// Gets evaluation timestamp.
    /// </summary>
    [JsonPropertyName("evaluated_at_utc")]
    public DateTimeOffset EvaluatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Factory for an allowed decision.
    /// </summary>
    public static AuthorizationDecision Allowed(string permissionCode) => new()
    {
        IsAuthorized = true,
        PermissionCode = permissionCode
    };

    /// <summary>
    /// Factory for a denied decision.
    /// </summary>
    public static AuthorizationDecision Denied(string permissionCode, string reason) => new()
    {
        IsAuthorized = false,
        PermissionCode = permissionCode,
        DenialReason = reason
    };
}

/// <summary>
/// Snapshot of staff roles and permissions persisted locally for offline authorization enforcement.
/// </summary>
public sealed record OfflineAuthorizationSnapshot
{
    /// <summary>
    /// Gets the snapshot ID.
    /// </summary>
    [JsonPropertyName("snapshot_id")]
    public Guid SnapshotId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the branch ID.
    /// </summary>
    [JsonPropertyName("branch_id")]
    public Guid BranchId { get; init; }

    /// <summary>
    /// Gets the issuance timestamp.
    /// </summary>
    [JsonPropertyName("issued_at_utc")]
    public DateTimeOffset IssuedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the expiration timestamp.
    /// </summary>
    [JsonPropertyName("expires_at_utc")]
    public DateTimeOffset ExpiresAtUtc { get; init; }

    /// <summary>
    /// Maps User ID string to their assigned active permission codes.
    /// </summary>
    [JsonPropertyName("user_permissions")]
    public IReadOnlyDictionary<string, IReadOnlyList<string>> UserPermissions { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>();
}
