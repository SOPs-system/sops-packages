# Sops.Database

`Sops.Database` is a production-quality, lightweight, provider-independent database infrastructure library supporting both relational (SQL) and non-relational (Document/NoSQL) storage systems for SOPs applications.

## Purpose

- **Provider-Independent Contracts**: Standardized interfaces for connection lifecycle, capabilities discovery, health probes, and transactions.
- **Relational Standard**: Built directly on `System.Data.Common` (`DbConnection`, `DbCommand`, `DbTransaction`, `DbDataReader`) for maximum compatibility and performance without imposing an ORM.
- **Document Model**: Capability-driven contracts for document collections, CRUD operations, and key-value/document storage.
- **Resilience & Safety**: Bounded exponential backoff retry executor that strictly refuses to retry non-idempotent writes or uncommitted transactions.
- **Security First**: Parameterized queries by design, sensitive connection string protection, and automated log redaction integration.
- **Optional Dependency Injection**: First-class support for both DI-based applications and standalone non-DI consumption.

---

## Installation

```bash
dotnet add package Sops.Database
```

---

## Configuration Example

Configure settings in `appsettings.json`, environment variables, or in-memory sources:

```json
{
  "Database": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Database=sops;Username=app;Password=secret",
    "ConnectionTimeoutSeconds": 15,
    "CommandTimeoutSeconds": 30,
    "ReadOnly": false,
    "TenantId": "tenant-01",
    "Pooling": {
      "Enabled": true,
      "MinPoolSize": 5,
      "MaxPoolSize": 100
    },
    "Retry": {
      "Enabled": true,
      "MaxAttempts": 3,
      "InitialDelayMilliseconds": 200,
      "MaxDelayMilliseconds": 2000,
      "BackoffMultiplier": 2.0
    }
  }
}
```

---

## Usage Examples

### 1. Dependency Injection Setup

```csharp
using Microsoft.Extensions.DependencyInjection;
using Sops.Configuration;
using Sops.Database.Extensions;
using Sops.Database.Relational;

var configuration = SopsConfiguration.CreateDefault();
var services = new ServiceCollection();

// Register Sops.Database
services.AddSopsDatabase(configuration);

// Register a concrete database provider
services.AddDatabaseProvider("Sqlite", (settings, sp) => new SqliteRelationalProvider(settings));

var provider = services.BuildServiceProvider();
var db = provider.GetRequiredService<IRelationalDatabaseProvider>();
```

### 2. Standalone Relational Query & Command

```csharp
using Sops.Database.Configuration;
using Sops.Database.Relational;

var settings = new DatabaseSettings
{
    Provider = "Sqlite",
    ConnectionString = "Data Source=:memory:"
};

// Parameterized non-query
var insertCmd = RelationalCommandDefinition.Create(
    "INSERT INTO Orders (Id, Total, Status) VALUES (@Id, @Total, @Status);",
    new { Id = "ORD-001", Total = 129.50m, Status = "Pending" });

await db.ExecuteNonQueryAsync(insertCmd);

// Idempotent parameterized query
var queryCmd = RelationalCommandDefinition.Idempotent(
    "SELECT Id, Total FROM Orders WHERE Status = @Status;",
    new { Status = "Pending" });

var orders = await db.QueryAsync(queryCmd, reader => new
{
    Id = reader.GetString(0),
    Total = reader.GetDecimal(1)
});
```

### 3. Explicit Atomic Transactions

```csharp
using (var tx = await db.BeginTransactionAsync())
{
    var cmd1 = RelationalCommandDefinition.Create(
        "UPDATE Accounts SET Balance = Balance - @Amount WHERE Id = @From;",
        new { Amount = 50.0m, From = "ACC-1" },
        transaction: tx.Transaction);

    await db.ExecuteNonQueryAsync(cmd1);

    var cmd2 = RelationalCommandDefinition.Create(
        "UPDATE Accounts SET Balance = Balance + @Amount WHERE Id = @To;",
        new { Amount = 50.0m, To = "ACC-2" },
        transaction: tx.Transaction);

    await db.ExecuteNonQueryAsync(cmd2);

    await tx.CommitAsync();
}
```

### 4. Non-Relational / Document Collections

```csharp
using Sops.Database.Documents;

var docProvider = new InMemoryDocumentDatabaseProvider();
var collection = docProvider.GetCollection<CustomerProfile>("Customers");

await collection.InsertAsync("cust-123", new CustomerProfile { Name = "Acme Corp", Active = true });
var customer = await collection.GetByIdAsync("cust-123");
```

---

## Resilience & Retry Rules

1. **Bounded Attempts**: Retries are strictly bounded (1–10 attempts max).
2. **Idempotency Gate**: Only operations marked as idempotent (`RelationalCommandDefinition.IsIdempotent = true`) are retried. Non-idempotent writes are executed once and never retried silently.
3. **No Commit Retries**: Transaction commits are never retried automatically without an explicit idempotency protocol.
4. **Cancellation-Aware**: All operations and delay timers observe cancellation tokens immediately.
5. **No Exception Swallowing**: The original failure exception is preserved and rethrown.

---

## Security Considerations

- **Sensitive Attribute**: `DatabaseSettings.ConnectionString` is decorated with `[Sensitive]` to integrate with `SopsLogRedactor` and configuration redaction.
- **No Concatenation**: SQL queries must be parameterized via `RelationalCommandDefinition`. Untrusted user strings must never be concatenated into query text.
- **Safe Diagnostic Summaries**: Diagnostic helpers (`DatabaseSettings.ToSafeSummary()`) mask connection strings and passwords.

---

## Framework Compatibility

- **Target**: `netstandard2.0`
- **Supported Runtimes**:
  - .NET Framework 4.8+
  - .NET Core 3.1+
  - .NET 6, .NET 7, .NET 8+
