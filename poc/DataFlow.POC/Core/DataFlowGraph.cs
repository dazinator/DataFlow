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

        // Build execution pipeline (adapters, routers, channels)
        var pipeline = BuildExecutionPipeline();

        // Execute all blocks via the pipeline
        await pipeline.ExecuteBlocksAsync(_blocks, _outgoingEdges, _incomingEdges, context, _logger);

        _logger.LogInformation("Completed execution of dataflow: {FlowName}", Name);
    }

    /// <summary>
    /// Builds the execution pipeline by creating typed channels, edge routers, and block adapters.
    /// This method performs all reflection-based setup once at build time to enable zero-boxing execution.
    /// </summary>
    /// <returns>An ExecutionPipeline containing all the infrastructure needed for execution.</returns>
    private ExecutionPipeline BuildExecutionPipeline()
    {
        var pipeline = new ExecutionPipeline();

        // Create typed channels for all buffered edges using their strategies
        foreach (var edge in _edges.Where(e => e.BufferMode != BufferMode.None))
        {
            var (writers, readers) = edge.Strategy.CreateTypedChannels(edge.DataType, edge.SourceBlock, edge.TargetBlocks);
            var edgeModel = new EdgeRuntimeModel
            {
                Writers = writers,
                Readers = readers
            };
            
            // Create typed edge router to eliminate boxing during routing
            edgeModel.Router = TypedEdgeRouterFactory.CreateTypedRouter(edge.DataType, edge, writers);
            
            pipeline.EdgeRuntimeModels[edge] = edgeModel;
            
            _logger.LogDebug("Created typed channels for edge: {Edge} with strategy {Strategy} and type {DataType}", 
                edge, edge.Strategy.EdgeType, edge.DataType.Name);
        }
        
        // Create executable block adapters for zero-boxing execution (reflection happens once here at build time)
        foreach (var block in _blocks)
        {
            var adapter = ExecutableBlockFactory.CreateAdapter(block);
            var blockModel = new BlockRuntimeModel(block, _outgoingEdges, _incomingEdges, pipeline)
            {
                Adapter = adapter
            };
            pipeline.BlockRuntimeModels[block] = blockModel;
            _logger.LogDebug("Created executable adapter for block: {BlockName} with types {InputType} -> {OutputType}",
                block.Name, block.InputType.Name, block.OutputType.Name);
        }

        return pipeline;
    }

    /// <summary>
    /// Helper class to hold all execution pipeline infrastructure.
    /// Responsible for orchestrating block execution with proper task scheduling.
    /// </summary>
    private class ExecutionPipeline
    {
        public Dictionary<IBlock, BlockRuntimeModel> BlockRuntimeModels { get; } = new();
        public Dictionary<Edge, EdgeRuntimeModel> EdgeRuntimeModels { get; } = new();

        /// <summary>
        /// Executes all blocks in the pipeline concurrently.
        /// Handles task scheduling and coordination.
        /// </summary>
        public async Task ExecuteBlocksAsync(
            IReadOnlyList<IBlock> blocks,
            Dictionary<IBlock, List<Edge>> outgoingEdges,
            Dictionary<IBlock, List<Edge>> incomingEdges,
            IExecutionContext context,
            ILogger<DataFlowGraph> logger)
        {
            var blockTasks = new List<Task>();
            foreach (var block in blocks)
            {
                var task = StartBlockTask(block, outgoingEdges, incomingEdges, context, logger);
                blockTasks.Add(task);
            }

            await Task.WhenAll(blockTasks);
        }

        /// <summary>
        /// Starts a task for executing a block.
        /// Encapsulates the task scheduling strategy, allowing for future customization
        /// such as custom schedulers or prioritization based on block position in the DAG.
        /// </summary>
        private Task StartBlockTask(
            IBlock block,
            Dictionary<IBlock, List<Edge>> outgoingEdges,
            Dictionary<IBlock, List<Edge>> incomingEdges,
            IExecutionContext context,
            ILogger<DataFlowGraph> logger)
        {
            var blockModel = BlockRuntimeModels[block];
            return Task.Run(async () =>
            {
                await blockModel.ExecuteAsync(context, logger);
            }, context.CancellationToken);
        }

        /// <summary>
        /// Gets the typed input stream for a block based on its incoming edges.
        /// </summary>
        internal object GetBlockInputStream(
            IBlock block,
            Dictionary<IBlock, List<Edge>> incomingEdges,
            Type inputItemType)
        {
            if (!incomingEdges.ContainsKey(block) || incomingEdges[block].Count == 0)
            {
                // Source block - no input, provide empty typed stream
                return ReflectionHelper.CreateEmptyTypedStream(inputItemType);
            }
            else if (incomingEdges[block].Count == 1)
            {
                // Single input edge
                var edge = incomingEdges[block][0];
                return GetTypedEdgeInput(block, edge, inputItemType);
            }
            else
            {
                // Multiple inputs - merge them
                var inputs = incomingEdges[block]
                    .Select(edge => GetTypedEdgeInput(block, edge, inputItemType))
                    .ToList();
                return ReflectionHelper.MergeTypedStreams(inputs, inputItemType);
            }
        }

        /// <summary>
        /// Gets typed input stream for a block from an edge.
        /// </summary>
        private object GetTypedEdgeInput(IBlock targetBlock, Edge edge, Type itemType)
        {
            if (edge.BufferMode == BufferMode.None)
            {
                // Inline - return direct reference to typed output stream
                if (!BlockRuntimeModels.ContainsKey(edge.SourceBlock))
                {
                    throw new InvalidOperationException(
                        $"Source block '{edge.SourceBlock.Name}' output not available for inline edge");
                }
                return BlockRuntimeModels[edge.SourceBlock].Output!;
            }
            else
            {
                // Buffered - get typed reader from channel and call ReadAllAsync
                if (!EdgeRuntimeModels.ContainsKey(edge))
                {
                    throw new InvalidOperationException($"Channels not found for edge: {edge}");
                }
                
                var edgeModel = EdgeRuntimeModels[edge];
                if (!edgeModel.Readers.ContainsKey(targetBlock))
                {
                    throw new InvalidOperationException(
                        $"Channel not found for target block '{targetBlock.Name}' in edge: {edge}");
                }
                
                // Get the typed ChannelReader<T> and call its ReadAllAsync method using ReflectionHelper
                var typedReader = edgeModel.Readers[targetBlock];
                return ReflectionHelper.GetTypedStreamFromChannelReader(typedReader, itemType, CancellationToken.None);
            }
        }

        /// <summary>
        /// Completes all outgoing channel writers for a block.
        /// </summary>
        internal void CompleteOutgoingChannels(
            IBlock block,
            Dictionary<IBlock, List<Edge>> outgoingEdges,
            ILogger<DataFlowGraph> logger,
            Exception? exception = null)
        {
            if (!outgoingEdges.ContainsKey(block))
            {
                return;
            }

            foreach (var edge in outgoingEdges[block])
            {
                if (EdgeRuntimeModels.TryGetValue(edge, out var edgeModel))
                {
                    // Deduplicate writers in case of competing edge strategy
                    var uniqueWriters = edgeModel.Writers.Values.Distinct().ToList();
                    
                    foreach (var writerObj in uniqueWriters)
                    {
                        ReflectionHelper.CompleteTypedWriter(writerObj, exception);
                    }
                    
                    if (exception == null)
                    {
                        logger.LogDebug("Completed channels for edge: {Edge}", edge);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Runtime model for a block during execution.
    /// Encapsulates block adapter, output stream, and execution logic.
    /// </summary>
    private class BlockRuntimeModel
    {
        private readonly IBlock _block;
        private readonly Dictionary<IBlock, List<Edge>> _outgoingEdges;
        private readonly Dictionary<IBlock, List<Edge>> _incomingEdges;
        private readonly ExecutionPipeline _pipeline;

        public IExecutableBlock? Adapter { get; set; }
        public object? Output { get; set; }

        public BlockRuntimeModel(
            IBlock block,
            Dictionary<IBlock, List<Edge>> outgoingEdges,
            Dictionary<IBlock, List<Edge>> incomingEdges,
            ExecutionPipeline pipeline)
        {
            _block = block;
            _outgoingEdges = outgoingEdges;
            _incomingEdges = incomingEdges;
            _pipeline = pipeline;
        }

        /// <summary>
        /// Executes the block with routing of its output to downstream blocks.
        /// Handles block execution, output routing, and channel completion for both success and error cases.
        /// </summary>
        public async Task ExecuteAsync(IExecutionContext context, ILogger<DataFlowGraph> logger)
        {
            try
            {
                var adapter = Adapter!;
                
                // Get the typed input stream for this block
                var typedInput = _pipeline.GetBlockInputStream(_block, _incomingEdges, adapter.InputItemType);

                // Execute the block using adapter - returns typed stream as object (NO BOXING per item)
                var typedOutput = await adapter.ExecuteUntypedAsync(typedInput, context);
                
                // Store typed output for downstream blocks
                Output = typedOutput;

                // Handle output routing with zero-boxing
                if (_outgoingEdges.ContainsKey(_block) && _outgoingEdges[_block].Count > 0)
                {
                    var edges = _outgoingEdges[_block];
                    var outputRouters = edges
                        .Where(e => _pipeline.EdgeRuntimeModels.ContainsKey(e) && _pipeline.EdgeRuntimeModels[e].Router != null)
                        .Select(e => _pipeline.EdgeRuntimeModels[e].Router!)
                        .ToList();
                    
                    // Enumerate typed output and route without boxing
                    await ReflectionHelper.EnumerateAndRouteTypedStreamAsync(
                        typedOutput, 
                        adapter.OutputItemType, 
                        outputRouters, 
                        context.CancellationToken);
                }
                else
                {
                    // Terminal block - enumerate output to completion
                    await ReflectionHelper.EnumerateTypedStreamAsync(typedOutput, adapter.OutputItemType, context.CancellationToken);
                }

                // Complete all outgoing typed channels
                _pipeline.CompleteOutgoingChannels(_block, _outgoingEdges, logger);

                logger.LogDebug("Block {BlockName} completed successfully", _block.Name);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Block {BlockName} failed with error", _block.Name);

                // Complete typed channel writers with exception
                _pipeline.CompleteOutgoingChannels(_block, _outgoingEdges, logger, ex);
                throw;
            }
        }
    }

    /// <summary>
    /// Runtime model for an edge during execution.
    /// Encapsulates channel writers, readers, and routers for an edge.
    /// </summary>
    private class EdgeRuntimeModel
    {
        public Dictionary<IBlock, object> Writers { get; set; } = new();
        public Dictionary<IBlock, object> Readers { get; set; } = new();
        public ITypedEdgeRouter? Router { get; set; }
    }
}
