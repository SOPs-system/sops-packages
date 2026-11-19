using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.Device.Abstractions;
using Sops.Device.Configuration;
using Sops.Device.Models;
using Sops.Device.Providers.Android;
using Sops.Device.Providers.Fallback;
using Sops.Device.Providers.IOS;
using Sops.Device.Providers.Linux;
using Sops.Device.Providers.MacOS;
using Sops.Device.Providers.Windows;

namespace Sops.Device.Providers
{
    /// <summary>
    /// Composite signal provider orchestrating platform detection and delegating to the appropriate
    /// native hardware signal provider with seamless fallback. Evaluates physical and operating system
    /// hardware signals independently from software installation identifiers.
    /// </summary>
    public sealed class CompositeDeviceSignalProvider : IDeviceSignalProvider
    {
        private readonly IDevicePlatformDetector _platformDetector;
        private readonly DeviceOptions _options;
        private readonly ILogger<CompositeDeviceSignalProvider>? _logger;

        private readonly IDeviceSignalProvider _windowsProvider;
        private readonly IDeviceSignalProvider _linuxProvider;
        private readonly IDeviceSignalProvider _macOsProvider;
        private readonly IDeviceSignalProvider _androidProvider;
        private readonly IDeviceSignalProvider _iosProvider;
        private readonly IDeviceSignalProvider _fallbackProvider;

        public CompositeDeviceSignalProvider(
            IDevicePlatformDetector platformDetector,
            DeviceOptions options,
            ILogger<CompositeDeviceSignalProvider>? logger = null)
            : this(
                platformDetector,
                options,
                new WindowsDeviceSignalProvider(options?.IncludeMacAddresses ?? false, options?.ApplicationSalt, options?.MaxNetworkAdapters ?? 2),
                new LinuxDeviceSignalProvider(options?.IncludeMacAddresses ?? false, options?.ApplicationSalt, options?.MaxNetworkAdapters ?? 2),
                new MacOSDeviceSignalProvider(options?.IncludeMacAddresses ?? false, options?.ApplicationSalt, options?.MaxNetworkAdapters ?? 2),
                new AndroidDeviceSignalProvider(options?.NativeSignalResolver, options?.ApplicationSalt),
                new IosDeviceSignalProvider(options?.NativeSignalResolver, options?.ApplicationSalt),
                new FallbackDeviceSignalProvider(options?.ApplicationSalt),
                logger)
        {
        }

        internal CompositeDeviceSignalProvider(
            IDevicePlatformDetector platformDetector,
            DeviceOptions options,
            IDeviceSignalProvider windowsProvider,
            IDeviceSignalProvider linuxProvider,
            IDeviceSignalProvider macOsProvider,
            IDeviceSignalProvider androidProvider,
            IDeviceSignalProvider iosProvider,
            IDeviceSignalProvider fallbackProvider,
            ILogger<CompositeDeviceSignalProvider>? logger = null)
        {
            _platformDetector = platformDetector ?? throw new ArgumentNullException(nameof(platformDetector));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;

            _windowsProvider = windowsProvider ?? throw new ArgumentNullException(nameof(windowsProvider));
            _linuxProvider = linuxProvider ?? throw new ArgumentNullException(nameof(linuxProvider));
            _macOsProvider = macOsProvider ?? throw new ArgumentNullException(nameof(macOsProvider));
            _androidProvider = androidProvider ?? throw new ArgumentNullException(nameof(androidProvider));
            _iosProvider = iosProvider ?? throw new ArgumentNullException(nameof(iosProvider));
            _fallbackProvider = fallbackProvider ?? throw new ArgumentNullException(nameof(fallbackProvider));
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<DeviceSignal>> GetSignalsAsync(CancellationToken cancellationToken = default)
        {
            var combinedSignals = new List<DeviceSignal>();

            // 1. Query platform-specific provider
            var platform = _options.CustomPlatform ?? _platformDetector.Detect();
            var targetProvider = SelectProvider(platform);

            try
            {
                var platformSignals = await targetProvider.GetSignalsAsync(cancellationToken).ConfigureAwait(false);
                if (platformSignals != null && platformSignals.Count > 0)
                {
                    combinedSignals.AddRange(platformSignals);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Primary platform signal provider {Platform} threw an exception; falling back to environment provider", platform);
            }

            // 2. If no hardware signals were available, invoke fallback provider
            bool hasAvailableHardwareSignal = combinedSignals.Any(s => s.IsAvailable);
            if (!hasAvailableHardwareSignal)
            {
                _logger?.LogInformation("No hardware signals were successfully collected on platform {Platform}. Activating fallback provider.", platform);
                try
                {
                    var fallbackSignals = await _fallbackProvider.GetSignalsAsync(cancellationToken).ConfigureAwait(false);
                    if (fallbackSignals != null)
                    {
                        foreach (var fb in fallbackSignals)
                        {
                            if (!combinedSignals.Any(s => s.Name.Equals(fb.Name, StringComparison.OrdinalIgnoreCase)))
                            {
                                combinedSignals.Add(fb);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Fallback signal provider failed");
                }
            }

            return combinedSignals;
        }

        private IDeviceSignalProvider SelectProvider(DevicePlatform platform)
        {
            switch (platform)
            {
                case DevicePlatform.Windows:
                    return _windowsProvider;
                case DevicePlatform.Linux:
                    return _linuxProvider;
                case DevicePlatform.MacOS:
                case DevicePlatform.MacCatalyst:
                    return _macOsProvider;
                case DevicePlatform.Android:
                    return _androidProvider;
                case DevicePlatform.IOS:
                    return _iosProvider;
                default:
                    return _fallbackProvider;
            }
        }
    }
}
