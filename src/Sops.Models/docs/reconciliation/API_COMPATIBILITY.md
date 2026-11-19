# Audit: API Compatibility & Non-Breaking Evolution Rules

## 1. Zero Regressions Mandate

Creating `Sops.Models` must not break or require immediate destructive changes across any of the 12 existing SOPs packages:
1. `Sops.Cache`
2. `Sops.Configuration`
3. `Sops.Database`
4. `Sops.DataLayer`
5. `Sops.Device`
6. `Sops.Drugs`
7. `Sops.Licensing`
8. `Sops.Logging`
9. `Sops.Mail`
10. `Sops.Payments`
11. `Sops.UI.Wpf`
12. `Sops.WhatsApp`

All 699 existing tests in the solution MUST continue to pass without modification.

---

## 2. API Versioning Rules for `Sops.Models`

`Sops.Models` adheres strictly to **Semantic Versioning 2.0.0** and wire backward compatibility:

### 2.1 Allowed Additive Changes (Minor Version Bump - e.g., 0.1.0 -> 0.2.0)
- Adding new optional properties with default values (`null` or sensible defaults).
- Adding new model contracts or schemas in new files.
- Adding new enum values (with designated unknown-value fallback handling in deserializers).
- Adding helper extension methods or factory methods.

### 2.2 Prohibited Breaking Changes (Requires Major Version - e.g., v1 -> v2)
- Renaming or removing existing properties in schemas or C# records.
- Changing property data types (e.g., `string` to `int`, `int` to `long`, or `long` to `string`).
- Changing JSON property naming conventions (snake_case wire names must remain constant).
- Altering validation constraints to make previously valid contracts invalid (e.g., lowering max string lengths, changing currency codes).
- Changing numeric scaling on `Money` or `Quantity`.

---

## 3. Wire Format Standards

To ensure transparent interoperability between C#, TypeScript, Dart, Python, and Go:

1. **JSON Property Naming**:
   - Wire standard is **`snake_case`** (or camelCase where specified in schemas).
   - In `Sops.Models`, all C# properties use `[JsonPropertyName("snake_case_name")]` to guarantee deterministic JSON mapping independent of global serializer options.
2. **Timestamps**:
   - All timestamps are UTC and formatted according to **ISO 8601 / RFC 3339** (e.g., `2026-11-19T08:30:00Z`).
   - Modeled in C# as `DateTimeOffset`.
3. **Identifiers**:
   - Modeled as RFC 4122 UUID / `Guid` or standard alphanumeric strings with regex patterns in schemas.
4. **Enums**:
   - Serialized as strings (e.g., `"tablet"`, `"approved"`), not integers, to avoid enum index drift across programming languages.
