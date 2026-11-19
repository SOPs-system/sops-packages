namespace Sops.Models.Tenancy;

using System;
using System.Text.Json.Serialization;
using Sops.Models.Common;

/// <summary>
/// Subscription tier of a registered pharmacy business.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SubscriptionTier
{
    /// <summary>Free starter tier.</summary>
    Free,
    /// <summary>Professional independent pharmacy.</summary>
    Professional,
    /// <summary>Multi-branch pharmacy chain.</summary>
    Enterprise,
    /// <summary>Custom enterprise contract.</summary>
    Custom
}

/// <summary>
/// Provisioning lifecycle status of a tenant chain database.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TenantProvisionStatus
{
    /// <summary>Awaiting provisioning.</summary>
    Pending,
    /// <summary>Database being migrated and initialized.</summary>
    Provisioning,
    /// <summary>Actively operating.</summary>
    Active,
    /// <summary>Account suspended (e.g. past-due bill).</summary>
    Suspended,
    /// <summary>Permanently decommissioned.</summary>
    Decommissioned
}

/// <summary>
/// Multi-tenant corporate group representing a pharmacy chain or business.
/// </summary>
public sealed record Chain
{
    /// <summary>
    /// Gets the unique chain identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the registered legal business name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the corporate tax registration number.
    /// </summary>
    [JsonPropertyName("tax_number")]
    public string TaxNumber { get; init; } = string.Empty;

    /// <summary>
    /// Gets the commercial registry number.
    /// </summary>
    [JsonPropertyName("commercial_registration")]
    public string? CommercialRegistration { get; init; }

    /// <summary>
    /// Gets the subscription tier.
    /// </summary>
    [JsonPropertyName("subscription_tier")]
    public SubscriptionTier SubscriptionTier { get; init; } = SubscriptionTier.Professional;

    /// <summary>
    /// Gets the tenant provisioning status.
    /// </summary>
    [JsonPropertyName("provision_status")]
    public TenantProvisionStatus ProvisionStatus { get; init; } = TenantProvisionStatus.Active;

    /// <summary>
    /// Gets the account creation timestamp.
    /// </summary>
    [JsonPropertyName("created_at_utc")]
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Globally registered pharmacy regulatory license.
/// </summary>
public sealed record Pharmacy
{
    /// <summary>
    /// Gets the unique pharmacy ID.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the owning chain ID.
    /// </summary>
    [JsonPropertyName("chain_id")]
    public Guid ChainId { get; init; }

    /// <summary>
    /// Gets the pharmacy name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the official government pharmacy license number.
    /// </summary>
    [JsonPropertyName("license_number")]
    public string LicenseNumber { get; init; } = string.Empty;

    /// <summary>
    /// Gets the supervising pharmacist's name.
    /// </summary>
    [JsonPropertyName("supervising_pharmacist")]
    public string? SupervisingPharmacist { get; init; }

    /// <summary>
    /// Gets whether this pharmacy license is currently active.
    /// </summary>
    [JsonPropertyName("is_active")]
    public bool IsActive { get; init; } = true;
}

/// <summary>
/// Tenant branch projection for physical branch or warehouse operations.
/// </summary>
public sealed record Branch
{
    /// <summary>
    /// Gets the unique branch ID.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the owning chain ID.
    /// </summary>
    [JsonPropertyName("chain_id")]
    public Guid ChainId { get; init; }

    /// <summary>
    /// Gets the optional reference to the global registered pharmacy license.
    /// </summary>
    [JsonPropertyName("global_pharmacy_id")]
    public Guid? GlobalPharmacyId { get; init; }

    /// <summary>
    /// Gets the organizational branch code (e.g. "BR-001", "WH-CAIRO").
    /// </summary>
    [JsonPropertyName("branch_code")]
    public string BranchCode { get; init; } = string.Empty;

    /// <summary>
    /// Gets the commercial branch name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether this facility functions as a central warehouse / distribution hub.
    /// </summary>
    [JsonPropertyName("is_warehouse")]
    public bool IsWarehouse { get; init; }

    /// <summary>
    /// Gets the physical street address.
    /// </summary>
    [JsonPropertyName("address")]
    public Address Address { get; init; } = new();

    /// <summary>
    /// Gets the contact phone number.
    /// </summary>
    [JsonPropertyName("phone")]
    public string Phone { get; init; } = string.Empty;

    /// <summary>
    /// Gets the managing pharmacist name.
    /// </summary>
    [JsonPropertyName("manager_name")]
    public string? ManagerName { get; init; }

    /// <summary>
    /// Gets whether this branch is currently active.
    /// </summary>
    [JsonPropertyName("is_active")]
    public bool IsActive { get; init; } = true;
}
