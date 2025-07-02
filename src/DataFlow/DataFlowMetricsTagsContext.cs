namespace Uniun.DataFlow;

using System.Diagnostics;
using Uniun.DataFlow.Metrics;

/// <summary>
/// Comprehensive metrics context that handles both flow-level and block-level metrics with cached tags
/// </summary>
public class DataFlowMetricsTagsContext : IMetricsTagsContext
{
    private readonly IDataFlowMetrics _metrics;
    private readonly KeyValuePair<string, object?>[] _flowTags;
    private KeyValuePair<string, object?>[] _completionTags = null;

    public DataFlowMetricsTagsContext(string flowName, Guid invocationId, IDataFlowMetrics metrics)
    {
        _metrics = metrics;
        Name = flowName;
        InvocationId = invocationId;

        // Cache flow-level tags (global + flow info)
        _flowTags = CreateFlowTags(flowName, invocationId, metrics.GlobalTags);
    }

    public string Name { get; }
    public Guid InvocationId { get; }

    public KeyValuePair<string, object?>[] Tags => _flowTags;

    public KeyValuePair<string, object?>[] CompletionTags { get => _completionTags; }

    public IDataFlowMetrics Metrics => _metrics;   

    /// <summary>
    /// Creates a BlockMetricsContext for a specific block (called by DataFlow)
    /// </summary>
    internal BlockMetricsTagsContext CreateBlockContext(string blockName)
    {
        return new BlockMetricsTagsContext(blockName, this, _metrics);
    }

    /// <summary>
    /// Creates a DataItemMetricsContext to track metrics for specific data items within the flow.
    /// </summary>
    internal DataItemMetricsContext CreateItemsContext(string itemsName)
    {
        return new DataItemMetricsContext(itemsName, this, _metrics);
    }

    public void Started()
    {
        _metrics.FlowStarted(this);
    }

    /// <summary>
    /// Marks the flow as completed with the specified duration and optional outcome label.
    /// </summary>
    /// <param name="duration"></param>
    /// <param name="isSuccessful"></param>
    public void Completed(double duration, bool? isSuccessful)
    {
        if (_completionTags is null && isSuccessful is not null)
        {         
            _completionTags = CreateCompletionTags(Tags, isSuccessful.Value);           
        }
        _metrics.FlowCompleted(this, duration);
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

    private static KeyValuePair<string, object?>[] CreateFlowTags(string flowName, Guid invocationId, TagList globalTags)
    {
        var tags = new KeyValuePair<string, object?>[globalTags.Count + 2];
        globalTags.CopyTo(tags, 0);
        tags[globalTags.Count] = new(DataFlowMetrics.TagNames.FlowName, flowName);
        tags[globalTags.Count + 1] = new(DataFlowMetrics.TagNames.FlowInvocationId, invocationId);
        return tags;
    }

    private static KeyValuePair<string, object?>[] CreateCompletionTags(KeyValuePair<string, object?>[] flowTags, bool successful)
    {
        var completionTags = new KeyValuePair<string, object?>[flowTags.Length + 1];
        flowTags.CopyTo(completionTags, 0);
        completionTags[flowTags.Length] = successful
            ? DataFlowMetrics.TagConstantValues.SuccessOutcomeTag
            : DataFlowMetrics.TagConstantValues.FailureOutcomeTag;
        return completionTags;
    }

    #endregion
}
