using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.Device.Abstractions;
using Sops.Device.Fingerprinting;
using Sops.Device.Models;

namespace Sops.Device.Providers.MacOS
{
    /// <summary>
    /// macOS / OS X signal provider retrieving IOPlatformUUID via ioreg.
    /// Does not require elevated privileges.
    /// </summary>
    public sealed class MacOSDeviceSignalProvider : IDeviceSignalProvider
    {
        private readonly ILogger<MacOSDeviceSignalProvider>? _logger;
        private readonly bool _includeMacAddresses;
        private readonly string? _salt;
        private readonly int _maxNetworkAdapters;

        public MacOSDeviceSignalProvider(
            bool includeMacAddresses = false,
            string? salt = null,
            int maxNetworkAdapters = 2,
            ILogger<MacOSDeviceSignalProvider>? logger = null)
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

            // 1. IOPlatformUUID from IORegistry
            CollectIOPlatformUuid(signals);

            // 2. Network MAC addresses
            if (_includeMacAddresses)
            {
                CollectNetworkMacSignals(signals);
            }

            return Task.FromResult<IReadOnlyList<DeviceSignal>>(signals);
        }

        private void CollectIOPlatformUuid(List<DeviceSignal> signals)
        {
            try
            {
                var psi = new ProcessStartInfo("ioreg", "-rd1 -c IOPlatformExpertDevice")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc != null)
                    {
                        string output = proc.StandardOutput.ReadToEnd();
                        proc.WaitForExit();

                        foreach (string line in output.Split('\n'))
                        {
                            if (line.Contains("IOPlatformUUID"))
                            {
                                int idx = line.IndexOf('=');
                                if (idx > 0)
                                {
                                    string rawUuid = line.Substring(idx + 1).Replace("\"", string.Empty).Trim();
                                    if (DeviceSignalNormalizer.IsValidSignal("IOPlatformUUID", rawUuid, out string? reason))
                                    {
                                        string norm = DeviceSignalNormalizer.Normalize("IOPlatformUUID", rawUuid);
                                        string hash = DeviceSignalNormalizer.ComputeSha256(norm, _salt);
                                        signals.Add(DeviceSignal.Available("IOPlatformUUID", "ioreg:IOPlatformExpertDevice", norm, hash, SignalStability.High, SignalReliability.High));
                                        return;
                                    }
                                    else
                                    {
                                        signals.Add(DeviceSignal.Rejected("IOPlatformUUID", "ioreg:IOPlatformExpertDevice", reason ?? "Rejected UUID."));
                                    }
                                }
                            }
                        }
                    }
                }

                signals.Add(DeviceSignal.Unavailable("IOPlatformUUID", "ioreg:IOPlatformExpertDevice", SignalStatus.Unavailable, "IOPlatformUUID property not found in ioreg output."));
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Failed executing ioreg to get IOPlatformUUID");
                signals.Add(DeviceSignal.Unavailable("IOPlatformUUID", "ioreg:IOPlatformExpertDevice", SignalStatus.Failed, ex.Message));
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
                        !nic.Name.StartsWith("bridge", StringComparison.OrdinalIgnoreCase) &&
                        !nic.Name.StartsWith("utun", StringComparison.OrdinalIgnoreCase) &&
                        !nic.Name.StartsWith("llw", StringComparison.OrdinalIgnoreCase) &&
                        !nic.Name.StartsWith("awdl", StringComparison.OrdinalIgnoreCase))
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
