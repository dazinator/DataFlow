namespace DataFlow.POC.Core;

using System.Threading.Channels;

/// <summary>
/// Defines the delivery semantics for an edge connection.
/// This is the core abstraction that separates concerns between topology and delivery.
/// </summary>
public enum EdgeType
{
    /// <summary>
    /// Broadcast delivery - all target blocks get all items.
    /// Each target has its own channel.
    /// </summary>
    Broadcast,

    /// <summary>
    /// Competing delivery - each item consumed once by one target.
    /// All targets share a single channel.
    /// </summary>
    Competing,

    /// <summary>
    /// Routed delivery - items routed based on criteria.
    /// Special routing logic applies.
    /// </summary>
    Routed
}

/// <summary>
/// Abstract base for edge strategies that handle delivery semantics.
/// Encapsulates how items flow from source to target(s).
/// 
/// PERFORMANCE NOTE: Uses object-typed channels and parameters for flexibility with heterogeneous types.
/// This introduces boxing overhead for value types. See DESIGN_DECISIONS.md "Loss of Strong Typing in Processing Loop"
/// for detailed analysis and potential optimization strategies.
/// </summary>
public abstract class EdgeStrategy
{
    protected EdgeStrategy(EdgeType edgeType, BufferMode bufferMode, int bufferCapacity)
    {
        EdgeType = edgeType;
        BufferMode = bufferMode;
        BufferCapacity = bufferCapacity;
    }

    /// <summary>
    /// The type of edge strategy.
    /// </summary>
    public EdgeType EdgeType { get; }

    /// <summary>
    /// The buffering mode for this edge.
    /// </summary>
    public BufferMode BufferMode { get; }

    /// <summary>
    /// The capacity of the buffer (if BufferMode is Bounded).
    /// </summary>
    public int BufferCapacity { get; }

    /// <summary>
    /// Creates the required channels for this edge strategy.
    /// 
    /// PERFORMANCE NOTE: Returns Channel&lt;object&gt; instead of Channel&lt;T&gt;.
    /// This causes boxing for value types. Consider using Edge.DataType with reflection
    /// to create properly typed channels if profiling shows GC pressure.
    /// </summary>
    public abstract Dictionary<IBlock, (ChannelWriter<object> writer, ChannelReader<object> reader)> CreateChannels(
        IBlock sourceBlock,
        IReadOnlyList<IBlock> targetBlocks);

    /// <summary>
    /// Routes an item to the appropriate target channel(s).
    /// 
    /// PERFORMANCE NOTE: Item parameter is object type, causing boxing for value types in hot path.
    /// This method is called for every item flowing through the graph.
    /// </summary>
    public abstract Task RouteItemAsync(
        object item,
        Dictionary<IBlock, (ChannelWriter<object> writer, ChannelReader<object> reader)> channels,
        CancellationToken cancellationToken);
}

/// <summary>
/// Broadcast edge strategy - writes each item to all target channels.
/// Each target gets its own channel and receives all items.
/// Optionally supports cloning items before broadcasting for mutation isolation.
/// </summary>
public class BroadcastEdgeStrategy : EdgeStrategy
{
    private readonly Func<object, object>? _cloneFunc;

    /// <summary>
    /// Creates a broadcast edge strategy without cloning.
    /// </summary>
    public BroadcastEdgeStrategy(BufferMode bufferMode = BufferMode.Bounded, int bufferCapacity = 100)
        : base(EdgeType.Broadcast, bufferMode, bufferCapacity)
    {
        _cloneFunc = null;
    }

    /// <summary>
    /// Creates a broadcast edge strategy with optional cloning.
    /// </summary>
    /// <param name="cloneFunc">
    /// Optional function to clone items before broadcasting. 
    /// If provided, each target receives an independent clone.
    /// If null, all targets receive the same reference (default broadcast behavior).
    /// 
    /// <strong>When to use cloning:</strong>
    /// - When targets may mutate items and you need mutation isolation
    /// - When items contain mutable state that should be independent per target
    /// - Not needed for immutable types (strings, records with immutable properties)
    /// 
    /// <strong>Performance implications:</strong>
    /// - Clone function signature is <c>Func&lt;object, object&gt;</c> which causes boxing for value types
    /// - Called once per item per target in the hot path (N items × M targets calls)
    /// - For value types, consider using reference types or accepting shared mutation
    /// - For high-throughput scenarios with value types, measure GC impact via profiling
    /// - See DESIGN_DECISIONS.md "Loss of Strong Typing in Processing Loop" for optimization strategies
    /// 
    /// <strong>Example:</strong>
    /// <code>
    /// // Clone mutable objects
    /// new BroadcastEdgeStrategy(
    ///     cloneFunc: item => ((MyMutableClass)item).Clone(),
    ///     BufferMode.Bounded, 100);
    /// </code>
    /// </param>
    /// <param name="bufferMode">The buffering mode for this edge.</param>
    /// <param name="bufferCapacity">The capacity of the buffer (if BufferMode is Bounded).</param>
    public BroadcastEdgeStrategy(
        Func<object, object>? cloneFunc,
        BufferMode bufferMode = BufferMode.Bounded,
        int bufferCapacity = 100)
        : base(EdgeType.Broadcast, bufferMode, bufferCapacity)
    {
        _cloneFunc = cloneFunc;
    }

    public override Dictionary<IBlock, (ChannelWriter<object> writer, ChannelReader<object> reader)> CreateChannels(
        IBlock sourceBlock,
        IReadOnlyList<IBlock> targetBlocks)
    {
        var channels = new Dictionary<IBlock, (ChannelWriter<object> writer, ChannelReader<object> reader)>();

        foreach (var target in targetBlocks)
        {
            var channel = Channel.CreateBounded<object>(new BoundedChannelOptions(BufferCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });
            channels[target] = (channel.Writer, channel.Reader);
        }

        return channels;
    }

    public override async Task RouteItemAsync(
        object item,
        Dictionary<IBlock, (ChannelWriter<object> writer, ChannelReader<object> reader)> channels,
        CancellationToken cancellationToken)
    {
        // If cloning is enabled, clone for each target
        if (_cloneFunc != null)
        {
            foreach (var (writer, _) in channels.Values)
            {
                var clonedItem = _cloneFunc(item);
                await writer.WriteAsync(clonedItem, cancellationToken);
            }
        }
        else
        {
            // Write same reference to all channels (standard broadcast)
            foreach (var (writer, _) in channels.Values)
            {
                await writer.WriteAsync(item, cancellationToken);
            }
        }
    }
}

/// <summary>
/// Competing edge strategy - writes each item to a shared channel.
/// All targets compete for items - each item consumed once by one target.
/// This enables true concurrent processing without special concurrent blocks.
/// </summary>
public class CompetingEdgeStrategy : EdgeStrategy
{
    public CompetingEdgeStrategy(BufferMode bufferMode = BufferMode.Bounded, int bufferCapacity = 100)
        : base(EdgeType.Competing, bufferMode, bufferCapacity)
    {
    }

    public override Dictionary<IBlock, (ChannelWriter<object> writer, ChannelReader<object> reader)> CreateChannels(
        IBlock sourceBlock,
        IReadOnlyList<IBlock> targetBlocks)
    {
        // Create a single shared channel
        var channel = Channel.CreateBounded<object>(new BoundedChannelOptions(BufferCapacity)
        {
            SingleReader = false, // Multiple readers compete
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        var channels = new Dictionary<IBlock, (ChannelWriter<object> writer, ChannelReader<object> reader)>();

        // All targets share the same channel reader
        foreach (var target in targetBlocks)
        {
            channels[target] = (channel.Writer, channel.Reader);
        }

        return channels;
    }

    public override async Task RouteItemAsync(
        object item,
        Dictionary<IBlock, (ChannelWriter<object> writer, ChannelReader<object> reader)> channels,
        CancellationToken cancellationToken)
    {
        // Write once to shared channel - first available consumer gets it
        if (channels.Any())
        {
            var (writer, _) = channels.Values.First();
            await writer.WriteAsync(item, cancellationToken);
        }
    }
}
