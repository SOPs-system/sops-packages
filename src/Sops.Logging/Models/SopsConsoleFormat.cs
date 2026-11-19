namespace Sops.Logging.Models;

/// <summary>
/// Supported console log formatting styles.
/// </summary>
public enum SopsConsoleFormat
{
    /// <summary>
    /// Structured JSON formatting. Standard for containerized, cloud, and production environments.
    /// </summary>
    Json,

    /// <summary>
    /// Developer-friendly colored text formatting for local development and testing.
    /// </summary>
    Simple
}
