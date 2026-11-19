using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Sops.Logging.Abstractions;
using Sops.Logging.Models;

namespace Sops.Logging.Formatters;

/// <summary>
/// Structured JSON console formatter designed for production and cloud log aggregators.
/// Automatically applies sensitive data redaction to messages, exceptions, and scopes.
/// </summary>
public sealed class SopsJsonConsoleFormatter : ConsoleFormatter
{
    /// <summary>
    /// The unique formatter name registered with the console logger provider.
    /// </summary>
    public const string FormatterName = "SopsJson";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    private readonly IOptionsMonitor<SopsLoggingOptions> _options;
    private readonly ISopsLogRedactor _redactor;

    /// <summary>
    /// Initializes a new instance of the <see cref="SopsJsonConsoleFormatter"/> class.
    /// </summary>
    /// <param name="options">The logging options monitor.</param>
    /// <param name="redactor">The sensitive data redactor.</param>
    public SopsJsonConsoleFormatter(
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

        // Apply redaction if enabled
        if (options.RedactSensitiveData)
        {
            message = _redactor.Redact(message);
        }

        var timestamp = options.UseUtcTimestamp
            ? DateTime.UtcNow.ToString("o")
            : DateTime.Now.ToString("o");

        string? exceptionString = null;
        if (logEntry.Exception != null)
        {
            exceptionString = logEntry.Exception.ToString();
            if (options.RedactSensitiveData)
            {
                exceptionString = _redactor.Redact(exceptionString);
            }
        }

        // Collect scopes if enabled
        Dictionary<string, object?>? scopes = null;
        if (options.IncludeScopes && scopeProvider != null)
        {
            scopes = new Dictionary<string, object?>();
            scopeProvider.ForEachScope((scope, state) =>
            {
                if (scope is IEnumerable<KeyValuePair<string, object?>> keyValues)
                {
                    foreach (var kv in keyValues)
                    {
                        var val = kv.Value?.ToString();
                        if (options.RedactSensitiveData && val != null)
                        {
                            val = _redactor.Redact(val);
                        }
                        state[kv.Key] = val;
                    }
                }
                else if (scope != null)
                {
                    var scopeStr = scope.ToString();
                    if (options.RedactSensitiveData)
                    {
                        scopeStr = _redactor.Redact(scopeStr);
                    }
                    state[$"Scope_{state.Count}"] = scopeStr;
                }
            }, scopes);
        }

        var payload = new Dictionary<string, object?>
        {
            ["Timestamp"] = timestamp,
            ["LogLevel"] = logEntry.LogLevel.ToString(),
            ["Category"] = logEntry.Category,
            ["EventId"] = logEntry.EventId.Id,
            ["Message"] = message
        };

        if (scopes != null && scopes.Count > 0)
        {
            payload["Scopes"] = scopes;
        }

        if (!string.IsNullOrEmpty(exceptionString))
        {
            payload["Exception"] = exceptionString;
        }

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        textWriter.WriteLine(json);
    }
}
