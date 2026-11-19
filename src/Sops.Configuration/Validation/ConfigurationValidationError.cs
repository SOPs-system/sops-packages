namespace Sops.Configuration.Validation;

/// <summary>
/// Represents a validation failure on a configuration property.
/// </summary>
public sealed class ConfigurationValidationError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationValidationError"/> class.
    /// </summary>
    /// <param name="propertyPath">The path or name of the failing property.</param>
    /// <param name="errorMessage">The error description.</param>
    /// <param name="isSensitive">Whether the property is sensitive.</param>
    public ConfigurationValidationError(string propertyPath, string errorMessage, bool isSensitive = false)
    {
        PropertyPath = propertyPath ?? string.Empty;
        ErrorMessage = errorMessage ?? "Validation failed.";
        IsSensitive = isSensitive;
    }

    /// <summary>
    /// Gets the property path (e.g. "Database:ConnectionString" or "Timeout").
    /// </summary>
    public string PropertyPath { get; }

    /// <summary>
    /// Gets the error description. This message is sanitized and never exposes secret values.
    /// </summary>
    public string ErrorMessage { get; }

    /// <summary>
    /// Gets a value indicating whether the property represents sensitive data.
    /// </summary>
    public bool IsSensitive { get; }

    /// <inheritdoc />
    public override string ToString() =>
        string.IsNullOrEmpty(PropertyPath)
            ? ErrorMessage
            : $"{PropertyPath}: {ErrorMessage}";
}
