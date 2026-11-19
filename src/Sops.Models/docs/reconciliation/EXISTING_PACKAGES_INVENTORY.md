# Audit: Existing Packages Model & DTO Inventory

This audit catalogs every data contract, model, DTO, value object, and enum across the 12 existing packages in `D:\SOPs-system\sops-packages\src`.

---

## Package 1: `Sops.Cache`
- **Location**: `src/Sops.Cache/`
- **Target**: `netstandard2.0`
- **Models / Types**:
  - `CacheEntryOptions`: Expiration settings (Absolute, Sliding), Priority.
  - `CacheResult<T>`: Outcome envelope (`Success`, `Value`, `Status`, `ErrorMessage`).
  - `CacheStatus`: Enum (`Hit`, `Miss`, `Error`, `Bypassed`).
  - `CacheSettings`, `MemoryCacheSettings`, `RedisCacheSettings`: Options classes.
- **Classification**: Local caching infrastructure models.
- **Ownership**: Belongs to `Sops.Cache`. Canonical `Sops.Models` provides generalized `ResultEnvelope<T>`.

---

## Package 2: `Sops.Configuration`
- **Location**: `src/Sops.Configuration/`
- **Target**: `netstandard2.0`
- **Models / Types**:
  - `SensitiveAttribute`: Attribute marking secrets to redact in logs/outputs.
  - `SopsConfigurationOptions`: Options for configuration loading and validation.
  - `SopsEnvironments`: Environment constants (`Development`, `Staging`, `Production`).
  - `ConfigurationValidationError`: Structural error reports for settings validation.
- **Classification**: Infrastructure configuration models.
- **Ownership**: Belongs to `Sops.Configuration`.

---

## Package 3: `Sops.Database`
- **Location**: `src/Sops.Database/`
- **Target**: `netstandard2.0`
- **Models / Types**:
  - `DatabaseCapabilities`: Flags describing engine capabilities (Transactions, FullTextSearch, JSON).
  - `DatabaseHealthResult`: Result of ping / health check.
  - `DatabaseSettings`: Configuration connection details, including multi-tenant `TenantId`.
  - `DocumentMetadata`, `DocumentFilter`, `DocumentBatch`: Document abstraction contracts.
  - `RelationalQuery`, `RelationalParameter`, `RelationalCommand`: Low-level SQL abstraction.
- **Classification**: Database driver abstractions.
- **Ownership**: Belongs to `Sops.Database`.

---

## Package 4: `Sops.DataLayer`
- **Location**: `src/Sops.DataLayer/`
- **Target**: `netstandard2.0`, `net48`
- **Models / Types**:
  - `Authentication`: `AuthenticationState`, `LoginRequest`, `LoginResult`, `TokenPair`, `UserInfo`.
  - `Connectivity`: `ConnectionState`, `ServerHealth`, `HealthStatus`.
  - `Events`: `EventEnvelope`, `LiveTopics`.
  - `Files`: `FileUploadRequest`, `FileUploadResult`, `FileDownloadRequest`, `FileDownloadResult`.
  - `Requests`: `DataRequest`, `RequestContext`, `RequestHeaders`, `RequestOptions`.
  - `Responses`: `DataError`, `DataOutcome`, `DataPage<T>`, `DataResult<T>`, `ResponseMetadata`.
- **Classification**: Client-side communication and request pipeline contracts.
- **Ownership**: Communication pipeline belongs to `Sops.DataLayer`. Canonical domain envelopes (`ResultEnvelope<T>`, `PageRequest`, `PageResult<T>`, `SyncEventEnvelope`) belong to `Sops.Models.Common` and `Sops.Models.Sync`.

---

## Package 5: `Sops.Device`
- **Location**: `src/Sops.Device/`
- **Target**: `netstandard2.0`, `net48`, `net8.0`
- **Models / Types**:
  - `DeviceFingerprint`: Raw hardware signals (CPU, Motherboard, MAC, OS, Disk).
  - `DeviceIdentity`: Persistent installation UUID, device code (Crockford Base32), installation time.
  - `DeviceIdentityComparison`: Hardware drift score, matched components, confidence.
  - `DeviceIdentityMode`: Enum (`Installation`, `Hardware`, `Hybrid`).
  - `DeviceInfo`: High-level host metrics (OS name, machine name, memory, architecture).
  - `DevicePlatform`: Enum (`Windows`, `Linux`, `OSX`, `Android`, `iOS`, `Unknown`).
  - `DeviceSignal`: Individual probe result with confidence and freshness.
- **Classification**: Hardware signal probing, device identification, and fingerprint analysis.
- **Ownership**: Raw probes belong to `Sops.Device`. High-level API projections (`DeviceInfo`, `DeviceIdentitySummary`, `DeviceStatus`) belong to `Sops.Models.Device`.

---

## Package 6: `Sops.Drugs`
- **Location**: `src/Sops.Drugs/`
- **Target**: `netstandard2.0`
- **Models / Types**:
  - `Drug`: Full drug catalogue definition (Id, SKU, Barcode, Arabic/English name, Category, DosageForm, Prices, Packages).
  - `DrugCatalog`: Catalogue collections and search metadata.
  - `DrugCategory`: Master category classification.
  - `DrugDosageForm`: Enum (Tablet, Capsule, Syrup, Sachet, Injection, Ointment, etc.).
  - `DrugIngredient`: Active ingredient and concentration.
  - `DrugPackageInfo`: Packaging tier (Box, Strip, Blister) and conversion factor.
  - `ConcentrationUnit`, `StorageType`: Enums.
- **Classification**: Drug catalogue domain logic and search.
- **Ownership**: Canonical contract belongs in `Sops.Models.Catalog.Drug`. `Sops.Drugs` will adopt the canonical contract as its core model.

---

## Package 7: `Sops.Licensing`
- **Location**: `src/Sops.Licensing/`
- **Target**: `netstandard2.0`
- **Models / Types**:
  - `ActivationRequest`, `ActivationResult`: Device license activation models.
  - `DeviceFingerprint`: Historical fingerprint definition in Licensing.
  - `LicenseCertificate`: Signed JWT/JSON certificate with claims and signatures.
  - `LicenseValidationResult`: Status, entitlements, expiration check.
  - `LicensingOptions`: Configuration options.
- **Classification**: Cryptographic license parsing, verification, and claims checking.
- **Ownership**: Crypto algorithms belong to `Sops.Licensing`. Universal license claims and entitlement DTOs belong to `Sops.Models.Licensing`.

---

## Package 8: `Sops.Logging`
- **Location**: `src/Sops.Logging/`
- **Target**: `netstandard2.0`
- **Models / Types**:
  - `SopsConsoleFormat`: Enum (`Default`, `Json`, `Systemd`).
  - `SopsLoggingOptions`: Configuration for sinks, log levels, sensitive redaction.
- **Classification**: Logging infrastructure.
- **Ownership**: Belongs to `Sops.Logging`.

---

## Package 9: `Sops.Mail`
- **Location**: `src/Sops.Mail/`
- **Target**: `netstandard2.0`
- **Models / Types**:
  - `MailAttachment`: Stream, filename, content type.
  - `MailMessage`: Subject, Body, HtmlBody, From, To, Cc, Bcc, Priority.
  - `MailRecipient`: Email and display name.
  - `MailSendResult`: Success flag, MessageId, Timestamp, Error.
- **Classification**: Email dispatch infrastructure.
- **Ownership**: Belongs to `Sops.Mail`. High-level notification DTOs belong to `Sops.Models.Notifications`.

---

## Package 10: `Sops.Payments`
- **Location**: `src/Sops.Payments/`
- **Target**: `netstandard2.0`
- **Models / Types**:
  - `Money`: Immutable value object with minor units (`long AmountMinorUnits`) and `Currency`.
  - `PaymentRequest`: Payment initiation parameters.
  - `PaymentResult`: Gateway transaction result, transaction reference, status.
  - `PaymentBillingData`, `PaymentCustomer`, `PaymentItem`: Supporting DTOs.
  - `PaymentWebhookEvent`, `PaymentVerificationResult`: Gateway verification.
  - `RefundRequest`, `RefundResult`: Refund handling.
- **Classification**: Online payment gateway (Paymob, card processing) client.
- **Ownership**: `Money` becomes standard in `Sops.Models.Common.Money`. Retail POS `Payment` belongs in `Sops.Models.Transactions`, while SaaS billing belongs in `Sops.Models.Billing`.

---

## Package 11: `Sops.UI.Wpf`
- **Location**: `src/Sops.UI.Wpf/`
- **Target**: `net48`
- **Models / Types**:
  - `Enums`: UI state enums (ButtonVariant, AlertType, BadgeVariant).
  - `Theme`: UI theme definition, color palette, design tokens.
  - WPF Controls, Resource Dictionaries, Styles.
- **Classification**: Desktop presentation and UI design system.
- **Ownership**: Belongs to `Sops.UI.Wpf`.

---

## Package 12: `Sops.WhatsApp`
- **Location**: `src/Sops.WhatsApp/`
- **Target**: `netstandard2.0`
- **Models / Types**:
  - `WhatsAppTextMessageRequest`, `WhatsAppTemplateMessageRequest`, `WhatsAppMediaMessageRequest`.
  - `WhatsAppMediaMetadata`, `WhatsAppMediaType`, `WhatsAppMessageResult`.
  - `WhatsAppWebhookPayload`, `WhatsAppDeliveryStatus`.
- **Classification**: Meta Cloud WhatsApp communication client.
- **Ownership**: Provider-specific models belong to `Sops.WhatsApp`. Universal notification envelopes belong to `Sops.Models.Notifications`.
