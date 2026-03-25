namespace DataFlow.Blazor.Events;

/// <summary>
/// Emitted by the graph immediately after the execution pipeline is built but before
/// any block starts executing. Captures the complete static topology — block names,
/// CLR types, item labels, and edge wiring — in a single event.
///
/// This is a structural declaration event, not a workflow audit event:
/// - It is NOT included in the audit log (History tab).
/// - It does NOT trigger a snapshot.
/// - It IS a delta event so the client receives it for running flows.
/// - Its label data IS folded into BlockSnapshot so completed-flow snapshots carry labels.
/// </summary>
public record FlowGraphDefinedEvent(
    IReadOnlyList<BlockDefinition> Blocks,
    IReadOnlyList<EdgeDefinition>  Edges,
    DateTime Timestamp
) : IDataFlowEvent;

/// <summary>
/// Static description of one block in the graph at the time the flow started.
/// </summary>
public record BlockDefinition(
    string  BlockName,
    string  BlockType,
    /// <summary>Optional friendly display name configured via block metadata. Null when not set.</summary>
    string? DisplayName,
    /// <summary>Human-readable label for the block's input item type. Null for source blocks.</summary>
    string? InputItemLabel,
    /// <summary>Human-readable label for the block's output item type. Null for sink blocks.</summary>
    string? OutputItemLabel,
    bool    IsSource,
    bool    IsSink
);

/// <summary>
/// Static description of one directed edge in the graph.
/// </summary>
public record EdgeDefinition(
    string SourceBlock,
    string TargetBlock,
    /// <summary>Configured buffer capacity. Null for unbounded (pass-through) edges.</summary>
    int?   BufferCapacity,
    /// <summary>
    /// Delivery semantics for this edge: "Broadcast" (every target receives every item),
    /// "Competing" (targets share a single channel — each item consumed by exactly one),
    /// or "Routed" (items directed to specific targets by a routing key).
    /// Defaults to "Broadcast" to preserve compatibility with older serialized
    /// FlowGraphDefinedEvent payloads and clients that predate this field.
    /// </summary>
    string EdgeType = "Broadcast"
);
