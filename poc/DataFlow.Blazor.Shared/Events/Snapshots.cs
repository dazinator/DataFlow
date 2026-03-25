namespace DataFlow.Blazor.Events;

public enum FlowState { NotStarted, Running, Completed, Failed }
public enum BlockState { Idle, Running, Completed, Failed }

public record FlowSnapshot(
    Guid InvocationId,
    string FlowName,
    DateTime StartTime,
    FlowState State,
    DateTime? CompletedAt,
    string? ErrorMessage,
    Dictionary<string, BlockSnapshot> Blocks,
    Dictionary<string, ChannelSnapshot> Channels,
    string? TriggerParamsJson = null,
    Guid? CorrelationId = null,
    int AttemptNumber = 1,
    Dictionary<string, EdgeSnapshot>? Edges = null,
    /// <summary>
    /// Block names in topological order (source → sink), populated from
    /// <see cref="FlowGraphDefinedEvent"/>. Used to render the Items table in
    /// the same order as the pipeline. Null for snapshots taken before this field was added.
    /// </summary>
    string[]? BlockOrder = null
);

public record BlockSnapshot(
    string BlockName,
    string BlockType,
    BlockState State,
    long ItemsConsumed,
    long ItemsProduced,
    DateTime? StartTime,
    DateTime? EndTime,
    string? ErrorMessage,
    bool IsSource = false,
    /// <summary>Human-readable label for the block's input item type. Populated from FlowGraphDefinedEvent.</summary>
    string? InputItemLabel = null,
    /// <summary>Human-readable label for the block's output item type. Populated from FlowGraphDefinedEvent.</summary>
    string? OutputItemLabel = null,
    /// <summary>Optional friendly display name configured via block metadata.</summary>
    string? DisplayName = null
);

public record ChannelSnapshot(
    string SourceBlock,
    string TargetBlock,
    int BufferCapacity,
    int CurrentCount,
    DateTime LastUpdate,
    int MaxCount = 0,
    int MinCount = int.MaxValue
);

public record EdgeSnapshot(
    string SourceBlock,
    string TargetBlock,
    long ItemsTransmitted,
    double MaxRatePerSecond,
    double MinRatePerSecond,
    double RateSampleSum,
    int RateSampleCount
);
