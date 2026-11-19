# Sops.Configuration

A lightweight, production-quality, reusable configuration library for .NET applications and packages.

`Sops.Configuration` builds upon standard `Microsoft.Extensions.Configuration` abstractions, providing strongly typed settings binding, robust validation via DataAnnotations and self-validating models, explicit environment resolution, and secret-safe error handling that guarantees credentials are never exposed in exception messages or diagnostic logs.

## Purpose

- Provide unified, predictable configuration loading across all SOPs packages and applications.
- Enforce strict validation of configuration settings to fail fast upon startup instead of failing during runtime execution.
- Guarantee that sensitive credentials, connection strings, and API keys are redacted and never leaked through validation errors, exceptions, or logs.
- Support standard environment conventions (`Development`, `Testing`, `Staging`, `Production`) without introducing global mutable state.
- Keep dependency injection completely optional for basic consumers while integrating smoothly with `IServiceCollection` when DI is present.

## Installation

Add the private NuGet package to your project:

```xml
<ItemGroup>
  <PackageReference Include="Sops.Configuration" Version="0.1.0" />
</ItemGroup>
```

Ensure your `NuGet.Config` is configured with the `github-sops` package source.

## Minimal Usage Example

No dependency injection container is required for basic consumption:

```csharp
using Sops.Configuration;
using Sops.Configuration.Extensions;

// 1. Create standard configuration root
var configuration = SopsConfiguration.CreateDefault(
    environment: "Production",
    basePath: AppContext.BaseDirectory);

// 2. Read values directly or require them
string appName = configuration.GetRequiredValue("ApplicationName");
string port = configuration.GetOptionalValue("Port", "8080");

// 3. Add / Update / Delete in memory (Runtime CRUD)
configuration.SetValue("Feature:Beta", "true");      // Add / Update
configuration.RemoveValue("Feature:Beta");          // Delete

// 4. Add / Update / Delete in JSON file on disk (Persistent CRUD)
configuration.UpdateJsonFile("appsettings.json", file =>
{
    file.Set("Database:Port", 5432)                 // Add / Update
        .Remove("TemporaryFlag");                   // Delete
});
```

## Configuration Precedence

By default, `SopsConfiguration.CreateDefault` loads configuration sources in the following precedence order (last source wins):

1. **Base JSON file**: `appsettings.json` (optional).
2. **Environment JSON file**: `appsettings.{Environment}.json` (e.g. `appsettings.Production.json`, optional).
3. **Environment variables**: prefixed (e.g. `SOPS_`) or all environment variables.
4. **In-memory overrides**: key-value pairs supplied directly at startup or in tests.

To customize sources or precedence, use the fluent `SopsConfigurationBuilder`:

```csharp
var config = SopsConfiguration.CreateBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .SetEnvironment("Staging")
    .AddJsonFile("custom-settings.json", optional: false)
    .AddEnvironmentVariables(prefix: "SOPS_")
    .AddInMemoryCollection(new[]
    {
        new KeyValuePair<string, string?>("Service:Timeout", "60")
    })
    .Build();
```

## Typed Settings Example

Define your settings class with standard data annotations and default values:

```csharp
using System.ComponentModel.DataAnnotations;
using Sops.Configuration.Models;

public class DatabaseSettings
{
    [Required(ErrorMessage = "ConnectionString is required.")]
    [Sensitive]
    public string ConnectionString { get; set; } = string.Empty;

    [Range(1, 100, ErrorMessage = "MaxPoolSize must be between 1 and 100.")]
    public int MaxPoolSize { get; set; } = 20;

    public int TimeoutSeconds { get; set; } = 30;
}
```

Bind and validate in one call:

```csharp
var dbSettings = configuration.GetValid<DatabaseSettings>("Database");

Console.WriteLine($"Timeout: {dbSettings.TimeoutSeconds}"); // 30 (default preserved if unconfigured)
```

## Validation Example

`Sops.Configuration` performs deep recursive validation across complex nested objects, arrays, and lists. It supports:

- **DataAnnotations**: `[Required]`, `[Range]`, `[MinLength]`, `[StringLength]`, `[RegularExpression]`, etc.
- **`IValidatableObject`**: custom multi-property cross-field validation rules.
- **`IValidatableSettings`**: custom self-validating logic yielding validation error messages.

```csharp
public class OrderServiceSettings : IValidatableSettings
{
    [Required]
    public string ServiceUrl { get; set; } = string.Empty;

    public int MinWorkers { get; set; } = 2;
    public int MaxWorkers { get; set; } = 10;

    public IEnumerable<string> Validate()
    {
        if (MinWorkers > MaxWorkers)
        {
            yield return "MinWorkers cannot exceed MaxWorkers.";
        }
    }
}

// Binds and validates; throws ConfigurationValidationException on failure
var settings = configuration.GetValid<OrderServiceSettings>("OrderService");
```

### Startup Validation with Dependency Injection

When using `Microsoft.Extensions.DependencyInjection`, use `AddSopsSettings` to fail fast during application startup:

```csharp
using Sops.Configuration.Extensions;

services.AddSopsSettings<DatabaseSettings>(configuration, sectionName: "Database", validateOnStart: true);
```

This registers both `DatabaseSettings` (for direct injection) and `IOptions<DatabaseSettings>`.

## Security Considerations

1. **Never Log Secrets**: Properties annotated with `[Sensitive]` or matching known secret patterns (e.g. `Password`, `ApiKey`, `Token`, `Secret`, `ConnectionString`, `Credential`, `PrivateKey`) are flagged as sensitive.
2. **Secret-Safe Exceptions**: When validation fails on a sensitive property, the actual value is never included in the `ConfigurationValidationException`, error collection, or formatted message. If an underlying validator includes the raw value, `SecretMasker` replaces it with `[REDACTED]`.
3. **No Hardcoded Secrets**: Secrets should never be committed into source repositories. Provide secrets via environment variables or secret managers in deployed environments.
4. **No Custom Encryption**: Uses standard .NET configuration components rather than reinventing encryption schemes.

## Framework Compatibility

- **Target Framework**: `netstandard2.0`
- **Compatibility**:
  - .NET Framework 4.8+
  - .NET 6.0, .NET 7.0, .NET 8.0, .NET 9.0, .NET 10.0, .NET 11.0+
- **Platform Neutral**: Pure managed C#, no OS- or platform-specific APIs.
