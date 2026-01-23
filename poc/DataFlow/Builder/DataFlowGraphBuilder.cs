namespace DataFlow.POC.Builder;

using DataFlow.POC.Core;
using DataFlow.POC.Registry;
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
    private readonly IServiceProvider? _serviceProvider;
    private readonly IBlockTypeRegistry? _registry;
    private readonly string _namespace;
    private readonly string _graphId = Guid.NewGuid().ToString(); // Generated once at builder creation
    private readonly List<IBlock> _blocks = new();
    private readonly Dictionary<string, IBlock> _blocksByName = new(); // Track blocks by their registration name
    private readonly List<Edge> _edges = new();
    private readonly HashSet<IBlock> _simpleConnectedSources = new(); // Track sources connected via simple Connect/ConnectBroadcast/ConnectCompeting
    private EpochSourceNode? _epochSource;
    private readonly List<EpochProcessorNode> _epochProcessors = new();
    private IEpochCoordinator? _epochCoordinator;

    /// <summary>
    /// Legacy constructor for inline graph building.
    /// Prefer using the constructor with IServiceProvider for DI-based graph building.
    /// </summary>
    [Obsolete("Use the constructor with IServiceProvider for DI-based graph building via services.AddDataFlows(). This constructor will be removed in a future version.")]
    public DataFlowGraphBuilder(string name, ILogger<DataFlowGraph>? logger = null)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _logger = logger ?? NullLogger<DataFlowGraph>.Instance;
        _serviceProvider = null;
        _registry = null;
        _namespace = "global";
    }

    /// <summary>
    /// Create a new graph builder with service provider support for DI block resolution.
    /// </summary>
    /// <param name="name">Name of the graph</param>
    /// <param name="serviceProvider">Service provider for resolving registered blocks</param>
    /// <param name="registry">Block type registry for block resolution and metadata</param>
    /// <param name="namespacePrefix">Optional namespace prefix for block resolution (defaults to "global")</param>
    /// <param name="logger">Optional logger</param>
    public DataFlowGraphBuilder(
        string name, 
        IServiceProvider serviceProvider,
        IBlockTypeRegistry registry,
        string? namespacePrefix = null,
        ILogger<DataFlowGraph>? logger = null)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _namespace = namespacePrefix ?? "global";
        _logger = logger ?? NullLogger<DataFlowGraph>.Instance;
    }

    /// <summary>
    /// Gets the unique identifier for the graph being built.
    /// Each graph instance has a unique ID for identification and tracking.
    /// </summary>
    public string GraphId => _graphId;

    /// <summary>
    /// Gets the service provider for DI resolution.
    /// Used internally by extension methods to resolve dependencies.
    /// </summary>
    internal IServiceProvider? GetServiceProvider() => _serviceProvider;

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
    /// The block will be resolved from the service provider using the provided name.
    /// </summary>
    /// <param name="name">The name of the block to resolve from DI</param>
    /// <returns>The builder for chaining</returns>
    /// <exception cref="InvalidOperationException">If no service provider was provided or block not found</exception>
    public DataFlowGraphBuilder UseBlock(string name)
    {
        if (_serviceProvider is null)
        {
            throw new InvalidOperationException(
                "Cannot use UseBlock() without a service provider. " +
                "Either pass a service provider to the DataFlowGraphBuilder constructor, " +
                "or use AddBlock() to add blocks directly.");
        }

        if (_registry is null)
        {
            throw new InvalidOperationException(
                "Cannot use UseBlock() without a block registry. " +
                "Ensure the registry is passed to the DataFlowGraphBuilder constructor.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Block name cannot be null or whitespace", nameof(name));
        }

        // Resolve the key with namespace prefix if needed
        var key = ResolveBlockKey(name);

        // Resolve block from registry
        var block = _registry.GetBlock(_serviceProvider, key);

        _blocks.Add(block);
        // Track by resolved key - for DI blocks, this will match block.Name
        // since SetContext is called with the keyed service key during DI resolution.
        // This maintains consistency with AddBlock() which tracks by block.Name.
        _blocksByName[key] = block;
        return this;
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
    /// Connect two blocks with an edge (internal method with buffer mode).
    /// </summary>
    private DataFlowGraphBuilder Connect(
        IBlock source,
        IBlock target,
        BufferMode bufferMode,
        int bufferCapacity)
    {
        // Enforce single-connection-per-source rule
        if (_simpleConnectedSources.Contains(source))
        {
            throw new InvalidOperationException(
                $"Source block '{source.Name}' has already been connected. " +
                $"Each source can only be connected once. Use ConnectBroadcast() or ConnectCompeting() to connect to multiple targets.");
        }

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
        // Enforce single-connection-per-source rule
        if (_simpleConnectedSources.Contains(source))
        {
            throw new InvalidOperationException(
                $"Source block '{source.Name}' has already been connected. " +
                $"Each source can only be connected once.");
        }

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
        // Enforce single-connection-per-source rule
        if (_simpleConnectedSources.Contains(source))
        {
            throw new InvalidOperationException(
                $"Source block '{source.Name}' has already been connected. " +
                $"Each source can only be connected once.");
        }

        var edge = new Edge(source, targets, new CompetingEdgeStrategy(BufferMode.Bounded, bufferCapacity));
        _edges.Add(edge);
        _simpleConnectedSources.Add(source);
        return this;
    }

    /// <summary>
    /// Connect two blocks by name.
    /// </summary>
    public DataFlowGraphBuilder Connect(
        string sourceName,
        string targetName,
        int bufferCapacity = 100)
    {
        var source = FindBlockByName(sourceName, "Source");
        var target = FindBlockByName(targetName, "Target");
        return Connect(source, target, BufferMode.Bounded, bufferCapacity);
    }

    /// <summary>
    /// Connect a source block to multiple target blocks by name with competing consumer semantics.
    /// Each item from the source will be delivered to exactly one target (competing consumers).
    /// This is useful for load balancing across multiple parallel workers.
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
        var source = FindBlockByName(sourceName, "Source");
        var targets = targetNames.Select(name => FindBlockByName(name, "Target")).ToList();
        
        if (targets.Count == 0)
        {
            throw new ArgumentException("At least one target block is required", nameof(targetNames));
        }
        
        return ConnectCompeting(source, targets, bufferCapacity);
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
            throw new ArgumentException($"{blockRole} block '{name}' not found");
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
    /// Sets the epoch coordinator for the graph (internal use by ConfigureEpochs).
    /// </summary>
    internal void SetEpochCoordinator(IEpochCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        if (_epochCoordinator != null)
        {
            throw new InvalidOperationException("Epoch coordinator has already been configured");
        }
        _epochCoordinator = coordinator;
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
    /// Build the dataflow graph.
    /// </summary>
    public DataFlowGraph Build()
    {
        var graph = new DataFlowGraph(_name, _graphId, _logger);

        foreach (var block in _blocks)
        {
            graph.AddBlock(block);
        }

        foreach (var edge in _edges)
        {
            graph.AddEdge(edge);
        }
        
        // Add epoch nodes if configured
        if (_epochSource != null)
        {
            graph.SetEpochSource(_epochSource);
            foreach (var processor in _epochProcessors)
            {
                graph.AddEpochProcessor(processor);
            }
        }
        
        // Set epoch coordinator if configured
        if (_epochCoordinator != null)
        {
            graph.SetEpochCoordinator(_epochCoordinator);
        }

        return graph;
    }
}
