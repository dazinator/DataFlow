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
    private readonly List<IBlock> _blocks = new();
    private readonly Dictionary<string, IBlock> _blocksByName = new(); // Track blocks by their registration name
    private readonly List<BufferNode> _bufferNodes = new();
    private readonly List<Edge> _edges = new();
    private readonly List<(IBlock source, BufferNode target)> _blockToBufferConnections = new();
    private readonly List<(BufferNode source, IBlock target)> _bufferToBlockConnections = new();
    private EpochSourceNode? _epochSource;
    private readonly List<EpochProcessorNode> _epochProcessors = new();

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
    /// Connect two blocks with an edge.
    /// Note: Only bounded buffer mode is supported to ensure memory constraints are considered.
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
        var edge = new Edge(source, target, bufferMode, bufferCapacity);
        _edges.Add(edge);
        return this;
    }

    /// <summary>
    /// Add a pre-configured edge to the graph.
    /// This allows using custom edge strategies like CompetingEdgeStrategy or CloningEdgeStrategy.
    /// </summary>
    public DataFlowGraphBuilder AddEdge(Edge edge)
    {
        _edges.Add(edge);
        return this;
    }

    /// <summary>
    /// Connect a router block to multiple target blocks using route-based filtering.
    /// Each target block is associated with a route key and will only receive items matching that key.
    /// This is more efficient than broadcasting to all targets with filter blocks.
    /// </summary>
    /// <param name="routerBlock">The router block producing RoutedItem<T></param>
    /// <param name="routeKeyToBlock">Mapping of route keys to target blocks</param>
    /// <param name="bufferCapacity">The buffer capacity for the channels</param>
    /// <returns>The builder for chaining</returns>
    public DataFlowGraphBuilder ConnectRouted(
        IBlock routerBlock,
        Dictionary<string, IBlock> routeKeyToBlock,
        int bufferCapacity)
    {
        var strategy = new RoutedItemEdgeStrategy(routeKeyToBlock, BufferMode.Bounded, bufferCapacity);
        var edge = new Edge(routerBlock, routeKeyToBlock.Values.ToList(), strategy);
        _edges.Add(edge);
        return this;
    }

    /// <summary>
    /// Connect a source block to multiple target blocks.
    /// Useful for broadcasting or routing scenarios.
    /// </summary>
    public DataFlowGraphBuilder ConnectMany(
        IBlock source,
        params IBlock[] targets)
    {
        return ConnectMany(source, 100, targets);
    }

    /// <summary>
    /// Connect a source block to multiple target blocks with specified buffer capacity.
    /// </summary>
    public DataFlowGraphBuilder ConnectMany(
        IBlock source,
        int bufferCapacity,
        params IBlock[] targets)
    {
        foreach (var target in targets)
        {
            Connect(source, target, BufferMode.Bounded, bufferCapacity);
        }
        return this;
    }

    /// <summary>
    /// Connect a source block to multiple target blocks with competing consumer semantics.
    /// Each item from the source will be delivered to exactly one target (competing consumers).
    /// This is useful for load balancing across multiple parallel workers.
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
        var edge = new Edge(source, targets, new CompetingEdgeStrategy(BufferMode.Bounded, bufferCapacity));
        _edges.Add(edge);
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
    /// Create a new buffer node with the specified data type and capacity.
    /// A buffer node serves as a shared channel that multiple producers can write to
    /// and multiple consumers can read from.
    /// </summary>
    /// <param name="dataType">The type of data flowing through the buffer</param>
    /// <param name="capacity">The maximum capacity of the buffer (for backpressure control)</param>
    /// <param name="name">Optional name for debugging and logging purposes</param>
    /// <returns>The created buffer node</returns>
    public BufferNode Buffer(Type dataType, int capacity = 100, string? name = null)
    {
        var buffer = new BufferNode(dataType, capacity, name);
        _bufferNodes.Add(buffer);
        return buffer;
    }

    /// <summary>
    /// Create a new buffer node with the specified generic type and capacity.
    /// A buffer node serves as a shared channel that multiple producers can write to
    /// and multiple consumers can read from.
    /// </summary>
    /// <typeparam name="T">The type of data flowing through the buffer</typeparam>
    /// <param name="capacity">The maximum capacity of the buffer (for backpressure control)</param>
    /// <param name="name">Optional name for debugging and logging purposes</param>
    /// <returns>The created buffer node</returns>
    public BufferNode<T> Buffer<T>(int capacity = 100, string? name = null)
    {
        var buffer = new BufferNode<T>(capacity, name);
        _bufferNodes.Add(buffer);
        return buffer;
    }

    /// <summary>
    /// Connect a block to a buffer node.
    /// The block will write its output to the buffer's channel.
    /// </summary>
    public DataFlowGraphBuilder Connect(IBlock source, BufferNode target)
    {
        if (source.OutputType != target.DataType)
        {
            throw new ArgumentException(
                $"Type mismatch: Source block '{source.Name}' output type {source.OutputType.Name} " +
                $"does not match buffer node '{target.GetName()}' data type {target.DataType.Name}");
        }

        _blockToBufferConnections.Add((source, target));
        return this;
    }

    /// <summary>
    /// Connect a buffer node to a block.
    /// The block will read its input from the buffer's channel.
    /// </summary>
    public DataFlowGraphBuilder Connect(BufferNode source, IBlock target)
    {
        if (source.DataType != target.InputType)
        {
            throw new ArgumentException(
                $"Type mismatch: Buffer node '{source.GetName()}' data type {source.DataType.Name} " +
                $"does not match target block '{target.Name}' input type {target.InputType.Name}");
        }

        _bufferToBlockConnections.Add((source, target));
        return this;
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
        var graph = new DataFlowGraph(_name, _logger);

        foreach (var block in _blocks)
        {
            graph.AddBlock(block);
        }

        foreach (var bufferNode in _bufferNodes)
        {
            graph.AddBufferNode(bufferNode);
        }

        foreach (var edge in _edges)
        {
            graph.AddEdge(edge);
        }

        // Add buffer connections
        foreach (var (source, target) in _blockToBufferConnections)
        {
            graph.AddBlockToBufferConnection(source, target);
        }

        foreach (var (source, target) in _bufferToBlockConnections)
        {
            graph.AddBufferToBlockConnection(source, target);
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

        return graph;
    }
}
