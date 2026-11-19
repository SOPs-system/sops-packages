using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sops.Logging.Formatters;
using Sops.Logging.Models;
using Sops.Logging.Redaction;

namespace Sops.Logging.Tests;

public class SopsConsoleFormatterTests
{
    private readonly SopsLogRedactor _redactor = new();

    private IOptionsMonitor<SopsLoggingOptions> CreateOptionsMonitor(SopsLoggingOptions options)
    {
        return new TestOptionsMonitor<SopsLoggingOptions>(options);
    }

    [Fact]
    public void JsonFormatter_OutputsValidStructuredJson()
    {
        var options = new SopsLoggingOptions { ConsoleFormat = SopsConsoleFormat.Json };
        var formatter = new SopsJsonConsoleFormatter(CreateOptionsMonitor(options), _redactor);

        using var writer = new StringWriter();
        var entry = new LogEntry<string>(
            LogLevel.Information,
            "Sops.Orders",
            new EventId(101, "OrderCreated"),
            "Order #12345 created successfully",
            exception: null,
            (state, ex) => state);

        formatter.Write(entry, scopeProvider: null, writer);

        var output = writer.ToString().Trim();
        Assert.NotEmpty(output);

        // Verify valid JSON
        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;

        Assert.Equal("Information", root.GetProperty("LogLevel").GetString());
        Assert.Equal("Sops.Orders", root.GetProperty("Category").GetString());
        Assert.Equal(101, root.GetProperty("EventId").GetInt32());
        Assert.Equal("Order #12345 created successfully", root.GetProperty("Message").GetString());
        Assert.True(root.TryGetProperty("Timestamp", out var timestampProp));
        Assert.NotEmpty(timestampProp.GetString()!);
    }

    [Fact]
    public void JsonFormatter_RedactsSensitiveDataInMessageAndException()
    {
        var options = new SopsLoggingOptions
        {
            ConsoleFormat = SopsConsoleFormat.Json,
            RedactSensitiveData = true
        };
        var formatter = new SopsJsonConsoleFormatter(CreateOptionsMonitor(options), _redactor);

        using var writer = new StringWriter();
        var ex = new InvalidOperationException("Auth failed: Bearer secret_bearer_token_xyz123");
        var entry = new LogEntry<string>(
            LogLevel.Error,
            "Sops.Auth",
            new EventId(500),
            "Failed login with password: MySecretPassword!",
            exception: ex,
            (state, e) => state);

        formatter.Write(entry, scopeProvider: null, writer);

        var output = writer.ToString().Trim();
        Assert.DoesNotContain("MySecretPassword!", output);
        Assert.DoesNotContain("secret_bearer_token_xyz123", output);

        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;
        Assert.Contains("[REDACTED]", root.GetProperty("Message").GetString());
        Assert.Contains("[REDACTED]", root.GetProperty("Exception").GetString());
    }

    [Fact]
    public void SimpleFormatter_OutputsReadableFormat()
    {
        var options = new SopsLoggingOptions { ConsoleFormat = SopsConsoleFormat.Simple };
        var formatter = new SopsSimpleConsoleFormatter(CreateOptionsMonitor(options), _redactor);

        using var writer = new StringWriter();
        var entry = new LogEntry<string>(
            LogLevel.Warning,
            "Sops.Inventory",
            new EventId(200),
            "Inventory synchronization delayed",
            exception: null,
            (state, ex) => state);

        formatter.Write(entry, scopeProvider: null, writer);

        var output = writer.ToString().Trim();
        Assert.Contains("[WRN]", output);
        Assert.Contains("[Sops.Inventory]", output);
        Assert.Contains("Inventory synchronization delayed", output);
    }

    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public TestOptionsMonitor(T currentValue) => CurrentValue = currentValue;
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
