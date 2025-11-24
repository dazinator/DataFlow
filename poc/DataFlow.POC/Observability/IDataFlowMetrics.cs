namespace DataFlow.POC.Observability;

using System.Diagnostics;

public interface IDataFlowMetrics
{
    void FlowStarted(DataFlowMetricsTagsContext context);
    void FlowCompleted(DataFlowMetricsTagsContext metricsContext, double durationMs);

    void BlockStarted(BlockMetricsTagsContext metricsContext);
    void BlockCompleted(BlockMetricsTagsContext metricsContext, double durationTotalMs);

    /// <summary>
    /// Records the number of operations completed by a block. This is useful to track flow rate (processing speed) of blocks.
    /// </summary>
    void BlockOperationsCompleted(BlockMetricsTagsContext context, long count);

    void ItemsProcessed(DataItemMetricsContext context, long count);

    /// <summary>
    /// Tags that will be appended to all metrics and activities.
    /// </summary>
    TagList GlobalTags { get; }
}
