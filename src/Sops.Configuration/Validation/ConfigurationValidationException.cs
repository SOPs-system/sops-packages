using System.Text;

namespace Sops.Configuration.Validation;

/// <summary>
/// Exception thrown when configuration validation fails.
/// Designed to never expose secret values in messages or diagnostic output.
/// </summary>
public sealed class ConfigurationValidationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationValidationException"/> class.
    /// </summary>
    /// <param name="settingsType">The type of the settings object being validated.</param>
    /// <param name="sectionName">The configuration section name, if applicable.</param>
    /// <param name="errors">The list of validation errors.</param>
    public ConfigurationValidationException(
        Type settingsType,
        string? sectionName,
        IReadOnlyList<ConfigurationValidationError> errors)
        : base(FormatMessage(settingsType, sectionName, errors))
    {
        SettingsType = settingsType ?? throw new ArgumentNullException(nameof(settingsType));
        SectionName = sectionName;
        Errors = errors ?? Array.Empty<ConfigurationValidationError>();
    }

    /// <summary>
    /// Gets the settings type that failed validation.
    /// </summary>
    public Type SettingsType { get; }

    /// <summary>
    /// Gets the section name from which settings were bound, or null if bound from root.
    /// </summary>
    public string? SectionName { get; }

    /// <summary>
    /// Gets the list of validation errors.
    /// </summary>
    public IReadOnlyList<ConfigurationValidationError> Errors { get; }

    private static string FormatMessage(
        Type settingsType,
        string? sectionName,
        IReadOnlyList<ConfigurationValidationError> errors)
    {
        var typeName = settingsType?.Name ?? "Settings";
        var sectionInfo = string.IsNullOrEmpty(sectionName) ? string.Empty : $" (Section: '{sectionName}')";
        var errorCount = errors?.Count ?? 0;

        var sb = new StringBuilder();
        sb.Append($"Configuration validation failed for '{typeName}'{sectionInfo} with {errorCount} error(s):");
        if (errors != null)
        {
            foreach (var error in errors)
            {
                sb.AppendLine();
                sb.Append($" - {error}");
            }
        }

        return sb.ToString();
    }
}
