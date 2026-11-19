using Microsoft.Extensions.Logging;
using Sops.Logging.Abstractions;

namespace Sops.Logging.Models;

/// <summary>
/// Configuration options for SOPs structured logging and redaction.
/// </summary>
public sealed class SopsLoggingOptions
{
    /// <summary>
    /// Gets or sets the default minimum log level. Defaults to <see cref="LogLevel.Information"/>.
    /// </summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Gets or sets whether console logging is enabled. Defaults to true.
    /// </summary>
    public bool EnableConsole { get; set; } = true;

    /// <summary>
    /// Gets or sets the console log output format. Defaults to <see cref="SopsConsoleFormat.Json"/>.
    /// </summary>
    public SopsConsoleFormat ConsoleFormat { get; set; } = SopsConsoleFormat.Json;

    /// <summary>
    /// Gets or sets whether automated redaction of sensitive data is enabled. Defaults to true.
    /// </summary>
    public bool RedactSensitiveData { get; set; } = true;

    /// <summary>
    /// Gets or sets whether logging scopes (e.g. CorrelationId, TraceId) are included in output. Defaults to true.
    /// </summary>
    public bool IncludeScopes { get; set; } = true;

    /// <summary>
    /// Gets or sets whether timestamps should be rendered in UTC format. Defaults to true.
    /// </summary>
    public bool UseUtcTimestamp { get; set; } = true;

    /// <summary>
    /// Gets or sets a custom redactor instance, or null to use the default <see cref="Redaction.SopsLogRedactor"/>.
    /// </summary>
    public ISopsLogRedactor? CustomRedactor { get; set; }

    /// <summary>
    /// Gets the list of additional regular expression patterns to redact from log messages.
    /// </summary>
    public IList<string> AdditionalRedactionPatterns { get; } = new List<string>();
}
