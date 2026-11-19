using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.Device.Abstractions;
using Sops.Device.Configuration;
using Sops.Device.Fingerprinting;
using Sops.Device.Models;

namespace Sops.Device
{
    /// <summary>
    /// Default implementation of <see cref="IDeviceService"/> providing unified cross-platform device operations.
    /// </summary>
    public sealed class DeviceService : IDeviceService
    {
        private readonly IDeviceIdentityProvider _identityProvider;
        private readonly IDeviceSignalProvider _signalProvider;
        private readonly IDevicePlatformDetector _platformDetector;
        private readonly DeviceOptions _options;
        private readonly ILogger<DeviceService>? _logger;

        public DeviceService(
            IDeviceIdentityProvider identityProvider,
            IDeviceSignalProvider signalProvider,
            IDevicePlatformDetector platformDetector,
            DeviceOptions options,
            ILogger<DeviceService>? logger = null)
        {
            _identityProvider = identityProvider ?? throw new ArgumentNullException(nameof(identityProvider));
            _signalProvider = signalProvider ?? throw new ArgumentNullException(nameof(signalProvider));
            _platformDetector = platformDetector ?? throw new ArgumentNullException(nameof(platformDetector));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<DeviceInfo> GetDeviceInfoAsync(CancellationToken cancellationToken = default)
        {
            var identity = await _identityProvider.GetIdentityAsync(cancellationToken).ConfigureAwait(false);
            var signals = await _signalProvider.GetSignalsAsync(cancellationToken).ConfigureAwait(false);

            var availability = new Dictionary<string, SignalStatus>(StringComparer.OrdinalIgnoreCase);
            bool isVm = false;

            foreach (var sig in signals)
            {
                availability[sig.Name] = sig.Status;
                if (sig.Status == SignalStatus.RejectedPlaceholder &&
                    (sig.DiagnosticStatus?.IndexOf("virtual", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     sig.DiagnosticStatus?.IndexOf("qemu", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     sig.DiagnosticStatus?.IndexOf("hyper-v", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    isVm = true;
                }
            }

            string platformVersion;
            try
            {
                platformVersion = RuntimeInformation.OSDescription;
            }
            catch
            {
                platformVersion = Environment.OSVersion.VersionString;
            }

            string architecture = IntPtr.Size == 8 ? "x64" : "x86";
            try
            {
                architecture = RuntimeInformation.ProcessArchitecture.ToString();
            }
            catch { }

            string deviceType;
            if (isVm)
            {
                deviceType = "VirtualMachine";
            }
            else if (identity.Platform == DevicePlatform.Android || identity.Platform == DevicePlatform.IOS)
            {
                deviceType = "Mobile";
            }
            else if (identity.Platform == DevicePlatform.Windows || identity.Platform == DevicePlatform.MacOS)
            {
                deviceType = "Desktop";
            }
            else if (identity.Platform == DevicePlatform.Linux)
            {
                deviceType = "Server";
            }
            else
            {
                deviceType = "Device";
            }

            return new DeviceInfo(
                deviceCode: identity.DeviceCode,
                installationId: identity.InstallationId,
                platform: identity.Platform,
                platformVersion: platformVersion,
                architecture: architecture,
                deviceType: deviceType,
                fingerprintVersion: identity.Fingerprint?.FingerprintVersion ?? DeviceFingerprintGenerator.CurrentVersion,
                signalAvailability: availability,
                appVersion: _options.AppVersion,
                collectedAtUtc: DateTimeOffset.UtcNow);
        }

        /// <inheritdoc />
        public Task<DeviceIdentity> GetIdentityAsync(CancellationToken cancellationToken = default)
        {
            return _identityProvider.GetIdentityAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<string> GetDeviceCodeAsync(CancellationToken cancellationToken = default)
        {
            var identity = await _identityProvider.GetIdentityAsync(cancellationToken).ConfigureAwait(false);
            return identity.DeviceCode;
        }

        /// <inheritdoc />
        public async Task<DeviceFingerprint> GetFingerprintAsync(CancellationToken cancellationToken = default)
        {
            var signals = await _signalProvider.GetSignalsAsync(cancellationToken).ConfigureAwait(false);
            return DeviceFingerprintGenerator.Generate(
                signals,
                _options.FingerprintVersion,
                _options.SecretKey,
                _options.ApplicationSalt,
                _options.KeyScope,
                _options.FingerprintAlgorithm);
        }

        /// <inheritdoc />
        public async Task<DeviceIdentityComparison> CompareAsync(DeviceFingerprint previous, CancellationToken cancellationToken = default)
        {
            var signals = await _signalProvider.GetSignalsAsync(cancellationToken).ConfigureAwait(false);
            var current = DeviceFingerprintGenerator.Generate(
                signals,
                _options.FingerprintVersion,
                _options.SecretKey,
                _options.ApplicationSalt,
                _options.KeyScope,
                _options.FingerprintAlgorithm);
            return DeviceChangeDetector.Compare(previous, current, _options.MaxNetworkWeightCap, signals);
        }
    }
}
