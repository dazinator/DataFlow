namespace DataFlow.Blazor.ItemTypes;

/// <summary>
/// Fluent builder for configuring data item type metadata.
/// Used inside the <c>services.AddDataFlowItemTypes(builder => { ... })</c> callback.
/// </summary>
public sealed class DataFlowItemTypeStoreBuilder
{
    private readonly Dictionary<Type, DataFlowItemTypeMetadata> _entries = new();

    /// <summary>Configures metadata for type <typeparamref name="T"/>.</summary>
    public ITypeMetadataBuilder ForType<T>() => ForType(typeof(T));

    /// <summary>Configures metadata for the supplied CLR type.</summary>
    public ITypeMetadataBuilder ForType(Type type) => new TypeMetadataBuilder(this, type);

    internal IDataFlowItemTypeStore Build() =>
        new DataFlowItemTypeStore(new Dictionary<Type, DataFlowItemTypeMetadata>(_entries));

    private void Apply(Type type, DataFlowItemTypeMetadata metadata) =>
        _entries[type] = metadata;

    // ── Builder returned by ForType<T>() ────────────────────────────────────

    /// <summary>
    /// Fluent API for setting metadata on a single type.
    /// </summary>
    public interface ITypeMetadataBuilder
    {
        /// <summary>Sets the human-readable label shown in the flow visualization.</summary>
        ITypeMetadataBuilder Label(string label);
    }

    private sealed class TypeMetadataBuilder : ITypeMetadataBuilder
    {
        private readonly DataFlowItemTypeStoreBuilder _parent;
        private readonly Type _type;
        private string? _label;

        internal TypeMetadataBuilder(DataFlowItemTypeStoreBuilder parent, Type type)
        {
            _parent = parent;
            _type   = type;
        }

        public ITypeMetadataBuilder Label(string label)
        {
            _label = label;
            _parent.Apply(_type, new DataFlowItemTypeMetadata { Label = _label });
            return this;
        }
    }
}
