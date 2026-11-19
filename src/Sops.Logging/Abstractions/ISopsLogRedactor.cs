namespace Sops.Logging.Abstractions;

/// <summary>
/// Defines a contract for redacting sensitive customer data, credentials, and secrets from log messages.
/// </summary>
public interface ISopsLogRedactor
{
    /// <summary>
    /// Redacts sensitive information from the specified text.
    /// </summary>
    /// <param name="text">The raw log message or text.</param>
    /// <returns>The sanitized text with sensitive information masked.</returns>
    string Redact(string? text);
}
