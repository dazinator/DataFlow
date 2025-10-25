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
    private readonly List<Edge> _edges = new();

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
    /// Build the dataflow graph.
    /// </summary>
    public DataFlowGraph Build()
    {
        var graph = new DataFlowGraph(_name, _logger);

        foreach (var block in _blocks)
        {
            graph.AddBlock(block);
        }

        foreach (var edge in _edges)
        {
            graph.AddEdge(edge);
        }

        return graph;
    }
}
