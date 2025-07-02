// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Metrics;

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Recrod metrics for the DataFlow system.
/// </summary>
public class DataFlowMetrics : IDataFlowMetrics
{

    private readonly Histogram<double> _blockProcessingDuration;
    private readonly Histogram<double> _flowExecutionDuration;
    private readonly ObservableGauge<int> _channelBufferUtilization;
    private readonly ObservableGauge<int> _activeChannelCount;
    private readonly Counter<long> _flowExecutionCount;
    private readonly Counter<long> _blockExecutionCount;
    private readonly UpDownCounter<int> _activeFlowCount;
    private readonly UpDownCounter<int> _activeBlockCount;
    private readonly Counter<long> _blockOperationsCompleted;
    private readonly Counter<long> _dataItemsProcessed;

    //private readonly Counter<long> _blockItemsProcessed;    

    private readonly ILogger<DataFlowMetrics> _logger;
    private readonly IMeterAccessor _meterAccessor;
    private readonly ChannelRegistry _channelRegistry = new ChannelRegistry();
    private readonly IOptions<DataFlowsOptions> _options;

    public TagList GlobalTags { get; }

    public DataFlowMetrics(
        ILogger<DataFlowMetrics> logger,
        IMeterAccessor meterAccessor,
        IOptions<DataFlowsOptions> options)
    {
        _logger = logger;
        _meterAccessor = meterAccessor;
        _options = options;
        GlobalTags = _options.Value.MetricTags;

        var meter = _meterAccessor.Meter ?? throw new InvalidOperationException("Meter is not available");

        _blockProcessingDuration = meter.CreateHistogram<double>(InstrumentNames.BlockDurationMs,
            unit: "ms",
            description: "Time taken to complete execution of a block in a DataFlow");

        _flowExecutionDuration = meter.CreateHistogram<double>(InstrumentNames.FlowDurationMs,
            unit: "ms",
            description: "Time taken to complete execution of a DataFlow.");

        _flowExecutionCount = meter.CreateCounter<long>(InstrumentNames.FlowExecutionCount,
            unit: "execution",
            description: "Number of completed flow executions");

        _blockExecutionCount = meter.CreateCounter<long>(InstrumentNames.BlockExecutionCount,
            unit: "execution",
            description: "Number of completed block executions");

        //// NEW: The two processing counters we're adding
        //_blockItemsProcessed = meter.CreateCounter<long>(InstrumentNames.BlockItemsProcessed,
        //    unit: "item",
        //    description: "Number of stream items processed by a block (batches, records, etc.)");

        _blockOperationsCompleted = meter.CreateCounter<long>(InstrumentNames.BlockOperationsCompleted,
           unit: "operation",
           description: "Number of operations a block is completing");

        _dataItemsProcessed = meter.CreateCounter<long>(InstrumentNames.DataItemsProcessed,
            unit: "item",
            description: "Number of business data items processed within stream items");

        _activeFlowCount = meter.CreateUpDownCounter<int>(InstrumentNames.ActiveFlowCount,
           unit: "flow",
           description: "Number of currently executing flows");

        _activeBlockCount = meter.CreateUpDownCounter<int>(InstrumentNames.ActiveBlockCount,
            unit: "block",
            description: "Number of currently executing blocks");

        _channelBufferUtilization = meter.CreateObservableGauge<int>(InstrumentNames.ChannelBufferUtilizationMetricName,
            () => GetAllChannelUtilizations(),
            unit: "%",
            description: "Current utilization of a channel buffer used by a block");

        _activeChannelCount = meter.CreateObservableGauge<int>(InstrumentNames.ActiveChannelCount,
           () => GetActiveChannelCount(),
           description: "Current utilization of a channel buffer used by a block");

    }

    private Measurement<int> GetActiveChannelCount()
    {
        var activeChannelCount = _channelRegistry.GetActiveChannelCount();
        // Report active channel count
        return new Measurement<int>(
            activeChannelCount, GlobalTags);
    }

    public void FlowStarted(DataFlowMetricsTagsContext metricsContext)
    {
        _activeFlowCount.Add(1, metricsContext.Tags);
    }
    public void FlowCompleted(DataFlowMetricsTagsContext metricsContext, double durationMs)
    {
        _flowExecutionDuration.Record(durationMs, metricsContext.CompletionTags ?? metricsContext.Tags);
        _flowExecutionCount.Add(1, metricsContext.CompletionTags ?? metricsContext.Tags);
        // for the active flow up down counter we want to maintain a single series so we don't use the completion tags here as they aren't availble when incrementing the counter.
        _activeFlowCount.Add(-1, metricsContext.Tags);
    }

    public void BlockStarted(BlockMetricsTagsContext metricsContext)
    {
        _activeBlockCount.Add(1, metricsContext.Tags);
    }

    public void BlockCompleted(BlockMetricsTagsContext metricsContext, double durationTotalMs)
    {
        // Record with the combined tags
        _blockProcessingDuration.Record(durationTotalMs, metricsContext.CompletionTags);
        _blockExecutionCount.Add(1, metricsContext.CompletionTags);

        // for the active flow up down counter we want to maintain a single series so we don't use the completion tags here as they aren't availble when incrementing the counter.
        _activeBlockCount.Add(-1, metricsContext.Tags);
    }

    public void ItemsProcessed(DataItemMetricsContext context, long count)
    {
        _dataItemsProcessed.Add(count, context.Tags);
    }
    public void BlockOperationsCompleted(BlockMetricsTagsContext context, long count)
    {
        _blockOperationsCompleted.Add(count, context.Tags);
    }

    //// NEW: The two processing metrics methods
    //public void BlockItemProcessed(string blockName, string flowName)
    //{
    //    var tags = CreateBlockProcessingTags(blockName, flowName);
    //    _blockItemsProcessed.Add(1, tags);
    //}

    //public void RecordDataItemsProcessed(string blockName, string flowName, string dataType, long count)
    //{
    //    var tags = CreateDataProcessingTags(blockName, flowName, dataType);
    //    _dataItemsProcessed.Add(count, tags);
    //}   

    /// <summary>
    /// Registers a channel with the metrics system to be observed.
    /// </summary>
    /// <param name="channel"></param>
    public IChannelMonitoringLease RegisterChannel(IMonitoredChannel channel)
    {
        return _channelRegistry.RegisterChannel(channel);
    }

    private IEnumerable<Measurement<int>> GetAllChannelUtilizations()
    {
        // Get active channel count

        // new KeyValuePair<string, object?>("metric", "active_channel_count"));

        // Get all channel snapshots
        var snapshots = _channelRegistry.GetChannelSnapshots();

        // Process snapshots
        foreach (var snapshot in snapshots)
        {
            if (snapshot.Capacity > 0)
            {
                var utilization = (int)((double)snapshot.CurrentCount / snapshot.Capacity * 100);
                yield return new Measurement<int>(utilization, snapshot.Tags);
                //if (snapshot.Tags.Count > 0)
                //{

                //}
                //else
                //{
                //    // Handle empty tags case
                //    yield return new Measurement<int>(utilization);
                //}             
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
        /// Number of completed flow executions
        /// </summary>
        [Description("Number of completed flow executions")]
        public const string FlowExecutionCount = "dataflow.flow.executions";

        /// <summary>
        /// Number of completed block executions
        /// </summary>
        [Description("Number of completed block executions")]
        public const string BlockExecutionCount = "dataflow.block.executions";

        /// <summary>
        /// Current utilization of channel buffer capacity between blocks
        /// </summary>
        [Description("Current utilization of channel buffer capacity between blocks")]
        public const string ChannelBufferUtilizationMetricName = "dataflow.channel.buffer.utilization";

        /// <summary>
        /// Current utilization of channel buffer capacity between blocks
        /// </summary>
        [Description("Current number of active channels")]
        public const string ActiveChannelCount = "dataflow.channel.active-count";

        [Description("Number of currently executing flows")]
        public const string ActiveFlowCount = "dataflow.flow.active-count";

        [Description("Number of currently executing blocks")]
        public const string ActiveBlockCount = "dataflow.block.active-count";

        /// <summary>
        /// Number of stream items processed by a block
        /// </summary>
        [Description("Number of individual operations completed by a block (e.g batches, transforms etc)")]
        public const string BlockOperationsCompleted = "dataflow.block.operations";

        /// <summary>
        /// Number of business data items processed within stream items
        /// </summary>
        [Description("Number of business data items processed within stream items")]
        public const string DataItemsProcessed = "dataflow.block.items.processed";
    }

    public static class TagNames
    {

        [Description("The invocation id of the flow")]
        public const string FlowInvocationId = "dataflow.flow.invocationid";

        [Description("Time name of the flow")]
        public const string FlowName = "dataflow.flow.name";

        [Description("Indicator of success of failure in execution")]
        public const string Outcome = "dataflow.outcome";

        [Description("The name of the block")]
        public const string BlockName = "dataflow.block.name";

        [Description("The capacity of a block channel")]
        public const string ChannelCapacity = "dataflow.block.capacity";

        [Description("Developer-provided label for business data processing")]
        public const string DataLabel = "dataflow.data.label";

    }

    public static class TagConstantValues
    {
#pragma warning disable IDE1006 // Naming Styles
        internal static KeyValuePair<string, object?> SuccessOutcomeTag = new KeyValuePair<string, object?>(TagNames.Outcome, TagConstantValues.OutcomeSuccess);

        internal static KeyValuePair<string, object?> FailureOutcomeTag = new KeyValuePair<string, object?>(TagNames.Outcome, TagConstantValues.OutcomeFailure);
#pragma warning restore IDE1006 // Naming Styles
        internal const string OutcomeSuccess = "success";
        internal const string OutcomeFailure = "failure";
    }

    /// <summary>
    /// Channel registry responsible for tracking and managing monitored channels.
    /// </summary>
    private class ChannelRegistry : IDisposable
    {
        private readonly ConcurrentDictionary<IMonitoredChannel, WeakReference<IMonitoredChannel>> _monitoredChannels =
      new ConcurrentDictionary<IMonitoredChannel, WeakReference<IMonitoredChannel>>();

        // Background timer for cleanup
        private readonly Timer _cleanupTimer;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);

        public ChannelRegistry()
        {
            // Set up a timer to ensure cleanup happens even if metrics aren't collected
            _cleanupTimer = new Timer(
                _ => PerformFullCleanup(),
                null,
                _cleanupInterval,
                _cleanupInterval);
        }

        public IChannelMonitoringLease RegisterChannel(IMonitoredChannel channel)
        {
            var weakRef = new WeakReference<IMonitoredChannel>(channel);
            _monitoredChannels.TryAdd(channel, weakRef);
            return new ChannelMonitoringLease(this, channel);
        }

        internal void UnregisterChannel(IMonitoredChannel channel)
        {
            _monitoredChannels.TryRemove(channel, out _);
        }

        public IEnumerable<ChannelMetricSnapshot> GetChannelSnapshots()
        {
            var snapshots = new List<ChannelMetricSnapshot>();

            foreach (var kvp in _monitoredChannels)
            {
                if (kvp.Value.TryGetTarget(out var channel))
                {
                    var snapshot = channel.GetMetricSnapshot();
                    if (snapshot != null)
                    {
                        snapshots.Add(snapshot);
                    }
                }
            }

            return snapshots;
        }

        public int GetActiveChannelCount()
        {
            int count = 0;
            foreach (var kvp in _monitoredChannels)
            {
                if (kvp.Value.TryGetTarget(out var channel) && channel.GetMetricSnapshot() != null)
                {
                    count++;
                }
            }
            return count;
        }

        internal void PerformFullCleanup()
        {
            var toRemove = new List<IMonitoredChannel>();

            foreach (var kvp in _monitoredChannels)
            {
                if (!kvp.Value.TryGetTarget(out var channel) || channel.GetMetricSnapshot() == null)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var channel in toRemove)
            {
                _monitoredChannels.TryRemove(channel, out _);
            }
        }

        public void Dispose()
        {
            _cleanupTimer.Dispose();
        }
    }

    /// <summary>
    /// Private implementation of the monitoring lease for this registry
    /// </summary>
    private class ChannelMonitoringLease : IChannelMonitoringLease
    {
        private readonly ChannelRegistry _registry;
        private readonly IMonitoredChannel _channel;
        private bool _disposed = false;

        public ChannelMonitoringLease(ChannelRegistry registry, IMonitoredChannel channel)
        {
            _registry = registry;
            _channel = channel;
        }

        public IMonitoredChannel Channel => _channel;

        public void Dispose()
        {
            if (!_disposed)
            {
                _registry.UnregisterChannel(_channel);
                _disposed = true;
            }
        }
    }
}
