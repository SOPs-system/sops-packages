using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sops.Configuration.Extensions;
using Sops.Configuration.Validation;

namespace Sops.Configuration.Tests;

public class ConfigurationValidationTests
{
    [Fact]
    public void Validation_MissingRequiredSetting_ThrowsConfigurationValidationException()
    {
        var inMemory = new Dictionary<string, string?>
        {
            // ConnectionString is omitted
            { "Database:MaxPoolSize", "20" }
        };

        var config = SopsConfiguration.CreateBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var ex = Assert.Throws<ConfigurationValidationException>(() =>
        {
            config.GetValid<TestDatabaseSettings>("Database");
        });

        Assert.Equal("Database", ex.SectionName);
        Assert.Contains("ConnectionString", ex.Message);
        Assert.Single(ex.Errors);
        Assert.Equal("ConnectionString", ex.Errors[0].PropertyPath);
    }

    [Fact]
    public void Validation_InvalidValue_RangeViolation_ThrowsConfigurationValidationException()
    {
        var inMemory = new Dictionary<string, string?>
        {
            { "Database:ConnectionString", "Server=localhost;" },
            { "Database:MaxPoolSize", "999" } // Range is 1..100
        };

        var config = SopsConfiguration.CreateBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var ex = Assert.Throws<ConfigurationValidationException>(() =>
        {
            config.GetValid<TestDatabaseSettings>("Database");
        });

        Assert.Contains("MaxPoolSize", ex.Message);
    }

    [Fact]
    public void Validation_DefaultValues_PreservedWhenNotConfigured()
    {
        var inMemory = new Dictionary<string, string?>
        {
            { "Database:ConnectionString", "Server=localhost;" }
            // MaxPoolSize and TimeoutSeconds are omitted
        };

        var config = SopsConfiguration.CreateBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var settings = config.GetValid<TestDatabaseSettings>("Database");

        Assert.Equal("Server=localhost;", settings.ConnectionString);
        Assert.Equal(10, settings.MaxPoolSize); // Default is 10
        Assert.Equal(30, settings.TimeoutSeconds); // Default is 30
    }

    [Fact]
    public void Validation_ConfigureDefaultsAction_AppliesDefaultsBeforeBinding()
    {
        var inMemory = new Dictionary<string, string?>
        {
            { "Database:ConnectionString", "Server=localhost;" }
        };

        var config = SopsConfiguration.CreateBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var settings = config.GetValid<TestDatabaseSettings>("Database", defaults =>
        {
            defaults.TimeoutSeconds = 90;
        });

        Assert.Equal(90, settings.TimeoutSeconds);
    }

    [Fact]
    public void Validation_NestedObjects_ValidatedRecursively()
    {
        var inMemory = new Dictionary<string, string?>
        {
            { "ApplicationName", "ValidApp" },
            // Nested Database has missing ConnectionString
            { "Database:MaxPoolSize", "50" }
        };

        var config = SopsConfiguration.CreateBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var ex = Assert.Throws<ConfigurationValidationException>(() =>
        {
            config.GetValid<TestAppSettings>();
        });

        Assert.Contains("Database:ConnectionString", ex.Message);
    }

    [Fact]
    public void Validation_Collections_ValidatedRecursively()
    {
        var inMemory = new Dictionary<string, string?>
        {
            { "ApplicationName", "ValidApp" },
            { "Database:ConnectionString", "Server=localhost;" },
            { "Endpoints:0:Name", "Endpoint1" },
            { "Endpoints:0:Url", "https://api.example.com" },
            { "Endpoints:1:Name", "Endpoint2" },
            { "Endpoints:1:Url", "not-a-valid-url" } // Invalid URL
        };

        var config = SopsConfiguration.CreateBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var ex = Assert.Throws<ConfigurationValidationException>(() =>
        {
            config.GetValid<TestAppSettings>();
        });

        Assert.Contains("Endpoints[1]:Url", ex.Message);
    }

    [Fact]
    public void Validation_IValidatableObject_ExecutesCustomLogic()
    {
        var inMemory = new Dictionary<string, string?>
        {
            { "MinValue", "100" },
            { "MaxValue", "50" } // Min > Max
        };

        var config = SopsConfiguration.CreateBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var ex = Assert.Throws<ConfigurationValidationException>(() =>
        {
            config.GetValid<TestValidatableObjectSettings>();
        });

        Assert.Contains("MinValue cannot be greater than MaxValue", ex.Message);
    }

    [Fact]
    public void Validation_IValidatableSettings_ExecutesCustomLogic()
    {
        var inMemory = new Dictionary<string, string?>
        {
            { "Mode", "InvalidMode" }
        };

        var config = SopsConfiguration.CreateBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var ex = Assert.Throws<ConfigurationValidationException>(() =>
        {
            config.GetValid<TestSelfValidatingSettings>();
        });

        Assert.Contains("Mode cannot be InvalidMode", ex.Message);
    }

    [Fact]
    public void Validation_GetRequiredValue_Missing_ThrowsConfigurationValidationException()
    {
        var config = SopsConfiguration.CreateBuilder().Build();

        Assert.Throws<ConfigurationValidationException>(() =>
        {
            config.GetRequiredValue("NonExistentKey");
        });
    }

    [Fact]
    public void Validation_GetOptionalValue_Missing_ReturnsFallbackDefault()
    {
        var config = SopsConfiguration.CreateBuilder().Build();

        var value = config.GetOptionalValue("NonExistentKey", "MyFallback");
        Assert.Equal("MyFallback", value);
    }

    [Fact]
    public void Validation_StartupValidation_WithServiceCollection_FailsFast()
    {
        var inMemory = new Dictionary<string, string?>
        {
            { "Database:MaxPoolSize", "500" } // Invalid, and ConnectionString is missing
        };

        var config = SopsConfiguration.CreateBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var services = new ServiceCollection();

        Assert.Throws<ConfigurationValidationException>(() =>
        {
            services.AddSopsSettings<TestDatabaseSettings>(config, "Database", validateOnStart: true);
        });
    }

    [Fact]
    public void Validation_StartupValidation_WithServiceCollection_Valid_RegistersServices()
    {
        var inMemory = new Dictionary<string, string?>
        {
            { "Database:ConnectionString", "Server=production.db;" },
            { "Database:MaxPoolSize", "50" }
        };

        var config = SopsConfiguration.CreateBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var services = new ServiceCollection();
        services.AddSopsSettings<TestDatabaseSettings>(config, "Database", validateOnStart: true);

        using var serviceProvider = services.BuildServiceProvider();
        var directSettings = serviceProvider.GetRequiredService<TestDatabaseSettings>();
        var optionsSettings = serviceProvider.GetRequiredService<IOptions<TestDatabaseSettings>>().Value;

        Assert.NotNull(directSettings);
        Assert.NotNull(optionsSettings);
        Assert.Equal("Server=production.db;", directSettings.ConnectionString);
        Assert.Equal("Server=production.db;", optionsSettings.ConnectionString);
        Assert.Equal(50, directSettings.MaxPoolSize);
    }
}
