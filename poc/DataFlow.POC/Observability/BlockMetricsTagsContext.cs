namespace DataFlow.POC.Observability;

/// <summary>
/// Block-specific metrics context with cached tags
/// </summary>
public class BlockMetricsTagsContext : IMetricsTagsContext
{
    private readonly IMetricsTagsContext _parentContext;
    private readonly IDataFlowMetrics _metrics;

    public KeyValuePair<string, object?>[] FlowWideTags { get; }
    public KeyValuePair<string, object?>[]? FlowLevelCompletionTags { get; private set; }
    public string Name { get; }

    internal BlockMetricsTagsContext(string blockName, IMetricsTagsContext parentContext, IDataFlowMetrics metrics)
    {
        Name = blockName;
        _parentContext = parentContext;
        _metrics = metrics;
        // Cache flow-level tags (global + flow info)
        FlowWideTags = CreateDefaultTags(_parentContext, blockName);
    }

    private static KeyValuePair<string, object?>[] CreateDefaultTags(IMetricsTagsContext flowContext, string blockName)
    {
        // we inherit flow level tags and add block name as an additional tag
        var blockTags = new KeyValuePair<string, object?>[flowContext.FlowWideTags.Length + 1];
        flowContext.FlowWideTags.CopyTo(blockTags, 0);
        blockTags[flowContext.FlowWideTags.Length] = new(DataFlowMetrics.TagNames.BlockName, blockName);
        return blockTags;
    }

    public void Started()
    {
        _metrics.BlockStarted(this);
    }

    public void OperationsComplete(long count)
    {
        _metrics.BlockOperationsCompleted(this, count);
    }

    /// <summary>
    /// Marks the block as completed with the specified duration and optional outcome label.
    /// </summary>
    public void Completed(double duration, bool? isSuccessful)
    {
        if (FlowLevelCompletionTags is null && isSuccessful is not null)
        {
            FlowLevelCompletionTags = CreateCompletionTags(FlowWideTags, isSuccessful.Value);
        }
        _metrics.BlockCompleted(this, duration);
    }

    /// <summary>
    /// Creates a DataItemMetricsContext to track metrics for specific data items and for this specific block within the flow.
    /// </summary>
    internal DataItemMetricsContext CreateItemsContext(string itemsName)
    {
        return new DataItemMetricsContext(itemsName, this, _metrics);
    }

    private static KeyValuePair<string, object?>[] CreateCompletionTags(KeyValuePair<string, object?>[] blockTags, bool successful)
    {
        var completionTags = new KeyValuePair<string, object?>[blockTags.Length + 1];
        blockTags.CopyTo(completionTags, 0);
        completionTags[blockTags.Length] = successful
            ? DataFlowMetrics.TagConstantValues.SuccessOutcomeTag
            : DataFlowMetrics.TagConstantValues.FailureOutcomeTag;
        return completionTags;
    }
}
