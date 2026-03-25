namespace DataFlow.Blazor.BlockTypes;

/// <summary>
/// Aggregating in-memory implementation of <see cref="IDataFlowBlockMetadataStore"/>.
/// Merges all <see cref="DataFlowBlockMetadataContribution"/> instances registered by each
/// <c>AddDataFlows()</c> call that supplied inline block metadata. Thread-safe for reads
/// after construction (constructed once as a singleton by the DI container).
/// Last-writer wins when the same block name is contributed more than once.
/// </summary>
internal sealed class DataFlowBlockMetadataStore : IDataFlowBlockMetadataStore
{
    private readonly Dictionary<string, DataFlowBlockMetadata> _metadata;

    internal DataFlowBlockMetadataStore(IEnumerable<DataFlowBlockMetadataContribution> contributions)
    {
        _metadata = new();
        foreach (var contribution in contributions)
            foreach (var (key, value) in contribution.Entries)
                _metadata[key] = value;
    }

    public DataFlowBlockMetadata? GetMetadata(string blockName) =>
        _metadata.TryGetValue(blockName, out var m) ? m : null;

    public string? GetDisplayName(string blockName) =>
        _metadata.TryGetValue(blockName, out var m) ? m.DisplayName : null;

    public string? GetTypeLabel(string blockName) =>
        _metadata.TryGetValue(blockName, out var m) ? m.TypeLabel : null;
}
