namespace Sops.Models.Device;

using System;
using System.Text.Json.Serialization;

/// <summary>
/// Supported host operating system platform.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DevicePlatform
{
    /// <summary>Microsoft Windows.</summary>
    Windows,
    /// <summary>Linux.</summary>
    Linux,
    /// <summary>Apple macOS.</summary>
    OSX,
    /// <summary>Google Android.</summary>
    Android,
    /// <summary>Apple iOS.</summary>
    iOS,
    /// <summary>Unknown or unsupported platform.</summary>
    Unknown
}

/// <summary>
/// Registration lifecycle status of an authorized POS workstation device.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeviceStatus
{
    /// <summary>Registered awaiting first activation.</summary>
    PendingActivation,
    /// <summary>Active and authorized for POS operation.</summary>
    Active,
    /// <summary>Temporarily suspended by administrator.</summary>
    Suspended,
    /// <summary>Permanently revoked (e.g. stolen hardware).</summary>
    Revoked,
    /// <summary>Decommissioned / retired from service.</summary>
    Decommissioned
}

/// <summary>
/// High-level workstation environment and hardware metrics.
/// </summary>
public sealed record DeviceInfo
{
    /// <summary>
    /// Gets the friendly machine hostname.
    /// </summary>
    [JsonPropertyName("machine_name")]
    public string MachineName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the operating system description string.
    /// </summary>
    [JsonPropertyName("operating_system")]
    public string OperatingSystem { get; init; } = string.Empty;

    /// <summary>
    /// Gets the detected platform.
    /// </summary>
    [JsonPropertyName("platform")]
    public DevicePlatform Platform { get; init; } = DevicePlatform.Windows;

    /// <summary>
    /// Gets the number of logical processor cores.
    /// </summary>
    [JsonPropertyName("processor_count")]
    public int ProcessorCount { get; init; } = 1;

    /// <summary>
    /// Gets total system physical memory in megabytes.
    /// </summary>
    [JsonPropertyName("total_memory_mb")]
    public long? TotalMemoryMb { get; init; }
}

/// <summary>
/// Safe, non-sensitive device registration summary for administrative and licensing APIs.
/// Does NOT expose raw hardware fingerprints, unhashed MAC addresses, or disk serials.
/// </summary>
public sealed record DeviceIdentitySummary
{
    /// <summary>
    /// Gets the unique persistent device identifier.
    /// </summary>
    [JsonPropertyName("device_id")]
    public Guid DeviceId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the human-friendly Crockford Base32 device code (e.g. "D7KX-92MA-88P").
    /// </summary>
    [JsonPropertyName("device_code")]
    public string DeviceCode { get; init; } = string.Empty;

    /// <summary>
    /// Gets the owning chain ID.
    /// </summary>
    [JsonPropertyName("chain_id")]
    public Guid ChainId { get; init; }

    /// <summary>
    /// Gets the registered pharmacy branch ID.
    /// </summary>
    [JsonPropertyName("pharmacy_id")]
    public Guid PharmacyId { get; init; }

    /// <summary>
    /// Gets the operating system platform.
    /// </summary>
    [JsonPropertyName("platform")]
    public DevicePlatform Platform { get; init; } = DevicePlatform.Windows;

    /// <summary>
    /// Gets the workstation computer name.
    /// </summary>
    [JsonPropertyName("device_name")]
    public string DeviceName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the current authorization status.
    /// </summary>
    [JsonPropertyName("status")]
    public DeviceStatus Status { get; init; } = DeviceStatus.Active;

    /// <summary>
    /// Gets the initial registration timestamp.
    /// </summary>
    [JsonPropertyName("registered_at_utc")]
    public DateTimeOffset RegisteredAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the most recent heartbeat ping timestamp.
    /// </summary>
    [JsonPropertyName("last_heartbeat_utc")]
    public DateTimeOffset? LastHeartbeatUtc { get; init; }
}
