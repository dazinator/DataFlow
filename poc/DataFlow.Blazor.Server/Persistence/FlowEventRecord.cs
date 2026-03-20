namespace DataFlow.Blazor.Server.Persistence;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// Append-only record of a single DataFlow event. Never mutated after insert.
/// Id (DB identity) is the catch-up cursor:
///   "give me all events WHERE FlowRunId = @id AND Id > @lastSeen"
/// </summary>
[Table("FlowEventRecords")]
public class FlowEventRecord
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    public Guid FlowRunId { get; set; }

    /// <summary>
    /// Discriminator used for deserialization, e.g. nameof(BlockStartedEvent).
    /// </summary>
    [MaxLength(128)]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// JSON-serialized event payload. Deserialized client-side using EventDeserializer.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>
    /// Optional tenant identifier for multi-tenant deployments.
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Stable identity of the originating work item (e.g. a queue message ID).
    /// Populated from FlowStartedEvent.CorrelationId; null for standalone runs.
    /// Indexed to allow efficient "all attempts for this message" queries.
    /// </summary>
    public Guid? CorrelationId { get; set; }
}
