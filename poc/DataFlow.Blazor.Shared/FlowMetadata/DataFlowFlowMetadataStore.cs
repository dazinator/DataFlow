namespace DataFlow.Blazor.FlowMetadata;

/// <summary>
/// Aggregating in-memory implementation of <see cref="IDataFlowFlowMetadataStore"/>.
/// Merges all <see cref="DataFlowFlowMetadataContribution"/> instances registered by
/// <c>AddDataFlows()</c> calls that set a flow display name. Thread-safe for reads
/// after construction (constructed once as a singleton by the DI container).
/// </summary>
internal sealed class DataFlowFlowMetadataStore : IDataFlowFlowMetadataStore
{
    private readonly Dictionary<string, DataFlowFlowMetadata> _metadata;

    internal DataFlowFlowMetadataStore(IEnumerable<DataFlowFlowMetadataContribution> contributions)
    {
        _metadata = new();
        foreach (var c in contributions)
            _metadata[c.FlowName] = c.Metadata;
    }

    public DataFlowFlowMetadata? GetMetadata(string flowName) =>
        _metadata.TryGetValue(flowName, out var m) ? m : null;

    public string? GetDisplayName(string flowName) =>
        _metadata.TryGetValue(flowName, out var m) ? m.DisplayName : null;
}
