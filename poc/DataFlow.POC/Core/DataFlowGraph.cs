namespace DataFlow.POC.Core;

using System.Threading.Channels;
using Microsoft.Extensions.Logging;

/// <summary>
/// Represents a dataflow graph that orchestrates block execution and edge management.
/// The graph owns topology, wiring, and execution coordination.
/// <para>
/// Architecture: Implements the Layered Responsibility Model for reduced-boxing execution.
/// - Block implementation: Strongly-typed business logic (IBlock&lt;TIn, TOut&gt;)
/// - Graph orchestration: Non-generic coordination using untyped references  
/// - Execution hot path: Cached typed delegates minimizing boxing overhead
/// </para>
/// <para>
/// See TYPED_CHANNEL_PERFORMANCE.md for detailed architecture and performance characteristics.
/// </para>
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
    /// Uses ExecutableBlockAdapter for zero-boxing execution.
    /// </summary>
    public async Task ExecuteAsync(IExecutionContext context)
    {
        _logger.LogInformation("Starting execution of dataflow: {FlowName}", Name);

        // Dictionary to store the typed output streams from each block (as objects)
        var blockOutputs = new Dictionary<IBlock, object>();

        // Dictionary to store typed channel writers and readers per edge per target block
        // Structure: Edge -> (TargetBlock -> Writer/Reader)
        var edgeChannelWriters = new Dictionary<Edge, Dictionary<IBlock, object>>();
        var edgeChannelReaders = new Dictionary<Edge, Dictionary<IBlock, object>>();
        
        // Dictionary to store typed edge routers for efficient routing without boxing
        var edgeRouters = new Dictionary<Edge, ITypedEdgeRouter>();
        
        // Dictionary to store executable block adapters for zero-boxing execution
        var blockAdapters = new Dictionary<IBlock, IExecutableBlock>();

        // Create typed channels for all buffered edges using their strategies
        foreach (var edge in _edges.Where(e => e.BufferMode != BufferMode.None))
        {
            var (writers, readers) = edge.Strategy.CreateTypedChannels(edge.DataType, edge.SourceBlock, edge.TargetBlocks);
            edgeChannelWriters[edge] = writers;
            edgeChannelReaders[edge] = readers;
            
            // Create typed edge router to eliminate boxing during routing
            var router = TypedEdgeRouterFactory.CreateTypedRouter(edge.DataType, edge, writers);
            edgeRouters[edge] = router;
            
            _logger.LogDebug("Created typed channels for edge: {Edge} with strategy {Strategy} and type {DataType}", 
                edge, edge.Strategy.EdgeType, edge.DataType.Name);
        }
        
        // Create executable block adapters for zero-boxing execution (reflection happens once here at build time)
        foreach (var block in _blocks)
        {
            var adapter = ExecutableBlockFactory.CreateAdapter(block);
            blockAdapters[block] = adapter;
            _logger.LogDebug("Created executable adapter for block: {BlockName} with types {InputType} -> {OutputType}",
                block.Name, block.InputType.Name, block.OutputType.Name);
        }

        // Start all blocks
        var blockTasks = new List<Task>();

        foreach (var block in _blocks)
        {
            var task = Task.Run(async () =>
            {
                try
                {
                    var adapter = blockAdapters[block];
                    
                    // Determine input source for this block (as typed stream object)
                    object typedInput;

                    if (!_incomingEdges.ContainsKey(block) || _incomingEdges[block].Count == 0)
                    {
                        // Source block - no input, provide empty typed stream
                        typedInput = CreateEmptyTypedStream(adapter.InputItemType);
                    }
                    else if (_incomingEdges[block].Count == 1)
                    {
                        // Single input edge
                        var edge = _incomingEdges[block][0];
                        typedInput = GetTypedEdgeInput(block, edge, blockOutputs, edgeChannelReaders, adapter.InputItemType);
                    }
                    else
                    {
                        // Multiple inputs - merge them
                        var inputs = _incomingEdges[block]
                            .Select(edge => GetTypedEdgeInput(block, edge, blockOutputs, edgeChannelReaders, adapter.InputItemType))
                            .ToList();
                        typedInput = MergeTypedStreams(inputs, adapter.InputItemType);
                    }

                    // Execute the block using adapter - returns typed stream as object (NO BOXING per item)
                    var typedOutput = await adapter.ExecuteUntypedAsync(typedInput, context);
                    
                    // Store typed output for downstream blocks
                    blockOutputs[block] = typedOutput;

                    // Handle output routing with zero-boxing
                    if (_outgoingEdges.ContainsKey(block) && _outgoingEdges[block].Count > 0)
                    {
                        var outgoingEdges = _outgoingEdges[block];
                        var outputRouters = outgoingEdges
                            .Where(e => edgeRouters.ContainsKey(e))
                            .Select(e => edgeRouters[e])
                            .ToList();
                        
                        // Enumerate typed output and route without boxing
                        await EnumerateAndRouteTypedStreamAsync(
                            typedOutput, 
                            adapter.OutputItemType, 
                            outputRouters, 
                            context.CancellationToken);
                    }
                    else
                    {
                        // Terminal block - enumerate output to completion
                        await EnumerateTypedStreamAsync(typedOutput, adapter.OutputItemType, context.CancellationToken);
                    }

                    // Complete all outgoing typed channels
                    if (_outgoingEdges.ContainsKey(block))
                    {
                        foreach (var edge in _outgoingEdges[block])
                        {
                            if (edgeChannelWriters.TryGetValue(edge, out var writers))
                            {
                                // Deduplicate writers in case of competing edge strategy
                                // where multiple targets share the same channel writer.
                                // 
                                // With CompetingEdgeStrategy, all target blocks receive the same
                                // writer instance (they share one channel for competition).
                                // We use Distinct() to ensure we only call Complete() once per unique writer,
                                // preventing "channel already closed" exceptions.
                                //
                                // Writers are objects, so Distinct() compares by reference equality,
                                // which correctly identifies when multiple dictionary entries point to the same writer.
                                var uniqueWriters = writers.Values
                                    .Distinct()
                                    .ToList();
                                
                                foreach (var writerObj in uniqueWriters)
                                {
                                    CompleteTypedWriter(writerObj);
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

                    // Complete typed channel writers with exception
                    if (_outgoingEdges.ContainsKey(block))
                    {
                        foreach (var edge in _outgoingEdges[block])
                        {
                            if (edgeChannelWriters.TryGetValue(edge, out var writers))
                            {
                                // Deduplicate writers in case of competing edge strategy
                                var uniqueWriters = writers.Values
                                    .Distinct()
                                    .ToList();
                                
                                foreach (var writerObj in uniqueWriters)
                                {
                                    CompleteTypedWriter(writerObj, ex);
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
    
    /// <summary>
    /// Creates an empty typed stream for source blocks with no input.
    /// Uses reflection to create IAsyncEnumerable<T> at runtime.
    /// </summary>
    private object CreateEmptyTypedStream(Type itemType)
    {
        var method = typeof(DataFlowGraph).GetMethod(nameof(CreateEmptyTypedStreamGeneric), 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var genericMethod = method!.MakeGenericMethod(itemType);
        return genericMethod.Invoke(null, null)!;
    }
    
    private static async IAsyncEnumerable<T> CreateEmptyTypedStreamGeneric<T>()
    {
        await Task.CompletedTask;
        yield break;
    }
    
    /// <summary>
    /// Gets typed input stream for a block from an edge.
    /// Returns the stream as object to maintain type information.
    /// </summary>
    private object GetTypedEdgeInput(
        IBlock targetBlock,
        Edge edge,
        Dictionary<IBlock, object> blockOutputs,
        Dictionary<Edge, Dictionary<IBlock, object>> edgeChannelReaders,
        Type itemType)
    {
        if (edge.BufferMode == BufferMode.None)
        {
            // Inline - return direct reference to typed output stream
            if (!blockOutputs.ContainsKey(edge.SourceBlock))
            {
                throw new InvalidOperationException(
                    $"Source block '{edge.SourceBlock.Name}' output not available for inline edge");
            }
            return blockOutputs[edge.SourceBlock];
        }
        else
        {
            // Buffered - get typed reader from channel and call ReadAllAsync
            if (!edgeChannelReaders.ContainsKey(edge))
            {
                throw new InvalidOperationException($"Channels not found for edge: {edge}");
            }
            
            var readers = edgeChannelReaders[edge];
            if (!readers.ContainsKey(targetBlock))
            {
                throw new InvalidOperationException(
                    $"Channel not found for target block '{targetBlock.Name}' in edge: {edge}");
            }
            
            // Get the typed ChannelReader<T> and call its ReadAllAsync method
            var typedReader = readers[targetBlock];
            
            // Use reflection to call ReadAllAsync on the typed reader
            var readerType = typeof(ChannelReader<>).MakeGenericType(itemType);
            var readAllAsyncMethod = readerType.GetMethod("ReadAllAsync");
            if (readAllAsyncMethod == null)
            {
                throw new InvalidOperationException($"Could not find ReadAllAsync method on {readerType.Name}");
            }
            
            // Invoke ReadAllAsync(CancellationToken.None) - returns IAsyncEnumerable<T>
            var typedEnumerable = readAllAsyncMethod.Invoke(typedReader, new object[] { CancellationToken.None });
            if (typedEnumerable == null)
            {
                throw new InvalidOperationException("ReadAllAsync returned null");
            }
            
            return typedEnumerable;
        }
    }
    
    /// <summary>
    /// Merges multiple typed streams into one.
    /// Uses reflection to call generic method at runtime.
    /// </summary>
    private object MergeTypedStreams(List<object> typedStreams, Type itemType)
    {
        var method = typeof(DataFlowGraph).GetMethod(nameof(MergeTypedStreamsGeneric),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var genericMethod = method!.MakeGenericMethod(itemType);
        return genericMethod.Invoke(null, new object[] { typedStreams })!;
    }
    
    private static IAsyncEnumerable<T> MergeTypedStreamsGeneric<T>(List<object> typedStreams)
    {
        var typed = typedStreams.Cast<IAsyncEnumerable<T>>().ToList();
        return MergeAsyncEnumerables(typed);
    }
    
    /// <summary>
    /// Enumerates a typed stream and routes items to typed routers without boxing.
    /// Uses reflection to call generic method at runtime.
    /// </summary>
    private async Task EnumerateAndRouteTypedStreamAsync(
        object typedStream,
        Type itemType,
        List<ITypedEdgeRouter> routers,
        CancellationToken cancellationToken)
    {
        var method = typeof(DataFlowGraph).GetMethod(nameof(EnumerateAndRouteTypedStreamGenericAsync),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var genericMethod = method!.MakeGenericMethod(itemType);
        var task = (Task)genericMethod.Invoke(null, new object[] { typedStream, routers, cancellationToken })!;
        await task;
    }
    
    private static async Task EnumerateAndRouteTypedStreamGenericAsync<T>(
        object typedStream,
        List<ITypedEdgeRouter> routers,
        CancellationToken cancellationToken)
    {
        var stream = (IAsyncEnumerable<T>)typedStream;
        await foreach (var item in stream.WithCancellation(cancellationToken))
        {
            // Route to all routers - using internal typed method when available
            foreach (var router in routers)
            {
                if (router is TypedEdgeRouter<T> typedRouter)
                {
                    // Use internal method for zero-boxing routing
                    await typedRouter.RouteTypedItemAsync(item, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // Fallback: box once and use public interface
                    await router.RouteItemAsync(item!, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
    
    /// <summary>
    /// Enumerates a typed stream to completion (for terminal blocks).
    /// Uses reflection to call generic method at runtime.
    /// </summary>
    private async Task EnumerateTypedStreamAsync(
        object typedStream,
        Type itemType,
        CancellationToken cancellationToken)
    {
        var method = typeof(DataFlowGraph).GetMethod(nameof(EnumerateTypedStreamGenericAsync),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var genericMethod = method!.MakeGenericMethod(itemType);
        var task = (Task)genericMethod.Invoke(null, new object[] { typedStream, cancellationToken })!;
        await task;
    }
    
    private static async Task EnumerateTypedStreamGenericAsync<T>(
        object typedStream,
        CancellationToken cancellationToken)
    {
        var stream = (IAsyncEnumerable<T>)typedStream;
        await foreach (var _ in stream.WithCancellation(cancellationToken))
        {
            // Terminal consumption - items are fully processed
        }
    }
    
    /// <summary>
    /// Completes a typed channel writer using reflection.
    /// </summary>
    private void CompleteTypedWriter(object writerObj, Exception? exception = null)
    {
        // Get the Complete method: ChannelWriter<T>.Complete(Exception? exception)
        var completeMethod = writerObj.GetType().GetMethod(nameof(ChannelWriter<int>.Complete), new[] { typeof(Exception) });
        if (completeMethod == null)
        {
            throw new InvalidOperationException($"Could not find Complete method on {writerObj.GetType().Name}");
        }
        
        completeMethod.Invoke(writerObj, new object?[] { exception });
    }
}
