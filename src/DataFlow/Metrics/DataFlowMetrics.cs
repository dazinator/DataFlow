// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Metrics;

using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
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

    private readonly IMeterAccessor _meterAccessor;
    private readonly ChannelRegistry _channelRegistry;
    private readonly IOptions<DataFlowsOptions> _options;

    public TagList GlobalTags { get; }

    public DataFlowMetrics(
        IMeterAccessor meterAccessor,
        ChannelRegistry channelRegistry,
        IOptions<DataFlowsOptions> options)
    {
        _meterAccessor = meterAccessor;
        _channelRegistry = channelRegistry;
        _options = options;
        GlobalTags = _options.Value.MetricTags;

        var meter = _meterAccessor.Meter ?? throw new InvalidOperationException("Meter is not available");

        _blockProcessingDuration = meter.CreateHistogram<double>(InstrumentNames.BlockDurationMs,
            unit: "ms",
            description: "Time taken to complete execution of a block in a DataFlow");

        _flowExecutionDuration = meter.CreateHistogram<double>(InstrumentNames.FlowDurationMs,
            unit: "ms",
            description: "Time taken to complete execution of a DataFlow.");

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
        _flowExecutionDuration.Record(durationTotalMs, allTags);
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
        /// Current utilization of channel buffer capacity between blocks
        /// </summary>
        [Description("Current utilization of channel buffer capacity between blocks")]
        public const string ChannelBufferUtilizationMetricName = "dataflow.channel.buffer.utilization";

        /// <summary>
        /// Current utilization of channel buffer capacity between blocks
        /// </summary>
        [Description("Current number of active channels")]
        public const string ActiveChannelCount = "dataflow.channel.active-count";
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


}
