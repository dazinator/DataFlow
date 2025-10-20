namespace Uniun.DataFlow.Builder.Graph;

using Microsoft.Extensions.DependencyInjection;
using Uniun.DataFlow.Blocks;

/// <summary>
/// A builder for constructing route branches.
/// Implements IRouteBuilder which extends IBranchBuilder to share the same API as branch builders.
/// Unlike regular branches, routes have special entry block requirements and Build() capability.
/// </summary>
public class RouteBuilder : IRouteBuilder
{
    private readonly DataFlowBuilderState _state;
    private readonly DataFlowGraph _graph;
    private string? _entryBlockName;
    private string? _lastSourceBlockName;

    public RouteBuilder(IServiceProvider serviceProvider, string routeName, DataFlowGraph? parentGraph = null)
    {
        ServiceProvider = serviceProvider;
        RouteName = routeName;
        BranchName = $"Route-{routeName}";
        BranchIndex = null; // Routes don't have numeric indices
        _state = new DataFlowBuilderState(serviceProvider);
        _graph = new DataFlowGraph(BranchName);
        ParentGraph = parentGraph;
        // Routes don't have a parent builder in the traditional sense
        ParentBuilder = null!; // Will not be used for routes
    }

    public IServiceProvider ServiceProvider { get; }
    public string RouteName { get; }
    public DataFlowGraph Graph => _graph;
    
    // IBranchBuilder properties
    public string BranchName { get; }
    public int? BranchIndex { get; }
    public IStructuredDataFlowBuilder ParentBuilder { get; }
    public string? LastSourceBlockInBranch => _lastSourceBlockName;
    public string? ParentBlockAtCreation => null; // Routes are created on-demand, not from a specific parent block
    
    /// <summary>
    /// The parent graph this route belongs to (if any).
    /// This allows routes to lookup existing branches from the parent.
    /// </summary>
    public DataFlowGraph? ParentGraph { get; }

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

        _graph.AddBlockDefinition(blockDefinition);

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

        _graph.AddConnection(connection);
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
            var prevBlockDef = _graph.GetBlockDefinition(_entryBlockName);
            prevBlockDef.IsEntryBlock = false;
        }

        _entryBlockName = blockName;
        var blockDef = _graph.GetBlockDefinition(blockName);
        blockDef.IsEntryBlock = true;
    }

    public string? GetEntryBlockName() => _entryBlockName;

    /// <summary>
    /// Builds the route as a DataFlow for execution by the routing block.
    /// This is called by the routing block when instantiating a route.
    /// </summary>
    public IDataFlow Build()
    {
        // Validate that we have an entry block
        if (string.IsNullOrEmpty(_entryBlockName))
        {
            throw new InvalidOperationException($"Route '{RouteName}' must have an entry block set");
        }

        // Validate the entry block exists and is a target block
        var entryBlockDef = _graph.GetBlockDefinition(_entryBlockName);
        if (!entryBlockDef.IsTargetBlock())
        {
            throw new InvalidOperationException(
                $"Entry block '{_entryBlockName}' for route '{RouteName}' must be a target block");
        }

        // Instantiate all blocks
        var blocks = new Dictionary<string, IBlock>();
        foreach (var definition in _graph.BlockDefinitions.Values)
        {
            var block = definition.Factory(ServiceProvider);
            blocks[definition.Name] = block;
            _state.Blocks[definition.Name] = block;
        }

        // Wire up connections
        foreach (var connection in _graph.Connections)
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
        return new DataFlow(_graph.Name, blocks.Values.ToList(), metrics);
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
