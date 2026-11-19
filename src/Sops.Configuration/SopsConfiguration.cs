using Microsoft.Extensions.Configuration;
using Sops.Configuration.Extensions;
using Sops.Configuration.Models;

namespace Sops.Configuration;

/// <summary>
/// Static entry point providing standard configuration loaders and environment resolution.
/// </summary>
public static class SopsConfiguration
{
    private const string SopsEnvVar = "SOPS_ENVIRONMENT";
    private const string DotNetEnvVar = "DOTNET_ENVIRONMENT";
    private const string AspNetEnvVar = "ASPNETCORE_ENVIRONMENT";

    /// <summary>
    /// Resolves the current environment name by checking environment variables in order:
    /// SOPS_ENVIRONMENT -> DOTNET_ENVIRONMENT -> ASPNETCORE_ENVIRONMENT.
    /// Defaults to <see cref="SopsEnvironments.Production"/> if none are set.
    /// Avoids mutable global state.
    /// </summary>
    /// <param name="explicitEnvironment">An optional explicit environment override.</param>
    /// <returns>The resolved, normalized environment name.</returns>
    public static string ResolveEnvironment(string? explicitEnvironment = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitEnvironment))
        {
            return SopsEnvironments.Normalize(explicitEnvironment);
        }

        var sopsEnv = Environment.GetEnvironmentVariable(SopsEnvVar);
        if (!string.IsNullOrWhiteSpace(sopsEnv))
        {
            return SopsEnvironments.Normalize(sopsEnv);
        }

        var dotNetEnv = Environment.GetEnvironmentVariable(DotNetEnvVar);
        if (!string.IsNullOrWhiteSpace(dotNetEnv))
        {
            return SopsEnvironments.Normalize(dotNetEnv);
        }

        var aspNetEnv = Environment.GetEnvironmentVariable(AspNetEnvVar);
        if (!string.IsNullOrWhiteSpace(aspNetEnv))
        {
            return SopsEnvironments.Normalize(aspNetEnv);
        }

        return SopsEnvironments.Production;
    }

    /// <summary>
    /// Creates a new <see cref="SopsConfigurationBuilder"/> with the specified or resolved environment and base path.
    /// </summary>
    /// <param name="environment">The environment name, or null to resolve from environment variables.</param>
    /// <param name="basePath">The base directory path, or null to default to application directory.</param>
    /// <returns>A new builder instance.</returns>
    public static SopsConfigurationBuilder CreateBuilder(string? environment = null, string? basePath = null)
    {
        return new SopsConfigurationBuilder(environment, basePath);
    }

    /// <summary>
    /// Creates and builds an <see cref="IConfigurationRoot"/> configured with standard SOPs defaults:
    /// base JSON file -> environment JSON file -> environment variables -> in-memory overrides.
    /// </summary>
    /// <param name="environment">The target environment name, or null to resolve automatically.</param>
    /// <param name="basePath">The base directory path, or null to default to application directory.</param>
    /// <param name="configureOptions">An optional delegate to configure default loading behavior.</param>
    /// <returns>A built <see cref="IConfigurationRoot"/>.</returns>
    public static IConfigurationRoot CreateDefault(
        string? environment = null,
        string? basePath = null,
        Action<SopsConfigurationOptions>? configureOptions = null)
    {
        var builder = new ConfigurationBuilder();
        builder.AddSopsDefaults(environment, basePath, configureOptions);
        return builder.Build();
    }
}
