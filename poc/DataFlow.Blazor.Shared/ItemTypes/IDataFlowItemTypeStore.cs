namespace DataFlow.Blazor.ItemTypes;

/// <summary>
/// Provides human-readable labels and metadata for data item types that flow through
/// DataFlow pipelines. Register via <c>services.AddDataFlowItemTypes(...)</c>.
///
/// Labels are resolved when the graph emits a <see cref="Events.FlowGraphDefinedEvent"/>
/// so they appear in the visualization without any instrumentation inside blocks.
/// </summary>
public interface IDataFlowItemTypeStore
{
    /// <summary>
    /// Returns the configured metadata for a CLR type, or <c>null</c> if none is registered.
    /// </summary>
    DataFlowItemTypeMetadata? GetMetadata(Type type);

    /// <summary>
    /// Returns a human-readable label for a CLR type.
    /// Falls back to a formatted CLR type name when no metadata is registered:
    /// <c>Invoice</c> → "Invoice", <c>Invoice[]</c> → "Invoice[]",
    /// <c>List&lt;Invoice&gt;</c> → "Invoice list".
    /// Returns an empty string for noise types such as <c>object</c>.
    /// </summary>
    string GetLabel(Type type);
}

/// <summary>
/// Metadata record associated with a data item type.
/// Extended with additional fields as the feature set grows.
/// </summary>
public sealed class DataFlowItemTypeMetadata
{
    public string? Label { get; init; }
}
