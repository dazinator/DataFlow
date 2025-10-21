namespace Uniun.DataFlow.Builder.Graph;

using Uniun.DataFlow.Blocks;

/// <summary>
/// Implementation of the runtime graph containing instantiated block instances.
/// This provides read-only access to blocks created during the Build phase.
/// </summary>
public class DataFlowRuntimeGraph : IDataFlowRuntimeGraph
{
    private readonly Dictionary<string, IBlock> _blockInstances;

    public DataFlowRuntimeGraph(
        string name,
        DataFlowGraph graph,
        Dictionary<string, IBlock> blockInstances)
    {
        Name = name;
        Graph = graph;
        _blockInstances = blockInstances;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public DataFlowGraph Graph { get; }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, IBlock> BlockInstances => _blockInstances;

    /// <inheritdoc/>
    public IBlock GetBlockInstance(string name)
    {
        if (!_blockInstances.TryGetValue(name, out var block))
        {
            throw new InvalidOperationException($"Block '{name}' not found in runtime graph");
        }
        return block;
    }

    /// <inheritdoc/>
    public bool TryGetBlockInstance(string name, out IBlock block)
    {
        return _blockInstances.TryGetValue(name, out block!);
    }

    /// <inheritdoc/>
    public TBlock GetBlockInstance<TBlock>(string name) where TBlock : IBlock
    {
        var block = GetBlockInstance(name);
        if (block is not TBlock typedBlock)
        {
            throw new InvalidOperationException(
                $"Block '{name}' is of type '{block.GetType().Name}' but expected '{typeof(TBlock).Name}'");
        }
        return typedBlock;
    }

    /// <inheritdoc/>
    public bool TryGetBlockInstance<TBlock>(string name, out TBlock block) where TBlock : IBlock
    {
        if (!_blockInstances.TryGetValue(name, out var untypedBlock))
        {
            block = default!;
            return false;
        }

        if (untypedBlock is TBlock typedBlock)
        {
            block = typedBlock;
            return true;
        }

        block = default!;
        return false;
    }
}
