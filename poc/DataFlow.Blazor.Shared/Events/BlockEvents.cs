namespace DataFlow.Blazor.Events;

public record BlockStartedEvent(
    string BlockName,
    string BlockType,
    DateTime Timestamp,
    bool IsSource = false
) : IDataFlowEvent;

public record BlockCompletedEvent(
    string BlockName,
    bool Success,
    DateTime Timestamp,
    string? ErrorMessage = null
) : IDataFlowEvent;

/// <summary>
/// Periodic (500 ms) and final metric snapshot for a block.
/// <para><b>ItemsConsumed</b> — items pulled from the block's input channel(s) so far.
/// Always 0 for source blocks (no input). For epoch-stream inputs the count reflects
/// epoch-stream containers, not inner items.</para>
/// <para><b>ItemsProduced</b> — items written into the block's output channel(s) so far.
/// Always 0 for pure sink blocks (no outgoing edges).</para>
/// </summary>
public record BlockMetricsEvent(
    string BlockName,
    long ItemsConsumed,
    long ItemsProduced,
    DateTime Timestamp
) : IDataFlowEvent;

public record EdgeProgressEvent(
    string SourceBlock,
    string TargetBlock,
    long ItemsTransmitted,
    DateTime Timestamp
) : IDataFlowEvent;
