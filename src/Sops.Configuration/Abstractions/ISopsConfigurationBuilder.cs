using Microsoft.Extensions.Configuration;

namespace Sops.Configuration.Abstractions;

/// <summary>
/// Defines a builder for configuring and creating application configuration roots.
/// </summary>
public interface ISopsConfigurationBuilder
{
    /// <summary>
    /// Gets the target environment for the configuration.
    /// </summary>
    string Environment { get; }

    /// <summary>
    /// Gets the base path for relative file resolution.
    /// </summary>
    string BasePath { get; }

    /// <summary>
    /// Sets the base path used to resolve file paths.
    /// </summary>
    /// <param name="basePath">The base directory path.</param>
    /// <returns>The builder instance.</returns>
    ISopsConfigurationBuilder SetBasePath(string basePath);

    /// <summary>
    /// Sets the target environment name (e.g. Development, Staging, Production).
    /// </summary>
    /// <param name="environment">The environment name.</param>
    /// <returns>The builder instance.</returns>
    ISopsConfigurationBuilder SetEnvironment(string environment);

    /// <summary>
    /// Adds a JSON configuration file.
    /// </summary>
    /// <param name="path">Path relative to BasePath or absolute path.</param>
    /// <param name="optional">Whether the file is optional.</param>
    /// <param name="reloadOnChange">Whether the configuration should reload when the file changes.</param>
    /// <returns>The builder instance.</returns>
    ISopsConfigurationBuilder AddJsonFile(string path, bool optional = true, bool reloadOnChange = false);

    /// <summary>
    /// Adds environment variables with an optional prefix.
    /// </summary>
    /// <param name="prefix">The prefix that environment variable keys must start with.</param>
    /// <returns>The builder instance.</returns>
    ISopsConfigurationBuilder AddEnvironmentVariables(string? prefix = null);

    /// <summary>
    /// Adds an in-memory collection of key-value pairs.
    /// </summary>
    /// <param name="initialData">Key-value pairs to add.</param>
    /// <returns>The builder instance.</returns>
    ISopsConfigurationBuilder AddInMemoryCollection(IEnumerable<KeyValuePair<string, string?>> initialData);

    /// <summary>
    /// Allows custom configuration of the underlying <see cref="IConfigurationBuilder"/>.
    /// </summary>
    /// <param name="configure">The configuration delegate.</param>
    /// <returns>The builder instance.</returns>
    ISopsConfigurationBuilder ConfigureBuilder(Action<IConfigurationBuilder> configure);

    /// <summary>
    /// Builds the configured <see cref="IConfigurationRoot"/>.
    /// </summary>
    /// <returns>The constructed configuration root.</returns>
    IConfigurationRoot Build();
}
