using Microsoft.Extensions.Configuration;
using Sops.Configuration.Extensions;

namespace Sops.Configuration.Tests;

public class ConfigurationPrecedenceTests : IDisposable
{
    private readonly string _tempDirectory;

    public ConfigurationPrecedenceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "SopsPrecedenceTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public void Precedence_DefaultOrder_BaseJson_OverriddenBy_EnvJson()
    {
        File.WriteAllText(Path.Combine(_tempDirectory, "appsettings.json"), "{\"TargetKey\": \"BaseJson\"}");
        File.WriteAllText(Path.Combine(_tempDirectory, "appsettings.Staging.json"), "{\"TargetKey\": \"EnvJson\"}");

        var config = SopsConfiguration.CreateDefault(
            environment: "Staging",
            basePath: _tempDirectory,
            options =>
            {
                options.IncludeEnvironmentVariables = false;
            });

        Assert.Equal("EnvJson", config["TargetKey"]);
    }

    [Fact]
    public void Precedence_DefaultOrder_EnvJson_OverriddenBy_EnvironmentVariables()
    {
        File.WriteAllText(Path.Combine(_tempDirectory, "appsettings.json"), "{\"Service:Key\": \"BaseJson\"}");
        File.WriteAllText(Path.Combine(_tempDirectory, "appsettings.Production.json"), "{\"Service:Key\": \"EnvJson\"}");

        var envVarKey = "SOPS_Service__Key";
        Environment.SetEnvironmentVariable(envVarKey, "EnvVarValue");

        try
        {
            var config = SopsConfiguration.CreateDefault(
                environment: "Production",
                basePath: _tempDirectory,
                options =>
                {
                    options.IncludeEnvironmentVariables = true;
                    options.EnvironmentVariablesPrefix = "SOPS_";
                });

            Assert.Equal("EnvVarValue", config["Service:Key"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarKey, null);
        }
    }

    [Fact]
    public void Precedence_DefaultOrder_EnvironmentVariables_OverriddenBy_InMemory()
    {
        var envVarKey = "SOPS_PRECTEST";
        Environment.SetEnvironmentVariable(envVarKey, "FromEnv");

        try
        {
            var inMemoryOverrides = new Dictionary<string, string?>
            {
                { "PRECTEST", "FromInMemory" }
            };

            var config = SopsConfiguration.CreateDefault(
                environment: "Production",
                basePath: _tempDirectory,
                options =>
                {
                    options.IncludeEnvironmentVariables = true;
                    options.EnvironmentVariablesPrefix = "SOPS_";
                    options.InMemoryOverrides = inMemoryOverrides;
                });

            Assert.Equal("FromInMemory", config["PRECTEST"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarKey, null);
        }
    }

    [Fact]
    public void Precedence_CustomRegistrationOrder_LastRegisteredSourceWins()
    {
        var source1 = new Dictionary<string, string?> { { "Conflict", "First" } };
        var source2 = new Dictionary<string, string?> { { "Conflict", "Second" } };
        var source3 = new Dictionary<string, string?> { { "Conflict", "Third" } };

        var config = SopsConfiguration.CreateBuilder()
            .AddInMemoryCollection(source1)
            .AddInMemoryCollection(source2)
            .AddInMemoryCollection(source3)
            .Build();

        Assert.Equal("Third", config["Conflict"]);
    }
}
