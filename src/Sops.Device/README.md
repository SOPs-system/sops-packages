# Sops.Device

Universal cross-platform device identity, hardware signal collection, device-code generation, identity persistence, and hardware-change detection SDK for **Smart Operating Pharmacies (SOPs)**.

---

## 1. Supported Target Frameworks & Runtimes

- **`.NET Standard 2.0`**: Broad compatibility across legacy and modern .NET runtimes.
- **`.NET Framework 4.8`**: Full desktop POS compatibility from Windows 7 SP1 to Windows 11.
- **`.NET 8.0`**: Modern high-performance desktop, cloud, and containerized Linux/macOS environments.

### Supported Operating Systems
| Operating System | Support Mode | Mechanism | Privileges Required |
|---|---|---|---|
| **Windows** (7 SP1 to 11) | Direct BCL & OS APIs | World-readable Registry (`advapi32.dll`), BIOS descriptor, System drive serial, WMI fallback, physical NIC | None (Standard user) |
| **Linux** (kernel 2.6+) | Direct BCL & OS APIs | `/etc/machine-id`, `/var/lib/dbus/machine-id`, `/sys/class/dmi/id/product_uuid`, `/sys/class/net` | None (Standard user) |
| **macOS** (10.12+) | Direct BCL & OS APIs | `IOPlatformUUID` via non-root `ioreg`, physical network interfaces | None (Standard user) |
| **Android** | Native Platform Adapter Hook | `DeviceOptions.NativeSignalResolver` (e.g. `Settings.Secure.ANDROID_ID` via host MAUI app) | Standard app sandbox |
| **iOS / iPadOS** | Native Platform Adapter Hook | `DeviceOptions.NativeSignalResolver` (e.g. `UIDevice.identifierForVendor` via host MAUI app) | Standard app sandbox |
| **Other / Unknown** | Graceful Fallback Provider | Cross-platform core count, sanitized machine name hash, architecture descriptor | None |

> **Note on Mobile Verification**: Standard desktop NuGet packages avoid forcing Android and iOS compiler workloads on Windows POS workstations. On mobile, host apps supply native sandbox identifiers via `DeviceOptions.NativeSignalResolver`.

---

## 2. Key Architectural Guarantees

### Separation of Concerns
1. **`InstallationId`**: Cryptographically random software installation UUID (`Guid`). Remains completely stable across all hardware modifications, driver updates, and peripheral additions.
2. **`HardwareFingerprint`**: Deterministic, versioned, canonically sorted SHA-256 combined hash of evaluated physical and operating system hardware anchors. **Does NOT conflate software installation identifiers with physical hardware.**
3. **`DeviceCode`**: Ambiguity-free Crockford Base32 support and registration code (e.g. `SOP-WIN-7K2M-9Q4D`) featuring an integrated modulo-32 check character.

### Privacy & Cryptographic Security
- **No Raw Hardware Exposure**: Raw MAC addresses, drive serials, and system UUIDs are strictly private to internal normalizers. All public APIs, logs, and telemetry expose only cryptographically processed hashes.
- **Keyed HMAC-SHA256 Privacy Model (Version 2)**:
  - *Low-Entropy Hardware Risk*: Predictable hardware identifiers (such as MAC addresses, where the top 24 bits are publicly registered OUIs and lower 24 bits represent device serials) only have ~24 bits of variable entropy (16.7 million combinations). An unkeyed hash or fixed public salt (like `sops:device:v1`) allows an attacker to exhaust the entire candidate keyspace in seconds using offline dictionary or rainbow-table attacks.
  - *Cryptographic Mitigation*: Version 2 introduces keyed `HMACSHA256` using securely injected secret keys (`DeviceOptions.SecretKey` or `SecretKeyHex`). Without the secret key, an offline attacker cannot verify candidate hardware identifiers.
  - *Key Scoping (`KeyScope`)*: Keys can be scoped to `Installation`, `Application`, `Tenant`, or `Server`. Scoping to `Tenant` prevents cross-tenant tracking and ensures the same physical hardware produces completely distinct fingerprints across separate pharmacy organizations.
- **Legacy Version 1 Compatibility**: Unkeyed SHA-256 with `ApplicationSalt` remains fully supported for backward compatibility with previously issued license bindings.
- **Not an Authentication Token**: Device codes and hardware fingerprints are identification anchors and drift evidence; they do not function as authentication passwords or proof-of-possession tokens. Strong device authentication utilizes cryptographic server tokens (`ServerToken`) issued upon registration.

### OEM Placeholder & Multicast Rejection
Automatically rejects dummy manufacturer strings and non-hardware artifacts:
- OEM placeholders: `To be filled by O.E.M.`, `Default string`, `System Serial Number`, `None`, `Not Specified`, `123456789`.
- All-zero / all-one GUIDs: `00000000-0000-0000-0000-000000000000`, `FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF`.
- Virtualization artifacts: `VMware-`, `VirtualBox`, `QEMU`, `Bochs`, `Hyper-V`.
- Invalid / Multicast MACs: `00:00:00:00:00:00`, `FF:FF:FF:FF:FF:FF`, IPv4 multicast (`01:00:5E:...`), IPv6 multicast (`33:33:...`), and any MAC with the IEEE 802 Individual/Group multicast bit set.

---

## 3. Installation & Dependency Direction

```xml
<PackageReference Include="Sops.Device" Version="0.1.0" />
```

Preferred dependency hierarchy:
```text
Sops.Configuration ──┐
                     ├──> Sops.Device
Sops.Logging ────────┘          ↑
                                │ (Consumes IDeviceService)
                          Sops.Licensing
                                ↑
                         Application UI (WPF / Desktop POS)
```

`Sops.Device` has zero dependency on `Sops.Licensing` or `Sops.DataLayer`.

---

## 4. Configuration & Dependency Injection

```csharp
using Sops.Device.Configuration;
using Sops.Device.Extensions;
using Sops.Device.Models;

services.AddSopsDevice(options =>
{
    // Custom support code prefix (defaults to "SOP")
    options.CodePrefix = "SOP";

    // Mode: Hybrid (recommended), Installation, or Hardware
    options.IdentityMode = DeviceIdentityMode.Hybrid;

    // Salting domain / tenant key to prevent cross-app tracking & dictionary attacks
    options.ApplicationSalt = "pharmacy:tenant:production";

    // Hardware signal settings
    options.EnableHardwareFingerprint = true;
    options.IncludeMacAddresses = true;
    options.MaxNetworkAdapters = 2;
    options.MaxNetworkWeightCap = 2.0;

    // Persistence settings
    options.StoragePath = @"C:\ProgramData\SOPs\Device\identity.json";
    options.StorageRetryCount = 3;
    options.StorageRetryDelayMs = 100;

    // Optional native resolver for MAUI / mobile hosts
    // options.NativeSignalResolver = key => key switch {
    //     "AndroidId" => Android.Provider.Settings.Secure.GetString(context.ContentResolver, "android_id"),
    //     _ => null
    // };
});
```

---

## 5. Public API Usage

```csharp
using Sops.Device.Abstractions;
using Sops.Device.Models;

public class TerminalManager
{
    private readonly IDeviceService _deviceService;
    private readonly IDeviceCodeGenerator _codeGenerator;

    public TerminalManager(IDeviceService deviceService, IDeviceCodeGenerator codeGenerator)
    {
        _deviceService = deviceService;
        _codeGenerator = codeGenerator;
    }

    public async Task InitializeTerminalAsync(CancellationToken cancellationToken = default)
    {
        // 1. Retrieve the stable, human-readable device code
        string deviceCode = await _deviceService.GetDeviceCodeAsync(cancellationToken);
        Console.WriteLine($"Support Code: {deviceCode}");

        // Validate code format and check character
        if (_codeGenerator.ValidateCode(deviceCode, out int version, out string? error))
        {
            Console.WriteLine($"Code valid! Format Version: {version}");
        }

        // 2. Retrieve safe diagnostic metadata
        DeviceInfo info = await _deviceService.GetDeviceInfoAsync(cancellationToken);
        Console.WriteLine($"Platform: {info.Platform} ({info.PlatformVersion})");
        Console.WriteLine($"Installation ID: {info.InstallationId}");
        Console.WriteLine($"Device Type: {info.DeviceType}");

        // 3. Retrieve pure hardware fingerprint
        DeviceFingerprint currentFp = await _deviceService.GetFingerprintAsync(cancellationToken);
        Console.WriteLine($"Fingerprint Hash: {currentFp.CombinedHash}");

        // 4. Compare with previously stored baseline fingerprint (e.g. from cloud activation)
        // DeviceIdentityComparison diff = await _deviceService.CompareAsync(baselineFp, cancellationToken);
        // if (diff.HasChanged)
        // {
        //     Console.WriteLine($"Hardware drift: {diff.SimilarityScore:P1} ({diff.Confidence})");
        //     Console.WriteLine($"Diagnosis: {diff.Assessment}");
        // }
    }
}
```

---

---

## 6. Universal Device Management Contracts

`Sops.Device.Management` provides contracts and abstractions for registering and managing devices across the pharmacy ecosystem:

### Models & Interfaces
- **`IDeviceRegistry`**: Client-facing interface for workstation registration and periodic status heartbeats.
  - `RegisterAsync(DeviceRegistrationRequest request, ...)`: Idempotent registration with replay protection via client-supplied `IdempotencyToken`.
  - `ReportHeartbeatAsync(DeviceHeartbeat heartbeat, ...)`: Reports liveness, updates `LastHeartbeatUtc`, and receives server status orders.
- **`IDeviceManagementService`**: Administrative interface for privileged operations.
  - `ListDevicesAsync(tenantId, statusFilter)`: Queries workstations by pharmacy tenant and status.
  - `RenameDeviceAsync`, `ReassignDeviceAsync`: Updates display names and branch assignments.
  - `SuspendDeviceAsync`, `ReactivateDeviceAsync`: Administrative hold and reactivation.
  - `RevokeDeviceAsync`: Permanent revocation for decommissioned, stolen, or compromised devices.
  - `ReplaceDeviceAsync`: Workstation migration associating new installations with retired hardware.
  - `GetAuditEventsAsync`: Immutable chronological audit logs.
- **`DeviceStatus` Lifecycle States**: `Unregistered`, `Pending`, `Active`, `Suspended`, `Revoked`, `Replaced`.
- **`InMemoryDeviceRegistry`**: Thread-safe in-memory implementation supporting idempotency deduplication, device-code collision detection, and full audit logging.

---

## 7. Offline-First Operation & Grace Period Reconciliation

SOPs pharmacies operate in environments with intermittent internet connectivity:
- **`LocalDeviceRegistration`**: Maintains locally cached snapshot of registration status (`InstallationId`, `DeviceCode`, `RegistrationId`, `LastKnownStatus`, `LastSyncAtUtc`, `ServerRegistrationToken`).
- **`DeviceOfflinePolicy` & Policy Enforcement**:
  - `GracePeriod` (default: 14 days, configurable).
  - `MaxClockRollbackTolerance` (default: 1 hour) detecting client clock manipulation.
  - `AllowOfflineOperations` flag governing whether offline operations are permitted.
  - Returns strongly-typed `DeviceOfflineState`: `ActiveOnline`, `WithinGracePeriod`, `OfflineGraceExpired`, `ClockRollbackDetected`, `Revoked`, `Suspended`, `NeverRegistered`, `CredentialsInvalid`.
- **Monotonic Revocation Protection**:
  - Once marked `Revoked`, stale cached responses or delayed network packets can NEVER un-revoke a device.
- **Transient Failure Resilience**: When a heartbeat or sync fails due to network outage, the device preserves its last-known `Active` status during the grace window and does NOT wipe local credentials.
- **Authoritative Status Precedence**: Authoritative server status (`Revoked` or `Suspended`) immediately halts remote access and overrides local cached state upon subsequent connectivity.

---

## 8. Protected Credential Storage & Security-Safe Diagnostics

- **`IDeviceCredentialStore` & `ProtectedFileDeviceCredentialStore`**:
  - Windows: Hardware/user-bound DPAPI encryption via direct `CryptProtectData` Win32 P/Invoke (`Crypt32.dll`).
  - Linux/macOS/Fallbacks: AES-256-CBC authenticated storage with atomic file updates (`.tmp` + `File.Replace`) and `.lock` mutexing.
  - Credential Loss Detection: Throws `DeviceCredentialStorageException` with `IsCredentialLost = true` when decryption fails, preventing silent re-registration loops.
- **Safe Non-Sensitive Thumbprints**:
  - `DeviceCredential.Thumbprint`: Safe truncated SHA-256 prefix of secret tokens for diagnostics and logs.
  - `DeviceCredential.DeviceToken`: Confidential bearer token, excluded from string conversions and diagnostic outputs.
  - Zero PII / raw MAC leakage: Hardware adapters produce irreversible hashes or keyed HMAC digests only.

---

## 9. Server Specification Contract

- **`DeviceRegistryServerSpecification`**: Documents production REST routes (`POST /api/v1/devices/register`, `POST /api/v1/devices/{id}/heartbeat`, `POST /api/v1/devices/{id}/revoke`, `POST /api/v1/devices/{id}/replace`), schema requirements, unique indexes (`TenantId + DeviceCode`, `TenantId + InstallationId`), and distributed locking expectations for backend engineers.

---

## 8. Integration with `Sops.Licensing`

`Sops.Licensing` consumes `Sops.Device` while preserving the strict dependency boundary:
- **Separation of Responsibilities**:
  - `Sops.Device`: Responsible for device identity, hardware fingerprinting, and device-management contracts. Does NOT manage seats or subscriptions.
  - `Sops.Licensing`: Responsible for license validation, seat limits, activation, and cryptographic signatures.
- **Revocation Enforcement**:
  - `LicensingOptions.DeviceStatusProvider`: Connects `Sops.Licensing` with device registration status.
  - If a device is `Revoked` or `Suspended`, `LicenseValidator` immediately fails validation (`DeviceMismatch`), preventing unauthorized access even if the physical hardware fingerprint matches.
- **Backward Compatibility**:
  - Existing node-locked licenses containing legacy hash bindings remain **100% backward-compatible**: `Matches()` checks both the new `Sops.Device` fingerprint and the legacy fallback algorithm.

---

## 9. Persistence, Recovery & Cross-Process Safety

- **Atomic Writes**: Writes first to a unique temporary file (`identity.json.<guid>.tmp`) and executes an atomic replacement (`File.Replace`), ensuring no corrupted half-written files occur during power loss.
- **Cross-Process Locking**: Employs an exclusive file-level lock (`identity.json.lock`) with exponential backoff retry to eliminate race conditions between concurrent processes starting at the same millisecond.
- **Corruption Isolation**: If JSON syntax corruption or checksum tampering is detected, the file is automatically archived to `identity.json.corrupt.<timestamp>` and logged without crashing the host application.
- **Read-Only / Lock Contention**: If an existing identity file cannot be accessed due to temporary antivirus locking or permissions, the SDK raises `DeviceStorageException` rather than silently generating a duplicate identity.
