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

        //_channelBufferUtilization = meter.CreateObservableGauge<int>(InstrumentNames.ChannelBufferUtilizationMetricName,
        //   () => GetAllChannelUtilizations(),
        //   unit: "%",
        //   description: "Current utilization of a channel buffer used by a block");

        _activeChannelCount = meter.CreateObservableGauge<int>(InstrumentNames.ActiveChannelCount,
           () => GetActiveChannelCount(),
           description: "Current utilization of a channel buffer used by a block");

    }

    private Measurement<int> GetActiveChannelCount()
    {
        var activeChannelCount = _channelRegistry.GetActiveChannelCount();

        var activeChannelCountTags = new TagList();
        foreach (var tag in GlobalTags)
        {
            activeChannelCountTags.Add(tag.Key, tag.Value);
        }
        activeChannelCountTags.Add(TagNames.ActiveChannelCount, activeChannelCount);

        // Report active channel count
        return new Measurement<int>(
            activeChannelCount, activeChannelCountTags);
    }

    public void FlowStarted(string flowName)
    {
        var tags = new KeyValuePair<string, object?>[GlobalTags.Count + 1];
        GlobalTags.CopyTo(tags, 0);
        tags[GlobalTags.Count] = new(TagNames.FlowName, flowName);
        _activeFlowCount.Add(1, tags);
    }
  
    public void BlockStarted(string flowName, string blockName)
    {
        var tags = new KeyValuePair<string, object?>[GlobalTags.Count + 2];
        GlobalTags.CopyTo(tags, 0);
        tags[GlobalTags.Count] = new(TagNames.BlockName, blockName);
        tags[GlobalTags.Count + 1] = new(TagNames.FlowName, flowName);     
        _activeBlockCount.Add(1, tags);
    }

    public void FlowCompleted(double durationTotalMs, string name, IDataFlowContext context, bool outcomeIsSuccessful)
    {
        // Create a tag array
        var allTags = new KeyValuePair<string, object?>[GlobalTags.Count + 3];

        // Copy global tags
        GlobalTags.CopyTo(allTags, 0);

        // Add specific tags
        allTags[GlobalTags.Count] = new(TagNames.FlowInvocationId, context.InvocationId);
        allTags[GlobalTags.Count + 1] = new(TagNames.FlowName, name);
        allTags[GlobalTags.Count + 2] = outcomeIsSuccessful ? TagConstantValues.SuccessOutcomeTag : TagConstantValues.FailureOutcomeTag;

        // Record with the combined tags
        _flowExecutionDuration.Record(durationTotalMs, allTags);
        // Record execution count with the same tags
        _flowExecutionCount.Add(1, allTags);

        var tags = new KeyValuePair<string, object?>[GlobalTags.Count + 1];
        GlobalTags.CopyTo(tags, 0);
        tags[GlobalTags.Count] = new(TagNames.FlowName, name);
        _activeFlowCount.Add(-1, tags);
    }

    public void BlockCompleted(double durationTotalMs, string flowName, string blockName, IDataFlowContext context, bool successful)
    {
        // Create a tag array that has capacity for global tags + specific tags
        var allTags = new KeyValuePair<string, object?>[GlobalTags.Count + 4];

        // Copy global tags
        GlobalTags.CopyTo(allTags, 0);

        // Add specific tags at the end
        allTags[GlobalTags.Count] = new(TagNames.FlowInvocationId, context.InvocationId);
        allTags[GlobalTags.Count + 1] = new(TagNames.FlowName, flowName);
        allTags[GlobalTags.Count + 2] = new(TagNames.BlockName, blockName);
        allTags[GlobalTags.Count + 3] = successful ? TagConstantValues.SuccessOutcomeTag : TagConstantValues.FailureOutcomeTag;
        // Record with the combined tags
        _blockProcessingDuration.Record(durationTotalMs, allTags);
        // Record execution count with the same tags
        _blockExecutionCount.Add(1, allTags);

        var tags = new KeyValuePair<string, object?>[GlobalTags.Count + 2];
        GlobalTags.CopyTo(tags, 0);
        tags[GlobalTags.Count] = new(TagNames.BlockName, blockName);
        tags[GlobalTags.Count + 1] = new(TagNames.FlowName, flowName);
        _activeBlockCount.Add(-1, tags);
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
    }

    public static class TagNames
    {
        /// <summary>
        /// The invocation id of the flow.
        /// </summary>
        [Description("The invocation id of the flow")]
        public const string FlowInvocationId = "dataflow.flow.invocationid";
        /// <summary>
        /// The name of the flow.
        /// </summary>
        [Description("Time name of the flow")]
        public const string FlowName = "dataflow.flow.name";
        /// <summary>
        /// The name of the flow.
        /// </summary>
        [Description("Indicator of success of failure in execution")]
        public const string Outcome = "dataflow.outcome";

        /// <summary>
        /// The name of the block.
        /// </summary>
        [Description("Time name of the block")]
        public const string BlockName = "dataflow.block.name";
        /// <summary>
        /// The name of the block.
        /// </summary>
        [Description("The capacity of a block channel")]
        public const string ChannelCapacity = "dataflow.block.capacity";
        /// <summary>
        /// The name of the block.
        /// </summary>
        [Description("The total number of active channels")]
        public const string ActiveChannelCount = "dataflow.active-channel-count";


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
        // Thread-safe collection of all monitored channels
        private ConcurrentBag<WeakReference<IMonitoredChannel>> _monitoredChannels =
            new ConcurrentBag<WeakReference<IMonitoredChannel>>();

        // ReaderWriterLock for registration operations
        private readonly ReaderWriterLockSlim _registrationLock =
            new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

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

        public void RegisterChannel(IMonitoredChannel channel)
        {
            _registrationLock.EnterReadLock();
            try
            {
                _monitoredChannels.Add(new WeakReference<IMonitoredChannel>(channel));
            }
            finally
            {
                _registrationLock.ExitReadLock();
            }
        }

        public IEnumerable<ChannelMetricSnapshot> GetChannelSnapshots()
        {
            // Use reader lock to safely iterate
            _registrationLock.EnterReadLock();
            try
            {
                var snapshots = new List<ChannelMetricSnapshot>();

                // Process all channels
                foreach (var weakRef in _monitoredChannels)
                {
                    if (weakRef.TryGetTarget(out var channel))
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
            finally
            {
                _registrationLock.ExitReadLock();
            }
        }

        public int GetActiveChannelCount()
        {
            _registrationLock.EnterReadLock();
            try
            {
                int count = 0;
                foreach (var weakRef in _monitoredChannels)
                {
                    if (weakRef.TryGetTarget(out var channel) && channel.GetMetricSnapshot() != null)
                    {
                        count++;
                    }
                }
                return count;
            }
            finally
            {
                _registrationLock.ExitReadLock();
            }
        }

        internal void PerformFullCleanup()
        {
            // Use TryEnterWriteLock to avoid deadlocks
            if (_registrationLock.TryEnterWriteLock(1000))
            {
                try
                {
                    var newCollection = new ConcurrentBag<WeakReference<IMonitoredChannel>>();

                    foreach (var weakRef in _monitoredChannels)
                    {
                        if (weakRef.TryGetTarget(out var channel) && channel.GetMetricSnapshot() != null)
                        {
                            newCollection.Add(weakRef);
                        }
                    }

                    Interlocked.Exchange(ref _monitoredChannels, newCollection);
                }
                finally
                {
                    _registrationLock.ExitWriteLock();
                }
            }
        }

        public void Dispose()
        {
            _cleanupTimer.Dispose();
            _registrationLock.Dispose();
        }
    }

}
