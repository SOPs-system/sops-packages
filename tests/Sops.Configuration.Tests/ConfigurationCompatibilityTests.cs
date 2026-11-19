using System.Runtime.Versioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sops.Configuration.Extensions;
using Sops.Configuration.Models;

namespace Sops.Configuration.Tests;

public class ConfigurationCompatibilityTests
{
    [Fact]
    public void Compatibility_AssemblyTargetsNetStandard20()
    {
        var assembly = typeof(SopsConfiguration).Assembly;
        var targetFrameworkAttr = assembly.GetCustomAttributes(typeof(TargetFrameworkAttribute), inherit: false)
            .Cast<TargetFrameworkAttribute>()
            .FirstOrDefault();

        Assert.NotNull(targetFrameworkAttr);
        Assert.Equal(".NETStandard,Version=v2.0", targetFrameworkAttr.FrameworkName);
    }

    [Fact]
    public void Compatibility_CanBeConsumedStandalone_WithoutDependencyInjection()
    {
        // Direct basic consumption using only core configuration abstractions
        var inMemory = new Dictionary<string, string?>
        {
            { "Database:ConnectionString", "Server=standalone.local;" },
            { "Database:MaxPoolSize", "15" }
        };

        var config = SopsConfiguration.CreateBuilder(environment: "Production")
            .AddInMemoryCollection(inMemory)
            .Build();

        var settings = config.GetValid<TestDatabaseSettings>("Database");

        Assert.NotNull(settings);
        Assert.Equal("Server=standalone.local;", settings.ConnectionString);
        Assert.Equal(15, settings.MaxPoolSize);
        Assert.Equal(30, settings.TimeoutSeconds); // default
    }

    [Fact]
    public void Compatibility_CanBeConsumedWithDependencyInjection()
    {
        var inMemory = new Dictionary<string, string?>
        {
            { "Database:ConnectionString", "Server=di.local;" },
            { "Database:MaxPoolSize", "40" }
        };

        var config = SopsConfiguration.CreateBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddSopsSettings<TestDatabaseSettings>("Database", validateOnStart: true);

        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<TestDatabaseSettings>();

        Assert.NotNull(resolved);
        Assert.Equal("Server=di.local;", resolved.ConnectionString);
        Assert.Equal(40, resolved.MaxPoolSize);
    }

    [Fact]
    public void Compatibility_EnvironmentConventions_AreSupported()
    {
        Assert.Equal("Development", SopsEnvironments.Development);
        Assert.Equal("Testing", SopsEnvironments.Testing);
        Assert.Equal("Staging", SopsEnvironments.Staging);
        Assert.Equal("Production", SopsEnvironments.Production);

        Assert.True(SopsEnvironments.IsDevelopment("development"));
        Assert.True(SopsEnvironments.IsDevelopment("Development"));
        Assert.True(SopsEnvironments.IsTesting("Testing"));
        Assert.True(SopsEnvironments.IsTesting("test"));
        Assert.True(SopsEnvironments.IsStaging("staging"));
        Assert.True(SopsEnvironments.IsStaging("stage"));
        Assert.True(SopsEnvironments.IsProduction("production"));
        Assert.True(SopsEnvironments.IsProduction("prod"));

        Assert.Equal(SopsEnvironments.Production, SopsEnvironments.Normalize(null));
        Assert.Equal(SopsEnvironments.Production, SopsEnvironments.Normalize("   "));
        Assert.Equal(SopsEnvironments.Development, SopsEnvironments.Normalize("dev")); // Non-standard keeps trimmed unless matched
    }

    [Fact]
    public void Compatibility_ResolveEnvironment_PrefersExplicit_ThenSops_ThenDotNet_ThenAspNet()
    {
        // 1. Explicit override always wins
        Assert.Equal(SopsEnvironments.Staging, SopsConfiguration.ResolveEnvironment("Staging"));

        // 2. SOPS_ENVIRONMENT
        var origSops = Environment.GetEnvironmentVariable("SOPS_ENVIRONMENT");
        var origDotNet = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        var origAspNet = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        try
        {
            Environment.SetEnvironmentVariable("SOPS_ENVIRONMENT", "Testing");
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Staging");

            Assert.Equal(SopsEnvironments.Testing, SopsConfiguration.ResolveEnvironment());

            Environment.SetEnvironmentVariable("SOPS_ENVIRONMENT", null);
            Assert.Equal(SopsEnvironments.Development, SopsConfiguration.ResolveEnvironment());

            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", null);
            Assert.Equal(SopsEnvironments.Staging, SopsConfiguration.ResolveEnvironment());

            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
            Assert.Equal(SopsEnvironments.Production, SopsConfiguration.ResolveEnvironment());
        }
        finally
        {
            Environment.SetEnvironmentVariable("SOPS_ENVIRONMENT", origSops);
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", origDotNet);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", origAspNet);
        }
    }
}
