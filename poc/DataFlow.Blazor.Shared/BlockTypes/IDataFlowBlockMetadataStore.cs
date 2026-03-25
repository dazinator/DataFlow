namespace DataFlow.Blazor.BlockTypes;

/// <summary>
/// Provides visualization metadata for block instances, keyed by their registered block name.
/// Registered as a singleton automatically by <c>AddDataFlows()</c> when inline metadata is
/// configured on any block.
/// </summary>
public interface IDataFlowBlockMetadataStore
{
    /// <summary>Returns the full metadata for the named block, or null if none is registered.</summary>
    DataFlowBlockMetadata? GetMetadata(string blockName);

    /// <summary>
    /// Returns the configured display name for the named block, or null if none is registered.
    /// </summary>
    string? GetDisplayName(string blockName);

    /// <summary>
    /// Returns the configured type label for the named block, or null if none is registered.
    /// Callers should fall back to <see cref="DataFlowBlockTypeNameFormatter.Format"/> when null.
    /// </summary>
    string? GetTypeLabel(string blockName);
}
