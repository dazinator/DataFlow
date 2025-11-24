namespace DataFlow.POC.Observability;

/// <summary>
/// Represents a metrics tagging context that provides consistent tag sets for metrics collection.
/// </summary>
public interface IMetricsTagsContext
{
    /// <summary>
    /// Gets the flow-level completion tags, if available.
    /// These tags include outcome information (success/failure) and are used for completion metrics.
    /// </summary>
    KeyValuePair<string, object?>[]? FlowLevelCompletionTags { get; }
    
    /// <summary>
    /// Gets the name of the flow or block this context represents.
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Gets the flow-wide tags that are common across all metrics for this execution.
    /// These tags typically include flow name and global tags.
    /// </summary>
    KeyValuePair<string, object?>[] FlowWideTags { get; }
}
