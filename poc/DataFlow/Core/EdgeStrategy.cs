namespace DataFlow.POC.Core;

using System.Collections.Concurrent;
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
    /// Creates the required typed channels for this edge strategy.
    /// Uses reflection with caching to create properly typed Channel&lt;T&gt; instances.
    /// This eliminates boxing overhead for value types.
    /// </summary>
    /// <param name="dataType">The type of data flowing through the channels</param>
    /// <param name="sourceBlock">The source block</param>
    /// <param name="targetBlocks">The target blocks</param>
    /// <returns>Typed channels - writers dictionary for routing, readers dictionary for consumption</returns>
    public abstract (Dictionary<IBlock, object> writers, Dictionary<IBlock, object> readers) CreateTypedChannels(
        Type dataType,
        IBlock sourceBlock,
        IReadOnlyList<IBlock> targetBlocks);

    /// <summary>
    /// Routes an item to the appropriate target channel(s) using strongly-typed channel writers.
    /// This method allows strategies to perform routing with typed channels, eliminating boxing overhead.
    /// Strategies can perform any necessary transformations (e.g., cloning) before writing to channels.
    /// </summary>
    /// <typeparam name="T">The type of items flowing through the channels</typeparam>
    /// <param name="item">The item to route</param>
    /// <param name="typedWriters">Dictionary of typed channel writers</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public abstract Task RouteTypedItemAsync<T>(
        T item,
        Dictionary<IBlock, ChannelWriter<T>> typedWriters,
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

    public override (Dictionary<IBlock, object> writers, Dictionary<IBlock, object> readers) CreateTypedChannels(
        Type dataType,
        IBlock sourceBlock,
        IReadOnlyList<IBlock> targetBlocks)
    {
        var writers = new Dictionary<IBlock, object>();
        var readers = new Dictionary<IBlock, object>();

        foreach (var target in targetBlocks)
        {
            var (writer, reader) = TypedChannelFactory.CreateTypedChannel(
                dataType,
                BufferMode,
                BufferCapacity,
                singleReader: true,
                singleWriter: false);
            
            writers[target] = writer;
            readers[target] = reader;
        }

        return (writers, readers);
    }

    /// <summary>
    /// Routes an item to all target channels using typed writers.
    /// Performs cloning if configured to ensure mutation isolation.
    /// Writes to all channels concurrently to avoid serialization bottleneck.
    /// </summary>
    public override async Task RouteTypedItemAsync<T>(
        T item,
        Dictionary<IBlock, ChannelWriter<T>> typedWriters,
        CancellationToken cancellationToken)
    {
        if (typedWriters.Count == 0)
        {
            return;
        }

        if (typedWriters.Count == 1)
        {
            // Optimization: single writer doesn't need Task.WhenAll overhead
            // Use direct enumeration instead of First() to avoid creating enumerator
            using var enumerator = typedWriters.Values.GetEnumerator();
            enumerator.MoveNext();
            var writer = enumerator.Current;
            
            if (_cloneFunc != null)
            {
                var clonedItem = (T)_cloneFunc(item!);
                await writer.WriteAsync(clonedItem, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            // Multiple writers: write concurrently to avoid serialization bottleneck
            var writeTasks = new Task[typedWriters.Count];
            int index = 0;
            
            if (_cloneFunc != null)
            {
                // Clone for each target to ensure isolation
                foreach (var writer in typedWriters.Values)
                {
                    var clonedItem = (T)_cloneFunc(item!);
                    writeTasks[index++] = writer.WriteAsync(clonedItem, cancellationToken).AsTask();
                }
            }
            else
            {
                // Write same reference to all channels (standard broadcast)
                foreach (var writer in typedWriters.Values)
                {
                    writeTasks[index++] = writer.WriteAsync(item, cancellationToken).AsTask();
                }
            }
            
            await Task.WhenAll(writeTasks).ConfigureAwait(false);
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

    public override (Dictionary<IBlock, object> writers, Dictionary<IBlock, object> readers) CreateTypedChannels(
        Type dataType,
        IBlock sourceBlock,
        IReadOnlyList<IBlock> targetBlocks)
    {
        // Create a single shared channel
        var (writer, reader) = TypedChannelFactory.CreateTypedChannel(
            dataType,
            BufferMode,
            BufferCapacity,
            singleReader: false, // Multiple readers compete
            singleWriter: false);

        var writers = new Dictionary<IBlock, object>();
        var readers = new Dictionary<IBlock, object>();

        // All targets share the same channel
        foreach (var target in targetBlocks)
        {
            writers[target] = writer;
            readers[target] = reader;
        }

        return (writers, readers);
    }

    /// <summary>
    /// Routes an item to the shared channel using typed writer.
    /// First available consumer gets the item.
    /// </summary>
    public override async Task RouteTypedItemAsync<T>(
        T item,
        Dictionary<IBlock, ChannelWriter<T>> typedWriters,
        CancellationToken cancellationToken)
    {
        if (typedWriters.Count > 0)
        {
            var writer = typedWriters.Values.First();
            await writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
        }
    }
}

