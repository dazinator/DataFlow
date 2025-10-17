namespace Uniun.DataFlow;

using System.Diagnostics;
using Uniun.DataFlow.Metrics;

/// <summary>
/// Comprehensive metrics context that handles both flow-level and block-level metrics with cached tags
/// </summary>
public class DataFlowMetricsTagsContext : IMetricsTagsContext
{
    private KeyValuePair<string, object?>[] _flowInstanceCompletionTags = null;

    public DataFlowMetricsTagsContext(string flowName, Guid invocationId, IDataFlowMetrics metrics)
    {
        Metrics = metrics;
        Name = flowName;
        InvocationId = invocationId;

        // Cache flow-level tags (global + flow info)
        FlowWideTags = CreateFlowLevelTags(flowName, metrics.GlobalTags);
        FlowInstanceTags = AddInstanceLevelTags(FlowWideTags, invocationId, metrics.GlobalTags);
    }

    public string Name { get; }
    public Guid InvocationId { get; }

    /// <summary>
    ///  Flow-level tags that are applicable for metrics that aggregate accross all instances / executions of this flow.
    /// </summary>
    public KeyValuePair<string, object?>[] FlowWideTags { get; }

    /// <summary>
    ///  Flow-instance level tags that are applicable for this specific execution / invocation of the flow (by invoicationId).
    /// </summary>
    public KeyValuePair<string, object?>[] FlowInstanceTags { get; }

    /// <summary>
    /// Flow-level tabs that are applicable for metrics that aggregate across all instances / executions of a flow name (i.e not labelled to specific instance / invoication)
    /// </summary>
    public KeyValuePair<string, object?>[] FlowLevelCompletionTags { get; private set; } = null;

    /// <summary>
    /// Flow-instance tabs level tags that are applicable for completion of this specific execution / invocation of the flow (by invoicationId).
    /// </summary>
    public KeyValuePair<string, object?>[] FlowInstanceCompletionTags { get => _flowInstanceCompletionTags; }

    public IDataFlowMetrics Metrics { get; }

    /// <summary>
    /// Creates a BlockMetricsContext for a specific block (called by DataFlow)
    /// </summary>
    internal BlockMetricsTagsContext CreateBlockContext(string blockName)
    {
        return new BlockMetricsTagsContext(blockName, this, Metrics);
    }

    /// <summary>
    /// Creates a DataItemMetricsContext to track metrics for specific data items within the flow.
    /// </summary>
    internal DataItemMetricsContext CreateItemsContext(string itemsName)
    {
        return new DataItemMetricsContext(itemsName, this, Metrics);
    }

    public void Started()
    {
        Metrics.FlowStarted(this);
    }

    /// <summary>
    /// Marks the flow as completed with the specified duration and optional outcome label.
    /// </summary>
    /// <param name="duration"></param>
    /// <param name="isSuccessful"></param>
    public void Completed(double duration, bool? isSuccessful)
    {
        if (FlowLevelCompletionTags is null && isSuccessful is not null)
        {
            FlowLevelCompletionTags = AddFlowLevelCompletionTags(FlowWideTags, isSuccessful.Value);
            _flowInstanceCompletionTags = AddFlowInstanceLevelCompletionTags(FlowLevelCompletionTags, duration);
        }
        Metrics.FlowCompleted(this, duration);
    }


    #region Data-level Metrics (flow-scoped with developer labels)

    ///// <summary>
    ///// Records business data items processed in this flow
    ///// </summary>
    ///// <param name="dataLabel">Developer-provided label (e.g., "invoices", "invoice_lines", "customers_processed")</param>
    ///// <param name="count">Number of items processed</param>
    ///// <remarks>Metric will automatically include flow-level and global tags, but will not include current block tags.</remarks>
    //public void RecordDataItems(string dataLabel, long count)
    //{
    //    var dataTags = CreateDataTags(FlowTags, dataLabel);
    //    _metrics.RecordDataItemsProcessedWithTags(dataTags, count);
    //}

    #endregion

    #region Tag Creation Methods

    private static KeyValuePair<string, object?>[] CreateFlowLevelTags(string flowName, TagList globalTags)
    {
        var tags = new KeyValuePair<string, object?>[globalTags.Count + 1];
        globalTags.CopyTo(tags, 0);
        tags[globalTags.Count] = new(DataFlowMetrics.TagNames.FlowName, flowName);
        return tags;
    }

    private static KeyValuePair<string, object?>[] AddInstanceLevelTags(KeyValuePair<string, object?>[] flowTags, Guid invocationId, TagList globalTags)
    {
        var tags = new KeyValuePair<string, object?>[flowTags.Count() + 1];
        flowTags.CopyTo(tags, 0);
        tags[globalTags.Count] = new(DataFlowMetrics.TagNames.FlowInvocationId, invocationId);
        return tags;
    }

    private static KeyValuePair<string, object?>[] AddFlowLevelCompletionTags(KeyValuePair<string, object?>[] originalTags, bool successful)
    {
        var completionTags = new KeyValuePair<string, object?>[originalTags.Length + 1];
        originalTags.CopyTo(completionTags, 0);
        completionTags[originalTags.Length] = successful
            ? DataFlowMetrics.TagConstantValues.SuccessOutcomeTag
            : DataFlowMetrics.TagConstantValues.FailureOutcomeTag;
        return completionTags;
    }

    private static KeyValuePair<string, object?>[] AddFlowInstanceLevelCompletionTags(KeyValuePair<string, object?>[] originalTags, double durationMs)
    {
        var completionTags = new KeyValuePair<string, object?>[originalTags.Length + 1];
        originalTags.CopyTo(completionTags, 0);
        completionTags[originalTags.Length] = new(DataFlowMetrics.TagNames.ExecutionDuration, durationMs);
        return completionTags;
    }

    #endregion
}
