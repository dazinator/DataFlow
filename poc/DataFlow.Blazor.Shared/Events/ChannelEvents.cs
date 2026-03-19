namespace DataFlow.Blazor.Events;

public record ChannelStatsEvent(
    string BlockName,
    int BufferCapacity,
    int CurrentCount,
    DateTime Timestamp
) : IDataFlowEvent;
