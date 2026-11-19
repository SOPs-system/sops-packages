using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sops.Logging.Extensions;
using Sops.Logging.Models;

namespace Sops.Logging.Tests;

public class SopsLoggingConfigurationTests
{
    [Fact]
    public void Configuration_BindsLoggingSettings_FromIConfiguration()
    {
        var inMemory = new Dictionary<string, string?>
        {
            { "Logging:LogLevel:Default", "Warning" },
            { "Logging:Sops:EnableConsole", "true" },
            { "Logging:Sops:ConsoleFormat", "Simple" },
            { "Logging:Sops:RedactSensitiveData", "true" },
            { "Logging:Sops:IncludeScopes", "false" }
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var services = new ServiceCollection();
        services.AddSopsLogging(config);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<SopsLoggingOptions>>().Value;

        Assert.Equal(LogLevel.Warning, options.MinimumLevel);
        Assert.True(options.EnableConsole);
        Assert.Equal(SopsConsoleFormat.Simple, options.ConsoleFormat);
        Assert.True(options.RedactSensitiveData);
        Assert.False(options.IncludeScopes);
    }

    [Fact]
    public void Configuration_ProgrammaticOptions_OverridesConfigurationSettings()
    {
        var inMemory = new Dictionary<string, string?>
        {
            { "Logging:LogLevel:Default", "Information" },
            { "Logging:Sops:ConsoleFormat", "Json" }
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var services = new ServiceCollection();
        services.AddSopsLogging(config, options =>
        {
            options.ConsoleFormat = SopsConsoleFormat.Simple;
            options.MinimumLevel = LogLevel.Debug;
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<SopsLoggingOptions>>().Value;

        Assert.Equal(LogLevel.Debug, options.MinimumLevel);
        Assert.Equal(SopsConsoleFormat.Simple, options.ConsoleFormat);
    }
}
