namespace Uniun.DataFlow.Metrics;

// Snapshot of channel metrics at a point in time
public record ChannelMetricSnapshot(
    int CurrentCount,
    int Capacity,
    IReadOnlyDictionary<string, string> Dimensions);


