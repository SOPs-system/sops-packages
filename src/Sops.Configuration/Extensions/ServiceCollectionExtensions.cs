using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Sops.Configuration.Extensions;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to integrate strongly typed settings
/// with the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a validated strongly typed settings object in the service container.
    /// By default, validates settings immediately on startup (fail-fast).
    /// Registers both <typeparamref name="TSettings"/> and <see cref="IOptions{TOptions}"/>.
    /// </summary>
    /// <typeparam name="TSettings">The settings type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="sectionName">The configuration section name to bind from.</param>
    /// <param name="validateOnStart">Whether to validate immediately on startup. Defaults to true.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configuration"/> is null.</exception>
    /// <exception cref="Sops.Configuration.Validation.ConfigurationValidationException">Thrown if validation fails when <paramref name="validateOnStart"/> is true.</exception>
    public static IServiceCollection AddSopsSettings<TSettings>(
        this IServiceCollection services,
        IConfiguration configuration,
        string? sectionName = null,
        bool validateOnStart = true) where TSettings : class, new()
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        if (validateOnStart)
        {
            var settings = configuration.GetValid<TSettings>(sectionName);
            services.AddSingleton(settings);
            services.AddSingleton<IOptions<TSettings>>(Options.Create(settings));
        }
        else
        {
            services.AddSingleton(sp => configuration.GetValid<TSettings>(sectionName));
            services.AddSingleton<IOptions<TSettings>>(sp => Options.Create(sp.GetRequiredService<TSettings>()));
        }

        return services;
    }

    /// <summary>
    /// Registers a validated strongly typed settings object resolved from the container's <see cref="IConfiguration"/>.
    /// </summary>
    /// <typeparam name="TSettings">The settings type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="sectionName">The configuration section name to bind from.</param>
    /// <param name="validateOnStart">Whether to validate immediately on startup. Defaults to true.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public static IServiceCollection AddSopsSettings<TSettings>(
        this IServiceCollection services,
        string? sectionName = null,
        bool validateOnStart = true) where TSettings : class, new()
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        services.AddSingleton(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            return configuration.GetValid<TSettings>(sectionName);
        });

        services.AddSingleton<IOptions<TSettings>>(sp => Options.Create(sp.GetRequiredService<TSettings>()));

        return services;
    }
}
