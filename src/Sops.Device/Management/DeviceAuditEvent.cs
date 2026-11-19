using System;

namespace Sops.Device.Management
{
    /// <summary>
    /// Immutable audit log entry recording a lifecycle event or administrative change on a device.
    /// Supports compliance tracking, security audits, and unauthorized modification detection.
    /// </summary>
    public sealed class DeviceAuditEvent
    {
        /// <summary>Unique event record ID.</summary>
        public string EventId { get; }

        /// <summary>Associated device registration identifier.</summary>
        public string RegistrationId { get; }

        /// <summary>Device support code.</summary>
        public string DeviceCode { get; }

        /// <summary>Lifecycle event category (e.g. Registered, HeartbeatReceived, Suspended, Reactivated, Revoked, Replaced).</summary>
        public string EventType { get; }

        /// <summary>Identity of the user, system process, or client initiating the event.</summary>
        public string InitiatedBy { get; }

        /// <summary>Structured description or reason for the event.</summary>
        public string Details { get; }

        /// <summary>Timestamp in UTC when the event occurred.</summary>
        public DateTimeOffset TimestampUtc { get; }

        /// <summary>Optional IP address of the client connection.</summary>
        public string? IpAddress { get; }

        public DeviceAuditEvent(
            string eventId,
            string registrationId,
            string deviceCode,
            string eventType,
            string initiatedBy,
            string details,
            DateTimeOffset? timestampUtc = null,
            string? ipAddress = null)
        {
            EventId = !string.IsNullOrWhiteSpace(eventId)
                ? eventId.Trim()
                : Guid.NewGuid().ToString("D");
            RegistrationId = registrationId ?? string.Empty;
            DeviceCode = deviceCode ?? string.Empty;
            EventType = eventType ?? "Unknown";
            InitiatedBy = initiatedBy ?? "System";
            Details = details ?? string.Empty;
            TimestampUtc = timestampUtc ?? DateTimeOffset.UtcNow;
            IpAddress = ipAddress?.Trim();
        }

        public static DeviceAuditEvent Create(
            string registrationId,
            string deviceCode,
            string eventType,
            string initiatedBy,
            string details,
            string? ipAddress = null)
        {
            return new DeviceAuditEvent(
                Guid.NewGuid().ToString("D"),
                registrationId,
                deviceCode,
                eventType,
                initiatedBy,
                details,
                DateTimeOffset.UtcNow,
                ipAddress);
        }
    }
}
