namespace Sops.Configuration.Models;

/// <summary>
/// Options for configuring default configuration loading sources and behavior.
/// </summary>
public sealed class SopsConfigurationOptions
{
    /// <summary>
    /// Gets or sets the target environment. If null, the environment is resolved from environment variables or defaults to Production.
    /// </summary>
    public string? Environment { get; set; }

    /// <summary>
    /// Gets or sets the base path for resolving relative configuration files. Defaults to the application's base directory.
    /// </summary>
    public string? BasePath { get; set; }

    /// <summary>
    /// Gets or sets the base JSON configuration file name. Defaults to "appsettings.json".
    /// </summary>
    public string BaseJsonFileName { get; set; } = "appsettings.json";

    /// <summary>
    /// Gets or sets whether the base JSON file is optional. Defaults to true.
    /// </summary>
    public bool OptionalBaseJsonFile { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to load the environment-specific JSON file (e.g. appsettings.{Environment}.json). Defaults to true.
    /// </summary>
    public bool IncludeEnvironmentJsonFile { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the environment-specific JSON file is optional. Defaults to true.
    /// </summary>
    public bool OptionalEnvironmentJsonFile { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include environment variables. Defaults to true.
    /// </summary>
    public bool IncludeEnvironmentVariables { get; set; } = true;

    /// <summary>
    /// Gets or sets an optional prefix for environment variables (e.g. "SOPS_").
    /// </summary>
    public string? EnvironmentVariablesPrefix { get; set; }

    /// <summary>
    /// Gets or sets whether files should reload when modified on disk. Defaults to false.
    /// </summary>
    public bool ReloadOnChange { get; set; }

    /// <summary>
    /// Gets or sets initial in-memory key-value overrides.
    /// </summary>
    public IDictionary<string, string?>? InMemoryOverrides { get; set; }
}
