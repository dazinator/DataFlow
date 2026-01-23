namespace DataFlow.Blazor.Events;

/// <summary>
/// Event raised to report channel statistics.
/// </summary>
public record ChannelStatsEvent(
    string BlockName,
    int BufferCapacity,
    int CurrentCount,
    DateTime Timestamp
);
