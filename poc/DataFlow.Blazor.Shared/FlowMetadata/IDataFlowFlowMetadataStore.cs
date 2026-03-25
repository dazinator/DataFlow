namespace DataFlow.Blazor.FlowMetadata;

/// <summary>
/// Provides visualization metadata for registered dataflows (graphs), keyed by their
/// registered flow name (e.g. <c>"journal-v2:my-graph"</c>).
/// Registered as a singleton populated at startup via <c>AddDataFlows()</c>.
/// </summary>
public interface IDataFlowFlowMetadataStore
{
    /// <summary>Returns the full metadata for the named flow, or <see langword="null"/> if none is registered.</summary>
    DataFlowFlowMetadata? GetMetadata(string flowName);

    /// <summary>
    /// Returns the configured display name for the named flow, or <see langword="null"/> if none is registered.
    /// </summary>
    string? GetDisplayName(string flowName);
}
