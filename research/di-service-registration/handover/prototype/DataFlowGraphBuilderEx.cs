namespace DataFlow.POC.Builder;

using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Extended builder for constructing dataflow graphs with dependency injection support.
/// Supports both direct block instances and blocks registered in the service collection.
/// </summary>
public class DataFlowGraphBuilderEx
{
    private readonly string _name;
    private readonly ILogger<DataFlowGraph> _logger;
    private readonly IServiceProvider? _serviceProvider;
    private readonly List<IBlock> _blocks = new();
    private readonly List<BufferNode> _bufferNodes = new();
    private readonly List<Edge> _edges = new();
    private readonly List<(IBlock source, BufferNode target)> _blockToBufferConnections = new();
    private readonly List<(BufferNode source, IBlock target)> _bufferToBlockConnections = new();
    private EpochSourceNode? _epochSource;
    private readonly List<EpochProcessorNode> _epochProcessors = new();
    private readonly Dictionary<string, IBlock> _blocksByName = new();

    /// <summary>
    /// Create a new graph builder.
    /// </summary>
    /// <param name="name">Name of the graph</param>
    /// <param name="serviceProvider">Optional service provider for resolving registered blocks</param>
    /// <param name="logger">Optional logger</param>
    public DataFlowGraphBuilderEx(
        string name, 
        IServiceProvider? serviceProvider = null,
        ILogger<DataFlowGraph>? logger = null)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _serviceProvider = serviceProvider;
        _logger = logger ?? NullLogger<DataFlowGraph>.Instance;
    }

    /// <summary>
    /// Add a block instance to the graph.
    /// This is the traditional approach - directly providing a block instance.
    /// </summary>
    public DataFlowGraphBuilderEx AddBlock(IBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        _blocks.Add(block);
        _blocksByName[block.Name] = block;
        return this;
    }

    /// <summary>
    /// Use a block that was registered with the service collection by name.
    /// The block will be resolved from DI using its registered key.
    /// </summary>
    /// <param name="name">The name/key used when registering the block</param>
    /// <returns>This builder for chaining</returns>
    /// <exception cref="InvalidOperationException">If no service provider was provided or block not found</exception>
    public DataFlowGraphBuilderEx UseBlock(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Block name cannot be null or whitespace", nameof(name));

        if (_serviceProvider == null)
            throw new InvalidOperationException(
                "Cannot use UseBlock() without a service provider. " +
                "Pass an IServiceProvider to the constructor or use AddBlock() instead.");

        // Resolve the block from DI by its registered key
        var block = _serviceProvider.GetKeyedService<IBlock>(name);
        if (block == null)
        {
            throw new InvalidOperationException(
                $"Block '{name}' not found in service collection. " +
                "Make sure it was registered using AddDataFlows().");
        }

        _blocks.Add(block);
        _blocksByName[name] = block;
        return this;
    }

    /// <summary>
    /// Get a block by name from those already added to this builder.
    /// </summary>
    private IBlock GetBlockByName(string name)
    {
        if (_blocksByName.TryGetValue(name, out var block))
            return block;

        throw new InvalidOperationException(
            $"Block '{name}' not found. Use AddBlock() or UseBlock() to add it first.");
    }

    /// <summary>
    /// Automatically connect the last added block to the previous block.
    /// </summary>
    public DataFlowGraphBuilderEx AutoConnect(int bufferCapacity = 100)
    {
        if (_blocks.Count < 2)
        {
            throw new InvalidOperationException(
                $"AutoConnect requires at least 2 blocks to have been added, but only {_blocks.Count} block(s) exist");
        }

        var source = _blocks[^2];
        var target = _blocks[^1];
        
        return Connect(source, target, BufferMode.Bounded, bufferCapacity);
    }

    /// <summary>
    /// Connect two blocks by instance.
    /// </summary>
    public DataFlowGraphBuilderEx Connect(
        IBlock source,
        IBlock target,
        int bufferCapacity = 100)
    {
        return Connect(source, target, BufferMode.Bounded, bufferCapacity);
    }

    /// <summary>
    /// Connect two blocks by name.
    /// Blocks must have been added via AddBlock() or UseBlock().
    /// </summary>
    public DataFlowGraphBuilderEx Connect(
        string sourceName,
        string targetName,
        int bufferCapacity = 100)
    {
        var source = GetBlockByName(sourceName);
        var target = GetBlockByName(targetName);
        
        return Connect(source, target, BufferMode.Bounded, bufferCapacity);
    }

    /// <summary>
    /// Connect two blocks with an edge (internal method with buffer mode).
    /// </summary>
    private DataFlowGraphBuilderEx Connect(
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
    public DataFlowGraphBuilderEx AddEdge(Edge edge)
    {
        _edges.Add(edge);
        return this;
    }

    /// <summary>
    /// Connect a router block to multiple target blocks using route-based filtering.
    /// </summary>
    public DataFlowGraphBuilderEx ConnectRouted(
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
    /// </summary>
    public DataFlowGraphBuilderEx ConnectMany(
        IBlock source,
        params IBlock[] targets)
    {
        return ConnectMany(source, 100, targets);
    }

    /// <summary>
    /// Connect a source block to multiple target blocks with specified buffer capacity.
    /// </summary>
    public DataFlowGraphBuilderEx ConnectMany(
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
    /// </summary>
    public DataFlowGraphBuilderEx ConnectCompeting(
        IBlock source,
        IReadOnlyList<IBlock> targets,
        int bufferCapacity = 100)
    {
        var edge = new Edge(source, targets, new CompetingEdgeStrategy(BufferMode.Bounded, bufferCapacity));
        _edges.Add(edge);
        return this;
    }

    /// <summary>
    /// Create a new buffer node with the specified data type and capacity.
    /// </summary>
    public BufferNode Buffer(Type dataType, int capacity = 100, string? name = null)
    {
        var buffer = new BufferNode(dataType, capacity, name);
        _bufferNodes.Add(buffer);
        return buffer;
    }

    /// <summary>
    /// Create a new buffer node with the specified generic type and capacity.
    /// </summary>
    public BufferNode<T> Buffer<T>(int capacity = 100, string? name = null)
    {
        var buffer = new BufferNode<T>(capacity, name);
        _bufferNodes.Add(buffer);
        return buffer;
    }

    /// <summary>
    /// Connect a block to a buffer node.
    /// </summary>
    public DataFlowGraphBuilderEx Connect(IBlock source, BufferNode target)
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
    /// </summary>
    public DataFlowGraphBuilderEx Connect(BufferNode source, IBlock target)
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

        foreach (var (source, target) in _blockToBufferConnections)
        {
            graph.AddBlockToBufferConnection(source, target);
        }

        foreach (var (source, target) in _bufferToBlockConnections)
        {
            graph.AddBufferToBlockConnection(source, target);
        }
        
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
