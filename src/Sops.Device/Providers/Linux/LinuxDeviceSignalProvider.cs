using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.Device.Abstractions;
using Sops.Device.Fingerprinting;
using Sops.Device.Models;

namespace Sops.Device.Providers.Linux
{
    /// <summary>
    /// Linux signal provider reading system identifiers without requiring root privileges.
    /// </summary>
    public sealed class LinuxDeviceSignalProvider : IDeviceSignalProvider
    {
        private readonly ILogger<LinuxDeviceSignalProvider>? _logger;
        private readonly bool _includeMacAddresses;
        private readonly string? _salt;
        private readonly int _maxNetworkAdapters;

        public LinuxDeviceSignalProvider(
            bool includeMacAddresses = false,
            string? salt = null,
            int maxNetworkAdapters = 2,
            ILogger<LinuxDeviceSignalProvider>? logger = null)
        {
            _includeMacAddresses = includeMacAddresses;
            _salt = salt;
            _maxNetworkAdapters = Math.Max(1, maxNetworkAdapters);
            _logger = logger;
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<DeviceSignal>> GetSignalsAsync(CancellationToken cancellationToken = default)
        {
            var signals = new List<DeviceSignal>();

            // 1. OS machine ID (/etc/machine-id or /var/lib/dbus/machine-id)
            CollectMachineId(signals);

            // 2. DMI Product UUID (if readable by standard user)
            CollectDmiUuid(signals);

            // 3. Network MAC addresses
            if (_includeMacAddresses)
            {
                CollectNetworkMacSignals(signals);
            }

            return Task.FromResult<IReadOnlyList<DeviceSignal>>(signals);
        }

        private void CollectMachineId(List<DeviceSignal> signals)
        {
            string[] candidatePaths = { "/etc/machine-id", "/var/lib/dbus/machine-id" };
            foreach (var path in candidatePaths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        string id = File.ReadAllText(path).Trim();
                        if (DeviceSignalNormalizer.IsValidSignal("OsMachineId", id, out string? reason))
                        {
                            string norm = DeviceSignalNormalizer.Normalize("OsMachineId", id);
                            string hash = DeviceSignalNormalizer.ComputeSha256(norm, _salt);
                            signals.Add(DeviceSignal.Available("OsMachineId", "File:" + path, norm, hash, SignalStability.High, SignalReliability.High));
                            return;
                        }
                        else
                        {
                            signals.Add(DeviceSignal.Rejected("OsMachineId", "File:" + path, reason ?? "Rejected machine-id."));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug(ex, "Failed reading {Path}", path);
                        signals.Add(DeviceSignal.Unavailable("OsMachineId", "File:" + path, SignalStatus.AccessDenied, ex.Message));
                    }
                }
            }

            signals.Add(DeviceSignal.Unavailable("OsMachineId", "Linux:machine-id", SignalStatus.Unavailable, "Neither /etc/machine-id nor dbus machine-id was found."));
        }

        private void CollectDmiUuid(List<DeviceSignal> signals)
        {
            string dmiPath = "/sys/class/dmi/id/product_uuid";
            if (File.Exists(dmiPath))
            {
                try
                {
                    string uuid = File.ReadAllText(dmiPath).Trim();
                    if (DeviceSignalNormalizer.IsValidSignal("ProductUuid", uuid, out string? reason))
                    {
                        string norm = DeviceSignalNormalizer.Normalize("ProductUuid", uuid);
                        string hash = DeviceSignalNormalizer.ComputeSha256(norm, _salt);
                        signals.Add(DeviceSignal.Available("ProductUuid", "File:" + dmiPath, norm, hash, SignalStability.High, SignalReliability.High));
                        return;
                    }
                    else
                    {
                        signals.Add(DeviceSignal.Rejected("ProductUuid", "File:" + dmiPath, reason ?? "Rejected product_uuid."));
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Access denied reading {Path}", dmiPath);
                    signals.Add(DeviceSignal.Unavailable("ProductUuid", "File:" + dmiPath, SignalStatus.AccessDenied, ex.Message));
                }
            }
        }

        private void CollectNetworkMacSignals(List<DeviceSignal> signals)
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic =>
                        nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                        !nic.Name.StartsWith("veth", StringComparison.OrdinalIgnoreCase) &&
                        !nic.Name.StartsWith("docker", StringComparison.OrdinalIgnoreCase) &&
                        !nic.Name.StartsWith("br-", StringComparison.OrdinalIgnoreCase) &&
                        !nic.Name.StartsWith("virbr", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(nic => nic.Id, StringComparer.Ordinal)
                    .ToList();

                int nicIndex = 0;
                foreach (var nic in interfaces)
                {
                    string mac = nic.GetPhysicalAddress().ToString();
                    if (string.IsNullOrWhiteSpace(mac) || mac.Length != 12) continue;

                    string signalName = nicIndex == 0 ? "NetworkMac" : $"NetworkMac_{nicIndex}";
                    if (DeviceSignalNormalizer.IsValidSignal(signalName, mac, out string? reason))
                    {
                        string norm = DeviceSignalNormalizer.Normalize(signalName, mac);
                        string hash = DeviceSignalNormalizer.ComputeSha256(norm, _salt);
                        signals.Add(DeviceSignal.Available(signalName, "NetworkInterface:" + nic.Name, norm, hash, SignalStability.Medium, SignalReliability.Medium));
                        nicIndex++;
                        if (nicIndex >= _maxNetworkAdapters) break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Failed gathering network interface MACs");
            }
        }
    }
}
