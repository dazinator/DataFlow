namespace DataFlow.POC.Core;

using System.Threading.Channels;
using Microsoft.Extensions.Logging;

/// <summary>
/// Represents a dataflow graph that orchestrates block execution and edge management.
/// The graph owns topology, wiring, and execution coordination.
/// </summary>
public class DataFlowGraph
{
    private readonly ILogger<DataFlowGraph> _logger;
    private readonly List<IBlock> _blocks = new();
    private readonly List<Edge> _edges = new();
    private readonly Dictionary<IBlock, List<Edge>> _outgoingEdges = new();
    private readonly Dictionary<IBlock, List<Edge>> _incomingEdges = new();

    public DataFlowGraph(string name, ILogger<DataFlowGraph> logger)
    {
        Name = name;
        _logger = logger;
    }

    /// <summary>
    /// The name of this dataflow graph.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// All blocks in the graph.
    /// </summary>
    public IReadOnlyList<IBlock> Blocks => _blocks;

    /// <summary>
    /// All edges in the graph.
    /// </summary>
    public IReadOnlyList<Edge> Edges => _edges;

    /// <summary>
    /// Add a block to the graph.
    /// </summary>
    public void AddBlock(IBlock block)
    {
        if (_blocks.Any(b => b.Name == block.Name))
        {
            throw new ArgumentException($"Block with name '{block.Name}' already exists");
        }
        _blocks.Add(block);
        _logger.LogDebug("Added block: {BlockName}", block.Name);
    }

    /// <summary>
    /// Add an edge connecting two blocks.
    /// </summary>
    public void AddEdge(Edge edge)
    {
        if (!_blocks.Contains(edge.SourceBlock))
        {
            throw new ArgumentException($"Source block '{edge.SourceBlock.Name}' not found in graph");
        }
        
        foreach (var targetBlock in edge.TargetBlocks)
        {
            if (!_blocks.Contains(targetBlock))
            {
                throw new ArgumentException($"Target block '{targetBlock.Name}' not found in graph");
            }
        }

        _edges.Add(edge);

        // Track outgoing edges per block
        if (!_outgoingEdges.ContainsKey(edge.SourceBlock))
        {
            _outgoingEdges[edge.SourceBlock] = new List<Edge>();
        }
        _outgoingEdges[edge.SourceBlock].Add(edge);

        // Track incoming edges per block (for all targets)
        foreach (var targetBlock in edge.TargetBlocks)
        {
            if (!_incomingEdges.ContainsKey(targetBlock))
            {
                _incomingEdges[targetBlock] = new List<Edge>();
            }
            _incomingEdges[targetBlock].Add(edge);
        }

        _logger.LogDebug("Added edge: {Edge}", edge);
    }

    /// <summary>
    /// Execute the dataflow graph.
    /// This wires up all blocks and edges, starts execution, and waits for completion.
    /// </summary>
    public async Task ExecuteAsync(IExecutionContext context)
    {
        _logger.LogInformation("Starting execution of dataflow: {FlowName}", Name);

        // Dictionary to store the output streams from each block
        var blockOutputs = new Dictionary<IBlock, IAsyncEnumerable<object>>();

        // Dictionary to store typed channel adapters per target block per edge
        // Structure: Edge -> (TargetBlock -> TypedChannelAdapter)
        var edgeChannels = new Dictionary<Edge, Dictionary<IBlock, TypedChannelAdapter>>();

        // Create typed channel adapters for all buffered edges using their strategies
        foreach (var edge in _edges.Where(e => e.BufferMode != BufferMode.None))
        {
            var channels = edge.Strategy.CreateTypedChannels(edge.DataType, edge.SourceBlock, edge.TargetBlocks);
            edgeChannels[edge] = channels;
            
            _logger.LogDebug("Created typed channel adapters for edge: {Edge} with strategy {Strategy} and type {DataType}", 
                edge, edge.Strategy.EdgeType, edge.DataType.Name);
        }

        // Start all blocks
        var blockTasks = new List<Task>();

        foreach (var block in _blocks)
        {
            var task = Task.Run(async () =>
            {
                try
                {
                    // Determine input source for this block
                    IAsyncEnumerable<object> input;

                    if (!_incomingEdges.ContainsKey(block) || _incomingEdges[block].Count == 0)
                    {
                        // Source block - no input
                        input = EmptyAsyncEnumerable();
                    }
                    else if (_incomingEdges[block].Count == 1)
                    {
                        // Single input edge
                        var edge = _incomingEdges[block][0];
                        input = GetEdgeInput(block, edge, blockOutputs, edgeChannels);
                    }
                    else
                    {
                        // Multiple inputs - merge them
                        var inputs = _incomingEdges[block]
                            .Select(edge => GetEdgeInput(block, edge, blockOutputs, edgeChannels))
                            .ToList();
                        input = MergeAsyncEnumerables(inputs);
                    }

                    // Execute the block
                    var output = block.ExecuteAsync(input, context);

                    // Handle output routing
                    if (_outgoingEdges.ContainsKey(block) && _outgoingEdges[block].Count > 0)
                    {
                        // Route output to downstream edges
                        output = RouteOutput(block, output, _outgoingEdges[block], edgeChannels, context.CancellationToken);
                    }

                    // Materialize the output (consume the enumerable)
                    await foreach (var item in output.WithCancellation(context.CancellationToken))
                    {
                        // Items are consumed/routed during enumeration
                    }

                    // Complete all outgoing typed channel adapters
                    if (_outgoingEdges.ContainsKey(block))
                    {
                        foreach (var edge in _outgoingEdges[block])
                        {
                            if (edgeChannels.TryGetValue(edge, out var channels))
                            {
                                // Deduplicate adapters in case of competing edge strategy
                                // where multiple targets share the same channel adapter.
                                // 
                                // With CompetingEdgeStrategy, all target blocks receive the same
                                // TypedChannelAdapter instance (they share one channel for competition).
                                // We use Distinct() to ensure we only call Complete() once per unique adapter,
                                // preventing "channel already closed" exceptions.
                                //
                                // TypedChannelAdapter is a reference type, so Distinct() compares by reference equality,
                                // which correctly identifies when multiple dictionary entries point to the same adapter.
                                var uniqueAdapters = channels.Values
                                    .Distinct()
                                    .ToList();
                                
                                foreach (var adapter in uniqueAdapters)
                                {
                                    adapter.Complete();
                                }
                                _logger.LogDebug("Completed channels for edge: {Edge}", edge);
                            }
                        }
                    }

                    _logger.LogDebug("Block {BlockName} completed successfully", block.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Block {BlockName} failed with error", block.Name);

                    // Complete typed channel adapters with exception
                    if (_outgoingEdges.ContainsKey(block))
                    {
                        foreach (var edge in _outgoingEdges[block])
                        {
                            if (edgeChannels.TryGetValue(edge, out var channels))
                            {
                                // Deduplicate adapters in case of competing edge strategy
                                var uniqueAdapters = channels.Values
                                    .Distinct()
                                    .ToList();
                                
                                foreach (var adapter in uniqueAdapters)
                                {
                                    adapter.Complete(ex);
                                }
                            }
                        }
                    }
                    throw;
                }
            }, context.CancellationToken);

            blockTasks.Add(task);
        }

        // Wait for all blocks to complete
        await Task.WhenAll(blockTasks);

        _logger.LogInformation("Completed execution of dataflow: {FlowName}", Name);
    }

    /// <summary>
    /// Gets the IAsyncEnumerable input stream for a target block from an edge.
    /// This method determines how the target block receives its input data:
    /// - For inline (unbuffered) edges: returns direct reference to source block's output stream
    /// - For buffered edges: returns stream that reads from the typed channel adapter associated with this target block
    /// 
    /// In competing consumer scenarios, multiple target blocks may share the same channel adapter,
    /// causing them to compete for items. In broadcast scenarios, each target has its own channel adapter.
    /// </summary>
    /// <param name="targetBlock">The target block that will consume the input</param>
    /// <param name="edge">The edge connecting source to target</param>
    /// <param name="blockOutputs">Dictionary of direct output streams from blocks (for inline edges)</param>
    /// <param name="edgeChannels">Dictionary of typed channel adapters created per edge and target block</param>
    /// <returns>An IAsyncEnumerable that the target block will iterate over as its input</returns>
    private IAsyncEnumerable<object> GetEdgeInput(
        IBlock targetBlock,
        Edge edge,
        Dictionary<IBlock, IAsyncEnumerable<object>> blockOutputs,
        Dictionary<Edge, Dictionary<IBlock, TypedChannelAdapter>> edgeChannels)
    {
        if (edge.BufferMode == BufferMode.None)
        {
            // Inline - connect directly to source block's output
            if (!blockOutputs.ContainsKey(edge.SourceBlock))
            {
                throw new InvalidOperationException(
                    $"Source block '{edge.SourceBlock.Name}' output not available for inline edge");
            }
            return blockOutputs[edge.SourceBlock];
        }
        else
        {
            // Buffered - read from typed channel adapter for this target block
            if (!edgeChannels.ContainsKey(edge))
            {
                throw new InvalidOperationException($"Channels not found for edge: {edge}");
            }
            
            var channels = edgeChannels[edge];
            if (!channels.ContainsKey(targetBlock))
            {
                throw new InvalidOperationException(
                    $"Channel not found for target block '{targetBlock.Name}' in edge: {edge}");
            }
            
            return channels[targetBlock].ReadAllAsync();
        }
    }

    /// <summary>
    /// Routes output from a block to all its outgoing edges using edge strategies.
    /// Each edge strategy determines how items are delivered to target blocks:
    /// - BroadcastEdgeStrategy: writes to all target channels (each gets all items)
    /// - CompetingEdgeStrategy: writes to shared channel (targets compete for items)
    /// Uses typed channel adapters to eliminate boxing overhead.
    /// </summary>
    private async IAsyncEnumerable<object> RouteOutput(
        IBlock sourceBlock,
        IAsyncEnumerable<object> output,
        List<Edge> outgoingEdges,
        Dictionary<Edge, Dictionary<IBlock, TypedChannelAdapter>> edgeChannels,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in output.WithCancellation(cancellationToken))
        {
            // Route to all outgoing edges using their strategies with typed channel adapters
            foreach (var edge in outgoingEdges)
            {
                if (edgeChannels.TryGetValue(edge, out var channels))
                {
                    await edge.Strategy.RouteItemAsync(item, channels, cancellationToken);
                }
            }

            yield return item; // Yield for potential observability or chaining of terminal blocks
        }
    }

    private static async IAsyncEnumerable<object> EmptyAsyncEnumerable()
    {
        await Task.CompletedTask;
        yield break;
    }

    private static async IAsyncEnumerable<T> MergeAsyncEnumerables<T>(List<IAsyncEnumerable<T>> sources)
    {
        // Simple merge - interleaves items from multiple sources
        // Note: More sophisticated merge strategies could be implemented
        var enumerators = sources.Select(s => s.GetAsyncEnumerator()).ToList();
        try
        {
            while (true)
            {
                var hasAny = false;
                foreach (var enumerator in enumerators)
                {
                    if (await enumerator.MoveNextAsync())
                    {
                        hasAny = true;
                        yield return enumerator.Current;
                    }
                }
                if (!hasAny) break;
            }
        }
        finally
        {
            foreach (var enumerator in enumerators)
            {
                await enumerator.DisposeAsync();
            }
        }
    }
}
