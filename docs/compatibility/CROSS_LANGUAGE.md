# Compatibility: Cross-Language Type Mapping Guide

To maintain exact type equivalence between C#, TypeScript, and Dart across our multi-platform architecture:

| Logical Model Type | C# Type (`netstandard2.0;net8.0;net10.0`) | TypeScript Type | Dart Type | JSON Schema Representation |
|---|---|---|---|---|
| **UUID / Identifier** | `System.Guid` | `string` | `String` | `"type": "string", "format": "uuid"` |
| **Financial Money** | `Sops.Models.Common.Money` | `Money` interface (`amountMinorUnits: number, currency: string`) | `Money` class (`int amountMinorUnits, String currency`) | `"type": "object", "properties": {"amount_minor_units": {"type": "integer"}, "currency": {"type": "string"}}` |
| **Quantity** | `Sops.Models.Common.Quantity` | `Quantity` interface (`value: number, unit: string`) | `Quantity` class (`double value, String unit`) | `"type": "object", "properties": {"value": {"type": "number"}, "unit": {"type": "string"}}` |
| **Integer (Count)** | `int` / `long` | `number` | `int` | `"type": "integer"` |
| **Decimal / Rate** | `decimal` | `number` | `double` | `"type": "number"` |
| **Boolean Flag** | `bool` | `boolean` | `bool` | `"type": "boolean"` |
| **String / Text** | `string` | `string` | `String` | `"type": "string"` |
| **Date Only** | `DateOnly` (`string` on netstandard2.0) | `string` (`YYYY-MM-DD`) | `DateTime` / `String` | `"type": "string", "format": "date"` |
| **Timestamp (UTC)** | `DateTimeOffset` | `string` (ISO 8601 UTC) | `DateTime` | `"type": "string", "format": "date-time"` |
| **Domain Enum** | C# `enum` (`[JsonConverter(typeof(JsonStringEnumConverter))]`) | TypeScript String Union (`"Tablet" \| "Capsule"`) | Dart `enum` with string serialization | `"type": "string", "enum": [...]` |
| **List / Collection** | `IReadOnlyList<T>` | `T[]` / `Array<T>` | `List<T>` | `"type": "array", "items": {...}` |
| **Dictionary / Map** | `IReadOnlyDictionary<string, string>` | `Record<string, string>` | `Map<String, String>` | `"type": "object", "additionalProperties": {"type": "string"}` |

---

## JavaScript Integer Precision Safety
In JavaScript / TypeScript, the maximum safe integer is $2^{53} - 1$ (`9,007,199,254,740,991`).
For currency in minor units:
- $9 \times 10^{15}$ minor units = 90 trillion currency units (EGP / USD).
- This exceeds any conceivable pharmacy or chain balance by many orders of magnitude.
- Therefore, modeling minor units as 64-bit integer (`long` in C#, `number` in TypeScript, `int` in Dart) is completely safe from IEEE 754 precision loss while maintaining extreme arithmetic performance.
