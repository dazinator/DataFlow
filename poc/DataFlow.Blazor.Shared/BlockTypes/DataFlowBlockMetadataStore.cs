namespace DataFlow.Blazor.BlockTypes;

/// <summary>
/// Default in-memory implementation of <see cref="IDataFlowBlockMetadataStore"/>.
/// Populated at startup via <see cref="DataFlowBlockMetadataStoreBuilder"/> and registered
/// as a singleton. Thread-safe for reads after construction.
/// </summary>
internal sealed class DataFlowBlockMetadataStore : IDataFlowBlockMetadataStore
{
    private readonly IReadOnlyDictionary<string, DataFlowBlockMetadata> _metadata;

    internal DataFlowBlockMetadataStore(IReadOnlyDictionary<string, DataFlowBlockMetadata> metadata)
    {
        _metadata = metadata;
    }

    public DataFlowBlockMetadata? GetMetadata(string blockName) =>
        _metadata.TryGetValue(blockName, out var m) ? m : null;

    public string? GetDisplayName(string blockName) =>
        _metadata.TryGetValue(blockName, out var m) ? m.DisplayName : null;

    public string? GetTypeLabel(string blockName) =>
        _metadata.TryGetValue(blockName, out var m) ? m.TypeLabel : null;
}
