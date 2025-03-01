// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Metrics;

using System.ComponentModel;
using System.Diagnostics.Metrics;


public class DataFlowMetrics
{   

    private readonly Histogram<double> _blockProcessingDuration;
    private readonly Histogram<double> _flowExecutionDuration;
    private readonly ObservableGauge<int> _channelBufferUtilization;
 
    private readonly ChannelRegistry _channelRegistry;

    public DataFlowMetrics(IMeterFactory meterFactory, ChannelRegistry channelRegistry)
    {
        _channelRegistry = channelRegistry;
        var meter = meterFactory.Create("Uniun.DataFlow");

        _blockProcessingDuration = meter.CreateHistogram<double>(InstrumentNames.BlockDurationMs,
            unit: "ms",
            description: "Time taken to process a block in DataFlow");

        _flowExecutionDuration = meter.CreateHistogram<double>(InstrumentNames.FlowDurationMs,
            unit: "ms",
            description: "Time taken to execute a DataFlow from start to completion");

        _channelBufferUtilization= meter.CreateObservableGauge<int>(InstrumentNames.ChannelBufferUtilizationMetricName,
            () => GetAllChannelUtilizations(),
            unit: "%",
            description: "Current utilization of channel buffer capacity between blocks");
       
    }

    /// <summary>
    /// Registers a channel with the metrics system to be observed.
    /// </summary>
    /// <param name="channel"></param>
    public void RegisterChannel(IMonitoredChannel channel)
    {
        _channelRegistry.RegisterChannel(channel);
    }

    private IEnumerable<Measurement<int>> GetAllChannelUtilizations()
    {
        // Get active channel count
        var activeChannelCount = _channelRegistry.GetActiveChannelCount();

        // Report active channel count
        yield return new Measurement<int>(
            activeChannelCount,
            new KeyValuePair<string, object?>("metric", "active_channel_count"));

        // Get all channel snapshots
        var snapshots = _channelRegistry.GetChannelSnapshots();

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

    public static class InstrumentNames
    {
        /// <summary>
        /// Time taken to process a block in DataFlow
        /// </summary>
        [Description("Time taken to process a block in DataFlow")]
        public const string BlockDurationMs = "dataflow.block.duration.ms";
        /// <summary>
        /// Time taken to execute a DataFlow from start to completion
        /// </summary>
        [Description("Time taken to execute a DataFlow from start to completion")]
        public const string FlowDurationMs = "dataflow.flow.duration.ms";
        /// <summary>
        /// Current utilization of channel buffer capacity between blocks
        /// </summary>
        [Description("Current utilization of channel buffer capacity between blocks")]
        public const string ChannelBufferUtilizationMetricName = "dataflow.channel.buffer.utilization";
    }
}

 


