using System;

namespace Sops.Pharmacy.Server.Domain.Devices
{
    public class TerminalDto
    {
        public string Id { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string DeviceType { get; set; } = "POS"; // POS, Mobile, Tablet, Manager
        public string HardwareFingerprint { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Approved, Revoked
        public DateTime LastSeenAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
