using System;

namespace Sops.DataLayer.Models.Events
{
    /// <summary>
    /// Versioned envelope for in-process application events and remote backend notifications.
    /// Captures source identity, entity keys, sequence numbers, and branch tenancy.
    /// </summary>
    public sealed class EventEnvelope
    {
        public string EventId { get; }
        public string EventType { get; }
        public string SchemaVersion { get; }
        public string Source { get; }
        public string? EntityType { get; }
        public string? EntityId { get; }
        public string? PharmacyId { get; }
        public string? BranchId { get; }
        public long? CursorOrSequence { get; }
        public DateTimeOffset OccurredAtUtc { get; }
        public string? CorrelationId { get; }
        public object? Payload { get; }

        public EventEnvelope(
            string eventType,
            object? payload = null,
            string? source = null,
            string? entityType = null,
            string? entityId = null,
            string? pharmacyId = null,
            string? branchId = null,
            long? cursorOrSequence = null,
            string? correlationId = null,
            string? eventId = null,
            string schemaVersion = "1.0")
        {
            if (string.IsNullOrWhiteSpace(eventType)) throw new ArgumentException("EventType cannot be empty.", nameof(eventType));

            EventId = eventId ?? Guid.NewGuid().ToString("N");
            EventType = eventType.Trim();
            SchemaVersion = schemaVersion ?? "1.0";
            Payload = payload;
            Source = source ?? "Client";
            EntityType = entityType;
            EntityId = entityId;
            PharmacyId = pharmacyId;
            BranchId = branchId;
            CursorOrSequence = cursorOrSequence;
            CorrelationId = correlationId;
            OccurredAtUtc = DateTimeOffset.UtcNow;
        }

        public static EventEnvelope Create<T>(string eventType, T payload, string? source = null, string? correlationId = null, string? pharmacyId = null, string? branchId = null) =>
            new EventEnvelope(eventType, payload, source: source, correlationId: correlationId, pharmacyId: pharmacyId, branchId: branchId);
    }
}
