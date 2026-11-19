using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sops.Logging.Models;

namespace Sops.Logging.Extensions;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to register and configure SOPs logging services.
/// </summary>
public static class SopsLoggingServiceCollectionExtensions
{
    /// <summary>
    /// Registers SOPs structured logging services into the service collection.
    /// Reads configuration from the provided <see cref="IConfiguration"/> instance and applies optional custom options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance (e.g. from SopsConfiguration.CreateDefault).</param>
    /// <param name="configure">Optional delegate to customize logging options.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public static IServiceCollection AddSopsLogging(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        Action<SopsLoggingOptions>? configure = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.AddSopsLogging(configuration, configure);
        });

        return services;
    }

    /// <summary>
    /// Registers SOPs structured logging services into the service collection with custom options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Delegate to customize logging options.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.</exception>
    public static IServiceCollection AddSopsLogging(
        this IServiceCollection services,
        Action<SopsLoggingOptions> configure)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        return services.AddSopsLogging(configuration: null, configure);
    }
}
