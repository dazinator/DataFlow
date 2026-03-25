namespace DataFlow.POC.Builder;

using DataFlow.POC.Core;
using DataFlow.POC.Registry;
using DataFlow.POC.Checkpointing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Builder for constructing dataflow graphs with a fluent API.
/// </summary>
public class DataFlowGraphBuilder
{
    private readonly string _name;
    private readonly ILogger<DataFlowGraph> _logger;
    private readonly string _namespace;
    private readonly string _graphId = Guid.NewGuid().ToString(); // Generated once at builder creation
    private readonly List<IBlock> _blocks = new(); // Blocks added via AddBlock()
    private readonly List<string> _pendingBlockNames = new(); // Block names to resolve via UseBlock()
    private readonly Dictionary<string, IBlock> _blocksByName = new(); // Track blocks by their registration name
    private readonly List<Edge> _edges = new();
    private readonly List<(string sourceName, string targetName, int bufferCapacity)> _pendingConnections = new(); // Connections to resolve during Build()
    private readonly List<(string sourceName, IEnumerable<string> targetNames, int bufferCapacity)> _pendingCompetingConnections = new(); // Competing connections to resolve during Build()
    private readonly HashSet<IBlock> _simpleConnectedSources = new(); // Track sources connected via simple Connect/ConnectBroadcast/ConnectCompeting
    private EpochSourceNode? _epochSource;
    private readonly List<EpochProcessorNode> _epochProcessors = new();
    private EpochConfiguration? _epochConfig; // Store epoch configuration for deferred coordinator creation
    private Func<ICheckpointStrategy?, IEpochCoordinator>? _epochCoordinatorFactory;

    /// <summary>
    /// Create a new graph builder.
    /// Use Build(IServiceProvider, IBlockTypeRegistry) to resolve blocks and build the graph.
    /// </summary>
    /// <param name="name">Name of the graph</param>
    /// <param name="namespacePrefix">Optional namespace prefix for block resolution (defaults to "global")</param>
    /// <param name="logger">Optional logger</param>
    public DataFlowGraphBuilder(
        string name,
        string? namespacePrefix = null,
        ILogger<DataFlowGraph>? logger = null)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _namespace = namespacePrefix ?? "global";
        _logger = logger ?? NullLogger<DataFlowGraph>.Instance;
    }

    /// <summary>
    /// Gets the unique identifier for the graph being built.
    /// Each graph instance has a unique ID for identification and tracking.
    /// </summary>
    public string GraphId => _graphId;



    /// <summary>
    /// Add a block to the graph.
    /// </summary>
    public DataFlowGraphBuilder AddBlock(IBlock block)
    {
        _blocks.Add(block);
        // Also track by the block's Name property for Connect lookups
        _blocksByName[block.Name] = block;
        return this;
    }

    /// <summary>
    /// Use a block registered with dependency injection.
    /// The block will be resolved from the service provider during Build().
    /// <para>
    /// Note: calling <c>UseBlock()</c> is optional for any block that also appears in a
    /// <see cref="Connect(string,string,int)"/>, <see cref="ConnectCompeting(string,IEnumerable{string},int)"/>,
    /// or <see cref="ConnectFanIn(IEnumerable{string},string,int)"/> call — those methods
    /// auto-register the blocks they reference.  <c>UseBlock()</c> is still required for
    /// blocks that are part of the graph but not referenced in any connection.
    /// </para>
    /// </summary>
    /// <param name="name">The name of the block to resolve from DI</param>
    /// <returns>The builder for chaining</returns>
    public DataFlowGraphBuilder UseBlock(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Block name cannot be null or whitespace", nameof(name));
        }

        // Store the name for later resolution in Build()
        _pendingBlockNames.Add(name);
        return this;
    }

    /// <summary>
    /// Queues a block name for DI resolution during Build(), but only if it has not already
    /// been registered directly (via <see cref="AddBlock"/>) or queued for resolution.
    /// This prevents duplicate entries when the same name appears in multiple Connect calls
    /// or when it was already added via <see cref="UseBlock"/>.
    /// </summary>
    private void EnsurePendingBlock(string name)
    {
        // Skip if already registered as a direct block instance
        var key = ResolveBlockKey(name);
        if (_blocksByName.ContainsKey(name) || _blocksByName.ContainsKey(key))
        {
            return;
        }

        // Skip if already queued for DI resolution (check both short name and resolved key)
        if (_pendingBlockNames.Contains(name) || _pendingBlockNames.Contains(key))
        {
            return;
        }

        _pendingBlockNames.Add(name);
    }

    /// <summary>
    /// Resolves the full key for a block by applying namespace prefix if needed.
    /// If the name already contains a colon (:), it's treated as a fully-qualified key.
    /// Otherwise, the current namespace prefix is applied.
    /// </summary>
    private string ResolveBlockKey(string name)
    {
        // If name contains ':', treat it as fully-qualified (e.g., "global:producer" or "moduleA:transformer")
        if (name.Contains(':'))
        {
            return name;
        }
        
        // Apply current namespace prefix
        return $"{_namespace}:{name}";
    }

    /// <summary>
    /// Automatically connect the last added block to the previous block.
    /// This provides a more refactor-friendly API when blocks are added sequentially.
    /// </summary>
    /// <param name="bufferCapacity">The buffer capacity for the edge (default: 100)</param>
    /// <returns>The builder for chaining</returns>
    /// <exception cref="InvalidOperationException">If there are fewer than 2 blocks</exception>
    public DataFlowGraphBuilder AutoConnect(int bufferCapacity = 100)
    {
        if (_blocks.Count < 2)
        {
            throw new InvalidOperationException($"AutoConnect requires at least 2 blocks to have been added, but only {_blocks.Count} block(s) exist");
        }

        var source = _blocks[^2]; // Second to last block
        var target = _blocks[^1]; // Last block
        
        return Connect(source, target, BufferMode.Bounded, bufferCapacity);
    }

    /// <summary>
    /// Connect two blocks with an edge for single target.
    /// For single targets, the edge strategy defaults to broadcast (most efficient for single target).
    /// To connect to multiple targets, use ConnectBroadcast() or ConnectCompeting().
    /// Note: A source can only be connected once. Subsequent connections will throw an exception.
    /// </summary>
    public DataFlowGraphBuilder Connect(
        IBlock source,
        IBlock target,
        int bufferCapacity = 100)
    {
        return Connect(source, target, BufferMode.Bounded, bufferCapacity);
    }

    /// <summary>
    /// Validates that the source block has not already been connected.
    /// Enforces the single-connection-per-source rule.
    /// </summary>
    private void ValidateSourceNotAlreadyConnected(IBlock source)
    {
        if (_simpleConnectedSources.Contains(source))
        {
            throw new InvalidOperationException(
                $"Source block '{source.Name}' has already been connected. " +
                $"Each source can only be connected once. Use ConnectBroadcast() or ConnectCompeting() to connect to multiple targets.");
        }
    }

    /// <summary>
    /// Connect two blocks with an edge (internal method with buffer mode).
    /// </summary>
    private DataFlowGraphBuilder Connect(
        IBlock source,
        IBlock target,
        BufferMode bufferMode,
        int bufferCapacity)
    {
        ValidateSourceNotAlreadyConnected(source);

        var edge = new Edge(source, target, bufferMode, bufferCapacity);
        _edges.Add(edge);
        _simpleConnectedSources.Add(source);
        return this;
    }

    /// <summary>
    /// Add a pre-configured edge to the graph.
    /// This allows using custom edge strategies like CompetingEdgeStrategy or CloningEdgeStrategy.
    /// Note: This is for advanced scenarios. For simple connections, use Connect(), ConnectBroadcast(), or ConnectCompeting().
    /// </summary>
    public DataFlowGraphBuilder AddEdge(Edge edge)
    {
        // AddEdge is for advanced scenarios and doesn't enforce single-connection rule
        // This allows multiple edges from same source with different strategies (e.g., metrics + routing)
        _edges.Add(edge);
        return this;
    }

    /// <summary>
    /// Connect a source block to multiple target blocks with broadcast semantics.
    /// All targets will receive all items from the source.
    /// Creates a single edge with BroadcastEdgeStrategy.
    /// Note: A source can only be connected once.
    /// </summary>
    /// <param name="source">The source block</param>
    /// <param name="targets">The target blocks that will all receive all items</param>
    /// <param name="bufferCapacity">The buffer capacity for the edge (default: 100)</param>
    /// <param name="cloneFunc">Optional function to clone items for mutation isolation</param>
    /// <returns>The builder for chaining</returns>
    public DataFlowGraphBuilder ConnectBroadcast(
        IBlock source,
        IReadOnlyList<IBlock> targets,
        int bufferCapacity = 100,
        Func<object, object>? cloneFunc = null)
    {
        ValidateSourceNotAlreadyConnected(source);

        var strategy = cloneFunc != null
            ? new BroadcastEdgeStrategy(cloneFunc, BufferMode.Bounded, bufferCapacity)
            : new BroadcastEdgeStrategy(BufferMode.Bounded, bufferCapacity);

        var edge = new Edge(source, targets, strategy);
        _edges.Add(edge);
        _simpleConnectedSources.Add(source);
        return this;
    }

    /// <summary>
    /// Connect a source block to a single target with broadcast semantics.
    /// For single targets, this is equivalent to Connect() but more explicit.
    /// Note: A source can only be connected once.
    /// </summary>
    /// <param name="source">The source block</param>
    /// <param name="target">The target block</param>
    /// <param name="bufferCapacity">The buffer capacity for the edge (default: 100)</param>
    /// <param name="cloneFunc">Optional function to clone items for mutation isolation</param>
    /// <returns>The builder for chaining</returns>
    public DataFlowGraphBuilder ConnectBroadcast(
        IBlock source,
        IBlock target,
        int bufferCapacity = 100,
        Func<object, object>? cloneFunc = null)
    {
        return ConnectBroadcast(source, new[] { target }, bufferCapacity, cloneFunc);
    }

    /// <summary>
    /// Connect a source block to multiple target blocks with competing consumer semantics.
    /// Each item from the source will be delivered to exactly one target (competing consumers).
    /// This is useful for load balancing across multiple parallel workers.
    /// Creates a single edge with CompetingEdgeStrategy.
    /// Note: A source can only be connected once.
    /// </summary>
    /// <param name="source">The source block</param>
    /// <param name="targets">The target blocks that will compete for items</param>
    /// <param name="bufferCapacity">The buffer capacity for the edge (default: 100)</param>
    /// <returns>The builder for chaining</returns>
    public DataFlowGraphBuilder ConnectCompeting(
        IBlock source,
        IReadOnlyList<IBlock> targets,
        int bufferCapacity = 100)
    {
        ValidateSourceNotAlreadyConnected(source);

        var edge = new Edge(source, targets, new CompetingEdgeStrategy(BufferMode.Bounded, bufferCapacity));
        _edges.Add(edge);
        _simpleConnectedSources.Add(source);
        return this;
    }

    /// <summary>
    /// Connect two blocks by name.
    /// The blocks do not need to be pre-registered via <see cref="UseBlock"/> — any block name
    /// that appears in a <c>Connect</c> call is automatically queued for DI resolution.
    /// Blocks already registered directly (via <see cref="AddBlock"/>) are not re-resolved.
    /// </summary>
    public DataFlowGraphBuilder Connect(
        string sourceName,
        string targetName,
        int bufferCapacity = 100)
    {
        EnsurePendingBlock(sourceName);
        EnsurePendingBlock(targetName);
        // Store the pending connection - it will be resolved during Build()
        _pendingConnections.Add((sourceName, targetName, bufferCapacity));
        return this;
    }

    /// <summary>
    /// Connect a source block to multiple target blocks by name with competing consumer semantics.
    /// Each item from the source will be delivered to exactly one target (competing consumers).
    /// This is useful for load balancing across multiple parallel workers.
    /// <para>
    /// The blocks do not need to be pre-registered via <see cref="UseBlock"/> — all names
    /// that appear here are automatically queued for DI resolution.
    /// </para>
    /// </summary>
    /// <param name="sourceName">The name of the source block</param>
    /// <param name="targetNames">The names of the target blocks that will compete for items</param>
    /// <param name="bufferCapacity">The buffer capacity for the edge (default: 100)</param>
    /// <returns>The builder for chaining</returns>
    public DataFlowGraphBuilder ConnectCompeting(
        string sourceName,
        IEnumerable<string> targetNames,
        int bufferCapacity = 100)
    {
        var targetList = targetNames.ToList();

        EnsurePendingBlock(sourceName);
        foreach (var name in targetList)
        {
            EnsurePendingBlock(name);
        }

        // Defer resolution to Build() so that blocks added via UseBlock() are available.
        // This matches the behavior of Connect(string, string) which also defers resolution.
        _pendingCompetingConnections.Add((sourceName, targetList, bufferCapacity));
        return this;
    }

    /// <summary>
    /// Connect multiple source blocks to a single target block (fan-in).
    /// Each source produces items that are all consumed by the shared target.
    /// This is equivalent to calling <see cref="Connect(string,string,int)"/> for each source,
    /// but makes the fan-in intent explicit and readable.
    /// <para>
    /// The blocks do not need to be pre-registered via <see cref="UseBlock"/> — all names
    /// that appear here are automatically queued for DI resolution.
    /// </para>
    /// </summary>
    /// <param name="sourceNames">Names of the source blocks</param>
    /// <param name="targetName">Name of the target (fan-in) block</param>
    /// <param name="bufferCapacity">Buffer capacity per edge (default: 100)</param>
    /// <returns>The builder for chaining</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sourceNames"/> is null</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="targetName"/> is null or whitespace, or when no source names are provided</exception>
    public DataFlowGraphBuilder ConnectFanIn(
        IEnumerable<string> sourceNames,
        string targetName,
        int bufferCapacity = 100)
    {
        ArgumentNullException.ThrowIfNull(sourceNames);
        if (string.IsNullOrWhiteSpace(targetName))
        {
            throw new ArgumentException("Target block name cannot be null or whitespace", nameof(targetName));
        }

        var sourceList = sourceNames.ToList();
        if (sourceList.Count == 0)
        {
            throw new ArgumentException("At least one source block name must be provided", nameof(sourceNames));
        }

        EnsurePendingBlock(targetName);

        foreach (var sourceName in sourceList)
        {
            if (string.IsNullOrWhiteSpace(sourceName))
            {
                throw new ArgumentException("A source block name cannot be null or whitespace", nameof(sourceNames));
            }

            EnsurePendingBlock(sourceName);
            _pendingConnections.Add((sourceName, targetName, bufferCapacity));
        }

        return this;
    }

    /// <summary>
    /// Find a block by name, trying both the original name and the resolved key.
    /// </summary>
    private IBlock FindBlockByName(string name, string blockRole)
    {
        // First try the name as-is (for blocks added directly or when name matches exactly)
        var block = _blocksByName.GetValueOrDefault(name);
        
        if (block is null)
        {
            // Try with resolved key (adds namespace prefix if needed)
            var key = ResolveBlockKey(name);
            block = _blocksByName.GetValueOrDefault(key);
        }
        
        if (block is null)
        {
            var key = ResolveBlockKey(name);
            throw new ArgumentException(
                $"{blockRole} block '{name}' not found. " +
                $"Block was either not added to the builder, or if using UseBlock(), " +
                $"make sure the block is registered with AddDataFlows(). " +
                $"Tried names: '{name}' and '{key}'.",
                nameof(name));
        }

        return block;
    }


    /// <summary>
    /// Sets the epoch source node for the graph (internal use by ConfigureEpochs).
    /// </summary>
    internal void SetEpochSource(EpochSourceNode source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (_epochSource != null)
        {
            throw new InvalidOperationException("Epoch source has already been configured");
        }
        _epochSource = source;
    }
    
    /// <summary>
    /// Sets the epoch configuration for the graph (internal use by ConfigureEpochs).
    /// The coordinator will be created during Build() when the service provider is available.
    /// </summary>
    /// <param name="config">The epoch configuration containing policy, processors, and hooks</param>
    /// <param name="coordinatorFactory">Optional factory for creating the coordinator. If null, a default factory will be used.</param>
    /// <exception cref="ArgumentNullException">Thrown when config is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when epoch configuration has already been set</exception>
    internal void SetEpochConfiguration(
        EpochConfiguration config,
        Func<ICheckpointStrategy?, IEpochCoordinator>? coordinatorFactory)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (_epochConfig != null)
        {
            throw new InvalidOperationException("Epoch configuration has already been set");
        }
        _epochConfig = config;
        _epochCoordinatorFactory = coordinatorFactory;
    }
    
    /// <summary>
    /// Adds an epoch processor node to the graph (internal use by ConfigureEpochs).
    /// </summary>
    internal void AddEpochProcessor(EpochProcessorNode processor)
    {
        ArgumentNullException.ThrowIfNull(processor);
        _epochProcessors.Add(processor);
    }
    
    /// <summary>
    /// Build the dataflow graph with the provided service provider and registry.
    /// Blocks added via UseBlock() will be resolved from the registry using the service provider.
    /// Epoch coordinator will be created if epochs were configured.
    /// </summary>
    /// <param name="serviceProvider">Service provider for resolving blocks and creating epoch coordinator</param>
    /// <param name="registry">Block type registry for block resolution</param>
    /// <returns>The constructed dataflow graph</returns>
    public DataFlowGraph Build(IServiceProvider serviceProvider, IBlockTypeRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(registry);
        
        var graph = new DataFlowGraph($"{_namespace}:{_name}", _graphId, _logger);

        // Add blocks that were added directly via AddBlock()
        foreach (var block in _blocks)
        {
            graph.AddBlock(block);
        }
        
        // Resolve and add blocks that were added via UseBlock()
        foreach (var blockName in _pendingBlockNames)
        {
            // Resolve the key with namespace prefix if needed
            var key = ResolveBlockKey(blockName);
            
            // Resolve block from registry
            var block = registry.GetBlock(serviceProvider, key);
            
            graph.AddBlock(block);
            // Track by resolved key for Connect() lookups
            _blocksByName[key] = block;
        }

        // Resolve and add pending connections (from Connect() calls with string names)
        foreach (var (sourceName, targetName, bufferCapacity) in _pendingConnections)
        {
            var source = FindBlockByName(sourceName, "Source");
            var target = FindBlockByName(targetName, "Target");
            
            ValidateSourceNotAlreadyConnected(source);
            
            var edge = new Edge(source, target, BufferMode.Bounded, bufferCapacity);
            graph.AddEdge(edge);
            _simpleConnectedSources.Add(source);
        }

        // Resolve and add pending competing connections (from ConnectCompeting() calls with string names)
        foreach (var (sourceName, targetNames, bufferCapacity) in _pendingCompetingConnections)
        {
            var source = FindBlockByName(sourceName, "Source");
            var targets = targetNames.Select(name => FindBlockByName(name, "Target")).ToList();

            if (targets.Count == 0)
            {
                throw new ArgumentException($"ConnectCompeting: no target blocks specified for source '{sourceName}'.");
            }

            ValidateSourceNotAlreadyConnected(source);

            var edge = new Edge(source, targets, new CompetingEdgeStrategy(BufferMode.Bounded, bufferCapacity));
            graph.AddEdge(edge);
            _simpleConnectedSources.Add(source);
        }

        // Add edges that were added directly via AddEdge() or Connect(IBlock, IBlock)
        foreach (var edge in _edges)
        {
            graph.AddEdge(edge);
        }
        
        // Create and set epoch coordinator if epochs were configured
        if (_epochConfig != null)
        {
            // Create coordinator factory if not provided
            var factory = _epochCoordinatorFactory ?? CreateDefaultCoordinatorFactory(serviceProvider);
            
            // Create coordinator
            var coordinator = factory(_epochConfig.CheckpointStrategy);
            
            // Set coordinator on graph
            graph.SetEpochCoordinator(coordinator);
            
            // Add epoch nodes if configured
            if (_epochSource != null)
            {
                graph.SetEpochSource(_epochSource);
                foreach (var processor in _epochProcessors)
                {
                    graph.AddEpochProcessor(processor);
                }
            }
        }

        return graph;
    }
    
    /// <summary>
    /// Creates a default coordinator factory that uses IServiceScopeFactory from the service provider.
    /// </summary>
    private static Func<ICheckpointStrategy?, IEpochCoordinator> CreateDefaultCoordinatorFactory(IServiceProvider serviceProvider)
    {
        return checkpointStrategy =>
        {
            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
            return new EpochCoordinator(scopeFactory, checkpointStrategy: checkpointStrategy);
        };
    }
}
