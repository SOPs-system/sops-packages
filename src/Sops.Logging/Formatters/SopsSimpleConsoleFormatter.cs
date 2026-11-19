using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Sops.Logging.Abstractions;
using Sops.Logging.Models;

namespace Sops.Logging.Formatters;

/// <summary>
/// Human-readable console formatter for local development, debugging, and testing.
/// Includes automatic sensitive data redaction.
/// </summary>
public sealed class SopsSimpleConsoleFormatter : ConsoleFormatter
{
    /// <summary>
    /// The unique formatter name registered with the console logger provider.
    /// </summary>
    public const string FormatterName = "SopsSimple";

    private readonly IOptionsMonitor<SopsLoggingOptions> _options;
    private readonly ISopsLogRedactor _redactor;

    /// <summary>
    /// Initializes a new instance of the <see cref="SopsSimpleConsoleFormatter"/> class.
    /// </summary>
    /// <param name="options">The logging options monitor.</param>
    /// <param name="redactor">The sensitive data redactor.</param>
    public SopsSimpleConsoleFormatter(
        IOptionsMonitor<SopsLoggingOptions> options,
        ISopsLogRedactor redactor) : base(FormatterName)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
    }

    /// <inheritdoc />
    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        var options = _options.CurrentValue;
        var message = logEntry.Formatter(logEntry.State, logEntry.Exception);

        if (string.IsNullOrEmpty(message) && logEntry.Exception == null)
        {
            return;
        }

        if (options.RedactSensitiveData)
        {
            message = _redactor.Redact(message);
        }

        var timestamp = options.UseUtcTimestamp
            ? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff UTC")
            : DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

        var levelAbbreviation = GetLogLevelString(logEntry.LogLevel);

        textWriter.Write($"[{timestamp}] [{levelAbbreviation}] [{logEntry.Category}] {message}");

        if (logEntry.Exception != null)
        {
            var exText = logEntry.Exception.ToString();
            if (options.RedactSensitiveData)
            {
                exText = _redactor.Redact(exText);
            }
            textWriter.WriteLine();
            textWriter.Write($"Exception: {exText}");
        }

        textWriter.WriteLine();
    }

    private static string GetLogLevelString(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "???"
    };
}
