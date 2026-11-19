using System;

namespace Sops.Device.Models
{
    /// <summary>
    /// Stability classification of a hardware or system signal over the lifecycle of a device.
    /// </summary>
    public enum SignalStability
    {
        /// <summary>Expected to remain identical unless the entire physical system is replaced (e.g. Motherboard, SMBIOS UUID).</summary>
        High = 0,

        /// <summary>May change during legitimate hardware upgrades (e.g. NIC replacement, primary disk upgrade).</summary>
        Medium = 1,

        /// <summary>Volatile or easily modified by user/software configuration (e.g. Hostname, IP address).</summary>
        Low = 2
    }

    /// <summary>
    /// Reliability classification indicating trustworthiness and resistance to spoofing or virtualization artifacts.
    /// </summary>
    public enum SignalReliability
    {
        /// <summary>Cryptographically strong or OS-enforced hardware identifier.</summary>
        High = 0,

        /// <summary>Standard hardware descriptor that may be virtualized or shared across batches.</summary>
        Medium = 1,

        /// <summary>Heuristic or user-configurable property.</summary>
        Low = 2
    }

    /// <summary>
    /// Diagnostic status of signal collection.
    /// </summary>
    public enum SignalStatus
    {
        /// <summary>Signal collected successfully and validated.</summary>
        Available = 0,

        /// <summary>Signal is absent on this specific machine.</summary>
        Unavailable = 1,

        /// <summary>OS permissions or sandboxing denied access.</summary>
        AccessDenied = 2,

        /// <summary>Signal type is not supported on this platform.</summary>
        NotSupported = 3,

        /// <summary>Collection failed due to an exception or I/O error.</summary>
        Failed = 4,

        /// <summary>Collected value matched a known OEM placeholder or blank/invalid pattern and was rejected.</summary>
        RejectedPlaceholder = 5
    }

    /// <summary>
    /// Represents an individual hardware or system signal evaluated for device identity.
    /// Raw hardware values are kept internal; public exposure uses SHA-256 hashes for privacy.
    /// </summary>
    public sealed class DeviceSignal
    {
        /// <summary>Unique name / category of the signal (e.g. MachineUuid, BiosSerial, NetworkMac).</summary>
        public string Name { get; }

        /// <summary>Whether the signal was successfully collected and validated.</summary>
        public bool IsAvailable { get; }

        /// <summary>System source provider (e.g. WMI, Registry, /etc/machine-id, IORegistry).</summary>
        public string Source { get; }

        /// <summary>Stability classification of the signal.</summary>
        public SignalStability Stability { get; }

        /// <summary>Reliability classification of the signal.</summary>
        public SignalReliability Reliability { get; }

        /// <summary>Collection timestamp in UTC.</summary>
        public DateTimeOffset CollectedAtUtc { get; }

        /// <summary>Diagnostic status.</summary>
        public SignalStatus Status { get; }

        /// <summary>Optional diagnostic error or explanation.</summary>
        public string? DiagnosticStatus { get; }

        /// <summary>SHA-256 hash of the normalized signal value. Safe for logging and telemetry.</summary>
        public string? ValueHash { get; }

        /// <summary>Internal normalized value. Never included in public logs or device codes.</summary>
        internal string? NormalizedValue { get; }

        public DeviceSignal(
            string name,
            bool isAvailable,
            string source,
            SignalStability stability,
            SignalReliability reliability,
            SignalStatus status,
            string? normalizedValue = null,
            string? valueHash = null,
            string? diagnosticStatus = null,
            DateTimeOffset? collectedAtUtc = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            IsAvailable = isAvailable;
            Source = source ?? string.Empty;
            Stability = stability;
            Reliability = reliability;
            Status = status;
            NormalizedValue = normalizedValue;
            ValueHash = valueHash;
            DiagnosticStatus = diagnosticStatus;
            CollectedAtUtc = collectedAtUtc ?? DateTimeOffset.UtcNow;
        }

        public static DeviceSignal Available(
            string name,
            string source,
            string normalizedValue,
            string valueHash,
            SignalStability stability = SignalStability.High,
            SignalReliability reliability = SignalReliability.High)
        {
            return new DeviceSignal(name, true, source, stability, reliability, SignalStatus.Available, normalizedValue, valueHash);
        }

        public static DeviceSignal Unavailable(string name, string source, SignalStatus status, string? reason = null)
        {
            return new DeviceSignal(name, false, source, SignalStability.Low, SignalReliability.Low, status, diagnosticStatus: reason);
        }

        public static DeviceSignal Rejected(string name, string source, string reason)
        {
            return new DeviceSignal(name, false, source, SignalStability.Low, SignalReliability.Low, SignalStatus.RejectedPlaceholder, diagnosticStatus: reason);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"[{Name}] Status={Status}, Source={Source}, Hash={(ValueHash != null ? ValueHash.Substring(0, Math.Min(12, ValueHash.Length)) + "..." : "none")}";
        }
    }
}
