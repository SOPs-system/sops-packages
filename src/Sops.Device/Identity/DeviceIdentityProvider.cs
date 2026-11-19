using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.Device.Abstractions;
using Sops.Device.Configuration;
using Sops.Device.Fingerprinting;
using Sops.Device.Models;

namespace Sops.Device.Identity
{
    /// <summary>
    /// Coordinates initialization, persistence, and caching of the device identity according to configured identity mode.
    /// </summary>
    public sealed class DeviceIdentityProvider : IDeviceIdentityProvider
    {
        private readonly IDeviceIdentityStore _store;
        private readonly InstallationIdentityManager _installationManager;
        private readonly IDeviceSignalProvider _signalProvider;
        private readonly IDeviceCodeGenerator _codeGenerator;
        private readonly IDevicePlatformDetector _platformDetector;
        private readonly DeviceOptions _options;
        private readonly ILogger<DeviceIdentityProvider>? _logger;

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private DeviceIdentity? _cachedIdentity;

        public DeviceIdentityProvider(
            IDeviceIdentityStore store,
            InstallationIdentityManager installationManager,
            IDeviceSignalProvider signalProvider,
            IDeviceCodeGenerator codeGenerator,
            IDevicePlatformDetector platformDetector,
            DeviceOptions options,
            ILogger<DeviceIdentityProvider>? logger = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _installationManager = installationManager ?? throw new ArgumentNullException(nameof(installationManager));
            _signalProvider = signalProvider ?? throw new ArgumentNullException(nameof(signalProvider));
            _codeGenerator = codeGenerator ?? throw new ArgumentNullException(nameof(codeGenerator));
            _platformDetector = platformDetector ?? throw new ArgumentNullException(nameof(platformDetector));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<DeviceIdentity> GetIdentityAsync(CancellationToken cancellationToken = default)
        {
            if (_cachedIdentity != null)
            {
                return _cachedIdentity;
            }

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_cachedIdentity != null)
                {
                    return _cachedIdentity;
                }

                // 1. Try reading stored identity record
                var storedIdentity = await _store.ReadIdentityAsync(cancellationToken).ConfigureAwait(false);
                var currentPlatform = _platformDetector.Detect();

                if (storedIdentity != null &&
                    !string.IsNullOrWhiteSpace(storedIdentity.InstallationId) &&
                    !string.IsNullOrWhiteSpace(storedIdentity.DeviceCode))
                {
                    // If hybrid mode, evaluate hardware change against previous fingerprint
                    if (_options.IdentityMode == DeviceIdentityMode.Hybrid && _options.EnableHardwareFingerprint)
                    {
                        var signals = await _signalProvider.GetSignalsAsync(cancellationToken).ConfigureAwait(false);
                        var currentFp = DeviceFingerprintGenerator.Generate(
                            signals,
                            _options.FingerprintVersion,
                            _options.SecretKey,
                            _options.ApplicationSalt,
                            _options.KeyScope,
                            _options.FingerprintAlgorithm);

                        if (storedIdentity.Fingerprint != null)
                        {
                            var comparison = DeviceChangeDetector.Compare(storedIdentity.Fingerprint, currentFp, _options.MaxNetworkWeightCap, signals);
                            if (comparison.HasChanged)
                            {
                                _logger?.LogInformation("Hardware drift detected for device {Code}: {Assessment}", storedIdentity.DeviceCode, comparison.Assessment);
                            }
                        }

                        // Update identity with refreshed fingerprint while retaining stable install ID
                        _cachedIdentity = new DeviceIdentity(
                            storedIdentity.InstallationId,
                            storedIdentity.DeviceCode,
                            _options.IdentityMode,
                            storedIdentity.Platform != DevicePlatform.Unknown ? storedIdentity.Platform : currentPlatform,
                            currentFp,
                            storedIdentity.CreatedAtUtc,
                            DateTimeOffset.UtcNow);

                        await _store.SaveIdentityAsync(_cachedIdentity, cancellationToken).ConfigureAwait(false);
                        return _cachedIdentity;
                    }

                    _cachedIdentity = storedIdentity;
                    return _cachedIdentity;
                }

                // 2. Initialize new identity
                string installId = await _installationManager.GetOrCreateInstallationIdAsync(cancellationToken).ConfigureAwait(false);
                string deviceCode = _codeGenerator.GenerateCode(installId, currentPlatform, _options.CodePrefix);

                DeviceFingerprint? fingerprint = null;
                if (_options.EnableHardwareFingerprint && (_options.IdentityMode == DeviceIdentityMode.Hardware || _options.IdentityMode == DeviceIdentityMode.Hybrid))
                {
                    var signals = await _signalProvider.GetSignalsAsync(cancellationToken).ConfigureAwait(false);
                    fingerprint = DeviceFingerprintGenerator.Generate(
                        signals,
                        _options.FingerprintVersion,
                        _options.SecretKey,
                        _options.ApplicationSalt,
                        _options.KeyScope,
                        _options.FingerprintAlgorithm);
                }

                _cachedIdentity = new DeviceIdentity(
                    installId,
                    deviceCode,
                    _options.IdentityMode,
                    currentPlatform,
                    fingerprint,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow);

                await _store.SaveIdentityAsync(_cachedIdentity, cancellationToken).ConfigureAwait(false);
                _logger?.LogInformation("Created and persisted new device identity: Code={Code}, Mode={Mode}, Platform={Platform}", deviceCode, _options.IdentityMode, currentPlatform);

                return _cachedIdentity;
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
