namespace DataFlow.Blazor.ItemTypes;

/// <summary>
/// Default in-memory implementation of <see cref="IDataFlowItemTypeStore"/>.
/// Populated at startup via <see cref="DataFlowItemTypeStoreBuilder"/> and registered
/// as a singleton. Thread-safe for reads after construction.
/// </summary>
internal sealed class DataFlowItemTypeStore : IDataFlowItemTypeStore
{
    private readonly IReadOnlyDictionary<Type, DataFlowItemTypeMetadata> _metadata;

    internal DataFlowItemTypeStore(IReadOnlyDictionary<Type, DataFlowItemTypeMetadata> metadata)
    {
        _metadata = metadata;
    }

    public DataFlowItemTypeMetadata? GetMetadata(Type type) =>
        _metadata.TryGetValue(type, out var m) ? m : null;

    public string GetLabel(Type type)
    {
        if (_metadata.TryGetValue(type, out var m) && m.Label is not null)
            return m.Label;

        return DataFlowTypeNameFormatter.Format(type);
    }
}
