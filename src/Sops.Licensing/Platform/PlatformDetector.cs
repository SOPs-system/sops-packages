using System;
using System.Runtime.InteropServices;

namespace Sops.Licensing.Platform
{
    /// <summary>
    /// Supported operating systems.
    /// </summary>
    public enum OperatingSystemType
    {
        /// <summary>Windows operating system.</summary>
        Windows,

        /// <summary>Linux operating system.</summary>
        Linux,

        /// <summary>macOS operating system.</summary>
        MacOS,

        /// <summary>Unknown or unsupported operating system.</summary>
        Unknown
    }

    /// <summary>
    /// Detects the host operating system and execution environment at runtime using netstandard2.0-safe APIs.
    /// Avoids Windows-only or Linux-only P/Invokes during detection.
    /// </summary>
    public static class PlatformDetector
    {
        private static readonly OperatingSystemType CurrentOsType;
        private static readonly bool Is64Bit;

        static PlatformDetector()
        {
            Is64Bit = IntPtr.Size == 8;

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    CurrentOsType = OperatingSystemType.Windows;
                    return;
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    CurrentOsType = OperatingSystemType.Linux;
                    return;
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    CurrentOsType = OperatingSystemType.MacOS;
                    return;
                }
            }
            catch
            {
                // Fallback for older environments
                var platform = Environment.OSVersion.Platform;
                switch (platform)
                {
                    case PlatformID.Win32NT:
                    case PlatformID.Win32S:
                    case PlatformID.Win32Windows:
                    case PlatformID.WinCE:
                        CurrentOsType = OperatingSystemType.Windows;
                        return;

                    case PlatformID.MacOSX:
                        CurrentOsType = OperatingSystemType.MacOS;
                        return;

                    case PlatformID.Unix:
                        // On older Mono/Framework, macOS may report as Unix
                        string uname = GetUname();
                        CurrentOsType = uname.IndexOf("Darwin", StringComparison.OrdinalIgnoreCase) >= 0
                            ? OperatingSystemType.MacOS
                            : OperatingSystemType.Linux;
                        return;

                    default:
                        CurrentOsType = OperatingSystemType.Unknown;
                        return;
                }
            }

            CurrentOsType = OperatingSystemType.Unknown;
        }

        /// <summary>Gets the current detected operating system.</summary>
        public static OperatingSystemType CurrentOS => CurrentOsType;

        /// <summary>Gets whether the current OS is Windows.</summary>
        public static bool IsWindows => CurrentOsType == OperatingSystemType.Windows;

        /// <summary>Gets whether the current OS is Linux.</summary>
        public static bool IsLinux => CurrentOsType == OperatingSystemType.Linux;

        /// <summary>Gets whether the current OS is macOS.</summary>
        public static bool IsMacOS => CurrentOsType == OperatingSystemType.MacOS;

        /// <summary>Gets whether the process is executing in a 64-bit address space.</summary>
        public static bool Is64BitProcess => Is64Bit;

        /// <summary>
        /// Checks whether the current Linux environment appears to be running in a headless container or server without desktop session bus.
        /// </summary>
        public static bool IsLinuxHeadless()
        {
            if (!IsLinux) return false;

            string? dbus = Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS");
            string? display = Environment.GetEnvironmentVariable("DISPLAY");
            string? wayland = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");

            return string.IsNullOrEmpty(dbus) && string.IsNullOrEmpty(display) && string.IsNullOrEmpty(wayland);
        }

        private static string GetUname()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("uname")
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
                    return output.Trim();
                }
            }
            catch
            {
                // Ignored
            }
            return string.Empty;
        }
    }
}
