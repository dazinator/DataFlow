// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Metrics;

using System.Diagnostics.Metrics;

public static class DataFlowMetrics
{
    // Define a meter for all DataFlow metrics
    private static readonly Meter DataFlowMeter = new("Uniun.DataFlow", "1.0.0");
    internal static readonly ChannelRegistry ChannelRegistry = new ChannelRegistry();

    // Define counters, histograms and gauges
    // TODO:
    //public static readonly Counter<long> ItemsProcessedCounter = DataFlowMeter.CreateCounter<long>(
    //    "dataflow.items.processed",
    //    description: "Number of items processed by DataFlow blocks");

    public static readonly Histogram<double> BlockProcessingDuration = DataFlowMeter.CreateHistogram<double>(
        "dataflow.block.duration.ms",
        unit: "ms",
        description: "Time taken to process a block in DataFlow");

    // Histogram for flow execution duration
    public static readonly Histogram<double> FlowExecutionDuration = DataFlowMeter.CreateHistogram<double>(
        "dataflow.flow.duration.ms",
        unit: "ms",
        description: "Time taken to execute a DataFlow from start to completion");

    //public static readonly ObservableGauge<int> ActiveDataFlowsGauge = DataFlowMeter.CreateObservableGauge<int>(
    //    "dataflow.active.count",
    //    () => new[] { new Measurement<int>(DataFlow.ActiveFlowCount) },
    //    description: "Number of active DataFlows");

    public const string ChannelBufferUtilizationMetricName = "dataflow.channel.buffer.utilization";
    // Single observable gauge that reports all channel utilizations
    public static readonly ObservableGauge<int> ChannelBufferUtilization = DataFlowMeter.CreateObservableGauge(
        ChannelBufferUtilizationMetricName,
        () => GetAllChannelUtilizations(),
        unit: "%",
        description: "Current utilization of channel buffer capacity between blocks");


    public static void RegisterChannel(IMonitoredChannel channel)
    {
        ChannelRegistry.RegisterChannel(channel);
    }

    private static IEnumerable<Measurement<int>> GetAllChannelUtilizations()
    {
        // Get active channel count
        var activeChannelCount = ChannelRegistry.GetActiveChannelCount();

        // Report active channel count
        yield return new Measurement<int>(
            activeChannelCount,
            new KeyValuePair<string, object?>("metric", "active_channel_count"));

        // Get all channel snapshots
        var snapshots = ChannelRegistry.GetChannelSnapshots();

        // Process snapshots
        foreach (var snapshot in snapshots)
        {
            if (snapshot.Capacity > 0)
            {
                var utilization = (int)((double)snapshot.CurrentCount / snapshot.Capacity * 100);

                // Convert dimensions to tags
                var tags = snapshot.Dimensions
                    .Select(d => new KeyValuePair<string, object?>(d.Key, d.Value))
                    .ToArray();

                yield return new Measurement<int>(utilization, tags);
            }
        }
    }  
}
