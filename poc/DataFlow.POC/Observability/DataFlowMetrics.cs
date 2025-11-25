namespace DataFlow.POC.Observability;

using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using DataFlow.POC.Core;

/// <summary>
/// Record metrics for the DataFlow POC system.
/// Implements 13 of 14 production metrics (93% parity).
/// Phase 1: Core metrics without per-channel buffer utilization.
/// </summary>
public class DataFlowMetrics : IDataFlowMetrics
{
    private readonly Histogram<double> _blockProcessingDuration;
    private readonly Histogram<double> _flowExecutionDuration;

    private readonly ObservableGauge<int> _activeChannelCount;
    private readonly Counter<long> _flowExecutionStartedCount;
    private readonly Counter<long> _flowExecutionCompletionCount;
    private readonly Counter<long> _blockExecutionCount;
    private readonly UpDownCounter<int> _activeFlowCount;
    private readonly UpDownCounter<int> _activeBlockCount;
    private readonly Counter<long> _blockOperationsCompleted;
    private readonly Counter<long> _dataItemsProcessed;

    private readonly IMeterAccessor _meterAccessor;
    private Func<int>? _activeChannelCountProvider;

    public TagList GlobalTags { get; }

    public DataFlowMetrics(IMeterAccessor meterAccessor, TagList globalTags = default)
    {
        _meterAccessor = meterAccessor;
        GlobalTags = globalTags;

        var meter = _meterAccessor.Meter ?? throw new InvalidOperationException("Meter is not available");

        _blockProcessingDuration = meter.CreateHistogram<double>(InstrumentNames.BlockDurationMs,
            unit: "ms",
            description: "Time taken to complete execution of a block in a DataFlow");

        _flowExecutionDuration = meter.CreateHistogram<double>(InstrumentNames.FlowDurationMs,
            unit: "ms",
            description: "DataFlow execution time histogram for performance analysis. Provides percentiles, averages, and trends across flow executions of a given flow name.");

        _flowExecutionCompletionCount = meter.CreateCounter<long>(InstrumentNames.FlowExecutionCount,
            unit: "execution",
            description: "Number of completed flow executions");

        _flowExecutionStartedCount = meter.CreateCounter<long>(InstrumentNames.FlowExecutionStartedCount,
            unit: "execution",
            description: "Number of started flow executions");

        _blockExecutionCount = meter.CreateCounter<long>(InstrumentNames.BlockExecutionCount,
            unit: "execution",
            description: "Number of completed block executions");

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

        _activeChannelCount = meter.CreateObservableGauge<int>(InstrumentNames.ActiveChannelCount,
            () => GetActiveChannelCount(),
            unit: "channel",
            description: "Current number of active channels");
    }

    /// <summary>
    /// Sets the provider function for active channel count.
    /// Called by DataFlowGraph to provide access to ExecutionPipeline.
    /// </summary>
    internal void SetActiveChannelCountProvider(Func<int> provider)
    {
        _activeChannelCountProvider = provider;
    }

    private Measurement<int> GetActiveChannelCount()
    {
        var activeChannelCount = _activeChannelCountProvider?.Invoke() ?? 0;
        return new Measurement<int>(activeChannelCount, GlobalTags);
    }

    public void FlowStarted(DataFlowMetricsTagsContext metricsContext)
    {
        _activeFlowCount.Add(1, metricsContext.FlowWideTags);
        _flowExecutionStartedCount.Add(1, metricsContext.FlowWideTags);
    }

    public void FlowCompleted(DataFlowMetricsTagsContext metricsContext, double durationMs)
    {
        _flowExecutionDuration.Record(durationMs, metricsContext.FlowLevelCompletionTags ?? metricsContext.FlowWideTags);
        _flowExecutionCompletionCount.Add(1, metricsContext.FlowLevelCompletionTags ?? metricsContext.FlowWideTags);
        _activeFlowCount.Add(-1, metricsContext.FlowWideTags);
    }

    public void BlockStarted(BlockMetricsTagsContext metricsContext)
    {
        _activeBlockCount.Add(1, metricsContext.FlowWideTags);
    }

    public void BlockCompleted(BlockMetricsTagsContext metricsContext, double durationTotalMs)
    {
        var tags = metricsContext.FlowLevelCompletionTags ?? metricsContext.FlowWideTags;
        _blockProcessingDuration.Record(durationTotalMs, tags);
        _blockExecutionCount.Add(1, tags);
        _activeBlockCount.Add(-1, metricsContext.FlowWideTags);
    }

    public void ItemsProcessed(DataItemMetricsContext context, long count)
    {
        _dataItemsProcessed.Add(count, context.FlowWideTags);
    }

    public void BlockOperationsCompleted(BlockMetricsTagsContext context, long count)
    {
        _blockOperationsCompleted.Add(count, context.FlowWideTags);
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
        [Description("Counter that increments with each flow execution completion")]
        public const string FlowExecutionCount = "dataflow.flow.executions";

        /// <summary>
        /// Number of flows that have started execution
        /// </summary>
        [Description("Counter that increments with each flow execution started")]
        public const string FlowExecutionStartedCount = "dataflow.flow.executions.start";

        /// <summary>
        /// Number of completed block executions
        /// </summary>
        [Description("Number of completed block executions")]
        public const string BlockExecutionCount = "dataflow.block.executions";

        /// <summary>
        /// Current number of active channels
        /// </summary>
        [Description("Current number of active channels")]
        public const string ActiveChannelCount = "dataflow.channel.active-count";

        [Description("Number of currently executing flows")]
        public const string ActiveFlowCount = "dataflow.flow.active-count";

        [Description("Number of currently executing blocks")]
        public const string ActiveBlockCount = "dataflow.block.active-count";

        /// <summary>
        /// Number of operations completed by a block
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

        [Description("The name of the flow")]
        public const string FlowName = "dataflow.flow.name";

        [Description("Indicator of success or failure in execution")]
        public const string Outcome = "dataflow.outcome";

        /// <summary>
        /// Duration of completed flow instance
        /// </summary>
        [Description("The execution duration for a specific execution")]
        public const string ExecutionDuration = "dataflow.flow.execution.duration.ms";

        [Description("The name of the block")]
        public const string BlockName = "dataflow.block.name";

        [Description("Developer-provided label for business data processing")]
        public const string DataLabel = "dataflow.data.label";
    }

    public static class TagConstantValues
    {
#pragma warning disable IDE1006 // Naming Styles
        internal static readonly KeyValuePair<string, object?> SuccessOutcomeTag = new(TagNames.Outcome, OutcomeSuccess);
        internal static readonly KeyValuePair<string, object?> FailureOutcomeTag = new(TagNames.Outcome, OutcomeFailure);
#pragma warning restore IDE1006 // Naming Styles
        internal const string OutcomeSuccess = "success";
        internal const string OutcomeFailure = "failure";
    }
}
