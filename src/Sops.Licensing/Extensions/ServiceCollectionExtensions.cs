using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Sops.Device.Abstractions;
using Sops.Device.Extensions;
using Sops.Licensing.Abstractions;
using Sops.Licensing.Activation;
using Sops.Licensing.Device;
using Sops.Licensing.Entitlements;
using Sops.Licensing.Models;
using Sops.Licensing.Storage;
using Sops.Licensing.Validation;

namespace Sops.Licensing.Extensions
{
    /// <summary>
    /// Dependency injection extensions for registering SOPs Licensing and Secure Storage services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers SOPs licensing, entitlement evaluation, digital certificate validation,
        /// and operating-system-backed secure storage using configuration settings.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The root or licensing configuration section.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSopsLicensing(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            var section = configuration.GetSection(LicensingOptions.SectionName);
            var options = new LicensingOptions();
            if (section.Exists())
            {
                section.Bind(options);
            }
            else
            {
                configuration.Bind(options);
            }

            return services.AddSopsLicensing(options);
        }

        /// <summary>
        /// Registers SOPs licensing services with an options configuration action.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">The configuration delegate.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSopsLicensing(
            this IServiceCollection services,
            Action<LicensingOptions> configure)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            var options = new LicensingOptions();
            configure(options);

            return services.AddSopsLicensing(options);
        }

        /// <summary>
        /// Registers SOPs licensing services with an existing <see cref="LicensingOptions"/> instance.
        /// </summary>
        public static IServiceCollection AddSopsLicensing(
            this IServiceCollection services,
            LicensingOptions options)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (options == null) throw new ArgumentNullException(nameof(options));

            services.TryAddSingleton(options);
            services.TryAddSingleton<IOptions<LicensingOptions>>(new OptionsWrapper<LicensingOptions>(options));

            // Secure storage backed by OS detection
            services.TryAddSingleton<ISecureStorage>(sp =>
            {
                var opt = sp.GetRequiredService<LicensingOptions>();
                return SecureStorageFactory.CreateStorage(opt);
            });

            // Device identity backed by Sops.Device
            services.AddSopsDevice();
            services.TryAddSingleton<IDeviceIdentifier>(sp =>
            {
                var deviceService = sp.GetService<IDeviceService>();
                var secureStorage = sp.GetService<ISecureStorage>();
                return new DeviceIdentifier(deviceService, secureStorage);
            });

            // Entitlement evaluation
            services.TryAddSingleton<IEntitlementEvaluator, EntitlementEvaluator>();

            // License validator
            services.TryAddSingleton<ILicenseValidator, LicenseValidator>();

            // License manager
            services.TryAddSingleton<ILicenseManager, LicenseManager>();

            return services;
        }
    }
}
