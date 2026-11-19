using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Sops.Logging.Abstractions;
using Sops.Logging.Formatters;
using Sops.Logging.Models;
using Sops.Logging.Redaction;

namespace Sops.Logging.Extensions;

/// <summary>
/// Extension methods for <see cref="ILoggingBuilder"/> to configure SOPs structured logging.
/// </summary>
public static class SopsLoggingBuilderExtensions
{
    /// <summary>
    /// Configures SOPs structured logging on the logging builder.
    /// </summary>
    /// <param name="builder">The logging builder.</param>
    /// <param name="configuration">Optional configuration instance to bind logging settings from.</param>
    /// <param name="configure">Optional delegate to configure logging options.</param>
    /// <returns>The logging builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public static ILoggingBuilder AddSopsLogging(
        this ILoggingBuilder builder,
        IConfiguration? configuration = null,
        Action<SopsLoggingOptions>? configure = null)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));

        var options = new SopsLoggingOptions();

        // 1. Bind options from configuration if provided
        if (configuration != null)
        {
            var sopsSection = configuration.GetSection("Logging:Sops");
            if (sopsSection.Exists())
            {
                sopsSection.Bind(options);
            }

            var defaultLogLevel = configuration["Logging:LogLevel:Default"];
            if (!string.IsNullOrEmpty(defaultLogLevel) && Enum.TryParse<LogLevel>(defaultLogLevel, ignoreCase: true, out var parsedLevel))
            {
                options.MinimumLevel = parsedLevel;
            }
        }

        // 2. Apply programmatic options override
        configure?.Invoke(options);

        // 3. Register options
        builder.Services.Configure<SopsLoggingOptions>(opt =>
        {
            opt.MinimumLevel = options.MinimumLevel;
            opt.EnableConsole = options.EnableConsole;
            opt.ConsoleFormat = options.ConsoleFormat;
            opt.RedactSensitiveData = options.RedactSensitiveData;
            opt.IncludeScopes = options.IncludeScopes;
            opt.UseUtcTimestamp = options.UseUtcTimestamp;
            opt.CustomRedactor = options.CustomRedactor;
            foreach (var p in options.AdditionalRedactionPatterns)
            {
                opt.AdditionalRedactionPatterns.Add(p);
            }
        });

        // 4. Register Redactor
        if (options.CustomRedactor != null)
        {
            builder.Services.TryAddSingleton<ISopsLogRedactor>(options.CustomRedactor);
        }
        else
        {
            builder.Services.TryAddSingleton<ISopsLogRedactor>(_ => new SopsLogRedactor(options.AdditionalRedactionPatterns));
        }

        // 5. Configure Minimum Level & Configuration filters
        builder.SetMinimumLevel(options.MinimumLevel);
        if (configuration != null)
        {
            builder.AddConfiguration(configuration.GetSection("Logging"));
        }

        // 6. Configure Console Logging
        if (options.EnableConsole)
        {
            builder.AddConsoleFormatter<SopsJsonConsoleFormatter, ConsoleFormatterOptions>();
            builder.AddConsoleFormatter<SopsSimpleConsoleFormatter, ConsoleFormatterOptions>();

            var formatterName = options.ConsoleFormat == SopsConsoleFormat.Simple
                ? SopsSimpleConsoleFormatter.FormatterName
                : SopsJsonConsoleFormatter.FormatterName;

            builder.AddConsole(consoleOptions =>
            {
                consoleOptions.FormatterName = formatterName;
            });
        }

        return builder;
    }
}
