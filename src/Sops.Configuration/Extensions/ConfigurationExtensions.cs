using Microsoft.Extensions.Configuration;
using Sops.Configuration.Validation;

namespace Sops.Configuration.Extensions;

/// <summary>
/// Extension methods for <see cref="IConfiguration"/> providing strongly typed, validated settings retrieval.
/// </summary>
public static class ConfigurationExtensions
{
    /// <summary>
    /// Binds configuration data to a new instance of <typeparamref name="TSettings"/> and validates it.
    /// Preserves property defaults when values are not provided in configuration.
    /// </summary>
    /// <typeparam name="TSettings">The type of settings object. Must have a public parameterless constructor.</typeparam>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="sectionName">The optional section name to bind from.</param>
    /// <param name="configureDefaults">An optional delegate to configure default values before binding.</param>
    /// <returns>A validated instance of <typeparamref name="TSettings"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> is null.</exception>
    /// <exception cref="ConfigurationValidationException">Thrown when validation fails.</exception>
    public static TSettings GetValid<TSettings>(
        this IConfiguration configuration,
        string? sectionName = null,
        Action<TSettings>? configureDefaults = null) where TSettings : class, new()
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        var settings = new TSettings();
        configureDefaults?.Invoke(settings);

        if (!string.IsNullOrEmpty(sectionName))
        {
            var section = configuration.GetSection(sectionName!);
            section.Bind(settings);
        }
        else
        {
            configuration.Bind(settings);
        }

        ConfigurationValidator.Validate(settings, sectionName);
        return settings;
    }

    /// <summary>
    /// Binds configuration data into an existing settings instance and validates it.
    /// </summary>
    /// <typeparam name="TSettings">The type of settings object.</typeparam>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="instance">The existing settings instance to bind into.</param>
    /// <param name="sectionName">The optional section name to bind from.</param>
    /// <returns>The bound and validated instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> or <paramref name="instance"/> is null.</exception>
    /// <exception cref="ConfigurationValidationException">Thrown when validation fails.</exception>
    public static TSettings BindValid<TSettings>(
        this IConfiguration configuration,
        TSettings instance,
        string? sectionName = null) where TSettings : class
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        if (!string.IsNullOrEmpty(sectionName))
        {
            var section = configuration.GetSection(sectionName!);
            section.Bind(instance);
        }
        else
        {
            configuration.Bind(instance);
        }

        ConfigurationValidator.Validate(instance, sectionName);
        return instance;
    }

    /// <summary>
    /// Retrieves a required configuration value by key. Throws if the key is missing or empty.
    /// Never exposes the value or secret in exception messages.
    /// </summary>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="key">The configuration key.</param>
    /// <returns>The non-empty configuration value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or empty.</exception>
    /// <exception cref="ConfigurationValidationException">Thrown when the required value is missing.</exception>
    public static string GetRequiredValue(this IConfiguration configuration, string key)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Configuration key cannot be null or whitespace.", nameof(key));

        var value = configuration[key];
        if (string.IsNullOrEmpty(value))
        {
            var isSensitive = SecretMasker.IsSensitivePropertyName(key);
            var error = new ConfigurationValidationError(
                key,
                $"Required configuration key '{key}' is missing or empty.",
                isSensitive);

            throw new ConfigurationValidationException(
                typeof(IConfiguration),
                null,
                new[] { error });
        }

        return value!;
    }

    /// <summary>
    /// Retrieves an optional configuration value, returning a default value if missing or empty.
    /// </summary>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="key">The configuration key.</param>
    /// <param name="defaultValue">The fallback default value.</param>
    /// <returns>The configured value or the default value.</returns>
    public static string GetOptionalValue(this IConfiguration configuration, string key, string defaultValue)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        if (string.IsNullOrWhiteSpace(key))
            return defaultValue;

        var value = configuration[key];
        return string.IsNullOrEmpty(value) ? defaultValue : value!;
    }

    /// <summary>
    /// Adds or updates a configuration value in memory for the running process.
    /// </summary>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="key">The configuration key (e.g. "Database:TimeoutSeconds").</param>
    /// <param name="value">The new value to set.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or empty.</exception>
    public static void SetValue(this IConfiguration configuration, string key, string? value)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Configuration key cannot be null or whitespace.", nameof(key));

        configuration[key] = value;
    }

    /// <summary>
    /// Removes or unsets a configuration value in memory for the running process by setting it to null.
    /// </summary>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="key">The configuration key to remove.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or empty.</exception>
    public static void RemoveValue(this IConfiguration configuration, string key)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Configuration key cannot be null or whitespace.", nameof(key));

        configuration[key] = null;
    }

    /// <summary>
    /// Opens, modifies, and persists changes (Add, Update, Delete) to a JSON configuration file on disk.
    /// </summary>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="filePath">The path to the JSON file to update.</param>
    /// <param name="configure">The action to modify settings.</param>
    /// <returns>The updated <see cref="Sops.Configuration.Storage.JsonConfigurationFile"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> or <paramref name="configure"/> is null.</exception>
    public static Sops.Configuration.Storage.JsonConfigurationFile UpdateJsonFile(
        this IConfiguration configuration,
        string filePath,
        Action<Sops.Configuration.Storage.JsonConfigurationFile> configure)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        var file = Sops.Configuration.Storage.JsonConfigurationFile.Open(filePath);
        configure(file);
        file.Save();
        return file;
    }
}
