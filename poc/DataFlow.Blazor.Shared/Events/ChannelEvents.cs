namespace DataFlow.Blazor.Events;

public record ChannelStatsEvent(
    string SourceBlock,
    string TargetBlock,
    int BufferCapacity,
    int CurrentCount,
    DateTime Timestamp
) : IDataFlowEvent;
