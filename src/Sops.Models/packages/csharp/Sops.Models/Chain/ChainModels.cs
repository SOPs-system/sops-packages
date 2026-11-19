namespace Sops.Models.Chain;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Sops.Models.Common;

/// <summary>
/// Status of an inter-branch inventory transfer workflow.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TransferStatus
{
    /// <summary>Draft transfer proposal.</summary>
    Draft,
    /// <summary>Formally requested by destination branch.</summary>
    Requested,
    /// <summary>Approved by source branch or warehouse.</summary>
    Approved,
    /// <summary>Rejected by source branch.</summary>
    Rejected,
    /// <summary>Dispatched and handed to courier.</summary>
    Dispatched,
    /// <summary>In physical transit.</summary>
    InTransit,
    /// <summary>Arrived and physical count verified at destination.</summary>
    Received,
    /// <summary>Stock successfully integrated into destination inventory.</summary>
    Completed,
    /// <summary>Transfer cancelled before dispatch.</summary>
    Cancelled
}

/// <summary>
/// Line item in an inter-branch inventory transfer.
/// </summary>
public sealed record TransferLine
{
    /// <summary>
    /// Gets the unique line ID.
    /// </summary>
    [JsonPropertyName("line_id")]
    public Guid LineId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the transferred drug ID.
    /// </summary>
    [JsonPropertyName("drug_id")]
    public Guid DrugId { get; init; }

    /// <summary>
    /// Gets the drug trade name.
    /// </summary>
    [JsonPropertyName("drug_name")]
    public string DrugName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the quantity requested by destination branch.
    /// </summary>
    [JsonPropertyName("requested_quantity")]
    public Quantity RequestedQuantity { get; init; }

    /// <summary>
    /// Gets the quantity approved by source branch.
    /// </summary>
    [JsonPropertyName("approved_quantity")]
    public Quantity? ApprovedQuantity { get; init; }

    /// <summary>
    /// Gets the quantity actually dispatched into transit.
    /// </summary>
    [JsonPropertyName("dispatched_quantity")]
    public Quantity? DispatchedQuantity { get; init; }

    /// <summary>
    /// Gets the quantity physically received at destination.
    /// </summary>
    [JsonPropertyName("received_quantity")]
    public Quantity? ReceivedQuantity { get; init; }

    /// <summary>
    /// Gets the manufacturer batch number dispatched.
    /// </summary>
    [JsonPropertyName("batch_number")]
    public string? BatchNumber { get; init; }

    /// <summary>
    /// Gets the expiration date (YYYY-MM-DD).
    /// </summary>
    [JsonPropertyName("expiry_date")]
    public string? ExpiryDate { get; init; }
}

/// <summary>
/// Multi-stage inter-branch inventory transfer workflow.
/// </summary>
public sealed record TransferRequest
{
    /// <summary>
    /// Gets the transfer ID.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the human-readable sequential transfer code (e.g. "TR-2026-00451").
    /// </summary>
    [JsonPropertyName("transfer_number")]
    public string TransferNumber { get; init; } = string.Empty;

    /// <summary>
    /// Gets the owning chain tenant ID.
    /// </summary>
    [JsonPropertyName("tenant_id")]
    public Guid TenantId { get; init; }

    /// <summary>
    /// Gets the source branch fulfilling the transfer.
    /// </summary>
    [JsonPropertyName("source_branch_id")]
    public Guid SourceBranchId { get; init; }

    /// <summary>
    /// Gets the destination branch requesting the transfer.
    /// </summary>
    [JsonPropertyName("destination_branch_id")]
    public Guid DestinationBranchId { get; init; }

    /// <summary>
    /// Gets the workflow status.
    /// </summary>
    [JsonPropertyName("status")]
    public TransferStatus Status { get; init; } = TransferStatus.Requested;

    /// <summary>
    /// Gets the transfer item lines.
    /// </summary>
    [JsonPropertyName("lines")]
    public IReadOnlyList<TransferLine> Lines { get; init; } = Array.Empty<TransferLine>();

    /// <summary>
    /// Gets the staff member who initiated the request.
    /// </summary>
    [JsonPropertyName("requested_by_user_id")]
    public Guid? RequestedByUserId { get; init; }

    /// <summary>
    /// Gets the manager who approved the transfer.
    /// </summary>
    [JsonPropertyName("approved_by_user_id")]
    public Guid? ApprovedByUserId { get; init; }

    /// <summary>
    /// Gets the timestamp when physical dispatch occurred.
    /// </summary>
    [JsonPropertyName("dispatched_at_utc")]
    public DateTimeOffset? DispatchedAtUtc { get; init; }

    /// <summary>
    /// Gets the timestamp when physical receipt occurred.
    /// </summary>
    [JsonPropertyName("received_at_utc")]
    public DateTimeOffset? ReceivedAtUtc { get; init; }

    /// <summary>
    /// Gets transfer notes or courier instructions.
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    /// <summary>
    /// Gets creation timestamp.
    /// </summary>
    [JsonPropertyName("created_at_utc")]
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Projected branch inventory level snapshot reported to chain management.
/// Non-authoritative: local sales always take precedence over cloud snapshots.
/// </summary>
public sealed record BranchStockSnapshot
{
    /// <summary>
    /// Gets the branch ID.
    /// </summary>
    [JsonPropertyName("branch_id")]
    public Guid BranchId { get; init; }

    /// <summary>
    /// Gets the master drug ID.
    /// </summary>
    [JsonPropertyName("drug_id")]
    public Guid DrugId { get; init; }

    /// <summary>
    /// Gets the drug SKU.
    /// </summary>
    [JsonPropertyName("drug_sku")]
    public string DrugSku { get; init; } = string.Empty;

    /// <summary>
    /// Gets the reported count in base units.
    /// </summary>
    [JsonPropertyName("quantity_in_base_units")]
    public long QuantityInBaseUnits { get; init; }

    /// <summary>
    /// Gets the base unit name.
    /// </summary>
    [JsonPropertyName("base_unit_name")]
    public string BaseUnitName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the monotonic snapshot sequence revision reported by the branch.
    /// </summary>
    [JsonPropertyName("snapshot_revision")]
    public long SnapshotRevision { get; init; }

    /// <summary>
    /// Gets the point-in-time timestamp when the snapshot was captured at the branch.
    /// </summary>
    [JsonPropertyName("snapshot_timestamp_utc")]
    public DateTimeOffset SnapshotTimestampUtc { get; init; }

    /// <summary>
    /// Gets the timestamp when the snapshot was persisted into the cloud.
    /// </summary>
    [JsonPropertyName("last_synchronized_utc")]
    public DateTimeOffset LastSynchronizedUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets whether this snapshot should be considered stale based on delay thresholds.
    /// </summary>
    [JsonPropertyName("is_stale")]
    public bool IsStale { get; init; }
}

/// <summary>
/// Daily aggregated financial summary reported by a branch to chain management.
/// </summary>
public sealed record BranchFinanceDaily
{
    /// <summary>
    /// Gets the branch ID.
    /// </summary>
    [JsonPropertyName("branch_id")]
    public Guid BranchId { get; init; }

    /// <summary>
    /// Gets the business date (YYYY-MM-DD).
    /// </summary>
    [JsonPropertyName("business_date")]
    public string BusinessDate { get; init; } = string.Empty;

    /// <summary>
    /// Gets total gross sales.
    /// </summary>
    [JsonPropertyName("gross_sales")]
    public Money GrossSales { get; init; }

    /// <summary>
    /// Gets total customer returns.
    /// </summary>
    [JsonPropertyName("returns")]
    public Money Returns { get; init; }

    /// <summary>
    /// Gets net sales (Gross - Returns).
    /// </summary>
    [JsonPropertyName("net_sales")]
    public Money NetSales { get; init; }

    /// <summary>
    /// Gets total completed transaction count.
    /// </summary>
    [JsonPropertyName("transaction_count")]
    public int TransactionCount { get; init; }
}
