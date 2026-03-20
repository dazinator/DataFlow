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
    int AttemptNumber = 1
);

public record BlockSnapshot(
    string BlockName,
    string BlockType,
    BlockState State,
    long ItemsProcessed,
    DateTime? StartTime,
    DateTime? EndTime,
    string? ErrorMessage
);

public record ChannelSnapshot(
    string BlockName,
    int BufferCapacity,
    int CurrentCount,
    DateTime LastUpdate
);
