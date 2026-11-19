using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Sops.Device.Abstractions;
using Sops.Licensing.Abstractions;
using Sops.Licensing.Models;
using Sops.Licensing.Platform;

namespace Sops.Licensing.Device
{
    /// <summary>
    /// Computes privacy-preserving installation and hardware signals for node-locked license binding.
    /// Integrates with <see cref="IDeviceService"/> from Sops.Device as the shared cross-platform identity SDK,
    /// while providing full backward compatibility for existing license bindings and legacy hardware signatures.
    /// Supports fuzzy hardware matching tolerance so legitimate single-component upgrades do not break activated licenses.
    /// </summary>
    public class DeviceIdentifier : IDeviceIdentifier
    {
        private readonly IDeviceService? _deviceService;
        private readonly ISecureStorage? _storage;
        private readonly string _installationIdKey = "installation.id";
        private readonly object _syncLock = new object();
        private string? _cachedInstallationId;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceIdentifier"/> class.
        /// </summary>
        /// <param name="storage">Optional secure storage instance to persist installation ID.</param>
        public DeviceIdentifier(ISecureStorage? storage = null)
            : this(null, storage)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceIdentifier"/> class with shared device service integration.
        /// </summary>
        /// <param name="deviceService">Optional shared device service from Sops.Device.</param>
        /// <param name="storage">Optional secure storage instance to persist installation ID.</param>
        public DeviceIdentifier(IDeviceService? deviceService, ISecureStorage? storage = null)
        {
            _deviceService = deviceService;
            _storage = storage;
        }

        /// <inheritdoc />
        public DeviceFingerprint GetCurrentFingerprint()
        {
            if (_deviceService != null)
            {
                try
                {
                    var identity = _deviceService.GetIdentityAsync().GetAwaiter().GetResult();
                    var fp = _deviceService.GetFingerprintAsync().GetAwaiter().GetResult();

                    var sigDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kvp in fp.SignalHashes)
                    {
                        sigDict[kvp.Key] = kvp.Value;
                    }

                    return new DeviceFingerprint
                    {
                        InstallationId = identity.InstallationId,
                        CombinedHash = fp.CombinedHash,
                        Signals = sigDict,
                        GeneratedAtUtc = fp.GeneratedAtUtc
                    };
                }
                catch
                {
                    // Fall back to legacy local signal calculation if device service encounters an error
                }
            }

            string installId = GetOrCreateInstallationId();
            var signals = CollectHardwareSignals();

            // Compute deterministic combined hash
            string combinedHash = ComputeCombinedHash(installId, signals);

            return new DeviceFingerprint
            {
                InstallationId = installId,
                CombinedHash = combinedHash,
                Signals = signals,
                GeneratedAtUtc = DateTimeOffset.UtcNow
            };
        }

        /// <inheritdoc />
        public bool Matches(string storedBindingHash, double tolerance = 0.7)
        {
            if (string.IsNullOrWhiteSpace(storedBindingHash)) return false;

            string expected = storedBindingHash.Trim();

            // 1. Evaluate against active device identity (via Sops.Device or legacy)
            var current = GetCurrentFingerprint();

            // Exact match against combined hash
            if (string.Equals(current.CombinedHash, expected, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Exact match against installation ID
            if (string.Equals(current.InstallationId, expected, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Multi-signal compound format: "installId:signal1;signal2..." or fuzzy score
            if (expected.Contains(":"))
            {
                string[] parts = expected.Split(new[] { ':' }, 2);
                string expectedInstallId = parts[0];
                string expectedSignalsString = parts.Length > 1 ? parts[1] : string.Empty;

                if (string.Equals(current.InstallationId, expectedInstallId, StringComparison.OrdinalIgnoreCase))
                {
                    string[] expectedSignalHashes = expectedSignalsString.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (expectedSignalHashes.Length > 0)
                    {
                        int matchCount = 0;
                        var currentHashes = new HashSet<string>(current.Signals.Values, StringComparer.OrdinalIgnoreCase);

                        foreach (string expHash in expectedSignalHashes)
                        {
                            if (currentHashes.Contains(expHash.Trim()))
                            {
                                matchCount++;
                            }
                        }

                        double ratio = (double)matchCount / expectedSignalHashes.Length;
                        if (ratio >= tolerance) return true;
                    }
                }
            }

            // 2. Backward compatibility fallback: if Sops.Device was active, check legacy algorithm in case license was issued under legacy format
            if (_deviceService != null)
            {
                try
                {
                    string legacyInstallId = GetOrCreateInstallationId();
                    var legacySignals = CollectHardwareSignals();
                    string legacyCombined = ComputeCombinedHash(legacyInstallId, legacySignals);

                    if (string.Equals(legacyCombined, expected, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(legacyInstallId, expected, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch { }
            }

            return false;
        }

        private string GetOrCreateInstallationId()
        {
            lock (_syncLock)
            {
                if (!string.IsNullOrEmpty(_cachedInstallationId))
                {
                    return _cachedInstallationId!;
                }

                if (_storage != null)
                {
                    try
                    {
                        string? storedId = _storage.GetStringAsync(_installationIdKey).GetAwaiter().GetResult();
                        if (!string.IsNullOrWhiteSpace(storedId) && Guid.TryParse(storedId, out _))
                        {
                            _cachedInstallationId = storedId!;
                            return _cachedInstallationId;
                        }
                    }
                    catch
                    {
                        // Fall through to file fallback
                    }
                }

                // File fallback in LocalAppData
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string idFile = Path.Combine(appData, "SOPs", ".installation-id");

                if (File.Exists(idFile))
                {
                    try
                    {
                        string fileId = File.ReadAllText(idFile).Trim();
                        if (Guid.TryParse(fileId, out _))
                        {
                            _cachedInstallationId = fileId;
                            return _cachedInstallationId;
                        }
                    }
                    catch { }
                }

                // Generate new persistent installation GUID
                string newId = Guid.NewGuid().ToString("D");
                try
                {
                    string dir = Path.GetDirectoryName(idFile)!;
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllText(idFile, newId);
                }
                catch { }

                if (_storage != null)
                {
                    try
                    {
                        _storage.SaveStringAsync(_installationIdKey, newId).GetAwaiter().GetResult();
                    }
                    catch { }
                }

                _cachedInstallationId = newId;
                return _cachedInstallationId;
            }
        }

        private Dictionary<string, string> CollectHardwareSignals()
        {
            var signals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // 1. OS and Architecture
            signals["os_platform"] = HashValue(PlatformDetector.CurrentOS.ToString());
            signals["os_architecture"] = HashValue(IntPtr.Size == 8 ? "x64" : "x86");
            signals["processor_count"] = HashValue(Environment.ProcessorCount.ToString());

            // 2. Machine specific ID
            string? machineId = ReadMachineSpecificId();
            if (!string.IsNullOrEmpty(machineId))
            {
                signals["machine_guid"] = HashValue(machineId!);
            }

            // 3. User domain / Computer name
            try
            {
                signals["machine_name"] = HashValue(Environment.MachineName);
            }
            catch { }

            // 4. System root / OS drive
            try
            {
                string sysDir = Environment.SystemDirectory;
                if (!string.IsNullOrEmpty(sysDir))
                {
                    signals["system_dir"] = HashValue(sysDir);
                }
            }
            catch { }

            return signals;
        }

        private string? ReadMachineSpecificId()
        {
            if (PlatformDetector.IsWindows)
            {
                try
                {
                    const int HKEY_LOCAL_MACHINE = unchecked((int)0x80000002);
                    const int KEY_READ = 0x20019;

                    if (WindowsRegistryNative.RegOpenKeyEx(new IntPtr(HKEY_LOCAL_MACHINE), @"SOFTWARE\Microsoft\Cryptography", 0, KEY_READ, out IntPtr hKey) == 0)
                    {
                        try
                        {
                            int size = 128;
                            var sb = new StringBuilder(size);
                            int type = 0;
                            if (WindowsRegistryNative.RegQueryValueEx(hKey, "MachineGuid", 0, ref type, sb, ref size) == 0)
                            {
                                return sb.ToString();
                            }
                        }
                        finally
                        {
                            WindowsRegistryNative.RegCloseKey(hKey);
                        }
                    }
                }
                catch { }
            }
            else if (PlatformDetector.IsLinux)
            {
                if (File.Exists("/etc/machine-id"))
                {
                    try { return File.ReadAllText("/etc/machine-id").Trim(); } catch { }
                }
                if (File.Exists("/var/lib/dbus/machine-id"))
                {
                    try { return File.ReadAllText("/var/lib/dbus/machine-id").Trim(); } catch { }
                }
            }
            else if (PlatformDetector.IsMacOS)
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("ioreg", "-rd1 -c IOPlatformExpertDevice")
                    {
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = System.Diagnostics.Process.Start(psi);
                    if (proc != null)
                    {
                        string output = proc.StandardOutput.ReadToEnd();
                        proc.WaitForExit();
                        // Look for IOPlatformUUID
                        foreach (string line in output.Split('\n'))
                        {
                            if (line.Contains("IOPlatformUUID"))
                            {
                                int idx = line.IndexOf('=');
                                if (idx > 0)
                                {
                                    return line.Substring(idx + 1).Replace("\"", string.Empty).Trim();
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            return null;
        }

        private static string ComputeCombinedHash(string installationId, Dictionary<string, string> signals)
        {
            var sb = new StringBuilder();
            sb.Append("INSTALL_ID:").Append(installationId).Append("\n");

            var sortedKeys = signals.Keys.OrderBy(k => k, StringComparer.Ordinal);
            foreach (var k in sortedKeys)
            {
                sb.Append(k).Append(':').Append(signals[k]).Append('\n');
            }

            return HashValue(sb.ToString());
        }

        private static string HashValue(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }

        private static class WindowsRegistryNative
        {
            [System.Runtime.InteropServices.DllImport("Advapi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            public static extern int RegOpenKeyEx(IntPtr hKey, string lpSubKey, int ulOptions, int samDesired, out IntPtr phkResult);

            [System.Runtime.InteropServices.DllImport("Advapi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            public static extern int RegQueryValueEx(IntPtr hKey, string lpValueName, int lpReserved, ref int lpType, StringBuilder lpData, ref int lpcbData);

            [System.Runtime.InteropServices.DllImport("Advapi32.dll")]
            public static extern int RegCloseKey(IntPtr hKey);
        }
    }
}
