using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.Device.Abstractions;
using Sops.Device.Fingerprinting;
using Sops.Device.Models;

namespace Sops.Device.Providers.IOS
{
    /// <summary>
    /// Apple iOS and iPadOS signal provider utilizing IdentifierForVendor (IDFV) and permitted device descriptors.
    /// Complies strictly with Apple privacy and App Store guidelines. Supports host application signal injection via
    /// <c>Func&lt;string, string?&gt;</c> resolver without requiring native mobile SDK compilation on desktop.
    /// </summary>
    public sealed class IosDeviceSignalProvider : IDeviceSignalProvider
    {
        private readonly ILogger<IosDeviceSignalProvider>? _logger;
        private readonly Func<string, string?>? _nativeResolver;
        private readonly string? _salt;
        private readonly string? _customVendorId;

        public IosDeviceSignalProvider(
            Func<string, string?>? nativeResolver = null,
            string? salt = null,
            string? customVendorId = null,
            ILogger<IosDeviceSignalProvider>? logger = null)
        {
            _nativeResolver = nativeResolver;
            _salt = salt;
            _customVendorId = customVendorId;
            _logger = logger;
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<DeviceSignal>> GetSignalsAsync(CancellationToken cancellationToken = default)
        {
            var signals = new List<DeviceSignal>();

            // 1. IdentifierForVendor (IDFV)
            CollectVendorId(signals);

            return Task.FromResult<IReadOnlyList<DeviceSignal>>(signals);
        }

        private void CollectVendorId(List<DeviceSignal> signals)
        {
            try
            {
                string? idfv = _customVendorId;

                if (string.IsNullOrEmpty(idfv) && _nativeResolver != null)
                {
                    idfv = _nativeResolver("AppleVendorId");
                }

                if (!string.IsNullOrEmpty(idfv) && DeviceSignalNormalizer.IsValidSignal("AppleVendorId", idfv, out string? reason))
                {
                    string norm = DeviceSignalNormalizer.Normalize("AppleVendorId", idfv!);
                    string hash = DeviceSignalNormalizer.ComputeSha256(norm, _salt);
                    signals.Add(DeviceSignal.Available("AppleVendorId", "UIDevice:IdentifierForVendor", norm, hash, SignalStability.High, SignalReliability.High));
                }
                else
                {
                    signals.Add(DeviceSignal.Unavailable("AppleVendorId", "UIDevice:IdentifierForVendor", SignalStatus.Unavailable, "IDFV not accessible without UIKit runtime or resolver hook."));
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Failed gathering iOS IDFV");
                signals.Add(DeviceSignal.Unavailable("AppleVendorId", "UIDevice:IdentifierForVendor", SignalStatus.Failed, ex.Message));
            }
        }
    }
}
