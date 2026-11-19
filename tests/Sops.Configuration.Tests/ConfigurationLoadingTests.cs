using Microsoft.Extensions.Configuration;
using Sops.Configuration.Extensions;

namespace Sops.Configuration.Tests;

public class ConfigurationLoadingTests : IDisposable
{
    private readonly string _tempDirectory;

    public ConfigurationLoadingTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "SopsConfigTests_" + Guid.NewGuid().ToString("N"));
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
            // Ignore cleanup errors in temp directory
        }
    }

    [Fact]
    public void Load_FromJsonFile_PopulatesConfiguration()
    {
        var jsonContent = @"{
            ""ApplicationName"": ""SopsTestApp"",
            ""Database"": {
                ""ConnectionString"": ""Server=localhost;Database=TestDb;"",
                ""MaxPoolSize"": 25
            }
        }";
        var jsonPath = Path.Combine(_tempDirectory, "appsettings.json");
        File.WriteAllText(jsonPath, jsonContent);

        var config = SopsConfiguration.CreateBuilder(basePath: _tempDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        Assert.Equal("SopsTestApp", config["ApplicationName"]);
        Assert.Equal("Server=localhost;Database=TestDb;", config["Database:ConnectionString"]);
        Assert.Equal("25", config["Database:MaxPoolSize"]);
    }

    [Fact]
    public void Load_FromEnvironmentSpecificJson_OverridesBaseJson()
    {
        var baseJson = @"{
            ""AppName"": ""BaseApp"",
            ""EnvironmentName"": ""Base"",
            ""Database"": { ""TimeoutSeconds"": 10 }
        }";
        var devJson = @"{
            ""EnvironmentName"": ""DevelopmentOverride"",
            ""Database"": { ""TimeoutSeconds"": 60 }
        }";

        File.WriteAllText(Path.Combine(_tempDirectory, "appsettings.json"), baseJson);
        File.WriteAllText(Path.Combine(_tempDirectory, "appsettings.Development.json"), devJson);

        var config = SopsConfiguration.CreateBuilder(environment: "Development", basePath: _tempDirectory)
            .AddEnvironmentSpecificJsonFile("appsettings.json", optional: false)
            .Build();

        Assert.Equal("BaseApp", config["AppName"]); // Kept from base
        Assert.Equal("DevelopmentOverride", config["EnvironmentName"]); // Overridden
        Assert.Equal("60", config["Database:TimeoutSeconds"]); // Overridden
    }

    [Fact]
    public void Load_FromEnvironmentVariables_PopulatesConfiguration()
    {
        var envVarKey = "SOPS_TEST_SETTING_" + Guid.NewGuid().ToString("N").Substring(0, 6);
        Environment.SetEnvironmentVariable(envVarKey, "CustomEnvValue");

        try
        {
            var config = SopsConfiguration.CreateBuilder()
                .AddEnvironmentVariables("SOPS_TEST_")
                .Build();

            var settingSuffix = envVarKey.Substring("SOPS_TEST_".Length);
            Assert.Equal("CustomEnvValue", config[settingSuffix]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarKey, null);
        }
    }

    [Fact]
    public void Load_FromInMemoryCollection_PopulatesConfiguration()
    {
        var inMemory = new Dictionary<string, string?>
        {
            { "InMemoryKey", "InMemoryValue" },
            { "Nested:Key", "NestedValue" }
        };

        var config = SopsConfiguration.CreateBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        Assert.Equal("InMemoryValue", config["InMemoryKey"]);
        Assert.Equal("NestedValue", config["Nested:Key"]);
    }

    [Fact]
    public void Load_OptionalFile_DoesNotExist_DoesNotThrow()
    {
        var config = SopsConfiguration.CreateBuilder(basePath: _tempDirectory)
            .AddJsonFile("nonexistent.json", optional: true)
            .Build();

        Assert.NotNull(config);
    }

    [Fact]
    public void Load_RequiredFile_DoesNotExist_ThrowsFileNotFound()
    {
        Assert.Throws<FileNotFoundException>(() =>
        {
            SopsConfiguration.CreateBuilder(basePath: _tempDirectory)
                .AddJsonFile("nonexistent_required.json", optional: false)
                .Build();
        });
    }

    [Fact]
    public void Load_NestedConfigurationSections_CanBeRetrieved()
    {
        var inMemory = new Dictionary<string, string?>
        {
            { "Services:Auth:Endpoints:Login", "https://auth.example.com/login" },
            { "Services:Auth:Endpoints:Logout", "https://auth.example.com/logout" },
            { "Services:Auth:Timeout", "15" }
        };

        var config = SopsConfiguration.CreateBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var authSection = config.GetSection("Services:Auth");
        Assert.True(authSection.Exists());
        Assert.Equal("15", authSection["Timeout"]);

        var endpointsSection = authSection.GetSection("Endpoints");
        Assert.Equal("https://auth.example.com/login", endpointsSection["Login"]);
        Assert.Equal("https://auth.example.com/logout", endpointsSection["Logout"]);
    }

    [Fact]
    public void Load_CancellationAndDisposal_TokenRegistrationCanBeDisposedCleanly()
    {
        var config = SopsConfiguration.CreateBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "Key", "Val" } })
            .Build();

        using var cts = new CancellationTokenSource();
        var callbackFired = false;

        var token = config.GetReloadToken();
        using (token.RegisterChangeCallback(_ => callbackFired = true, null))
        {
            // Registration is active
            Assert.False(callbackFired);
            cts.Cancel(); // Cancellation triggered
        }

        // Configuration root disposal
        if (config is IDisposable disposable)
        {
            disposable.Dispose();
        }

        Assert.False(callbackFired);
    }
}
