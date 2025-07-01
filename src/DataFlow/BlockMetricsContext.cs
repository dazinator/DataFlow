namespace Uniun.DataFlow;
using Uniun.DataFlow.Metrics;

/// <summary>
/// Block-specific metrics context with cached tags
/// </summary>
public class BlockMetricsContext
{
    private readonly string _blockName;
    private readonly DataFlowMetricsContext _flowContext;
    private KeyValuePair<string, object?>[] _completionTags = null;


    public KeyValuePair<string, object?>[] BlockTags { get; }
    public KeyValuePair<string, object?>[] CompletionTags { get => _completionTags; }

    internal BlockMetricsContext(string blockName, DataFlowMetricsContext flowContext)
    {
        _blockName = blockName;
        _flowContext = flowContext;
        // Cache flow-level tags (global + flow info)
        BlockTags = CreateBlockTags(_flowContext, blockName);
    }

    private static KeyValuePair<string, object?>[] CreateBlockTags(DataFlowMetricsContext flowContext, string blockName)
    {
        // we inherit flow level tags and add block name as an additional tag
        var blockTags = new KeyValuePair<string, object?>[flowContext.FlowTags.Length + 1];
        flowContext.FlowTags.CopyTo(blockTags, 0);
        blockTags[flowContext.FlowTags.Length] = new(DataFlowMetrics.TagNames.BlockName, blockName);
        return blockTags;
    }

    public void SetCompletionOutcome(bool successful)
    {
        if (_completionTags is null)
        {
            // inherit the typical tags and add success/failure outcome tag.
            _completionTags = CreateCompletionTags(BlockTags, successful);
        }
    }  


    ///// <summary>
    ///// Records that this block processed one stream item (automatic metric)
    ///// </summary>
    //public void RecordBlockItemProcessed()
    //{
    //    _metrics.RecordBlockItemProcessedWithTags(_blockTags);
    //}

    ///// <summary>
    ///// Records business data items processed within stream items (optional metric)
    ///// </summary>
    ///// <param name="dataType">Type of business data (e.g., "invoices", "invoice_lines")</param>
    ///// <param name="count">Number of items processed</param>
    //public void RecordDataItems(string dataType, long count)
    //{
    //    var dataTags = CreateDataTags(_blockTags, dataType);
    //    _metrics.RecordDataItemsProcessedWithTags(dataTags, count);
    //}


    //private static KeyValuePair<string, object?>[] CreateDataTags(KeyValuePair<string, object?>[] blockTags, string dataType)
    //{
    //    var dataTags = new KeyValuePair<string, object?>[blockTags.Length + 1];
    //    blockTags.CopyTo(dataTags, 0);
    //    dataTags[blockTags.Length] = new(DataFlowMetrics.TagNames.DataType, dataType);
    //    return dataTags;
    //}

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
