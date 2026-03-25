namespace DataFlow.Blazor.FlowMetadata;

/// <summary>
/// Visualization metadata for a registered dataflow (graph).
/// Configured at registration time via the <c>DataFlowBuilder</c> fluent API.
/// </summary>
public sealed class DataFlowFlowMetadata
{
    /// <summary>
    /// Human-readable display name shown as the flow title in the visualization.
    /// When null the flow's registered key is used.
    /// </summary>
    public string? DisplayName { get; init; }
}
