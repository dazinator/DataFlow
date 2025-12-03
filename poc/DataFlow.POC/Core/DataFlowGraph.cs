namespace DataFlow.POC.Core;

using System.Diagnostics;
using System.Threading.Channels;
using DataFlow.POC.Observability;
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
    private EpochSourceNode? _epochSource;
    private readonly List<EpochProcessorNode> _epochProcessors = new();
    private readonly IDataFlowMetrics? _metrics;

    private static readonly ActivitySource ActivitySource = new("DataFlow");

    public DataFlowGraph(string name, ILogger<DataFlowGraph> logger, IDataFlowMetrics? metrics = null)
    {
        Name = name;
        _logger = logger;
        _metrics = metrics;
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
    /// The epoch source node for the graph, if configured.
    /// </summary>
    public EpochSourceNode? EpochSource => _epochSource;

    /// <summary>
    /// All epoch processor nodes in the graph.
    /// </summary>
    public IReadOnlyList<EpochProcessorNode> EpochProcessors => _epochProcessors;

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
    /// Sets the epoch source node for the graph.
    /// </summary>
    internal void SetEpochSource(EpochSourceNode source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (_epochSource != null)
        {
            throw new InvalidOperationException("Epoch source has already been set");
        }
        _epochSource = source;
        _logger.LogDebug("Set epoch source node");
    }
    
    /// <summary>
    /// Adds an epoch processor node to the graph.
    /// </summary>
    internal void AddEpochProcessor(EpochProcessorNode processor)
    {
        ArgumentNullException.ThrowIfNull(processor);
        _epochProcessors.Add(processor);
        _logger.LogDebug("Added epoch processor node");
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
    /// Gets all outgoing edges from the specified block.
    /// </summary>
    /// <param name="block">The block to query.</param>
    /// <returns>An enumerable of outgoing edges.</returns>
    public IEnumerable<Edge> GetOutgoingEdges(IBlock block)
    {
        if (_outgoingEdges.TryGetValue(block, out var edges))
        {
            return edges;
        }
        return Enumerable.Empty<Edge>();
    }

    /// <summary>
    /// Gets all incoming edges to the specified block.
    /// </summary>
    /// <param name="block">The block to query.</param>
    /// <returns>An enumerable of incoming edges.</returns>
    public IEnumerable<Edge> GetIncomingEdges(IBlock block)
    {
        if (_incomingEdges.TryGetValue(block, out var edges))
        {
            return edges;
        }
        return Enumerable.Empty<Edge>();
    }

    /// <summary>
    /// Execute the dataflow graph.
    /// This wires up all blocks and edges, starts execution, and waits for completion.
    /// Uses ExecutableBlockAdapter for zero-boxing execution.
    /// </summary>
    public async Task ExecuteAsync(IExecutionContext context)
    {
        _logger.LogInformation("Starting execution of dataflow: {FlowName}", Name);

        Stopwatch? stopwatch = null;
        var isSuccessful = false;
        DataFlowMetricsTagsContext? flowMetrics = null;

        // Use metrics from context if available, otherwise use graph's metrics
        var metrics = context.Metrics ?? _metrics;

        // Initialize metrics context if metrics are available
        if (metrics != null)
        {
            flowMetrics = new DataFlowMetricsTagsContext(Name, context.InvocationId, metrics);
            flowMetrics.Started();
        }

        using (var flowActivity = ActivitySource.StartActivity(ActivityNames.FlowExecute))
        {
            if (flowActivity is not null)
            {
                if (metrics != null)
                {
                    flowActivity.AddTags(metrics.GlobalTags);
                }
                flowActivity.AddTag(ActivityNames.TagNames.FlowInvocationId, context.InvocationId);
                flowActivity.AddTag(ActivityNames.TagNames.FlowName, Name);
                flowActivity.DisplayName = $"{ActivityNames.Flow} {Name}";
            }
            else
            {
                stopwatch = Stopwatch.StartNew();
            }

            try
            {
                // Build execution pipeline (adapters, routers, channels)
                var pipeline = BuildExecutionPipeline();
                
                // Set the active channel count provider for metrics
                if (metrics is DataFlowMetrics metricsImpl)
                {
                    metricsImpl.SetActiveChannelCountProvider(() => 
                        pipeline.EdgeRuntimeModels.Count);
                }

                // Collect all tasks to wait for
                var allTasks = new List<Task>();
        
                // Add block execution tasks - pass graph so flow name can be accessed
                var blockExecutionTask = pipeline.ExecuteBlocksAsync(_blocks, _outgoingEdges, _incomingEdges, context, flowActivity, this, _logger);
                allTasks.Add(blockExecutionTask);
        
                // Add epoch processor completion tasks if epochs are configured
                if (_epochProcessors.Count > 0)
                {
                    _logger.LogDebug("Including {ProcessorCount} epoch processor(s) in graph execution", _epochProcessors.Count);
                    foreach (var processor in _epochProcessors)
                    {
                        allTasks.Add(processor.CompletionTask);
                    }
                }

                // Wait for all tasks to complete
                await Task.WhenAll(allTasks);
                
                flowActivity?.SetStatus(ActivityStatusCode.Ok);
                isSuccessful = true;

                _logger.LogInformation("Completed execution of dataflow: {FlowName}", Name);
            }
            catch (Exception ex)
            {
                if (flowActivity is not null)
                {
                    flowActivity.SetStatus(ActivityStatusCode.Error, ex.Message);
                    flowActivity.SetTag(ActivityNames.TagNames.ErrorType, ex.GetType().FullName);
                    
                    if (ex is OperationCanceledException)
                    {
                        flowActivity.SetTag(ActivityNames.TagNames.Cancelled, "true");
                    }
                }
                
                throw;
            }
            finally
            {
                double flowDuration;
                if (flowActivity != null)
                {
                    flowActivity.Stop();
                    flowDuration = flowActivity.Duration.TotalMilliseconds;
                }
                else
                {
                    stopwatch?.Stop();
                    flowDuration = stopwatch?.Elapsed.TotalMilliseconds ?? 0;
                }
                
                flowMetrics?.Completed(flowDuration, isSuccessful);
            }
        }
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
            
            // Check if this edge routes epoch streams and pre-compile the routing delegate
            // This eliminates type checking and reflection during execution for millions of epochs
            edgeModel.EpochStreamRoutingDelegate = ReflectionHelper.CreateEpochStreamRoutingDelegate(edge.DataType);
            
            if (edgeModel.EpochStreamRoutingDelegate != null)
            {
                _logger.LogDebug("Created epoch stream routing delegate for edge: {Edge} with item type {ItemType}", 
                    edge, edge.DataType.GetGenericArguments()[0].Name);
                
                // Also create container routing delegate to eliminate dynamic casts for container routing
                edgeModel.ContainerRoutingDelegate = ReflectionHelper.CreateContainerRoutingDelegate(edge.DataType);
                
                if (edgeModel.ContainerRoutingDelegate != null)
                {
                    _logger.LogDebug("Created container routing delegate for edge: {Edge}", edge);
                }
            }
            
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
            Activity? flowActivity,
            DataFlowGraph graph,
            ILogger<DataFlowGraph> logger)
        {
            var blockTasks = new List<Task>();
            foreach (var block in blocks)
            {
                var task = StartBlockTask(block, outgoingEdges, incomingEdges, context, flowActivity, graph, logger);
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
            Activity? flowActivity,
            DataFlowGraph graph,
            ILogger<DataFlowGraph> logger)
        {
            var blockModel = BlockRuntimeModels[block];
            return Task.Run(async () =>
            {
                await blockModel.ExecuteAsync(context, flowActivity, graph, logger);
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
            var inputs = new List<object>();

            // Add inputs from edges
            AddInputsFromEdges(block, incomingEdges, inputItemType, inputs);

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
        /// Completes all outgoing channel writers for a block.
        /// </summary>
        internal void CompleteOutgoingChannels(
            IBlock block,
            Dictionary<IBlock, List<Edge>> outgoingEdges,
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
        }
    }

    /// <summary>
    /// Runtime model for a block during execution.
    /// Encapsulates block adapter, output stream, and execution logic.
    /// </summary>
    private class BlockRuntimeModel
    {
        private static readonly ActivitySource ActivitySource = new("DataFlow");
        
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
        public async Task ExecuteAsync(IExecutionContext context, Activity? flowActivity, DataFlowGraph graph, ILogger<DataFlowGraph> logger)
        {
            Stopwatch? stopwatch = null;
            var isSuccessful = false;
            BlockMetricsTagsContext? blockMetrics = null;

            // Get metrics from context
            var metrics = context.Metrics;

            // Initialize block metrics context if metrics are available
            if (metrics != null)
            {
                var flowMetrics = new DataFlowMetricsTagsContext(graph.Name, context.InvocationId, metrics);
                blockMetrics = flowMetrics.CreateBlockContext(_block.Name);
                blockMetrics.Started();
            }

            using var activity = ActivitySource.StartActivity(
                ActivityNames.BlockExecute,
                ActivityKind.Internal,
                flowActivity?.Context ?? default);

            if (activity is not null)
            {
                if (metrics != null)
                {
                    activity.AddTags(metrics.GlobalTags);
                }
                activity.AddTag(ActivityNames.TagNames.FlowName, graph.Name);
                activity.AddTag(ActivityNames.TagNames.BlockName, _block.Name);
                activity.DisplayName = $"{ActivityNames.Block} {_block.Name}";
            }
            else
            {
                stopwatch = Stopwatch.StartNew();
            }

            try
            {
                logger.LogDebug("Block {BlockName} starting execution (Thread: {ThreadId})", _block.Name, Environment.CurrentManagedThreadId);
                
                var adapter = Adapter!;
                
                // Get the typed input stream for this block                
                var typedInput = _pipeline.GetBlockInputStream(_block, _incomingEdges, adapter.InputItemType);
                logger.LogDebug("Block {BlockName} got input stream", _block.Name);

                // Execute the block using adapter - returns typed stream as object (NO BOXING per item)
                var typedOutput = await adapter.ExecuteUntypedAsync(typedInput, context);
                
                logger.LogDebug("Block {BlockName} execute completed, starting enumeration", _block.Name);
                
                // Store typed output for downstream blocks
                Output = typedOutput;

                // Collect output routers from edges and buffer nodes
                var outputRouters = new List<ITypedEdgeRouter>();
                Func<object, List<ITypedEdgeRouter>, CancellationToken, Task>? epochStreamDelegate = null;

                // Add routers from regular edges
                if (_outgoingEdges.ContainsKey(_block) && _outgoingEdges[_block].Count > 0)
                {
                    var edges = _outgoingEdges[_block];
                    var edgeRouters = edges
                        .Where(e => _pipeline.EdgeRuntimeModels.ContainsKey(e) && _pipeline.EdgeRuntimeModels[e].Router != null)
                        .Select(e => _pipeline.EdgeRuntimeModels[e].Router!)
                        .ToList();
                    outputRouters.AddRange(edgeRouters);
                    
                    // Check if any edge has a pre-compiled epoch stream routing delegate
                    // All edges for the same block should have the same type, so we take the first non-null delegate
                    epochStreamDelegate = edges
                        .Where(e => _pipeline.EdgeRuntimeModels.ContainsKey(e))
                        .Select(e => _pipeline.EdgeRuntimeModels[e].EpochStreamRoutingDelegate)
                        .FirstOrDefault(d => d != null);
                }

                // Route output
                if (outputRouters.Count > 0)
                {
                   logger.LogDebug("Block {BlockName} routing output to {RouterCount} routers", _block.Name, outputRouters.Count);
                    // Enumerate typed output and route without boxing
                    // Use pre-compiled epoch stream delegate if available (eliminates type checks)
                    await ReflectionHelper.EnumerateAndRouteTypedStreamAsync(
                        typedOutput, 
                        adapter.OutputItemType, 
                        outputRouters,
                        epochStreamDelegate,
                        context.CancellationToken);
                    
                    logger.LogDebug("Block {BlockName} completed routing output", _block.Name);
                }
                else
                {
                    logger.LogDebug("Block {BlockName} is terminal, enumerating to completion", _block.Name);
                    
                    // Terminal block - enumerate output to completion
                    await ReflectionHelper.EnumerateTypedStreamAsync(typedOutput, adapter.OutputItemType, context.CancellationToken);
                    
                    logger.LogDebug("Block {BlockName} completed enumeration", _block.Name);
                }

                // Complete all outgoing typed channels
                _pipeline.CompleteOutgoingChannels(_block, _outgoingEdges, logger);

                logger.LogDebug("Block {BlockName} completed successfully", _block.Name);
                
                activity?.SetStatus(ActivityStatusCode.Ok);
                isSuccessful = true;
            }
            catch (Exception ex)
            {
                if (activity is not null)
                {
                    activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                    activity.SetTag(ActivityNames.TagNames.ErrorType, ex.GetType().FullName);
                    
                    if (ex is OperationCanceledException)
                    {
                        activity.SetTag(ActivityNames.TagNames.Cancelled, "true");
                    }
                }

                logger.LogError(ex, "Block {BlockName} failed with error", _block.Name);

                // Complete typed channel writers with exception
                _pipeline.CompleteOutgoingChannels(_block, _outgoingEdges, logger, ex);
                throw;
            }
            finally
            {
                double blockDuration;
                if (activity != null)
                {
                    activity.Stop();
                    blockDuration = activity.Duration.TotalMilliseconds;
                }
                else
                {
                    stopwatch?.Stop();
                    blockDuration = stopwatch?.Elapsed.TotalMilliseconds ?? 0;
                }
                
                blockMetrics?.Completed(blockDuration, isSuccessful);
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
        
        /// <summary>
        /// Pre-compiled epoch stream routing delegate.
        /// If not null, this edge routes epoch streams and the delegate should be used
        /// instead of generic routing logic. Compiled once at graph build time.
        /// </summary>
        public Func<object, List<ITypedEdgeRouter>, CancellationToken, Task>? EpochStreamRoutingDelegate { get; set; }
        
        /// <summary>
        /// Pre-compiled container routing delegate for epoch streams.
        /// If not null, this delegate routes individual epoch stream containers to a specific target block,
        /// eliminating dynamic casts and type checks at runtime.
        /// Signature: (router, container, targetBlock, cancellationToken) => Task
        /// </summary>
        public Func<ITypedEdgeRouter, object, IBlock, CancellationToken, Task>? ContainerRoutingDelegate { get; set; }
    }
}
