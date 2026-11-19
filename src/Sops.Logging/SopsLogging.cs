using Microsoft.Extensions.Logging;
using Sops.Logging.Extensions;
using Sops.Logging.Models;

namespace Sops.Logging;

/// <summary>
/// Static entry point providing standalone logger creation without requiring a full DI service container.
/// </summary>
public static class SopsLogging
{
    /// <summary>
    /// Creates a standalone <see cref="ILoggerFactory"/> configured with SOPs structured logging and redaction.
    /// </summary>
    /// <param name="configure">Optional delegate to customize logging options.</param>
    /// <returns>A new <see cref="ILoggerFactory"/> instance.</returns>
    public static ILoggerFactory CreateLoggerFactory(Action<SopsLoggingOptions>? configure = null)
    {
        return LoggerFactory.Create(builder =>
        {
            builder.AddSopsLogging(configuration: null, configure);
        });
    }

    /// <summary>
    /// Creates a standalone typed logger instance for <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type category for the logger.</typeparam>
    /// <param name="configure">Optional delegate to customize logging options.</param>
    /// <returns>A configured <see cref="ILogger{T}"/> instance.</returns>
    public static ILogger<T> CreateLogger<T>(Action<SopsLoggingOptions>? configure = null)
    {
        var factory = CreateLoggerFactory(configure);
        return factory.CreateLogger<T>();
    }

    /// <summary>
    /// Creates a standalone logger instance with the specified category name.
    /// </summary>
    /// <param name="categoryName">The category name for the logger.</param>
    /// <param name="configure">Optional delegate to customize logging options.</param>
    /// <returns>A configured <see cref="ILogger"/> instance.</returns>
    public static ILogger CreateLogger(string categoryName, Action<SopsLoggingOptions>? configure = null)
    {
        var factory = CreateLoggerFactory(configure);
        return factory.CreateLogger(categoryName);
    }
}
