# Sops.Models — Dependency Inversion & Architectural Boundary Rules

## 1. Direction of Dependencies

The fundamental dependency rule across the entire SOPs package ecosystem is:

> **All packages and applications depend inwards toward `Sops.Models`. `Sops.Models` depends on NOTHING.**

```text
       ┌────────────────┐
       │   Sops.Models  │ <── Root Contract Package
       └───────▲────────┘
               │ (Contracts Only)
    ┌──────────┴──────────────┬─────────────────────────┐
    │                         │                         │
┌───┴───────────┐     ┌───────┴────────┐        ┌───────┴────────┐
│  Sops.Drugs   │     │  Sops.Payments │        │  Sops.Device   │
└───────▲───────┘     └───────▲────────┘        └───────▲────────┘
        │                     │                         │
        └──────────────┬──────┴─────────────────────────┘
                       │
             ┌─────────┴─────────┐
             │  Sops.DataLayer   │
             └─────────▲─────────┘
                       │
       ┌───────────────┴───────────────┐
       │                               │
┌──────┴───────────────┐     ┌─────────┴────────────┐
│ Sops.UI.Wpf (POS UI) │     │ Server Infrastructure│
└──────────────────────┘     └──────────────────────┘
```

## 2. Forbidden Dependencies

To ensure 100% portability, zero runtime conflicts, and universal cross-language compatibility, `Sops.Models` is strictly forbidden from referencing:

1. **Framework-Specific Assemblies**:
   - `PresentationCore`, `PresentationFramework`, `WindowsBase` (WPF).
   - `Microsoft.AspNetCore.*` (ASP.NET Core).
2. **Data Access & Storage Providers**:
   - `Microsoft.EntityFrameworkCore` (EF Core).
   - `Npgsql`, `Npgsql.EntityFrameworkCore.PostgreSQL`.
   - `Microsoft.Data.Sqlite`, `SQLitePCLRaw.*`, `SQLCipher`.
   - `StackExchange.Redis`.
3. **Network & Communications Libraries**:
   - `System.Net.Http` (no direct HTTP clients; only payload DTOs).
   - `MailKit`, `MimeKit` (Mail implementations).
4. **Hardware & Operating System Hooks**:
   - `System.Management`, `Microsoft.Win32.Registry`.
   - Direct device IO, printer drivers, or barcode scanner serial ports.
5. **Security & Cryptographic Sinks**:
   - DPAPI, Windows Data Protection.
   - Private key files, certificate stores, or keystores.

## 3. Allowed Dependencies

`Sops.Models` permits only:
- `netstandard2.0`, `net8.0`, `net10.0` standard runtime BCL types.
- `System.Text.Json` (v8.0.5) for high-performance JSON serialization attributes (`[JsonPropertyName]`, `[JsonConverter]`, `[JsonIgnore]`).
- Nullable reference type annotations (`Nullable = enable`).

## 4. Integration via Extension Methods and Adapters

Consumer packages (`Sops.Drugs`, `Sops.Payments`, `Sops.Device`, `Sops.Licensing`) provide adapter methods and type converters to map between their internal operational models and `Sops.Models` canonical contracts.

`Sops.Models` provides extension points and mapping contracts in `Sops.Models.Mapping` to standardize bi-directional mapping without coupling to third-party mapping libraries.
