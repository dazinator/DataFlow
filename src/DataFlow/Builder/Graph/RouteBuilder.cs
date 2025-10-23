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
    private IBlock? _entryBlock;

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
    
    /// <summary>
    /// Gets the entry block for this route. This is the InputChannelBlock that receives items from the routing block.
    /// Available after Build() is called.
    /// </summary>
    public IBlock? EntryBlock => _entryBlock;
    
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
    
    /// <summary>
    /// Gets the last block in the route that is also an ISourceBlock.
    /// Returns null if the route ends with a terminal (target-only) block.
    /// This is important for merge scenarios - only source blocks can output to a merge block.
    /// </summary>
    public string? GetLastSourceBlockForMerge()
    {
        // Get all blocks that don't have any outgoing connections (potential terminal blocks)
        // Exclude the InputChannelBlock as it's internal infrastructure
        var allBlockNames = _graph.BlockDefinitions
            .Where(kvp => !kvp.Value.Metadata.ContainsKey("IsRouteInputChannel"))
            .Select(kvp => kvp.Key)
            .ToHashSet();
            
        var sourceBlocksWithOutgoingConnections = _graph.Connections
            .Select(c => c.SourceBlockName)
            .ToHashSet();
        
        // Find blocks that have no outgoing connections (terminal blocks)
        var terminalBlockNames = allBlockNames.Except(sourceBlocksWithOutgoingConnections).ToList();
        
        // If we have terminal blocks, check if any is a source block
        foreach (var terminalBlockName in terminalBlockNames)
        {
            var blockDef = _graph.BlockDefinitions[terminalBlockName];
            if (blockDef.IsSourceBlock())
            {
                // This terminal block is a source block, it can merge
                return terminalBlockName;
            }
        }
        
        // No terminal source block found - the route ends with a target-only block
        // This route cannot merge
        return null;
    }

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
    /// Ensures an InputChannelBlock exists at the start of the route's pipeline.
    /// The InputChannelBlock is connected to the route's entry target block.
    /// If an InputChannelBlock already exists in the correct position, does nothing.
    /// </summary>
    /// <typeparam name="T">The type of items that will be routed to this route</typeparam>
    /// <param name="channelFactory">Factory for creating the channel</param>
    /// <param name="options">Options for the InputChannelBlock</param>
    /// <returns>The name of the InputChannelBlock (either existing or newly created)</returns>
    public string EnsureInputChannelBlock<T>(IBoundedChannelFactory channelFactory, BlockOptions options)
    {
        // Check if there's already an InputChannelBlock in the graph
        var existingInputChannel = _graph.BlockDefinitions.Values
            .FirstOrDefault(bd => bd.Metadata.ContainsKey("IsRouteInputChannel") || 
                                 (bd.BlockType.IsGenericType && 
                                  bd.BlockType.GetGenericTypeDefinition() == typeof(Uniun.DataFlow.Blocks.InputChannel.InputChannelBlock<>)));
        
        if (existingInputChannel != null)
        {
            // Already has an InputChannelBlock, return its name
            return existingInputChannel.Name;
        }
        
        // Get the entry block - this must be a target block
        var entryBlocks = _graph.GetEntryBlocks().ToList();
        if (!entryBlocks.Any())
        {
            throw new InvalidOperationException(
                $"Route '{RouteName}' must have at least one entry block before calling EnsureInputChannelBlock. " +
                "The first target block added is automatically marked as the entry block, or you can explicitly call AsEntry() on a target block.");
        }
        
        var entryBlockName = entryBlocks.First().Name;
        
        // Create a new InputChannelBlock
        var inputChannelBlockName = $"input-channel-{RouteName}";
        
        // Add InputChannelBlock to the route's graph
        AddBlockDefinition(
            inputChannelBlockName,
            sp => new Uniun.DataFlow.Blocks.InputChannel.InputChannelBlock<T>(
                inputChannelBlockName,
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Uniun.DataFlow.Blocks.InputChannel.InputChannelBlock<T>>>(),
                channelFactory,
                options),
            inputType: null, // InputChannelBlock doesn't receive from other blocks in the route
            outputType: typeof(T),
            metadata: new Dictionary<string, object>
            {
                ["IsRouteInputChannel"] = true
            });
        
        // Connect InputChannelBlock to the entry target block
        AddConnection(inputChannelBlockName, entryBlockName, typeof(T));
        
        return inputChannelBlockName;
    }

    /// <summary>
    /// Gets the InputChannelBlock for this route after it has been ensured.
    /// </summary>
    /// <typeparam name="T">The type of items</typeparam>
    /// <returns>The InputChannelBlock instance, or null if not found</returns>
    public Uniun.DataFlow.Blocks.InputChannel.InputChannelBlock<T>? GetInputChannelBlock<T>()
    {
        var inputChannelBlockName = _graph.BlockDefinitions.Values
            .FirstOrDefault(bd => bd.Metadata.ContainsKey("IsRouteInputChannel"))?.Name;
        
        if (inputChannelBlockName != null && _state.Blocks.TryGetValue(inputChannelBlockName, out var block))
        {
            return block as Uniun.DataFlow.Blocks.InputChannel.InputChannelBlock<T>;
        }
        
        return null;
    }

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

        // Store the InputChannelBlock as the entry block for easy access
        // The InputChannelBlock is the block that the routing block writes to
        var inputChannelBlockDef = _graph.BlockDefinitions.Values
            .FirstOrDefault(bd => bd.Metadata.ContainsKey("IsRouteInputChannel"));
        if (inputChannelBlockDef != null && blocks.TryGetValue(inputChannelBlockDef.Name, out var inputChannelBlock))
        {
            _entryBlock = inputChannelBlock;
        }

        // Create runtime graph for initialization
        var runtimeGraph = new DataFlowRuntimeGraph(_graph.Name, _graph, blocks);
        
        // Call OnDataFlowInitialized on all blocks that implement IDataFlowInitializable
        foreach (var block in blocks.Values)
        {
            if (block is IDataFlowInitializable initializableBlock)
            {
                initializableBlock.OnDataFlowInitialized(runtimeGraph, CancellationToken.None);
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

    public ISourceBlock<T> GetSourceBlock<T>(string blockName)
    {
        if (!_state.Blocks.TryGetValue(blockName, out var block))
        {
            throw new InvalidOperationException($"Block '{blockName}' not found in route '{RouteName}'");
        }

        if (block is not ISourceBlock<T> sourceBlock)
        {
            throw new InvalidOperationException(
                $"Block '{blockName}' is not a source block for type {typeof(T).Name}");
        }

        return sourceBlock;
    }

    /// <summary>
    /// Gets a block by name without type checking.
    /// </summary>
    /// <param name="blockName">The name of the block to retrieve.</param>
    /// <returns>The block instance.</returns>
    /// <remarks>
    /// This method bypasses type checking and is intended for scenarios like merge connections
    /// where the exact type may not be known at compile time.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the block is not found.</exception>
    public IBlock GetBlock(string blockName)
    {
        if (!_state.Blocks.TryGetValue(blockName, out var block))
        {
            throw new InvalidOperationException($"Block '{blockName}' not found in route '{RouteName}'");
        }

        return block;
    }
}
