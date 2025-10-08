namespace Uniun.DataFlow.Builder.Graph;

using Uniun.DataFlow.Blocks;

/// <summary>
/// Interface for the structured dataflow builder that provides graph-based flow definition.
/// This interface enables extension methods and allows sub-builders to work with different implementations.
/// </summary>
public interface IStructuredDataFlowBuilder
{
    /// <summary>
    /// Gets the graph representation of the dataflow.
    /// </summary>
    DataFlowGraph Graph { get; }

    /// <summary>
    /// Gets the service provider for resolving dependencies.
    /// </summary>
    IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Adds a block definition to the graph without instantiating it yet.
    /// </summary>
    void AddBlockDefinition<TBlock>(
        string name,
        Func<IServiceProvider, TBlock> factory,
        Type? inputType = null,
        Type? outputType = null,
        Dictionary<string, object>? metadata = null) where TBlock : IBlock;

    /// <summary>
    /// Adds a connection between two blocks in the graph.
    /// </summary>
    void AddConnection(
        string sourceBlockName,
        string targetBlockName,
        Type? dataType = null,
        Dictionary<string, object>? metadata = null);

    /// <summary>
    /// Sets the last source block for positional chaining.
    /// </summary>
    void SetLastSourceBlock(string blockName);

    /// <summary>
    /// Gets the last source block name for positional chaining.
    /// </summary>
    string? GetLastSourceBlockName();
}
