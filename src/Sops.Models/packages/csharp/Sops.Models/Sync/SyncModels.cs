namespace Sops.Models.Sync;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Mutation classification in event synchronization.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MutationKind
{
    /// <summary>New entity created.</summary>
    Created,
    /// <summary>Existing entity updated.</summary>
    Updated,
    /// <summary>Entity soft or hard deleted.</summary>
    Deleted,
    /// <summary>Domain action or transition executed.</summary>
    ActionExecuted
}

/// <summary>
/// Universal domain mutation event envelope for offline-first replication.
/// </summary>
public sealed record SyncEventEnvelope
{
    /// <summary>
    /// Gets the unique event ID for idempotency and deduplication.
    /// </summary>
    [JsonPropertyName("event_id")]
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the aggregate root type name (e.g. "Transaction", "StockLot", "TransferRequest").
    /// </summary>
    [JsonPropertyName("aggregate_type")]
    public string AggregateType { get; init; } = string.Empty;

    /// <summary>
    /// Gets the string identifier of the modified aggregate.
    /// </summary>
    [JsonPropertyName("aggregate_id")]
    public string AggregateId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the domain event name (e.g. "TransactionCompleted", "StockAdjusted").
    /// </summary>
    [JsonPropertyName("event_type")]
    public string EventType { get; init; } = string.Empty;

    /// <summary>
    /// Gets the mutation kind.
    /// </summary>
    [JsonPropertyName("mutation_kind")]
    public MutationKind MutationKind { get; init; } = MutationKind.Created;

    /// <summary>
    /// Gets the point-in-time timestamp when the event occurred locally.
    /// </summary>
    [JsonPropertyName("occurred_at_utc")]
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the tenant chain ID.
    /// </summary>
    [JsonPropertyName("tenant_id")]
    public Guid TenantId { get; init; }

    /// <summary>
    /// Gets the branch ID where the event was generated.
    /// </summary>
    [JsonPropertyName("branch_id")]
    public Guid BranchId { get; init; }

    /// <summary>
    /// Gets the serialized JSON payload matching the canonical schema.
    /// </summary>
    [JsonPropertyName("payload_json")]
    public string PayloadJson { get; init; } = string.Empty;

    /// <summary>
    /// Gets the schema version (e.g. "v1").
    /// </summary>
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; init; } = "v1";
}

/// <summary>
/// Batch of sync events uploaded by a branch node upon reconnecting to the internet.
/// </summary>
public sealed record SyncBatch
{
    /// <summary>
    /// Gets the unique batch ID.
    /// </summary>
    [JsonPropertyName("batch_id")]
    public Guid BatchId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the source branch ID.
    /// </summary>
    [JsonPropertyName("branch_id")]
    public Guid BranchId { get; init; }

    /// <summary>
    /// Gets the sequence counter start.
    /// </summary>
    [JsonPropertyName("client_sequence_start")]
    public long ClientSequenceStart { get; init; }

    /// <summary>
    /// Gets the sequence counter end.
    /// </summary>
    [JsonPropertyName("client_sequence_end")]
    public long ClientSequenceEnd { get; init; }

    /// <summary>
    /// Gets the ordered list of synchronization events.
    /// </summary>
    [JsonPropertyName("events")]
    public IReadOnlyList<SyncEventEnvelope> Events { get; init; } = Array.Empty<SyncEventEnvelope>();

    /// <summary>
    /// Gets the transmission timestamp.
    /// </summary>
    [JsonPropertyName("sent_at_utc")]
    public DateTimeOffset SentAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Granular row-level change descriptor for delta synchronization.
/// </summary>
public sealed record RecordChange
{
    /// <summary>
    /// Gets the table name.
    /// </summary>
    [JsonPropertyName("table_name")]
    public string TableName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the primary key string of the record.
    /// </summary>
    [JsonPropertyName("record_id")]
    public string RecordId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the change type ("Insert", "Update", "Delete").
    /// </summary>
    [JsonPropertyName("change_type")]
    public string ChangeType { get; init; } = "Insert";

    /// <summary>
    /// Gets the serialized row data JSON.
    /// </summary>
    [JsonPropertyName("data_json")]
    public string? DataJson { get; init; }

    /// <summary>
    /// Gets the change timestamp.
    /// </summary>
    [JsonPropertyName("changed_at_utc")]
    public DateTimeOffset ChangedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Status of an approval change request.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChangeRequestStatus
{
    /// <summary>Pending manager review.</summary>
    Pending,
    /// <summary>Approved by manager.</summary>
    Approved,
    /// <summary>Rejected by manager.</summary>
    Rejected,
    /// <summary>Expired without review.</summary>
    Expired
}

/// <summary>
/// Management approval decision for a sensitive operation.
/// </summary>
public sealed record ChangeDecision
{
    /// <summary>
    /// Gets the decision ("Approved" or "Rejected").
    /// </summary>
    [JsonPropertyName("decision")]
    public string Decision { get; init; } = "Approved";

    /// <summary>
    /// Gets the manager user ID.
    /// </summary>
    [JsonPropertyName("decided_by_user_id")]
    public Guid DecidedByUserId { get; init; }

    /// <summary>
    /// Gets the decision timestamp.
    /// </summary>
    [JsonPropertyName("decided_at_utc")]
    public DateTimeOffset DecidedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets manager review notes.
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}

/// <summary>
/// Request for manager approval of a sensitive operation (e.g. large price discount, transaction void, stock write-off).
/// </summary>
public sealed record ChangeRequest
{
    /// <summary>
    /// Gets the request ID.
    /// </summary>
    [JsonPropertyName("request_id")]
    public Guid RequestId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the tenant chain ID.
    /// </summary>
    [JsonPropertyName("tenant_id")]
    public Guid TenantId { get; init; }

    /// <summary>
    /// Gets the branch ID.
    /// </summary>
    [JsonPropertyName("branch_id")]
    public Guid BranchId { get; init; }

    /// <summary>
    /// Gets the staff member requesting approval.
    /// </summary>
    [JsonPropertyName("requested_by_user_id")]
    public Guid RequestedByUserId { get; init; }

    /// <summary>
    /// Gets the type of sensitive change (e.g. "StockAdjustment", "TransactionVoid", "HighDiscount").
    /// </summary>
    [JsonPropertyName("change_type")]
    public string ChangeType { get; init; } = string.Empty;

    /// <summary>
    /// Gets the operational justification.
    /// </summary>
    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Gets the request status.
    /// </summary>
    [JsonPropertyName("status")]
    public ChangeRequestStatus Status { get; init; } = ChangeRequestStatus.Pending;

    /// <summary>
    /// Gets the decision details if resolved.
    /// </summary>
    [JsonPropertyName("decision")]
    public ChangeDecision? Decision { get; init; }

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    [JsonPropertyName("created_at_utc")]
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
