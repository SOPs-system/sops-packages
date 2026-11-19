using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sops.Device.Abstractions;
using Sops.Device.Configuration;
using Sops.Device.Identity;
using Sops.Device.Persistence;
using Sops.Device.Providers;

namespace Sops.Device.Extensions
{
    /// <summary>
    /// Dependency injection extensions for registering SOPs device identification services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the SOPs device identity and hardware signal SDK with the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Optional configuration action.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSopsDevice(
            this IServiceCollection services,
            Action<DeviceOptions>? configure = null)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            var options = new DeviceOptions();
            configure?.Invoke(options);
            services.TryAddSingleton(options);

            services.TryAddSingleton<IDevicePlatformDetector>(sp =>
            {
                var opt = sp.GetRequiredService<DeviceOptions>();
                return new DefaultDevicePlatformDetector(opt.CustomPlatform);
            });

            services.TryAddSingleton<IDeviceIdentityStore>(sp =>
            {
                var opt = sp.GetRequiredService<DeviceOptions>();
                var logger = sp.GetService<ILogger<FileDeviceIdentityStore>>();
                return new FileDeviceIdentityStore(opt.StoragePath, opt.StorageRetryCount, opt.StorageRetryDelayMs, logger);
            });

            services.TryAddSingleton<InstallationIdentityManager>();

            services.TryAddSingleton<IDeviceSignalProvider>(sp =>
            {
                var detector = sp.GetRequiredService<IDevicePlatformDetector>();
                var opt = sp.GetRequiredService<DeviceOptions>();
                var logger = sp.GetService<ILogger<CompositeDeviceSignalProvider>>();
                return new CompositeDeviceSignalProvider(detector, opt, logger);
            });

            services.TryAddSingleton<IDeviceCodeGenerator>(sp =>
            {
                var opt = sp.GetRequiredService<DeviceOptions>();
                return new DeviceCodeGenerator(opt.CodePrefix);
            });

            services.TryAddSingleton<IDeviceIdentityProvider, DeviceIdentityProvider>();
            services.TryAddSingleton<IDeviceService, DeviceService>();

            return services;
        }
    }
}
