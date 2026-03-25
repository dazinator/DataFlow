namespace DataFlow.Blazor.BlockTypes;

/// <summary>
/// Visualization metadata for a single block instance.
/// Configured at registration time via <see cref="DataFlowBlockMetadataStoreBuilder"/>.
/// </summary>
public sealed class DataFlowBlockMetadata
{
    /// <summary>
    /// Human-readable display name shown as the block title in the visualization.
    /// When null the block's registered name is used (truncated if long).
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Overrides the block type label shown beneath the title in the visualization.
    /// When null, <see cref="DataFlowBlockTypeNameFormatter.Format"/> provides a sensible
    /// default for native block types, falling back to <c>Type.Name</c> for unknown types.
    /// </summary>
    public string? TypeLabel { get; init; }
}
