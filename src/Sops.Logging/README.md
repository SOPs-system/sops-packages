# Sops.Logging

A lightweight, production-quality structured logging library for .NET applications with automated sensitive data redaction.

`Sops.Logging` integrates natively with `Microsoft.Extensions.Logging` and `Sops.Configuration`. It provides structured JSON logging for cloud aggregators, human-readable console logging for local development, and built-in redaction that ensures customer credit cards, CVVs, passwords, API keys, OTPs, and authorization headers are never written to logs.

## Purpose

- Provide consistent, structured logging across all SOPs packages and microservices.
- Ensure strict compliance with security standards by automatically masking sensitive data before it reaches any sink.
- Integrate seamlessly with standard `Microsoft.Extensions.Logging` (`ILogger<T>`, `ILoggerFactory`).
- Offer zero-configuration defaults with full customization through `IConfiguration` or code.

## Installation

Add the private NuGet package to your project:

```xml
<ItemGroup>
  <PackageReference Include="Sops.Logging" Version="0.1.0" />
</ItemGroup>
```

## Minimal Usage Example

Using standard dependency injection with `Sops.Configuration`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sops.Configuration;
using Sops.Logging.Extensions;

// 1. Initialize configuration
var configuration = SopsConfiguration.CreateDefault(
    environment: "Production",
    basePath: AppContext.BaseDirectory);

// 2. Register logging in service collection
var services = new ServiceCollection();
services.AddSopsLogging(configuration);

// 3. Resolve logger
using var provider = services.BuildServiceProvider();
var logger = provider.GetRequiredService<ILogger<Program>>();

// 4. Log messages
logger.LogInformation("SOPs service started");
logger.LogWarning("Inventory synchronization delayed");
```

## Standalone Usage (Without DI)

You can create standalone loggers without configuring a dependency injection container:

```csharp
using Sops.Logging;
using Microsoft.Extensions.Logging;

var logger = SopsLogging.CreateLogger<OrderProcessor>();
logger.LogInformation("Processing batch {BatchId}", 42);
```

## Sensitive Data Redaction

`Sops.Logging` automatically detects and masks sensitive patterns in log messages and exception details:

| Sensitive Pattern | Example Input | Redacted Output |
|---|---|---|
| **Credit Cards** | `4111-2222-3333-4444` | `[REDACTED_CARD]` |
| **CVV / CVC** | `cvv: 123` | `cvv: [REDACTED]` |
| **Auth Headers** | `Bearer eyJhbGci...` | `Bearer [REDACTED]` |
| **Passwords** | `password=secret123` | `password="[REDACTED]"` |
| **API Keys & Secrets** | `api_key: sk_live_xyz` | `api_key: "[REDACTED]"` |
| **OTPs & Verification** | `otp: 123456` | `otp: "[REDACTED]"` |
| **Known Tokens** | `ghp_12345678...` | `[REDACTED_TOKEN]` |

### Custom Redaction Patterns

Add domain-specific patterns in code:

```csharp
services.AddSopsLogging(configuration, options =>
{
    options.AdditionalRedactionPatterns.Add(@"\bNationalId:\s*\d{10}\b");
});
```

## Configuration

In `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "System": "Warning"
    },
    "Sops": {
      "EnableConsole": true,
      "ConsoleFormat": "Json",
      "RedactSensitiveData": true,
      "IncludeScopes": true,
      "UseUtcTimestamp": true
    }
  }
}
```

### Console Formats

- **`Json`** (Default for Production): Emits single-line structured JSON with ISO 8601 timestamps, log level, event ID, scopes, message, and exception:
  ```json
  {"Timestamp":"2026-11-17T19:30:00.0000000Z","LogLevel":"Information","Category":"Program","EventId":0,"Message":"SOPs service started"}
  ```
- **`Simple`** (Recommended for Development): Emits clean human-readable output:
  ```text
  [2026-11-17 19:30:00.000 UTC] [INF] [Program] SOPs service started
  ```

## Scopes & Correlation

Logging scopes are included in JSON output when `IncludeScopes` is enabled:

```csharp
using (logger.BeginScope(new Dictionary<string, object>
{
    ["CorrelationId"] = correlationId,
    ["TenantId"] = tenantId
}))
{
    logger.LogInformation("Order received");
}
```

## Framework Compatibility

- **Target Framework**: `netstandard2.0`
- **Compatibility**:
  - .NET Framework 4.8+
  - .NET 6.0, .NET 7.0, .NET 8.0, .NET 9.0, .NET 10.0, .NET 11.0+
- **Platform Neutral**: Pure managed C#, no OS-specific dependencies.
