/// <summary>
/// SecretAuditEventOutput — DTO for a single audit event in API responses.
/// Mirrors the backend SecretAuditEventOutput (sequential ContentRead events
/// can be merged server-side into a single output item with a populated
/// ChunkIds list; non-merged events use the scalar fields only).
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public class SecretAuditEventOutput
    {
        public string EventType { get; set; } = string.Empty;

        public DateTime OccurredAt { get; set; }

        public DateTime? FirstAt { get; set; }

        public DateTime? LastAt { get; set; }

        public long SequenceNumber { get; set; }

        public long? SequenceFrom { get; set; }

        public long? SequenceTo { get; set; }

        public AuditActorOutput Actor { get; set; } = new();

        public string? ContentId { get; set; }

        public List<string>? ChunkIds { get; set; }

        public JsonElement? Payload { get; set; }

        public string Hash { get; set; } = string.Empty;

        public string PrevHash { get; set; } = string.Empty;
    }
}
