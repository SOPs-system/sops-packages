# Sops.DataOperations

Unified offline-first synchronization, record change approvals, authenticated encrypted database backup, safe restoration, and cloud reconciliation package for the Smart Operating Pharmacies (SOPs) ecosystem.

## Features

- **Offline-First Synchronization**: Bidirectional synchronization between Local SQLCipher, Global Cloud PostgreSQL, and Tenant Projections with durable outbox/inbox processing, checkpointing, and replay protection.
- **Change Request & Approvals**: Secure multi-tiered approval workflows with separation of duties, dual approvals for high-risk operations, and transactional state application.
- **Encrypted Database Backups**: Stream-based authenticated encryption (AES-256-CBC + HMAC-SHA256) with bounded memory usage, authenticated manifests, zero plaintext disk snapshots, and automated retention management.
- **Safe Staged Restoration**: 10-step verified restore lifecycle with preflight verification, maintenance-mode locking, pre-restore safety copies, rollback protection, and staging verification.
- **Post-Restore Cloud Reconciliation**: Recovery-generation rotation, checkpoint drift reconciliation, duplicate detection, and conflict quarantine.
- **Multi-Platform Support**: Multi-targets `netstandard2.0`, `net8.0`, and `net10.0` with verified .NET Framework 4.8 consumption for Windows POS desktop environments.
