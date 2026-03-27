namespace DataFlow.POC.Core;

using System.Diagnostics;
using System.Threading.Channels;
using DataFlow.Blazor.BlockTypes;
using DataFlow.Blazor.Events;
using DataFlow.Blazor.ItemTypes;
using DataFlow.POC.Observability;
using Microsoft.Extensions.DependencyInjection;
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
    private IEpochCoordinator? _epochCoordinator;

    private static readonly ActivitySource ActivitySource = new("DataFlow");

    private static string CreateNewGraphId() => Guid.NewGuid().ToString();

    public DataFlowGraph(string name, ILogger<DataFlowGraph> logger, IDataFlowMetrics? metrics = null)
    {
        Name = name;
        GraphId = CreateNewGraphId();
        _logger = logger;
        _metrics = metrics;
    }

    internal DataFlowGraph(string name, string graphId, ILogger<DataFlowGraph> logger, IDataFlowMetrics? metrics = null)
    {
        Name = name;
        GraphId = graphId ?? throw new ArgumentNullException(nameof(graphId));
        _logger = logger;
        _metrics = metrics;
    }

    /// <summary>
    /// The name of this dataflow graph.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Unique identifier for this graph instance.
    /// Each graph instance has a unique ID for identification and tracking.
    /// </summary>
    public string GraphId { get; }

    /// <summary>
    /// The epoch coordinator for this graph, if configured.
    /// </summary>
    public IEpochCoordinator? EpochCoordinator => _epochCoordinator;

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
    /// Sets the epoch coordinator for the graph.
    /// </summary>
    internal void SetEpochCoordinator(IEpochCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        if (_epochCoordinator != null)
        {
            throw new InvalidOperationException("Epoch coordinator has already been set");
        }
        _epochCoordinator = coordinator;
        _logger.LogDebug("Set epoch coordinator");
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

        // Resolve optional event sink — null if the consumer hasn't registered Uniun.DataFlow.Blazor.Server
        await using var flowScope = context.ScopeFactory?.CreateAsyncScope();
        var eventSink = flowScope?.ServiceProvider.GetService<IFlowEventSink>();

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

            // Emit FlowStartedEvent (includes trigger params if provided)
            if (eventSink is not null)
            {
                await eventSink.AppendAsync(context.InvocationId,
                    new FlowStartedEvent(context.InvocationId, Name, DateTime.UtcNow, context.TriggerParamsJson,
                        context.CorrelationId, context.AttemptNumber));
            }

            try
            {
                // Build execution pipeline (adapters, routers, channels)
                var pipeline = BuildExecutionPipeline();

                // Emit topology event — describes the static graph structure and item type
                // labels before any block starts. Clients use this to render the Items table.
                if (eventSink is not null)
                {
                    var itemTypeStore  = flowScope?.ServiceProvider.GetService<IDataFlowItemTypeStore>();
                    var blockMetaStore = flowScope?.ServiceProvider.GetService<IDataFlowBlockMetadataStore>();

                    var blockDefs = TopologicallySortedBlocks().Select(b =>
                    {
                        var isSource = !_incomingEdges.ContainsKey(b) || _incomingEdges[b].Count == 0;
                        var isSink   = !_outgoingEdges.ContainsKey(b) || _outgoingEdges[b].Count == 0;
                        var blockType = blockMetaStore?.GetTypeLabel(b.Name)
                                        ?? DataFlowBlockTypeNameFormatter.Format(b.GetType());
                        return new BlockDefinition(
                            BlockName:       b.Name,
                            BlockType:       blockType,
                            DisplayName:     blockMetaStore?.GetDisplayName(b.Name),
                            InputItemLabel:  isSource ? null : ResolveItemLabel(itemTypeStore, b.InputType),
                            OutputItemLabel: isSink   ? null : ResolveItemLabel(itemTypeStore, b.OutputType),
                            IsSource:        isSource,
                            IsSink:          isSink);
                    }).ToList();

                    var edgeDefs = _edges
                        .SelectMany(e => e.TargetBlocks.Select(t => new EdgeDefinition(
                            SourceBlock:    e.SourceBlock.Name,
                            TargetBlock:    t.Name,
                            BufferCapacity: e.BufferMode == BufferMode.Bounded ? e.BufferCapacity : null,
                            EdgeType:       e.Strategy.EdgeType.ToString()
                        ))).ToList();

                    await eventSink.AppendAsync(context.InvocationId,
                        new FlowGraphDefinedEvent(blockDefs, edgeDefs, DateTime.UtcNow));
                }
                
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

                // Emit final ChannelStatsEvent for every buffer so the UI reflects the
                // drained state at completion (all blocks have finished, so counts are 0).
                if (eventSink is not null)
                {
                    var finalMonitors = pipeline.EdgeRuntimeModels.Values
                        .SelectMany(e => e.BufferMonitors.Values);
                    foreach (var monitor in finalMonitors)
                    {
                        await eventSink.AppendAsync(context.InvocationId,
                            new ChannelStatsEvent(monitor.SourceBlock, monitor.TargetBlock,
                                monitor.Capacity, monitor.CurrentCount, DateTime.UtcNow));
                    }
                }

                // Emit FlowCompletedEvent (success)
                if (eventSink is not null)
                {
                    await eventSink.AppendAsync(context.InvocationId,
                        new FlowCompletedEvent(context.InvocationId, Success: true, DateTime.UtcNow));
                }
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

                // Emit FlowCompletedEvent (failure)
                if (eventSink is not null)
                {
                    await eventSink.AppendAsync(context.InvocationId,
                        new FlowCompletedEvent(context.InvocationId, Success: false, DateTime.UtcNow, ex.Message));
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
    /// <summary>
    /// Resolves a human-readable label for an item type using the optional store,
    /// falling back to the store's built-in CLR type name formatter.
    /// Returns null when the label is empty (e.g. <c>object</c>).
    /// </summary>
    /// <summary>
    /// Returns blocks in topological (source-to-sink) order using Kahn's algorithm.
    /// Registration order is used as a tiebreaker within each wave so the relative
    /// ordering of parallel branches is stable across runs.
    /// Falls back to appending any remaining blocks if a cycle is detected.
    /// </summary>
    private IEnumerable<IBlock> TopologicallySortedBlocks()
    {
        var registrationIndex = _blocks.Select((b, i) => (b, i)).ToDictionary(x => x.b, x => x.i);

        var inDegree = _blocks.ToDictionary(b => b,
            b => _incomingEdges.TryGetValue(b, out var inc) ? inc.Count : 0);

        // Seed the first wave with source blocks, preserving registration order
        var ready = _blocks
            .Where(b => inDegree[b] == 0)
            .OrderBy(b => registrationIndex[b])
            .ToList();

        var result = new List<IBlock>(_blocks.Count);
        int head = 0;

        while (head < ready.Count)
        {
            var block = ready[head++];
            result.Add(block);

            if (!_outgoingEdges.TryGetValue(block, out var outEdges)) continue;

            var newlyReady = new List<IBlock>();
            foreach (var edge in outEdges)
            {
                foreach (var target in edge.TargetBlocks)
                {
                    if (!inDegree.ContainsKey(target)) continue;
                    if (--inDegree[target] == 0)
                        newlyReady.Add(target);
                }
            }

            // Append this wave in registration order so parallel branches are stable
            newlyReady.Sort((a, b) => registrationIndex[a].CompareTo(registrationIndex[b]));
            ready.AddRange(newlyReady);
        }

        // Safety net: append any unreachable blocks (shouldn't happen in a valid DAG)
        foreach (var b in _blocks.Except(result))
            result.Add(b);

        return result;
    }

    private static string? ResolveItemLabel(IDataFlowItemTypeStore? store, Type type)
    {
        var label = store is not null
            ? store.GetLabel(type)
            : DataFlowTypeNameFormatter.Format(type);
        return label.Length > 0 ? label : null;
    }

    private ExecutionPipeline BuildExecutionPipeline()
    {
        var pipeline = new ExecutionPipeline();

        // Create typed channels for all buffered edges using their strategies
        foreach (var edge in _edges.Where(e => e.BufferMode != BufferMode.None))
        {
            var (writers, readers) = edge.Strategy.CreateTypedChannels(edge.DataType, edge.SourceBlock, edge.TargetBlocks);

            // Build one buffer monitor per (source, target) channel reader.
            // Only bounded edges have meaningful capacity; skip unbounded (None mode won't reach here,
            // but guard anyway with capacity = 0 meaning "unbounded").
            var bufferMonitors = new Dictionary<IBlock, IBufferMonitor>();
            if (edge.BufferMode == BufferMode.Bounded)
            {
                foreach (var (targetBlock, readerObj) in readers)
                {
                    bufferMonitors[targetBlock] = BufferMonitorFactory.Create(
                        edge.SourceBlock.Name,
                        targetBlock.Name,
                        edge.BufferCapacity,
                        edge.DataType,
                        readerObj);
                }
            }

            var edgeModel = new EdgeRuntimeModel
            {
                Writers = writers,
                Readers = readers,
                BufferMonitors = bufferMonitors
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

            // Each block gets its own DI scope so it owns its own DbContext instance.
            // This prevents concurrent blocks from sharing a non-thread-safe DbContext
            // through a shared scoped IFlowEventSink.
            await using var blockScope = context.ScopeFactory?.CreateAsyncScope();
            var eventSink = blockScope?.ServiceProvider.GetService<IFlowEventSink>();

            try
            {
                logger.LogDebug("Block {BlockName} starting execution (Thread: {ThreadId})", _block.Name, Environment.CurrentManagedThreadId);

                // Emit BlockStartedEvent
                if (eventSink is not null)
                {
                    var isSource = !_incomingEdges.ContainsKey(_block) || _incomingEdges[_block].Count == 0;
                    await eventSink.AppendAsync(context.InvocationId,
                        new BlockStartedEvent(_block.Name, _block.GetType().Name, DateTime.UtcNow, IsSource: isSource));
                }

                var adapter = Adapter!;

                // Two shared counters incremented atomically on the hot path:
                //   inputCounter  — items pulled from this block's input channel(s)
                //   outputCounter — items written to this block's output channel(s)
                var inputCounter  = new long[1];
                var outputCounter = new long[1];

                // Get the typed input stream for this block, wrapped with a counting shim
                // so every item pull atomically increments inputCounter.
                // For source blocks GetBlockInputStream returns null; WrapWithInputCounter
                // passes null through unchanged (inputCounter stays 0).
                var typedInput = _pipeline.GetBlockInputStream(_block, _incomingEdges, adapter.InputItemType);
                var countedInput = ReflectionHelper.WrapWithInputCounter(typedInput, adapter.InputItemType, inputCounter);
                logger.LogDebug("Block {BlockName} got input stream", _block.Name);

                // Execute the block using adapter - returns typed stream as object (NO BOXING per item)
                var typedOutput = await adapter.ExecuteUntypedAsync(countedInput, context);

                logger.LogDebug("Block {BlockName} execute completed, starting enumeration", _block.Name);

                // Store typed output for downstream blocks
                Output = typedOutput;

                // Collect output routers and buffer monitors from edges
                var outputRouters = new List<ITypedEdgeRouter>();
                var bufferMonitors = new List<IBufferMonitor>();
                Func<object, List<ITypedEdgeRouter>, CancellationToken, Task<long>>? epochStreamDelegate = null;

                // Add routers from regular edges
                if (_outgoingEdges.ContainsKey(_block) && _outgoingEdges[_block].Count > 0)
                {
                    var edges = _outgoingEdges[_block];
                    var edgeRouters = edges
                        .Where(e => _pipeline.EdgeRuntimeModels.ContainsKey(e) && _pipeline.EdgeRuntimeModels[e].Router != null)
                        .Select(e => _pipeline.EdgeRuntimeModels[e].Router!)
                        .ToList();
                    outputRouters.AddRange(edgeRouters);

                    foreach (var edge in edges.Where(e => _pipeline.EdgeRuntimeModels.ContainsKey(e)))
                        bufferMonitors.AddRange(_pipeline.EdgeRuntimeModels[edge].BufferMonitors.Values);

                    // Check if any edge has a pre-compiled epoch stream routing delegate
                    // All edges for the same block should have the same type, so we take the first non-null delegate
                    epochStreamDelegate = edges
                        .Where(e => _pipeline.EdgeRuntimeModels.ContainsKey(e))
                        .Select(e => _pipeline.EdgeRuntimeModels[e].EpochStreamRoutingDelegate)
                        .FirstOrDefault(d => d != null);
                }

                // Also monitor the depth of this block's *incoming* channels from the consumer side.
                // The source block's timer owns the same monitor objects but stops when the source
                // completes — which means the buffer count would freeze mid-drain if we relied solely
                // on the source's timer.  Including the monitors here ensures the count keeps updating
                // as long as *this* block is still running and consuming.
                if (_incomingEdges.TryGetValue(_block, out var inEdges))
                {
                    foreach (var edge in inEdges.Where(e => _pipeline.EdgeRuntimeModels.ContainsKey(e)))
                    {
                        if (_pipeline.EdgeRuntimeModels[edge].BufferMonitors.TryGetValue(_block, out var inMonitor))
                            bufferMonitors.Add(inMonitor);
                    }
                }

                // Route output with live 500 ms progress reporting.
                // The inputCounter / outputCounter arrays were created above alongside the input wrapping.
                // A PeriodicTimer reads both and emits BlockMetricsEvent ticks while the pump runs.
                Task<long> pumpTask;
                if (outputRouters.Count > 0)
                {
                   logger.LogDebug("Block {BlockName} routing output to {RouterCount} routers", _block.Name, outputRouters.Count);
                    pumpTask = ReflectionHelper.EnumerateAndRouteTypedStreamAsync(
                        typedOutput,
                        adapter.OutputItemType,
                        outputRouters,
                        epochStreamDelegate,
                        outputCounter,
                        context.CancellationToken);
                }
                else
                {
                    logger.LogDebug("Block {BlockName} is terminal, enumerating to completion", _block.Name);
                    pumpTask = ReflectionHelper.EnumerateTypedStreamAsync(
                        typedOutput, adapter.OutputItemType, outputCounter, context.CancellationToken);
                }

                // Run the 500 ms progress timer concurrently with the pump.
                // Linked to context.CancellationToken so it also stops on flow cancellation.
                using var timerCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
                var progressTimerTask = eventSink is not null
                    ? RunBlockProgressTimerAsync(_block.Name, outputCounter, inputCounter, outputRouters, bufferMonitors, eventSink, context.InvocationId, timerCts.Token)
                    : Task.CompletedTask;

                long itemsEmitted;
                try
                {
                    itemsEmitted = await pumpTask;
                    if (outputRouters.Count > 0)
                        logger.LogDebug("Block {BlockName} completed routing output", _block.Name);
                    else
                        logger.LogDebug("Block {BlockName} completed enumeration", _block.Name);
                }
                finally
                {
                    // Always stop the timer when the pump finishes (success or error).
                    timerCts.Cancel();
                    try { await progressTimerTask; } catch (OperationCanceledException) { }
                }

                // Emit the definitive final BlockMetricsEvent with exact pump counts.
                var finalInputCount = Volatile.Read(ref inputCounter[0]);
                if ((itemsEmitted > 0 || finalInputCount > 0) && eventSink is not null)
                {
                    await eventSink.AppendAsync(context.InvocationId,
                        new BlockMetricsEvent(_block.Name, finalInputCount, itemsEmitted, DateTime.UtcNow));
                }

                // Emit definitive final EdgeProgressEvent for each outgoing edge.
                if (eventSink is not null)
                {
                    foreach (var router in outputRouters)
                    {
                        foreach (var targetBlock in router.TargetBlocks)
                        {
                            var edgeCount = router.GetItemsWrittenToTarget(targetBlock);
                            if (edgeCount > 0)
                            {
                                await eventSink.AppendAsync(context.InvocationId,
                                    new EdgeProgressEvent(_block.Name, targetBlock.Name, edgeCount, DateTime.UtcNow));
                            }
                        }
                    }
                }

                // Complete all outgoing typed channels
                _pipeline.CompleteOutgoingChannels(_block, _outgoingEdges, logger);

                logger.LogDebug("Block {BlockName} completed successfully", _block.Name);

                // Emit BlockCompletedEvent (success)
                if (eventSink is not null)
                {
                    await eventSink.AppendAsync(context.InvocationId,
                        new BlockCompletedEvent(_block.Name, Success: true, DateTime.UtcNow));
                }

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

                // Emit BlockCompletedEvent (failure)
                if (eventSink is not null)
                {
                    await eventSink.AppendAsync(context.InvocationId,
                        new BlockCompletedEvent(_block.Name, Success: false, DateTime.UtcNow, ex.Message));
                }

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

        /// <summary>
        /// Fires a BlockMetricsEvent every 500 ms while the pump is running.
        /// Stopped by cancelling <paramref name="cancellationToken"/> once the pump finishes.
        /// Sink errors are swallowed so the timer never crashes the block execution.
        /// </summary>
        private static async Task RunBlockProgressTimerAsync(
            string blockName,
            long[] outputCounter,
            long[] inputCounter,
            List<ITypedEdgeRouter> outputRouters,
            List<IBufferMonitor> bufferMonitors,
            IFlowEventSink eventSink,
            Guid invocationId,
            CancellationToken cancellationToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
            try
            {
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    var now = DateTime.UtcNow;

                    // Block-level metrics: items consumed from input and items written to output.
                    var outputCount = Volatile.Read(ref outputCounter[0]);
                    var inputCount  = Volatile.Read(ref inputCounter[0]);
                    if (outputCount > 0 || inputCount > 0)
                    {
                        try
                        {
                            await eventSink.AppendAsync(invocationId,
                                new BlockMetricsEvent(blockName, inputCount, outputCount, now));
                        }
                        catch { /* sink errors must not crash the flow */ }
                    }

                    // Per-edge progress: how many items reached each specific target.
                    foreach (var router in outputRouters)
                    {
                        foreach (var targetBlock in router.TargetBlocks)
                        {
                            var edgeCount = router.GetItemsWrittenToTarget(targetBlock);
                            if (edgeCount > 0)
                            {
                                try
                                {
                                    await eventSink.AppendAsync(invocationId,
                                        new EdgeProgressEvent(blockName, targetBlock.Name, edgeCount, now));
                                }
                                catch { /* sink errors must not crash the flow */ }
                            }
                        }
                    }

                    // Per-edge buffer depth: how many items are currently queued in each channel.
                    foreach (var monitor in bufferMonitors)
                    {
                        try
                        {
                            await eventSink.AppendAsync(invocationId,
                                new ChannelStatsEvent(monitor.SourceBlock, monitor.TargetBlock,
                                    monitor.Capacity, monitor.CurrentCount, now));
                        }
                        catch { /* sink errors must not crash the flow */ }
                    }
                }
            }
            catch (OperationCanceledException) { }
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
        /// Buffer monitors for each target — one per channel reader.
        /// Polled by the progress timer to emit ChannelStatsEvent ticks.
        /// </summary>
        public Dictionary<IBlock, IBufferMonitor> BufferMonitors { get; set; } = new();

        /// <summary>
        /// Pre-compiled epoch stream routing delegate.
        /// If not null, this edge routes epoch streams and the delegate should be used
        /// instead of generic routing logic. Compiled once at graph build time.
        /// </summary>
        public Func<object, List<ITypedEdgeRouter>, CancellationToken, Task<long>>? EpochStreamRoutingDelegate { get; set; }

        /// <summary>
        /// Pre-compiled container routing delegate for epoch streams.
        /// If not null, this delegate routes individual epoch stream containers to a specific target block,
        /// eliminating dynamic casts and type checks at runtime.
        /// Signature: (router, container, targetBlock, cancellationToken) => Task
        /// </summary>
        public Func<ITypedEdgeRouter, object, IBlock, CancellationToken, Task>? ContainerRoutingDelegate { get; set; }
    }
}
