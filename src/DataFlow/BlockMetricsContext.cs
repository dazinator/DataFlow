namespace Uniun.DataFlow;
using Uniun.DataFlow.Metrics;

/// <summary>
/// Block-specific metrics context with cached tags
/// </summary>
public class BlockMetricsContext
{
    private readonly string _blockName;
    private readonly KeyValuePair<string, object?>[] _blockTags;
    private readonly IDataFlowMetrics _metrics;

    internal BlockMetricsContext(string blockName, KeyValuePair<string, object?>[] blockTags, IDataFlowMetrics metrics)
    {
        _blockName = blockName;
        _blockTags = blockTags;
        _metrics = metrics;
    }

    /// <summary>
    /// Records that this block started executing
    /// </summary>
    public void BlockStarted()
    {
        _metrics.BlockStarted(_blockTags);
    }

    /// <summary>
    /// Records that this block completed executing
    /// </summary>
    public void BlockCompleted(double durationMs, bool successful)
    {
        var completionTags = CreateBlockCompletionTags(_blockTags, successful);
        _metrics.BlockCompleted(durationMs, completionTags);
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

    private static KeyValuePair<string, object?>[] CreateBlockCompletionTags(KeyValuePair<string, object?>[] blockTags, bool successful)
    {
        var completionTags = new KeyValuePair<string, object?>[blockTags.Length + 1];
        blockTags.CopyTo(completionTags, 0);
        completionTags[blockTags.Length] = successful
            ? DataFlowMetrics.TagConstantValues.SuccessOutcomeTag
            : DataFlowMetrics.TagConstantValues.FailureOutcomeTag;
        return completionTags;
    }

}
