using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.Device.Abstractions;
using Sops.Device.Fingerprinting;
using Sops.Device.Models;

namespace Sops.Device.Providers.Windows
{
    /// <summary>
    /// Windows platform signal provider utilizing non-privileged registry queries, WMI fallbacks,
    /// and standard .NET APIs. Designed for full compatibility from Windows 7 SP1 to Windows 11.
    /// Does not require administrative privileges.
    /// </summary>
    public sealed class WindowsDeviceSignalProvider : IDeviceSignalProvider
    {
        private readonly ILogger<WindowsDeviceSignalProvider>? _logger;
        private readonly bool _includeMacAddresses;
        private readonly string? _salt;
        private readonly int _maxNetworkAdapters;

        public WindowsDeviceSignalProvider(
            bool includeMacAddresses = false,
            string? salt = null,
            int maxNetworkAdapters = 2,
            ILogger<WindowsDeviceSignalProvider>? logger = null)
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

            // 1. Cryptography MachineGuid from Registry (World-readable)
            CollectMachineGuid(signals);

            // 2. Hardware SMBIOS / BIOS / Baseboard signals via Registry / WMI
            CollectBiosAndBoardSignals(signals);

            // 3. System Drive Volume Serial
            CollectSystemDriveSignal(signals);

            // 4. Physical Network MAC Addresses (optional)
            if (_includeMacAddresses)
            {
                CollectNetworkMacSignals(signals);
            }

            return Task.FromResult<IReadOnlyList<DeviceSignal>>(signals);
        }

        private void CollectMachineGuid(List<DeviceSignal> signals)
        {
            const int HKEY_LOCAL_MACHINE = unchecked((int)0x80000002);
            const int KEY_READ = 0x20019;

            IntPtr hKey = IntPtr.Zero;
            try
            {
                int result = Advapi32.RegOpenKeyEx(new IntPtr(HKEY_LOCAL_MACHINE), @"SOFTWARE\Microsoft\Cryptography", 0, KEY_READ, out hKey);
                if (result == 0 && hKey != IntPtr.Zero)
                {
                    int type = 0;
                    int size = 128;
                    var sb = new StringBuilder(size);

                    result = Advapi32.RegQueryValueEx(hKey, "MachineGuid", 0, ref type, sb, ref size);
                    if (result == 0)
                    {
                        string guid = sb.ToString().Trim();
                        EvaluateAndAddSignal(signals, "MachineGuid", guid, "Registry:HKLM\\SOFTWARE\\Microsoft\\Cryptography\\MachineGuid", SignalStability.High, SignalReliability.High);
                        return;
                    }
                }

                signals.Add(DeviceSignal.Unavailable("MachineGuid", "Registry:HKLM\\SOFTWARE\\Microsoft\\Cryptography", SignalStatus.AccessDenied, "Unable to read MachineGuid via RegOpenKeyEx (Code " + result + ")"));
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Failed reading Windows MachineGuid");
                signals.Add(DeviceSignal.Unavailable("MachineGuid", "Registry:HKLM\\SOFTWARE\\Microsoft\\Cryptography", SignalStatus.Failed, ex.Message));
            }
            finally
            {
                if (hKey != IntPtr.Zero)
                {
                    Advapi32.RegCloseKey(hKey);
                }
            }
        }

        private void CollectBiosAndBoardSignals(List<DeviceSignal> signals)
        {
            const int HKEY_LOCAL_MACHINE = unchecked((int)0x80000002);
            const int KEY_READ = 0x20019;

            // Try non-admin registry path for BIOS / Hardware description
            IntPtr hKey = IntPtr.Zero;
            try
            {
                int res = Advapi32.RegOpenKeyEx(new IntPtr(HKEY_LOCAL_MACHINE), @"HARDWARE\DESCRIPTION\System\BIOS", 0, KEY_READ, out hKey);
                if (res == 0 && hKey != IntPtr.Zero)
                {
                    ReadRegistryString(hKey, "BaseBoardProduct", "BaseBoardProduct", signals, SignalStability.High, SignalReliability.High);
                    ReadRegistryString(hKey, "BaseBoardManufacturer", "BaseBoardManufacturer", signals, SignalStability.High, SignalReliability.Medium);
                    ReadRegistryString(hKey, "BIOSReleaseDate", "BiosReleaseDate", signals, SignalStability.Medium, SignalReliability.Medium);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Failed reading BIOS registry entries");
            }
            finally
            {
                if (hKey != IntPtr.Zero) Advapi32.RegCloseKey(hKey);
            }

            // Optional WMI query for SMBIOS UUID if not in registry
            if (!signals.Any(s => s.Name == "BaseBoardProduct" && s.IsAvailable))
            {
                TryQueryWmiSmbios(signals);
            }
        }

        private void ReadRegistryString(IntPtr hKey, string valueName, string signalName, List<DeviceSignal> signals, SignalStability stability, SignalReliability reliability)
        {
            int type = 0;
            int size = 256;
            var sb = new StringBuilder(size);

            if (Advapi32.RegQueryValueEx(hKey, valueName, 0, ref type, sb, ref size) == 0)
            {
                string val = sb.ToString();
                EvaluateAndAddSignal(signals, signalName, val, @"Registry:HARDWARE\DESCRIPTION\System\BIOS\" + valueName, stability, reliability);
            }
        }

        private void TryQueryWmiSmbios(List<DeviceSignal> signals)
        {
            try
            {
                var wmiType = Type.GetType("System.Management.ManagementObjectSearcher, System.Management");
                if (wmiType != null)
                {
                    using (var searcher = (IDisposable)Activator.CreateInstance(wmiType, "SELECT UUID FROM Win32_ComputerSystemProduct")!)
                    {
                        var getMethod = wmiType.GetMethod("Get", Type.EmptyTypes);
                        if (getMethod != null)
                        {
                            var collection = getMethod.Invoke(searcher, null) as System.Collections.IEnumerable;
                            if (collection != null)
                            {
                                foreach (var obj in collection)
                                {
                                    if (obj is IDisposable dispObj)
                                    {
                                        using (dispObj)
                                        {
                                            var itemProp = obj.GetType().GetProperty("Item", new[] { typeof(string) });
                                            var uuidVal = itemProp?.GetValue(obj, new object[] { "UUID" })?.ToString();
                                            if (!string.IsNullOrWhiteSpace(uuidVal))
                                            {
                                                EvaluateAndAddSignal(signals, "SmbiosUuid", uuidVal!, "WMI:Win32_ComputerSystemProduct", SignalStability.High, SignalReliability.High);
                                                return;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // WMI service may be disabled, filtered, or in a sandboxed process.
            }
        }

        private void CollectSystemDriveSignal(List<DeviceSignal> signals)
        {
            try
            {
                string sysDir = Environment.SystemDirectory;
                string? root = Path.GetPathRoot(sysDir);
                if (!string.IsNullOrEmpty(root))
                {
                    var drive = new DriveInfo(root!);
                    if (drive.IsReady)
                    {
                        string driveDescriptor = $"{drive.DriveType}_{drive.DriveFormat}_{drive.TotalSize / (1024 * 1024 * 1024)}GB";
                        EvaluateAndAddSignal(signals, "SystemDrive", driveDescriptor, "DriveInfo:SystemRoot", SignalStability.Medium, SignalReliability.Medium);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Failed gathering system drive signal");
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
                        nic.Description.IndexOf("virtual", StringComparison.OrdinalIgnoreCase) < 0 &&
                        nic.Description.IndexOf("pseudo", StringComparison.OrdinalIgnoreCase) < 0 &&
                        nic.Description.IndexOf("hyper-v", StringComparison.OrdinalIgnoreCase) < 0 &&
                        nic.Description.IndexOf("vmware", StringComparison.OrdinalIgnoreCase) < 0 &&
                        nic.Description.IndexOf("vpn", StringComparison.OrdinalIgnoreCase) < 0 &&
                        nic.Description.IndexOf("wsl", StringComparison.OrdinalIgnoreCase) < 0 &&
                        nic.Description.IndexOf("bluetooth", StringComparison.OrdinalIgnoreCase) < 0 &&
                        nic.Description.IndexOf("tap", StringComparison.OrdinalIgnoreCase) < 0 &&
                        nic.Description.IndexOf("npcap", StringComparison.OrdinalIgnoreCase) < 0)
                    .OrderBy(nic => nic.Id, StringComparer.Ordinal)
                    .ToList();

                int nicIndex = 0;
                foreach (var nic in interfaces)
                {
                    string mac = nic.GetPhysicalAddress().ToString();
                    if (string.IsNullOrWhiteSpace(mac) || mac.Length != 12) continue;

                    string signalName = nicIndex == 0 ? "NetworkMac" : $"NetworkMac_{nicIndex}";
                    EvaluateAndAddSignal(signals, signalName, mac, "NetworkInterface:" + nic.Name, SignalStability.Medium, SignalReliability.Medium);
                    nicIndex++;
                    if (nicIndex >= _maxNetworkAdapters) break;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Failed gathering network interface MACs");
            }
        }

        private void EvaluateAndAddSignal(
            List<DeviceSignal> signals,
            string name,
            string rawValue,
            string source,
            SignalStability stability,
            SignalReliability reliability)
        {
            if (DeviceSignalNormalizer.IsValidSignal(name, rawValue, out string? reason))
            {
                string normalized = DeviceSignalNormalizer.Normalize(name, rawValue);
                string hash = DeviceSignalNormalizer.ComputeSha256(normalized, _salt);
                signals.Add(DeviceSignal.Available(name, source, normalized, hash, stability, reliability));
            }
            else
            {
                signals.Add(DeviceSignal.Rejected(name, source, reason ?? "Rejected as placeholder or invalid signal."));
            }
        }

        private static class Advapi32
        {
            [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern int RegOpenKeyEx(IntPtr hKey, string lpSubKey, int ulOptions, int samDesired, out IntPtr phkResult);

            [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern int RegQueryValueEx(IntPtr hKey, string lpValueName, int lpReserved, ref int lpType, StringBuilder lpData, ref int lpcbData);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern int RegCloseKey(IntPtr hKey);
        }
    }
}
