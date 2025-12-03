namespace DataFlow.POC.Visualization;

/// <summary>
/// Options for controlling graph rendering behavior.
/// </summary>
public class GraphRenderOptions
{
    /// <summary>
    /// Direction for flowcharts (LR, TB, RL, BT).
    /// Primarily used by Mermaid renderer.
    /// Default: LR (left to right).
    /// </summary>
    public string Direction { get; set; } = "LR";

    /// <summary>
    /// Whether to include type information in node labels.
    /// Default: true.
    /// </summary>
    public bool IncludeTypeInfo { get; set; } = true;

    /// <summary>
    /// Whether to include edge capacity information in edge labels.
    /// Default: false (to keep diagrams clean).
    /// </summary>
    public bool ShowBufferCapacity { get; set; } = false;

    /// <summary>
    /// Whether to show epoch nodes (EpochSource and EpochProcessors) if present.
    /// Only relevant for POC graphs that have epoch processing configured.
    /// Default: true.
    /// </summary>
    public bool ShowEpochNodes { get; set; } = true;
}
