using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Sops.Device.Abstractions;
using Sops.Device.Models;

namespace Sops.Device.Providers
{
    /// <summary>
    /// Default implementation of <see cref="IDevicePlatformDetector"/> leveraging .NET runtime information
    /// with graceful fallbacks for legacy runtimes and simulated test environments.
    /// </summary>
    public sealed class DefaultDevicePlatformDetector : IDevicePlatformDetector
    {
        private readonly DevicePlatform? _overridePlatform;

        public DefaultDevicePlatformDetector(DevicePlatform? overridePlatform = null)
        {
            _overridePlatform = overridePlatform;
        }

        /// <inheritdoc />
        public DevicePlatform Detect()
        {
            if (_overridePlatform.HasValue && _overridePlatform.Value != DevicePlatform.Unknown)
            {
                return _overridePlatform.Value;
            }

            try
            {
                // Check modern .NET OperatingSystem methods via reflection for cross-targeting compatibility
                var osType = typeof(Environment).Assembly.GetType("System.OperatingSystem");
                if (osType != null)
                {
                    if (InvokeStaticBool(osType, "IsAndroid")) return DevicePlatform.Android;
                    if (InvokeStaticBool(osType, "IsIOS")) return DevicePlatform.IOS;
                    if (InvokeStaticBool(osType, "IsMacCatalyst")) return DevicePlatform.MacCatalyst;
                }

                // Check standard RuntimeInformation
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return DevicePlatform.Windows;
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return DevicePlatform.MacOS;
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // Check Android indicators on Linux kernel
                    if (IsAndroidLinuxEnvironment())
                    {
                        return DevicePlatform.Android;
                    }
                    return DevicePlatform.Linux;
                }
            }
            catch
            {
                // Fallback for legacy runtime environments
            }

            // Environment.OSVersion fallback
            var platform = Environment.OSVersion.Platform;
            switch (platform)
            {
                case PlatformID.Win32NT:
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                case PlatformID.WinCE:
                    return DevicePlatform.Windows;

                case PlatformID.MacOSX:
                    return DevicePlatform.MacOS;

                case PlatformID.Unix:
                    return IsAndroidLinuxEnvironment() ? DevicePlatform.Android : DevicePlatform.Linux;

                default:
                    return DevicePlatform.Unknown;
            }
        }

        private static bool InvokeStaticBool(Type type, string methodName)
        {
            try
            {
                var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                if (method != null && method.ReturnType == typeof(bool))
                {
                    return (bool)method.Invoke(null, null)!;
                }
            }
            catch { }
            return false;
        }

        private static bool IsAndroidLinuxEnvironment()
        {
            try
            {
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANDROID_ROOT")) ||
                    !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANDROID_DATA")))
                {
                    return true;
                }

                if (File.Exists("/system/build.prop"))
                {
                    return true;
                }
            }
            catch { }
            return false;
        }
    }
}
