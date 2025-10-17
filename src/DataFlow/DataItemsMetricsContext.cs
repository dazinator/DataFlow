namespace Uniun.DataFlow;
using Uniun.DataFlow.Metrics;

/// <summary>
/// Data-item specific metrics context with cached tags
/// </summary>
public class DataItemMetricsContext : IMetricsTagsContext
{
    private readonly IMetricsTagsContext _parentContext;
    private readonly IDataFlowMetrics _metrics;
    private KeyValuePair<string, object?>[] _completionTags = null;


    public KeyValuePair<string, object?>[] FlowWideTags { get; }
    public KeyValuePair<string, object?>[] FlowLevelCompletionTags { get => _completionTags; }

    public string Name { get; }

    internal DataItemMetricsContext(string name, IMetricsTagsContext parentContext, IDataFlowMetrics metrics)
    {
        Name = name;
        _parentContext = parentContext;
        _metrics = metrics;
        // Cache flow-level tags (global + flow info)
        FlowWideTags = CreateDefaultTags(_parentContext, name);
    }

    private static KeyValuePair<string, object?>[] CreateDefaultTags(IMetricsTagsContext parentContext, string name)
    {
        // we inherit flow level tags and add block name as an additional tag
        var blockTags = new KeyValuePair<string, object?>[parentContext.FlowWideTags.Length + 1];
        parentContext.FlowWideTags.CopyTo(blockTags, 0);
        blockTags[parentContext.FlowWideTags.Length] = new(DataFlowMetrics.TagNames.DataLabel, name);
        return blockTags;
    }

    public void SetCompletionOutcome(bool successful)
    {
        // inherit the typical tags and add success/failure outcome tag.
        _completionTags ??= CreateCompletionTags(FlowWideTags, successful);
    }

    public void RecordItemsProcessed(long count)
    {
        _metrics.ItemsProcessed(this, count);
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
