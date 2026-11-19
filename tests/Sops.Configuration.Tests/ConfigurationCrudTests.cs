using Microsoft.Extensions.Configuration;
using Sops.Configuration.Extensions;
using Sops.Configuration.Storage;

namespace Sops.Configuration.Tests;

public class ConfigurationCrudTests : IDisposable
{
    private readonly string _tempDirectory;

    public ConfigurationCrudTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "SopsCrudTests_" + Guid.NewGuid().ToString("N"));
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
    public void InMemory_AddAndUpdate_ModifiesConfigurationValues()
    {
        var inMemory = new Dictionary<string, string?>
        {
            { "App:Name", "OriginalApp" }
        };

        var config = SopsConfiguration.CreateBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        Assert.Equal("OriginalApp", config["App:Name"]);

        // 1. Update existing value
        config.SetValue("App:Name", "UpdatedApp");
        Assert.Equal("UpdatedApp", config["App:Name"]);

        // 2. Add new value
        config.SetValue("App:Version", "2.1.0");
        Assert.Equal("2.1.0", config["App:Version"]);
    }

    [Fact]
    public void InMemory_Delete_RemovesConfigurationValue()
    {
        var inMemory = new Dictionary<string, string?>
        {
            { "Feature:BetaFlag", "true" },
            { "Feature:Timeout", "30" }
        };

        var config = SopsConfiguration.CreateBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        Assert.Equal("true", config["Feature:BetaFlag"]);

        // Delete key
        config.RemoveValue("Feature:BetaFlag");
        Assert.Null(config["Feature:BetaFlag"]);

        // Other keys unaffected
        Assert.Equal("30", config["Feature:Timeout"]);
    }

    [Fact]
    public void JsonFile_AddReadUpdateDelete_OperatesCorrectly()
    {
        var filePath = Path.Combine(_tempDirectory, "test_settings.json");

        // 1. Open and Add initial values
        var file = JsonConfigurationFile.Open(filePath);
        file.Set("ServiceName", "AuthService")
            .Set("Database:Host", "db.example.com")
            .Set("Database:Port", 5432)
            .Set("Database:Enabled", true);

        file.Save();
        Assert.True(File.Exists(filePath));

        // 2. Read values
        var reloadedFile = JsonConfigurationFile.Open(filePath);
        Assert.Equal("AuthService", reloadedFile.Read("ServiceName"));
        Assert.Equal("db.example.com", reloadedFile.Read("Database:Host"));
        Assert.Equal("5432", reloadedFile.Read("Database:Port"));
        Assert.Equal("true", reloadedFile.Read("Database:Enabled"));

        // 3. Update existing value and add nested value
        reloadedFile.Set("Database:Port", 5433)
                    .Set("Database:Credentials:Username", "dbadmin");
        reloadedFile.Save();

        var updatedFile = JsonConfigurationFile.Open(filePath);
        Assert.Equal("5433", updatedFile.Read("Database:Port"));
        Assert.Equal("dbadmin", updatedFile.Read("Database:Credentials:Username"));

        // 4. Delete / Remove value
        var removed = updatedFile.Remove("Database:Credentials:Username");
        Assert.True(removed);
        updatedFile.Save();

        var verifiedFile = JsonConfigurationFile.Open(filePath);
        Assert.Null(verifiedFile.Read("Database:Credentials:Username"));
        Assert.Equal("5433", verifiedFile.Read("Database:Port"));
    }

    [Fact]
    public void Configuration_UpdateJsonFile_PersistsChangesAndCanBeReloaded()
    {
        var filePath = Path.Combine(_tempDirectory, "appsettings.json");
        File.WriteAllText(filePath, @"{ ""AppTitle"": ""InitialTitle"", ""Counter"": 1 }");

        var config = SopsConfiguration.CreateBuilder(basePath: _tempDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        Assert.Equal("InitialTitle", config["AppTitle"]);

        // Use extension method to update JSON file on disk
        config.UpdateJsonFile(filePath, editor =>
        {
            editor.Set("AppTitle", "UpdatedTitle")
                  .Set("Counter", 42)
                  .Set("NewSection:Setting", "DynamicValue");
        });

        // Load fresh configuration from the updated file
        var reloadedConfig = SopsConfiguration.CreateBuilder(basePath: _tempDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        Assert.Equal("UpdatedTitle", reloadedConfig["AppTitle"]);
        Assert.Equal("42", reloadedConfig["Counter"]);
        Assert.Equal("DynamicValue", reloadedConfig["NewSection:Setting"]);
    }
}
