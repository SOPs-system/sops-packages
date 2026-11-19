using System;
using System.Collections.Generic;

namespace Sops.Device.Management
{
    /// <summary>
    /// Formal specification and architectural blueprint for implementing production server-side
    /// device registry endpoints in the SOPs cloud or on-premise backend services.
    /// <para>
    /// <b>Security Notice:</b> The client SDK provides transport-neutral contracts (<see cref="IDeviceRegistry"/>,
    /// <see cref="IDeviceManagementService"/>) and an in-memory adapter (<see cref="InMemoryDeviceRegistry"/>) for testing
    /// and local development. The in-memory adapter is <b>NOT</b> sufficient for distributed production deployments
    /// because memory state is lost on process restart and cannot coordinate across multiple backend replicas.
    /// </para>
    /// </summary>
    public static class DeviceRegistryServerSpecification
    {
        /// <summary>
        /// Standardized REST API route contracts required for remote device registry implementation.
        /// </summary>
        public static class Routes
        {
            public const string Register = "/api/v1/devices/register";
            public const string GetById = "/api/v1/devices/{registrationId}";
            public const string GetByCode = "/api/v1/devices/by-code/{deviceCode}";
            public const string Heartbeat = "/api/v1/devices/{registrationId}/heartbeat";
            public const string RotateCredentials = "/api/v1/devices/{registrationId}/rotate-credentials";
            public const string ListByTenant = "/api/v1/tenants/{tenantId}/devices";
            public const string Suspend = "/api/v1/devices/{registrationId}/suspend";
            public const string Reactivate = "/api/v1/devices/{registrationId}/reactivate";
            public const string Revoke = "/api/v1/devices/{registrationId}/revoke";
            public const string Replace = "/api/v1/devices/{registrationId}/replace";
            public const string AuditEvents = "/api/v1/devices/{registrationId}/audit-events";
        }

        /// <summary>
        /// Explicit catalog of operations that strictly require authoritative server-side implementation and enforcement.
        /// Local workstations cannot autonomously decide these operations.
        /// </summary>
        public static readonly IReadOnlyList<string> AuthoritativeServerOperations = new[]
        {
            "RegistrationApprovalAndStatusTransition",
            "CryptographicDeviceTokenIssuanceAndSignature",
            "DurableIdempotencyDeduplicationWithTtl",
            "TenantDeviceCodeUniquenessEnforcement",
            "CrossTenantDeviceCollisionPrevention",
            "AuthoritativeTimeSynchronizationForClockRollbackResistance",
            "MonotonicRevocationAndSuspensionEnforcement",
            "AuthorizedDeviceHardwareMigrationAndReplacement",
            "CentralizedAuditTrailPersistenceAndImmutability"
        };

        /// <summary>
        /// Mandatory database table schema, index, and concurrency constraints required by any production database backing the registry.
        /// </summary>
        public static class DatabaseRequirements
        {
            /// <summary>Unique index on TenantId + DeviceCode ensures no two workstations within an organization share a code.</summary>
            public const string UniqueDeviceCodeIndex = "CREATE UNIQUE INDEX UX_Devices_Tenant_DeviceCode ON Devices (TenantId, DeviceCode);";

            /// <summary>Unique index on RegistrationId provides primary key lookup.</summary>
            public const string UniqueRegistrationIdIndex = "CREATE UNIQUE INDEX UX_Devices_RegistrationId ON Devices (RegistrationId);";

            /// <summary>Durable idempotency table with TTL to prevent replay attacks across backend restarts.</summary>
            public const string IdempotencyTable =
                "CREATE TABLE DeviceIdempotencyRecords (" +
                "  IdempotencyToken VARCHAR(128) PRIMARY KEY," +
                "  PayloadSignature VARCHAR(64) NOT NULL," +
                "  ResultStatus VARCHAR(32) NOT NULL," +
                "  RegistrationId VARCHAR(64) NULL," +
                "  CachedResponseJson TEXT NOT NULL," +
                "  CreatedAtUtc DATETIME2 NOT NULL," +
                "  ExpiresAtUtc DATETIME2 NOT NULL" +
                ");";

            /// <summary>Optimistic concurrency row version to prevent race conditions during updates.</summary>
            public const string ConcurrencyControl = "Devices table must maintain a BIGINT RowVersion or TIMESTAMP for optimistic concurrency.";
        }
    }
}
