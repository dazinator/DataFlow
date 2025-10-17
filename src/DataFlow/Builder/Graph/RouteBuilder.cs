namespace Uniun.DataFlow.Builder.Graph;

using Microsoft.Extensions.DependencyInjection;
using Uniun.DataFlow.Blocks;

/// <summary>
/// A builder for constructing route sub-dataflows.
/// Implements IRouteBuilder which extends IStructuredDataFlowBuilder to share the same API as the main builder.
/// </summary>
public class RouteBuilder : IRouteBuilder
{
    private readonly DataFlowBuilderState _state;
    private string? _entryBlockName;
    private string? _lastSourceBlockName;

    public RouteBuilder(IServiceProvider serviceProvider, string routeName)
    {
        ServiceProvider = serviceProvider;
        RouteName = routeName;
        _state = new DataFlowBuilderState(serviceProvider);
        Graph = new DataFlowGraph($"Route-{routeName}");
    }

    public IServiceProvider ServiceProvider { get; }
    public string RouteName { get; }
    public DataFlowGraph Graph { get; }

    public void AddBlockDefinition<TBlock>(
        string name,
        Func<IServiceProvider, TBlock> factory,
        Type? inputType = null,
        Type? outputType = null,
        Dictionary<string, object>? metadata = null) where TBlock : IBlock
    {
        var blockDefinition = new BlockDefinition(
            name,
            typeof(TBlock),
            sp => factory(sp))
        {
            InputType = inputType,
            OutputType = outputType,
            Metadata = metadata ?? new Dictionary<string, object>()
        };

        Graph.AddBlockDefinition(blockDefinition);

        // Automatically set the first target block as the entry block if none is set yet
        if (_entryBlockName == null && blockDefinition.IsTargetBlock())
        {
            _entryBlockName = name;
            blockDefinition.IsEntryBlock = true;
        }
    }

    public void AddConnection(
        string sourceBlockName,
        string targetBlockName,
        Type? dataType = null,
        Dictionary<string, object>? metadata = null)
    {
        var connection = new BlockConnection(sourceBlockName, targetBlockName)
        {
            DataType = dataType,
            Metadata = metadata ?? new Dictionary<string, object>()
        };

        Graph.AddConnection(connection);
    }

    public void SetLastSourceBlock(string blockName)
    {
        _lastSourceBlockName = blockName;
    }

    public string? GetLastSourceBlockName() => _lastSourceBlockName;

    public void SetEntryBlock(string blockName)
    {
        // Clear previous entry block flag if any
        if (_entryBlockName != null)
        {
            var prevBlockDef = Graph.GetBlockDefinition(_entryBlockName);
            prevBlockDef.IsEntryBlock = false;
        }

        _entryBlockName = blockName;
        var blockDef = Graph.GetBlockDefinition(blockName);
        blockDef.IsEntryBlock = true;
    }

    public string? GetEntryBlockName() => _entryBlockName;

    public IDataFlow Build()
    {
        // Validate that we have an entry block
        if (string.IsNullOrEmpty(_entryBlockName))
        {
            throw new InvalidOperationException($"Route '{RouteName}' must have an entry block set");
        }

        // Validate the entry block exists and is a target block
        var entryBlockDef = Graph.GetBlockDefinition(_entryBlockName);
        if (!entryBlockDef.IsTargetBlock())
        {
            throw new InvalidOperationException(
                $"Entry block '{_entryBlockName}' for route '{RouteName}' must be a target block");
        }

        // Instantiate all blocks
        var blocks = new Dictionary<string, IBlock>();
        foreach (var definition in Graph.BlockDefinitions.Values)
        {
            var block = definition.Factory(ServiceProvider);
            blocks[definition.Name] = block;
            _state.Blocks[definition.Name] = block;
        }

        // Wire up connections
        foreach (var connection in Graph.Connections)
        {
            var sourceBlock = blocks[connection.SourceBlockName];
            var targetBlock = blocks[connection.TargetBlockName];

            // Use reflection to call SetSource on the target block
            var setSourceMethod = targetBlock.GetType().GetMethod(nameof(ITargetBlock<object>.SetSource));
            if (setSourceMethod != null)
            {
                setSourceMethod.Invoke(targetBlock, new object[] { sourceBlock });
            }
            else
            {
                throw new InvalidOperationException(
                    $"Target block '{connection.TargetBlockName}' does not have a {nameof(ITargetBlock<object>.SetSource)} method");
            }
        }

        // Create and return the dataflow
        var metrics = ServiceProvider.GetRequiredService<Uniun.DataFlow.Metrics.IDataFlowMetrics>();
        return new DataFlow(Graph.Name, blocks.Values.ToList(), metrics);
    }

    public ITargetBlock<T> GetTargetBlock<T>(string blockName)
    {
        if (!_state.Blocks.TryGetValue(blockName, out var block))
        {
            throw new InvalidOperationException($"Block '{blockName}' not found in route '{RouteName}'");
        }

        if (block is not ITargetBlock<T> targetBlock)
        {
            throw new InvalidOperationException(
                $"Block '{blockName}' is not a target block for type {typeof(T).Name}");
        }

        return targetBlock;
    }
}
