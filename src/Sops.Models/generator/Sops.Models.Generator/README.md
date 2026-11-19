# SOPs.Models — Unified Self-Contained Domain Models Ecosystem

This directory is the single, self-contained home for the canonical `Sops.Models` domain contracts ecosystem within the SOPs monorepo.

---

## 1. Directory Structure

```text
src/Sops.Models/
├── schemas/
│   ├── v1/                       # 57 Authoritative JSON Schemas (Draft 2020-12)
│   └── fixtures/                 # Cross-language verification test fixtures
│
├── generator/
│   ├── Sops.Models.Generator/    # Reusable C# code generation & validation library
│   └── Sops.Models.Generator.Tool/ # .NET Tool CLI (sops-models-gen)
│
├── packages/
│   ├── csharp/
│   │   └── Sops.Models/          # Multi-target C# NuGet package (netstandard2.0, net8.0, net10.0)
│   ├── typescript/               # @sops-system/models npm package
│   └── dart/                     # sops_models Dart package
│
├── tests/
│   ├── Sops.Models.Tests/        # C# domain model and serialization tests
│   ├── Generator.Tests/          # Generator library & drift-check tests
│   └── CrossLanguage/            # Node.js / multi-language fixture tests
│
├── samples/
│   ├── Net48Consumer/            # .NET Framework 4.8 sample consumer
│   ├── ModernDotNetConsumer/     # Modern .NET 8.0 sample consumer
│   ├── TypeScriptConsumer/       # Node.js / TypeScript consumer
│   └── DartConsumer/             # Dart application consumer
│
├── docs/
│   ├── architecture/             # Architecture decision records
│   ├── contracts/                # Canonical contract specifications
│   ├── reconciliation/           # Reconciliation & integration matrices
│   ├── compatibility/            # Cross-language compatibility rules
│   └── database-mapping/         # PostgreSQL & SQLCipher mapping guides
│
├── README.md                     # Ecosystem guide (this file)
└── version.json                  # Contract & package version manifest
```

---

## 2. Generator Tool Usage

The generator CLI tool `sops-models-gen` is located in `generator/Sops.Models.Generator.Tool`:

```bash
# Validate all canonical JSON schemas
dotnet run --project generator/Sops.Models.Generator.Tool -- validate

# Verify zero schema-to-code drift across C#, TypeScript, and Dart
dotnet run --project generator/Sops.Models.Generator.Tool -- check

# List canonical model inventory
dotnet run --project generator/Sops.Models.Generator.Tool -- list

# Regenerate language packages
dotnet run --project generator/Sops.Models.Generator.Tool -- generate --language all
```

---

## 3. Package Identities

- **C#**: `Sops.Models` (NuGet package, targeting `netstandard2.0;net8.0;net10.0`)
- **Generator Library**: `Sops.Models.Generator` (NuGet library)
- **Generator Tool**: `Sops.Models.Generator.Tool` (.NET Tool: `sops-models-gen`)
- **TypeScript**: `@sops-system/models` (npm package)
- **Dart**: `sops_models` (Dart/Flutter package)

---

## 4. Key Invariants & Policies

1. **Zero Database Coupling**: `Sops.Models` contains pure domain contracts with zero runtime dependencies on EF Core, SQLite, Npgsql, or WPF.
2. **Dual Wire Money Serialization**: `Money` serializes both `amountInMinorUnits` (canonical camelCase) and `amount_minor_units` (legacy snake_case), plus `decimalString` to protect 64-bit integer values exceeding $2^{53}-1$ against JavaScript float64 precision loss.
3. **Strict Drug Invariants**: Barcodes belong strictly to the `Drug` entity, never package tiers. Embedded packages enforce conversion factors $\ge 1$.
4. **Patient Privacy**: Sensitive diagnostic strings and plaintext national IDs are omitted from `PatientSummary`.
