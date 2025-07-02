namespace Uniun.DataFlow;
using Uniun.DataFlow.Metrics;

/// <summary>
/// Block-specific metrics context with cached tags
/// </summary>
public class BlockMetricsTagsContext: IMetricsTagsContext
{
    private readonly string _Name;
    private readonly IMetricsTagsContext _parentContext;
    private readonly IDataFlowMetrics _metrics;
    private KeyValuePair<string, object?>[] _completionTags = null;


    public KeyValuePair<string, object?>[] Tags { get; }
    public KeyValuePair<string, object?>[] CompletionTags { get => _completionTags; }
    public string Name => _Name;

    internal BlockMetricsTagsContext(string blockName, IMetricsTagsContext parentContext, IDataFlowMetrics metrics)
    {
        _Name = blockName;
        _parentContext = parentContext;
        _metrics = metrics;
        // Cache flow-level tags (global + flow info)
        Tags = CreateDefaultTags(_parentContext, blockName);
    }

    private static KeyValuePair<string, object?>[] CreateDefaultTags(IMetricsTagsContext flowContext, string blockName)
    {
        // we inherit flow level tags and add block name as an additional tag
        var blockTags = new KeyValuePair<string, object?>[flowContext.Tags.Length + 1];
        flowContext.Tags.CopyTo(blockTags, 0);
        blockTags[flowContext.Tags.Length] = new(DataFlowMetrics.TagNames.BlockName, blockName);
        return blockTags;
    }  

    public void Started()
    {
        _metrics.BlockStarted(this);
    }

    /// <summary>
    /// Marks the block as completed with the specified duration and optional outcome label.
    /// </summary>
    /// <param name="duration"></param>
    /// <param name="isSuccessful"></param>
    public void Completed(double duration, bool? isSuccessful)
    {
        if (_completionTags is null && isSuccessful is not null)
        {
            _completionTags = CreateCompletionTags(Tags, isSuccessful.Value);
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
