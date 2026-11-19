using System;
using System.Collections.Generic;
using Sops.Device.Models;

namespace Sops.Device.Management
{
    /// <summary>
    /// Supplementary hardware, operating system, and application metadata reported during device registration and diagnostics.
    /// Does not contain raw sensitive hardware serial numbers.
    /// </summary>
    public sealed class DeviceMetadata
    {
        /// <summary>Local network hostname.</summary>
        public string? Hostname { get; }

        /// <summary>Operating system platform category.</summary>
        public DevicePlatform OsPlatform { get; }

        /// <summary>Operating system version or distribution description.</summary>
        public string? OsVersion { get; }

        /// <summary>Version of the host application running on the device.</summary>
        public string? AppVersion { get; }

        /// <summary>Processor architecture (e.g. x64, arm64, x86).</summary>
        public string? Architecture { get; }

        /// <summary>Device manufacturer and model descriptor (e.g. "Dell OptiPlex 7090").</summary>
        public string? DeviceModel { get; }

        /// <summary>Total physical RAM in megabytes, if available.</summary>
        public long? TotalMemoryMb { get; }

        /// <summary>Custom extensible key-value attributes.</summary>
        public IReadOnlyDictionary<string, string> CustomAttributes { get; }

        public DeviceMetadata(
            string? hostname = null,
            DevicePlatform osPlatform = DevicePlatform.Windows,
            string? osVersion = null,
            string? appVersion = null,
            string? architecture = null,
            string? deviceModel = null,
            long? totalMemoryMb = null,
            IReadOnlyDictionary<string, string>? customAttributes = null)
        {
            Hostname = hostname?.Trim();
            OsPlatform = osPlatform;
            OsVersion = osVersion?.Trim();
            AppVersion = appVersion?.Trim();
            Architecture = architecture?.Trim();
            DeviceModel = deviceModel?.Trim();
            TotalMemoryMb = totalMemoryMb;
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (customAttributes != null)
            {
                foreach (var kvp in customAttributes)
                {
                    dict[kvp.Key] = kvp.Value;
                }
            }
            CustomAttributes = dict;
        }
    }
}
