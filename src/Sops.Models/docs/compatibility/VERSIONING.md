# Compatibility: Model Evolution & Versioning Policy

## 1. Schema Lifecycle

Each domain contract follows a deterministic lifecycle:
```text
Experimental ──> Stable (v1) ──> Deprecated ──> Retired (v2 migration)
```

1. **Experimental**: Early development; field changes permitted.
2. **Stable (v1)**: Production deployment. Breaking changes strictly forbidden. Backward compatibility guaranteed.
3. **Deprecated**: Marked with deprecation warnings in schemas and code (`[Obsolete]`), active support maintained for at least 12 months.
4. **Retired**: Removed in the next major schema milestone (v2).

---

## 2. Unknown Enum Handling
To ensure that new enum values introduced in the cloud do not crash older POS clients:
- Deserializers in older clients should fall back to a designated `Unknown` / default enum member rather than throwing deserialization exceptions.
- C# enums should always include an explicit zero member (`Unknown = 0`) where applicable.

---

## 3. Tolerant Reader Pattern
Consumers must practice Martin Fowler's **Tolerant Reader Pattern**:
- Ignore unknown properties in incoming JSON payloads (`UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip`).
- Do not make strict assumptions about property ordering in JSON objects.
- Accept ISO 8601 timestamps with or without fractional seconds.
