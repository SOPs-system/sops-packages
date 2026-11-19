using System.Reflection;
using Sops.Configuration.Models;

namespace Sops.Configuration.Validation;

/// <summary>
/// Utility for identifying sensitive configuration properties and redacting secret values from diagnostic messages.
/// </summary>
public static class SecretMasker
{
    /// <summary>
    /// The standard replacement string for redacted sensitive values.
    /// </summary>
    public const string RedactedPlaceholder = "[REDACTED]";

    private static readonly string[] SensitiveKeywords = new[]
    {
        "password",
        "secret",
        "apikey",
        "token",
        "privatekey",
        "connectionstring",
        "credential",
        "cert",
        "certificate",
        "accesskey",
        "clientsecret",
        "authkey"
    };

    /// <summary>
    /// Determines whether a property name indicates sensitive content.
    /// </summary>
    /// <param name="propertyName">The property or key name.</param>
    /// <returns>True if the name matches sensitive patterns; otherwise, false.</returns>
    public static bool IsSensitivePropertyName(string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
            return false;

        var normalized = propertyName!.Replace("-", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
        for (var i = 0; i < SensitiveKeywords.Length; i++)
        {
            if (normalized.Contains(SensitiveKeywords[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether a <see cref="PropertyInfo"/> represents a sensitive property.
    /// </summary>
    /// <param name="property">The property to check.</param>
    /// <returns>True if marked with <see cref="SensitiveAttribute"/> or having a sensitive name; otherwise, false.</returns>
    public static bool IsSensitive(PropertyInfo property)
    {
        if (property == null)
            throw new ArgumentNullException(nameof(property));

        if (property.IsDefined(typeof(SensitiveAttribute), inherit: true))
            return true;

        return IsSensitivePropertyName(property.Name);
    }

    /// <summary>
    /// Returns the standard masked representation of a secret value.
    /// </summary>
    /// <param name="value">The secret value to mask.</param>
    /// <returns>The redacted placeholder string.</returns>
    public static string Mask(string? value) => RedactedPlaceholder;

    /// <summary>
    /// Redacts known secret values from an error message if present.
    /// </summary>
    /// <param name="errorMessage">The error message to sanitize.</param>
    /// <param name="rawSecretValue">The secret value to redact.</param>
    /// <returns>The sanitized message.</returns>
    public static string RedactValueFromMessage(string errorMessage, string? rawSecretValue)
    {
        if (string.IsNullOrEmpty(errorMessage) || string.IsNullOrEmpty(rawSecretValue))
            return errorMessage;

        // Only redact if the secret value has meaningful length to avoid replacing single characters
        if (rawSecretValue!.Length < 2)
            return errorMessage;

        return errorMessage.Replace(rawSecretValue, RedactedPlaceholder);
    }
}
