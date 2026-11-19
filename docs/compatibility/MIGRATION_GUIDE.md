# Compatibility: Existing Packages Migration Guide

This guide describes how existing SOPs packages should migrate or integrate with `Sops.Models`.

---

## 1. Migration for `Sops.Drugs`

### Current State
`Sops.Drugs` contains its own `Drug`, `DrugPackageInfo`, and `DrugDosageForm` in `Sops.Drugs.Models`.

### Recommended Integration
1. Add project reference to `Sops.Models`.
2. Map `Sops.Drugs.Models.Drug` to `Sops.Models.Catalog.Drug`:
   ```csharp
   public static class DrugMappingExtensions
   {
       public static Sops.Models.Catalog.Drug ToContract(this Sops.Drugs.Models.Drug source)
       {
           // Maps cleanly to canonical contract
       }
   }
   ```
3. In subsequent releases, `Sops.Drugs` can directly use `Sops.Models.Catalog.Drug`.

---

## 2. Migration for `Sops.Payments`

### Current State
`Sops.Payments` contains `Money`, `PaymentRequest`, and `PaymentResult`.

### Recommended Integration
1. `Sops.Payments.Models.Money` provides implicit conversion operators to and from `Sops.Models.Common.Money`:
   ```csharp
   public static implicit operator Sops.Models.Common.Money(Money m) =>
       Sops.Models.Common.Money.FromMinorUnits(m.AmountMinorUnits, m.Currency);
       
   public static implicit operator Money(Sops.Models.Common.Money m) =>
       Money.FromMinorUnits(m.AmountMinorUnits, m.Currency);
   ```
2. Retail POS `Payment` models are moved to `Sops.Models.Transactions.Payment`, keeping gateway-specific models inside `Sops.Payments`.

---

## 3. Migration for `Sops.Device` & `Sops.Licensing`

### Current State
`Sops.Device` probes hardware signals. `Sops.Licensing` validates signed certificates.

### Recommended Integration
1. `Sops.Device` remains the internal hardware collector.
2. `Sops.Device` generates `Sops.Models.Device.DeviceIdentitySummary` when exposing device status to cloud APIs.
3. `Sops.Licensing` parses `Sops.Models.Licensing.LicenseClaims` from validated JWTs, decoupling claims representation from cryptographic parsing.
