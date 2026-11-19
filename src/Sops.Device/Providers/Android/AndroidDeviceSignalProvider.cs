using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.Device.Abstractions;
using Sops.Device.Fingerprinting;
using Sops.Device.Models;

namespace Sops.Device.Providers.Android
{
    /// <summary>
    /// Android platform signal provider respecting privacy restrictions and scoped application sandbox boundaries.
    /// Uses app-scoped identifiers and allowed device metadata. Supports host application signal injection via
    /// <c>Func&lt;string, string?&gt;</c> resolver without requiring native mobile SDK compilation on desktop.
    /// </summary>
    public sealed class AndroidDeviceSignalProvider : IDeviceSignalProvider
    {
        private readonly ILogger<AndroidDeviceSignalProvider>? _logger;
        private readonly Func<string, string?>? _nativeResolver;
        private readonly string? _salt;
        private readonly string? _customAndroidId;

        public AndroidDeviceSignalProvider(
            Func<string, string?>? nativeResolver = null,
            string? salt = null,
            string? customAndroidId = null,
            ILogger<AndroidDeviceSignalProvider>? logger = null)
        {
            _nativeResolver = nativeResolver;
            _salt = salt;
            _customAndroidId = customAndroidId;
            _logger = logger;
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<DeviceSignal>> GetSignalsAsync(CancellationToken cancellationToken = default)
        {
            var signals = new List<DeviceSignal>();

            // 1. App-scoped Android ID (Settings.Secure.ANDROID_ID or resolver)
            CollectAndroidScopedId(signals);

            // 2. Build metadata
            CollectBuildMetadata(signals);

            return Task.FromResult<IReadOnlyList<DeviceSignal>>(signals);
        }

        private void CollectAndroidScopedId(List<DeviceSignal> signals)
        {
            try
            {
                string? id = _customAndroidId;

                if (string.IsNullOrEmpty(id) && _nativeResolver != null)
                {
                    id = _nativeResolver("AndroidId");
                }

                if (string.IsNullOrEmpty(id))
                {
                    id = TryReadAndroidIdViaReflection();
                }

                if (!string.IsNullOrEmpty(id) && DeviceSignalNormalizer.IsValidSignal("AndroidScopedId", id, out string? reason))
                {
                    string norm = DeviceSignalNormalizer.Normalize("AndroidScopedId", id!);
                    string hash = DeviceSignalNormalizer.ComputeSha256(norm, _salt);
                    signals.Add(DeviceSignal.Available("AndroidScopedId", "Android:Settings.Secure", norm, hash, SignalStability.High, SignalReliability.High));
                }
                else
                {
                    signals.Add(DeviceSignal.Unavailable("AndroidScopedId", "Android:Settings.Secure", SignalStatus.Unavailable, "Android ID not accessible without active native context or resolver hook."));
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Failed gathering Android ID");
                signals.Add(DeviceSignal.Unavailable("AndroidScopedId", "Android:Settings.Secure", SignalStatus.Failed, ex.Message));
            }
        }

        private void CollectBuildMetadata(List<DeviceSignal> signals)
        {
            try
            {
                string? manufacturer = _nativeResolver?.Invoke("Manufacturer") ?? Environment.GetEnvironmentVariable("ANDROID_MANUFACTURER");
                string? model = _nativeResolver?.Invoke("Model") ?? Environment.GetEnvironmentVariable("ANDROID_MODEL");

                if (!string.IsNullOrEmpty(manufacturer) && !string.IsNullOrEmpty(model))
                {
                    string descriptor = $"{manufacturer}_{model}";
                    if (DeviceSignalNormalizer.IsValidSignal("AndroidDeviceModel", descriptor, out _))
                    {
                        string norm = DeviceSignalNormalizer.Normalize("AndroidDeviceModel", descriptor);
                        string hash = DeviceSignalNormalizer.ComputeSha256(norm, _salt);
                        signals.Add(DeviceSignal.Available("AndroidDeviceModel", "Android:Build", norm, hash, SignalStability.High, SignalReliability.Medium));
                    }
                }
            }
            catch { }
        }

        private static string? TryReadAndroidIdViaReflection()
        {
            try
            {
                var settingsSecureType = Type.GetType("Android.Provider.Settings+Secure, Mono.Android");
                if (settingsSecureType != null)
                {
                    // Available when running under Mono.Android / Xamarin / MAUI
                    var getStringMethod = settingsSecureType.GetMethod("GetString", new[] { Type.GetType("Android.Content.ContentResolver, Mono.Android")!, typeof(string) });
                }
            }
            catch { }
            return null;
        }
    }
}
