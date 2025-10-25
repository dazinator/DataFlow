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
        if (!_blocks.Contains(edge.TargetBlock))
        {
            throw new ArgumentException($"Target block '{edge.TargetBlock.Name}' not found in graph");
        }

        _edges.Add(edge);

        // Track outgoing edges per block
        if (!_outgoingEdges.ContainsKey(edge.SourceBlock))
        {
            _outgoingEdges[edge.SourceBlock] = new List<Edge>();
        }
        _outgoingEdges[edge.SourceBlock].Add(edge);

        // Track incoming edges per block
        if (!_incomingEdges.ContainsKey(edge.TargetBlock))
        {
            _incomingEdges[edge.TargetBlock] = new List<Edge>();
        }
        _incomingEdges[edge.TargetBlock].Add(edge);

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

        // Dictionary to store channel writers for buffered edges
        var edgeChannels = new Dictionary<Edge, (ChannelWriter<object> writer, ChannelReader<object> reader)>();

        // Create channels for all buffered edges
        foreach (var edge in _edges.Where(e => e.BufferMode != BufferMode.None))
        {
            // Only bounded channels are supported
            var channel = Channel.CreateBounded<object>(new BoundedChannelOptions(edge.BufferCapacity)
            {
                SingleReader = false, // Multiple readers for broadcast scenarios
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait // Backpressure
            });

            edgeChannels[edge] = (channel.Writer, channel.Reader);
            _logger.LogDebug("Created bounded channel for edge: {Edge} with capacity {Capacity}", edge, edge.BufferCapacity);
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
                        input = GetEdgeInput(edge, blockOutputs, edgeChannels);
                    }
                    else
                    {
                        // Multiple inputs - merge them
                        var inputs = _incomingEdges[block]
                            .Select(edge => GetEdgeInput(edge, blockOutputs, edgeChannels))
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

                    // Complete all outgoing channels
                    if (_outgoingEdges.ContainsKey(block))
                    {
                        foreach (var edge in _outgoingEdges[block])
                        {
                            if (edgeChannels.TryGetValue(edge, out var channel))
                            {
                                channel.writer.Complete();
                                _logger.LogDebug("Completed channel for edge: {Edge}", edge);
                            }
                        }
                    }

                    _logger.LogDebug("Block {BlockName} completed successfully", block.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Block {BlockName} failed with error", block.Name);

                    // Complete channels with exception
                    if (_outgoingEdges.ContainsKey(block))
                    {
                        foreach (var edge in _outgoingEdges[block])
                        {
                            if (edgeChannels.TryGetValue(edge, out var channel))
                            {
                                channel.writer.Complete(ex);
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

    private IAsyncEnumerable<object> GetEdgeInput(
        Edge edge,
        Dictionary<IBlock, IAsyncEnumerable<object>> blockOutputs,
        Dictionary<Edge, (ChannelWriter<object> writer, ChannelReader<object> reader)> edgeChannels)
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
            // Buffered - read from channel
            if (!edgeChannels.ContainsKey(edge))
            {
                throw new InvalidOperationException($"Channel not found for edge: {edge}");
            }
            return edgeChannels[edge].reader.ReadAllAsync();
        }
    }

    /// <summary>
    /// Routes output from a block to all its outgoing edges.
    /// This method handles the broadcasting mechanism where each item is written to all
    /// downstream edge channels. This enables:
    /// 1. Broadcasting - same item sent to multiple consumers
    /// 2. Routing with filters - item sent to all paths, filters decide what passes through
    /// 
    /// The key insight: RouteFilterBlocks act as filters on broadcast streams,
    /// NOT as competing consumers. Each edge gets its own channel, so all filters
    /// see all items and can independently decide what to pass through.
    /// </summary>
    private async IAsyncEnumerable<object> RouteOutput(
        IBlock sourceBlock,
        IAsyncEnumerable<object> output,
        List<Edge> outgoingEdges,
        Dictionary<Edge, (ChannelWriter<object> writer, ChannelReader<object> reader)> edgeChannels,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in output.WithCancellation(cancellationToken))
        {
            // Write to all outgoing edge channels (broadcast)
            // Each edge has its own channel, so there's no competition between consumers
            foreach (var edge in outgoingEdges)
            {
                if (edgeChannels.TryGetValue(edge, out var channel))
                {
                    await channel.writer.WriteAsync(item, cancellationToken);
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
