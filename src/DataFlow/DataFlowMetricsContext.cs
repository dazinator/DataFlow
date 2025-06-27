namespace Uniun.DataFlow;

using System.Diagnostics;
using Uniun.DataFlow.Metrics;

/// <summary>
/// Comprehensive metrics context that handles both flow-level and block-level metrics with cached tags
/// </summary>
public class DataFlowMetricsContext
{
    private readonly IDataFlowMetrics _metrics;
    private readonly KeyValuePair<string, object?>[] _flowTags;

    public DataFlowMetricsContext(string flowName, Guid invocationId, IDataFlowMetrics metrics)
    {
        _metrics = metrics;
        FlowName = flowName;
        InvocationId = invocationId;

        // Cache flow-level tags (global + flow info)
        _flowTags = CreateFlowTags(flowName, invocationId, metrics.GlobalTags);
    }

    public string FlowName { get; }
    public Guid InvocationId { get; }

    #region Flow-Level Metrics

    public void FlowStarted()
    {
        _metrics.FlowStarted(_flowTags);
    }

    public void FlowCompleted(double durationMs, bool successful)
    {
        var completionTags = CreateFlowCompletionTags(_flowTags, successful);
        _metrics.FlowCompleted(durationMs, completionTags);
    }

    #endregion

    #region Block Context Creation (called by DataFlow)

    /// <summary>
    /// Creates a BlockMetricsContext for a specific block (called by DataFlow)
    /// </summary>
    internal BlockMetricsContext CreateBlockContext(string blockName)
    {
        var blockTags = CreateBlockTags(blockName, _flowTags);
        return new BlockMetricsContext(blockName, blockTags, _metrics);
    }

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

    private static KeyValuePair<string, object?>[] CreateFlowCompletionTags(KeyValuePair<string, object?>[] flowTags, bool successful)
    {
        var completionTags = new KeyValuePair<string, object?>[flowTags.Length + 1];
        flowTags.CopyTo(completionTags, 0);
        completionTags[flowTags.Length] = successful
            ? DataFlowMetrics.TagConstantValues.SuccessOutcomeTag
            : DataFlowMetrics.TagConstantValues.FailureOutcomeTag;
        return completionTags;
    }

    private static KeyValuePair<string, object?>[] CreateBlockTags(string blockName, KeyValuePair<string, object?>[] flowTags)
    {
        var blockTags = new KeyValuePair<string, object?>[flowTags.Length + 1];
        flowTags.CopyTo(blockTags, 0);
        blockTags[flowTags.Length] = new(DataFlowMetrics.TagNames.BlockName, blockName);
        return blockTags;
    }

    #endregion
}
