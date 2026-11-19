using Microsoft.Extensions.Configuration;
using Sops.Configuration.Models;

namespace Sops.Configuration.Extensions;

/// <summary>
/// Extension methods for <see cref="IConfigurationBuilder"/> to configure standard SOPs configuration sources.
/// </summary>
public static class ConfigurationBuilderExtensions
{
    /// <summary>
    /// Adds default SOPs configuration sources in standard precedence order:
    /// base JSON -> environment JSON -> environment variables -> in-memory overrides.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="environment">The target environment name. If null, resolved from environment variables or defaults to Production.</param>
    /// <param name="basePath">The base directory path. If null, defaults to application base directory.</param>
    /// <param name="configureOptions">Optional delegate to fine-tune configuration sources.</param>
    /// <returns>The configuration builder.</returns>
    public static IConfigurationBuilder AddSopsDefaults(
        this IConfigurationBuilder builder,
        string? environment = null,
        string? basePath = null,
        Action<SopsConfigurationOptions>? configureOptions = null)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));

        var options = new SopsConfigurationOptions
        {
            Environment = environment,
            BasePath = basePath
        };
        configureOptions?.Invoke(options);

        var resolvedEnv = SopsEnvironments.Normalize(options.Environment ?? SopsConfiguration.ResolveEnvironment());
        var resolvedPath = options.BasePath ?? AppDomain.CurrentDomain.BaseDirectory;

        builder.SetBasePath(resolvedPath);

        // 1. Base JSON file (e.g. appsettings.json)
        if (!string.IsNullOrEmpty(options.BaseJsonFileName))
        {
            builder.AddJsonFile(options.BaseJsonFileName, optional: options.OptionalBaseJsonFile, reloadOnChange: options.ReloadOnChange);
        }

        // 2. Environment-specific JSON file (e.g. appsettings.Development.json)
        if (options.IncludeEnvironmentJsonFile && !string.IsNullOrEmpty(options.BaseJsonFileName))
        {
            var ext = Path.GetExtension(options.BaseJsonFileName);
            var nameWithoutExt = Path.GetFileNameWithoutExtension(options.BaseJsonFileName);
            var envFileName = $"{nameWithoutExt}.{resolvedEnv}{ext}";
            builder.AddJsonFile(envFileName, optional: options.OptionalEnvironmentJsonFile, reloadOnChange: options.ReloadOnChange);
        }

        // 3. Environment variables
        if (options.IncludeEnvironmentVariables)
        {
            if (!string.IsNullOrEmpty(options.EnvironmentVariablesPrefix))
            {
                builder.AddEnvironmentVariables(options.EnvironmentVariablesPrefix);
            }
            else
            {
                builder.AddEnvironmentVariables();
            }
        }

        // 4. In-memory overrides
        if (options.InMemoryOverrides != null && options.InMemoryOverrides.Count > 0)
        {
            builder.AddInMemoryCollection(options.InMemoryOverrides);
        }

        return builder;
    }

    /// <summary>
    /// Adds an environment-specific JSON configuration file (e.g. appsettings.{Environment}.json).
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="environment">The environment name.</param>
    /// <param name="baseFileName">The base file name. Defaults to "appsettings.json".</param>
    /// <param name="optional">Whether the file is optional.</param>
    /// <param name="reloadOnChange">Whether the file should reload on change.</param>
    /// <returns>The configuration builder.</returns>
    public static IConfigurationBuilder AddSopsEnvironmentJson(
        this IConfigurationBuilder builder,
        string environment,
        string baseFileName = "appsettings.json",
        bool optional = true,
        bool reloadOnChange = false)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));

        var resolvedEnv = SopsEnvironments.Normalize(environment);
        var ext = Path.GetExtension(baseFileName);
        var nameWithoutExt = Path.GetFileNameWithoutExtension(baseFileName);
        var envFileName = $"{nameWithoutExt}.{resolvedEnv}{ext}";

        return builder.AddJsonFile(envFileName, optional: optional, reloadOnChange: reloadOnChange);
    }
}
