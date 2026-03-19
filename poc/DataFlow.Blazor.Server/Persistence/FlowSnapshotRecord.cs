namespace DataFlow.Blazor.Server.Persistence;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// Materialized snapshot of a flow's state at a given point in its event log.
/// Created on FlowRunCompleted (and optionally every N events for long-running flows).
/// When the client loads a flow, it fetches the latest snapshot + events after it —
/// never re-folding the entire log from scratch.
/// </summary>
[Table("FlowSnapshotRecords")]
public class FlowSnapshotRecord
{
    [Key]
    public Guid FlowRunId { get; set; }

    /// <summary>
    /// The SequenceNumber of the last event folded into this snapshot.
    /// Used to compute the delta query: SequenceNumber > AsOfSequence.
    /// </summary>
    public long AsOfSequence { get; set; }

    /// <summary>
    /// JSON-serialized FlowSnapshot.
    /// </summary>
    public string SnapshotJson { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Optional tenant identifier for multi-tenant deployments.
    /// </summary>
    public Guid? TenantId { get; set; }
}
