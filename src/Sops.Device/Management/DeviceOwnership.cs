using System;

namespace Sops.Device.Management
{
    /// <summary>
    /// Represents the organizational ownership and hierarchical boundary of a registered device.
    /// Ensures pharmacy tenant isolation, branch tracking, and user assignment.
    /// </summary>
    public sealed class DeviceOwnership
    {
        /// <summary>
        /// Unique pharmacy tenant or enterprise organization identifier.
        /// </summary>
        public string TenantId { get; }

        /// <summary>
        /// Optional pharmacy branch or physical store identifier.
        /// </summary>
        public string? BranchId { get; }

        /// <summary>
        /// Optional pharmacy license or registration number.
        /// </summary>
        public string? PharmacyId { get; }

        /// <summary>
        /// Optional designated operator or technician user ID assigned to this POS workstation.
        /// </summary>
        public string? AssignedToUserId { get; }

        /// <summary>
        /// Target environment (e.g. "Production", "Staging", "Training").
        /// </summary>
        public string? Environment { get; }

        public DeviceOwnership(
            string tenantId,
            string? branchId = null,
            string? pharmacyId = null,
            string? assignedToUserId = null,
            string? environment = "Production")
        {
            TenantId = !string.IsNullOrWhiteSpace(tenantId)
                ? tenantId.Trim()
                : throw new ArgumentException("TenantId is required for device ownership.", nameof(tenantId));
            BranchId = branchId?.Trim();
            PharmacyId = pharmacyId?.Trim();
            AssignedToUserId = assignedToUserId?.Trim();
            Environment = environment?.Trim() ?? "Production";
        }

        public DeviceOwnership WithBranch(string? branchId) =>
            new DeviceOwnership(TenantId, branchId, PharmacyId, AssignedToUserId, Environment);

        public DeviceOwnership WithUser(string? userId) =>
            new DeviceOwnership(TenantId, BranchId, PharmacyId, userId, Environment);
    }
}
