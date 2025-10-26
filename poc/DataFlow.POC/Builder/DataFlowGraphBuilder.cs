namespace DataFlow.POC.Builder;

using DataFlow.POC.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Builder for constructing dataflow graphs with a fluent API.
/// </summary>
public class DataFlowGraphBuilder
{
    private readonly string _name;
    private readonly ILogger<DataFlowGraph> _logger;
    private readonly List<IBlock> _blocks = new();
    private readonly List<BufferNode> _bufferNodes = new();
    private readonly List<Edge> _edges = new();
    private readonly List<(IBlock source, BufferNode target)> _blockToBufferConnections = new();
    private readonly List<(BufferNode source, IBlock target)> _bufferToBlockConnections = new();

    public DataFlowGraphBuilder(string name, ILogger<DataFlowGraph>? logger = null)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _logger = logger ?? NullLogger<DataFlowGraph>.Instance;
    }

    /// <summary>
    /// Add a block to the graph.
    /// </summary>
    public DataFlowGraphBuilder AddBlock(IBlock block)
    {
        _blocks.Add(block);
        return this;
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
    /// Connect two blocks by name.
    /// </summary>
    public DataFlowGraphBuilder Connect(
        string sourceName,
        string targetName,
        int bufferCapacity = 100)
    {
        var source = _blocks.FirstOrDefault(b => b.Name == sourceName)
            ?? throw new ArgumentException($"Source block '{sourceName}' not found");
        var target = _blocks.FirstOrDefault(b => b.Name == targetName)
            ?? throw new ArgumentException($"Target block '{targetName}' not found");

        return Connect(source, target, BufferMode.Bounded, bufferCapacity);
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

        return graph;
    }
}
