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
    private readonly List<BufferNode> _bufferNodes = new();
    private readonly List<Edge> _edges = new();
    private readonly Dictionary<IBlock, List<Edge>> _outgoingEdges = new();
    private readonly Dictionary<IBlock, List<Edge>> _incomingEdges = new();
    private readonly Dictionary<BufferNode, List<IBlock>> _bufferProducers = new();
    private readonly Dictionary<BufferNode, List<IBlock>> _bufferConsumers = new();

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
    /// All buffer nodes in the graph.
    /// </summary>
    public IReadOnlyList<BufferNode> BufferNodes => _bufferNodes;

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
    /// Add a buffer node to the graph.
    /// </summary>
    public void AddBufferNode(BufferNode bufferNode)
    {
        if (_bufferNodes.Contains(bufferNode))
        {
            throw new ArgumentException($"Buffer node already exists in graph");
        }
        _bufferNodes.Add(bufferNode);
        _logger.LogDebug("Added buffer node: {BufferName}", bufferNode.GetName());
    }

    /// <summary>
    /// Add a connection from a block to a buffer node.
    /// </summary>
    public void AddBlockToBufferConnection(IBlock source, BufferNode target)
    {
        if (!_blocks.Contains(source))
        {
            throw new ArgumentException($"Source block '{source.Name}' not found in graph");
        }
        if (!_bufferNodes.Contains(target))
        {
            throw new ArgumentException($"Target buffer node '{target.GetName()}' not found in graph");
        }

        if (!_bufferProducers.ContainsKey(target))
        {
            _bufferProducers[target] = new List<IBlock>();
        }
        _bufferProducers[target].Add(source);
        _logger.LogDebug("Added connection: {SourceBlock} -> BufferNode({BufferName})", source.Name, target.Name);
    }

    /// <summary>
    /// Add a connection from a buffer node to a block.
    /// </summary>
    public void AddBufferToBlockConnection(BufferNode source, IBlock target)
    {
        if (!_bufferNodes.Contains(source))
        {
            throw new ArgumentException($"Source buffer node '{source.GetName()}' not found in graph");
        }
        if (!_blocks.Contains(target))
        {
            throw new ArgumentException($"Target block '{target.Name}' not found in graph");
        }

        if (!_bufferConsumers.ContainsKey(source))
        {
            _bufferConsumers[source] = new List<IBlock>();
        }
        _bufferConsumers[source].Add(target);
        _logger.LogDebug("Added connection: BufferNode({BufferName}) -> {TargetBlock}", source.Name, target.Name);
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

        // Create typed channels for all buffer nodes
        foreach (var bufferNode in _bufferNodes)
        {
            // Determine if single reader/writer optimization can be applied
            var producerCount = _bufferProducers.ContainsKey(bufferNode) ? _bufferProducers[bufferNode].Count : 0;
            var consumerCount = _bufferConsumers.ContainsKey(bufferNode) ? _bufferConsumers[bufferNode].Count : 0;
            
            var singleWriter = producerCount <= 1;
            var singleReader = consumerCount <= 1;

            var (writer, reader) = TypedChannelFactory.CreateTypedChannel(
                bufferNode.DataType,
                BufferMode.Bounded,
                bufferNode.Capacity,
                singleReader,
                singleWriter);

            var bufferModel = new BufferRuntimeModel
            {
                Writer = writer,
                Reader = reader
            };
            pipeline.BufferRuntimeModels[bufferNode] = bufferModel;
            
            _logger.LogDebug("Created typed channel for buffer node: {BufferName} (type={DataType}, capacity={Capacity}, producers={Producers}, consumers={Consumers})",
                bufferNode.GetName(), bufferNode.DataType.Name, bufferNode.Capacity, producerCount, consumerCount);
        }

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
            var blockModel = new BlockRuntimeModel(block, _outgoingEdges, _incomingEdges, _bufferProducers, _bufferConsumers, pipeline)
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
        public Dictionary<BufferNode, BufferRuntimeModel> BufferRuntimeModels { get; } = new();

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
        /// Gets the typed input stream for a block based on its incoming edges and buffer nodes.
        /// </summary>
        internal object GetBlockInputStream(
            IBlock block,
            Dictionary<IBlock, List<Edge>> incomingEdges,
            Dictionary<BufferNode, List<IBlock>> bufferConsumers,
            Type inputItemType)
        {
            var inputs = new List<object>();

            // Add inputs from edges and buffers
            AddInputsFromEdges(block, incomingEdges, inputItemType, inputs);
            AddInputsFromBufferNodes(block, bufferConsumers, inputItemType, inputs);

            // Return appropriate stream based on input count
            return MergeInputStreams(inputs, inputItemType);
        }

        /// <summary>
        /// Adds input streams from incoming edges to the inputs list.
        /// </summary>
        private void AddInputsFromEdges(
            IBlock block,
            Dictionary<IBlock, List<Edge>> incomingEdges,
            Type inputItemType,
            List<object> inputs)
        {
            if (incomingEdges.ContainsKey(block) && incomingEdges[block].Count > 0)
            {
                inputs.AddRange(incomingEdges[block].Select(edge => GetTypedEdgeInput(block, edge, inputItemType)));
            }
        }

        /// <summary>
        /// Adds input streams from buffer nodes to the inputs list.
        /// </summary>
        private void AddInputsFromBufferNodes(
            IBlock block,
            Dictionary<BufferNode, List<IBlock>> bufferConsumers,
            Type inputItemType,
            List<object> inputs)
        {
            var bufferNodesToRead = bufferConsumers
                .Where(kvp => kvp.Value.Contains(block))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var bufferNode in bufferNodesToRead)
            {
                if (BufferRuntimeModels.TryGetValue(bufferNode, out var bufferModel))
                {
                    var typedStream = ReflectionHelper.GetTypedStreamFromChannelReader(bufferModel.Reader, inputItemType, CancellationToken.None);
                    inputs.Add(typedStream);
                }
            }
        }

        /// <summary>
        /// Merges input streams based on count: empty, single, or multiple.
        /// </summary>
        private object MergeInputStreams(List<object> inputs, Type inputItemType)
        {
            if (inputs.Count == 0)
            {
                // Source block - no input, provide empty typed stream
                return ReflectionHelper.CreateEmptyTypedStream(inputItemType);
            }
            else if (inputs.Count == 1)
            {
                return inputs[0];
            }
            else
            {
                // Multiple inputs - merge them
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
        /// Completes all outgoing channel writers for a block, including buffer nodes.
        /// </summary>
        internal void CompleteOutgoingChannels(
            IBlock block,
            Dictionary<IBlock, List<Edge>> outgoingEdges,
            Dictionary<BufferNode, List<IBlock>> bufferProducers,
            ILogger<DataFlowGraph> logger,
            Exception? exception = null)
        {
            // Complete regular edge channels
            if (outgoingEdges.ContainsKey(block))
            {
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

            // Complete buffer node channels
            var bufferNodesToComplete = bufferProducers
                .Where(kvp => kvp.Value.Contains(block))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var bufferNode in bufferNodesToComplete)
            {
                if (BufferRuntimeModels.TryGetValue(bufferNode, out var bufferModel))
                {
                    bool shouldComplete = false;
                    
                    lock (bufferModel.CompletionLock)
                    {
                        bufferModel.CompletedProducers.Add(block);
                        
                        // Complete only when all producers have finished
                        var totalProducers = bufferProducers[bufferNode].Count;
                        var completedProducers = bufferModel.CompletedProducers.Count;
                        shouldComplete = completedProducers == totalProducers;
                    }
                    
                    if (shouldComplete)
                    {
                        ReflectionHelper.CompleteTypedWriter(bufferModel.Writer, exception);
                        
                        if (exception == null)
                        {
                            logger.LogDebug("Completed channel for buffer node: {BufferName} (all {Count} producers finished)", 
                                bufferNode.GetName(), bufferProducers[bufferNode].Count);
                        }
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
        private readonly Dictionary<BufferNode, List<IBlock>> _bufferProducers;
        private readonly Dictionary<BufferNode, List<IBlock>> _bufferConsumers;
        private readonly ExecutionPipeline _pipeline;

        public IExecutableBlock? Adapter { get; set; }
        public object? Output { get; set; }

        public BlockRuntimeModel(
            IBlock block,
            Dictionary<IBlock, List<Edge>> outgoingEdges,
            Dictionary<IBlock, List<Edge>> incomingEdges,
            Dictionary<BufferNode, List<IBlock>> bufferProducers,
            Dictionary<BufferNode, List<IBlock>> bufferConsumers,
            ExecutionPipeline pipeline)
        {
            _block = block;
            _outgoingEdges = outgoingEdges;
            _incomingEdges = incomingEdges;
            _bufferProducers = bufferProducers;
            _bufferConsumers = bufferConsumers;
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
                var typedInput = _pipeline.GetBlockInputStream(_block, _incomingEdges, _bufferConsumers, adapter.InputItemType);

                // Execute the block using adapter - returns typed stream as object (NO BOXING per item)
                var typedOutput = await adapter.ExecuteUntypedAsync(typedInput, context);
                
                // Store typed output for downstream blocks
                Output = typedOutput;

                // Collect output routers from edges and buffer nodes
                var outputRouters = new List<ITypedEdgeRouter>();

                // Add routers from regular edges
                if (_outgoingEdges.ContainsKey(_block) && _outgoingEdges[_block].Count > 0)
                {
                    var edges = _outgoingEdges[_block];
                    var edgeRouters = edges
                        .Where(e => _pipeline.EdgeRuntimeModels.ContainsKey(e) && _pipeline.EdgeRuntimeModels[e].Router != null)
                        .Select(e => _pipeline.EdgeRuntimeModels[e].Router!)
                        .ToList();
                    outputRouters.AddRange(edgeRouters);
                }

                // Add routers for buffer nodes this block writes to
                var bufferNodesToWrite = _bufferProducers
                    .Where(kvp => kvp.Value.Contains(_block))
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var bufferNode in bufferNodesToWrite)
                {
                    if (_pipeline.BufferRuntimeModels.TryGetValue(bufferNode, out var bufferModel))
                    {
                        var router = TypedBufferNodeRouterFactory.CreateTypedRouter(
                            adapter.OutputItemType,
                            bufferNode,
                            bufferModel.Writer);
                        outputRouters.Add(router);
                    }
                }

                // Route output
                if (outputRouters.Count > 0)
                {
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
                _pipeline.CompleteOutgoingChannels(_block, _outgoingEdges, _bufferProducers, logger);

                logger.LogDebug("Block {BlockName} completed successfully", _block.Name);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Block {BlockName} failed with error", _block.Name);

                // Complete typed channel writers with exception
                _pipeline.CompleteOutgoingChannels(_block, _outgoingEdges, _bufferProducers, logger, ex);
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

    /// <summary>
    /// Runtime model for a buffer node during execution.
    /// Encapsulates the typed channel writer and reader for the buffer.
    /// </summary>
    private class BufferRuntimeModel
    {
        public object Writer { get; set; } = null!;
        public object Reader { get; set; } = null!;
        public HashSet<IBlock> CompletedProducers { get; } = new();
        public object CompletionLock { get; } = new();
    }
}
