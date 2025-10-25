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
/// PERFORMANCE: Now uses runtime-typed channels via reflection to eliminate boxing overhead for value types.
/// The DataType parameter is used to create properly typed Channel&lt;T&gt; instances.
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
    /// Creates the required typed channel adapters for this edge strategy.
    /// Uses reflection with caching to create properly typed Channel&lt;T&gt; instances.
    /// This eliminates boxing overhead for value types.
    /// </summary>
    /// <param name="dataType">The type of data flowing through the channels</param>
    /// <param name="sourceBlock">The source block</param>
    /// <param name="targetBlocks">The target blocks</param>
    /// <returns>Dictionary mapping target blocks to their typed channel adapters</returns>
    public abstract Dictionary<IBlock, TypedChannelAdapter> CreateTypedChannels(
        Type dataType,
        IBlock sourceBlock,
        IReadOnlyList<IBlock> targetBlocks);

    /// <summary>
    /// Routes an item to the appropriate target channel(s) using typed channel adapters.
    /// The adapters provide efficient non-generic access with minimal reflection overhead.
    /// </summary>
    /// <param name="item">The item to route</param>
    /// <param name="channels">The typed channel adapters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public abstract Task RouteItemAsync(
        object item,
        Dictionary<IBlock, TypedChannelAdapter> channels,
        CancellationToken cancellationToken);
}

/// <summary>
/// Broadcast edge strategy - writes each item to all target channels.
/// Each target gets its own channel and receives all items.
/// Optionally supports cloning items before broadcasting for mutation isolation.
/// Now uses typed channels to eliminate boxing overhead.
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
    public BroadcastEdgeStrategy(
        Func<object, object>? cloneFunc,
        BufferMode bufferMode = BufferMode.Bounded,
        int bufferCapacity = 100)
        : base(EdgeType.Broadcast, bufferMode, bufferCapacity)
    {
        _cloneFunc = cloneFunc;
    }

    public override Dictionary<IBlock, TypedChannelAdapter> CreateTypedChannels(
        Type dataType,
        IBlock sourceBlock,
        IReadOnlyList<IBlock> targetBlocks)
    {
        var channels = new Dictionary<IBlock, TypedChannelAdapter>();

        foreach (var target in targetBlocks)
        {
            var adapter = TypedChannelFactory.CreateTypedChannel(
                dataType,
                BufferMode,
                BufferCapacity,
                singleReader: true,
                singleWriter: false);
            
            channels[target] = adapter;
        }

        return channels;
    }

    public override async Task RouteItemAsync(
        object item,
        Dictionary<IBlock, TypedChannelAdapter> channels,
        CancellationToken cancellationToken)
    {
        // If cloning is enabled, clone for each target
        if (_cloneFunc != null)
        {
            foreach (var adapter in channels.Values)
            {
                var clonedItem = _cloneFunc(item);
                await adapter.WriteAsync(clonedItem, cancellationToken);
            }
        }
        else
        {
            // Write same reference to all channels (standard broadcast)
            foreach (var adapter in channels.Values)
            {
                await adapter.WriteAsync(item, cancellationToken);
            }
        }
    }
}

/// <summary>
/// Competing edge strategy - writes each item to a shared channel.
/// All targets compete for items - each item consumed once by one target.
/// This enables true concurrent processing without special concurrent blocks.
/// Now uses typed channels to eliminate boxing overhead.
/// </summary>
public class CompetingEdgeStrategy : EdgeStrategy
{
    public CompetingEdgeStrategy(BufferMode bufferMode = BufferMode.Bounded, int bufferCapacity = 100)
        : base(EdgeType.Competing, bufferMode, bufferCapacity)
    {
    }

    public override Dictionary<IBlock, TypedChannelAdapter> CreateTypedChannels(
        Type dataType,
        IBlock sourceBlock,
        IReadOnlyList<IBlock> targetBlocks)
    {
        // Create a single shared channel adapter
        var adapter = TypedChannelFactory.CreateTypedChannel(
            dataType,
            BufferMode,
            BufferCapacity,
            singleReader: false, // Multiple readers compete
            singleWriter: false);

        var channels = new Dictionary<IBlock, TypedChannelAdapter>();

        // All targets share the same channel adapter
        foreach (var target in targetBlocks)
        {
            channels[target] = adapter;
        }

        return channels;
    }

    public override async Task RouteItemAsync(
        object item,
        Dictionary<IBlock, TypedChannelAdapter> channels,
        CancellationToken cancellationToken)
    {
        // Write once to shared channel - first available consumer gets it
        if (channels.Any())
        {
            var adapter = channels.Values.First();
            await adapter.WriteAsync(item, cancellationToken);
        }
    }
}
