using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sops.Configuration;
using Sops.Logging.Extensions;

namespace Sops.Logging.Tests;

public class SopsLoggingIntegrationTests
{
    public class Program { }

    [Fact]
    public void Integration_MatchesUserSnippet_ResolvesAndLogsSuccessfully()
    {
        var configuration = SopsConfiguration.CreateDefault(
            environment: "Production",
            basePath: AppContext.BaseDirectory);

        var services = new ServiceCollection();
        services.AddSopsLogging(configuration);

        using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<Program>>();

        Assert.NotNull(logger);

        // Execute without exception
        logger.LogInformation("SOPs service started");
        logger.LogWarning("Inventory synchronization delayed");
    }

    [Fact]
    public void Integration_WithScopes_ExecutesWithoutError()
    {
        var configuration = SopsConfiguration.CreateDefault(
            environment: "Development",
            basePath: AppContext.BaseDirectory);

        var services = new ServiceCollection();
        services.AddSopsLogging(configuration, opt =>
        {
            opt.IncludeScopes = true;
        });

        using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<Program>>();

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = Guid.NewGuid().ToString("N"),
            ["TenantId"] = "tenant-001"
        }))
        {
            logger.LogInformation("Executing scoped operation");
        }
    }
}
