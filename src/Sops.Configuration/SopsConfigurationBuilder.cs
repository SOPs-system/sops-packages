using Microsoft.Extensions.Configuration;
using Sops.Configuration.Abstractions;
using Sops.Configuration.Models;

namespace Sops.Configuration;

/// <summary>
/// Fluent builder for constructing an <see cref="IConfigurationRoot"/> with SOPs standard conventions and precedence.
/// </summary>
public sealed class SopsConfigurationBuilder : ISopsConfigurationBuilder
{
    private readonly ConfigurationBuilder _innerBuilder = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SopsConfigurationBuilder"/> class.
    /// </summary>
    /// <param name="environment">The initial environment name, or null to resolve from environment variables.</param>
    /// <param name="basePath">The initial base path, or null to default to application directory.</param>
    public SopsConfigurationBuilder(string? environment = null, string? basePath = null)
    {
        Environment = SopsEnvironments.Normalize(environment ?? SopsConfiguration.ResolveEnvironment());
        BasePath = basePath ?? AppDomain.CurrentDomain.BaseDirectory;
        _innerBuilder.SetBasePath(BasePath);
    }

    /// <inheritdoc />
    public string Environment { get; private set; }

    /// <inheritdoc />
    public string BasePath { get; private set; }

    /// <inheritdoc />
    public ISopsConfigurationBuilder SetBasePath(string basePath)
    {
        if (string.IsNullOrWhiteSpace(basePath))
            throw new ArgumentException("Base path cannot be null or whitespace.", nameof(basePath));

        BasePath = basePath;
        _innerBuilder.SetBasePath(basePath);
        return this;
    }

    /// <inheritdoc />
    public ISopsConfigurationBuilder SetEnvironment(string environment)
    {
        Environment = SopsEnvironments.Normalize(environment);
        return this;
    }

    /// <inheritdoc />
    public ISopsConfigurationBuilder AddJsonFile(string path, bool optional = true, bool reloadOnChange = false)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("File path cannot be null or whitespace.", nameof(path));

        _innerBuilder.AddJsonFile(path, optional: optional, reloadOnChange: reloadOnChange);
        return this;
    }

    /// <summary>
    /// Adds base and environment-specific JSON files (e.g. appsettings.json and appsettings.Development.json).
    /// </summary>
    /// <param name="baseFileName">The base file name. Defaults to "appsettings.json".</param>
    /// <param name="optional">Whether the files are optional.</param>
    /// <param name="reloadOnChange">Whether the files should reload on change.</param>
    /// <returns>The builder instance.</returns>
    public ISopsConfigurationBuilder AddEnvironmentSpecificJsonFile(
        string baseFileName = "appsettings.json",
        bool optional = true,
        bool reloadOnChange = false)
    {
        AddJsonFile(baseFileName, optional: optional, reloadOnChange: reloadOnChange);

        var ext = Path.GetExtension(baseFileName);
        var nameWithoutExt = Path.GetFileNameWithoutExtension(baseFileName);
        var envFileName = $"{nameWithoutExt}.{Environment}{ext}";

        AddJsonFile(envFileName, optional: optional, reloadOnChange: reloadOnChange);
        return this;
    }

    /// <inheritdoc />
    public ISopsConfigurationBuilder AddEnvironmentVariables(string? prefix = null)
    {
        if (!string.IsNullOrEmpty(prefix))
        {
            _innerBuilder.AddEnvironmentVariables(prefix);
        }
        else
        {
            _innerBuilder.AddEnvironmentVariables();
        }

        return this;
    }

    /// <inheritdoc />
    public ISopsConfigurationBuilder AddInMemoryCollection(IEnumerable<KeyValuePair<string, string?>> initialData)
    {
        if (initialData == null)
            throw new ArgumentNullException(nameof(initialData));

        _innerBuilder.AddInMemoryCollection(initialData);
        return this;
    }

    /// <inheritdoc />
    public ISopsConfigurationBuilder ConfigureBuilder(Action<IConfigurationBuilder> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        configure(_innerBuilder);
        return this;
    }

    /// <inheritdoc />
    public IConfigurationRoot Build()
    {
        return _innerBuilder.Build();
    }
}
