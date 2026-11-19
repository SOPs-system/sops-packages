# Sops.Licensing

Production-quality, cross-platform licensing, entitlement evaluation, digital certificate validation, device identity binding, and OS-specific secure storage infrastructure for SOPs applications and services.

## Features

- **Cross-Platform Secure Storage (`ISecureStorage`)**:
  - Windows: Native Windows Credential Manager (`Advapi32.dll`) and DPAPI-protected file storage (`Crypt32.dll`, CurrentUser and LocalMachine scopes).
  - macOS: Native Keychain Services (`Security.framework`).
  - Linux: Secret Service API and AES-256 encrypted vault with protected key derivation for headless environments.
  - Fail-closed security: Never falls back silently to plaintext.
- **Universal Entitlements (`IEntitlementEvaluator`)**:
  - Arbitrary feature authorization (e.g., `app.login`, `database.connect`, `database.record.read`, `pos.access`).
  - Default-deny security model.
  - Exact matches, explicit denials (`!feature`), scoped wildcards (`database.*`), numeric limits, and per-feature expiration.
- **Digitally Signed Certificates (`ILicenseValidator`)**:
  - Asymmetric cryptographic signatures (ECDSA / RSA) with canonical JSON serialization.
  - Trusted public key verification without embedding private keys.
  - Clock rollback protection with secure monotonic trusted timestamps.
- **Device Identity Binding (`IDeviceIdentifier`)**:
  - Stable installation identity combined with hardware signals.
  - Privacy-preserving SHA-256 hashing.
  - Fuzzy tolerance for legitimate component upgrades.
- **Online & Offline Activation (`ILicenseManager`)**:
  - Online HTTPS activation, refresh, deactivation, and revocation checking.
  - Offline activation request generation and cryptographically signed response activation.
- **Full Dependency Injection & Standalone Support**:
  - Optional `services.AddSopsLicensing(configuration)`.
  - Zero-configuration standalone instantiate-and-use model.
