namespace DataFlow.POC.Core;

using System.Threading.Channels;

/// <summary>
/// Selective routing edge strategy that routes items based on content inspection.
/// Unlike RoutedItemEdgeStrategy which requires wrapping items in RoutedItem&lt;T&gt; records,
/// this strategy works directly with any item type using a user-provided route selector function.
/// 
/// This eliminates:
/// - Record allocation overhead (no RoutedItem&lt;T&gt; wrapper)
/// - Broadcasting to all routes (items sent only to matching route)
/// - Filtering overhead (no RouteFilterBlock needed)
/// 
/// Performance characteristics:
/// - O(1) route lookup per item
/// - Zero broadcast overhead
/// - Zero allocation overhead (no wrapper records)
/// - One cast per item (TItem -> object -> T for typed channel write)
/// </summary>
/// <typeparam name="TItem">The type of items being routed</typeparam>
public class SelectiveRoutingEdgeStrategy<TItem> : EdgeStrategy
{
    private readonly Dictionary<string, IBlock> _routeKeyToBlock;
    private readonly Func<TItem, string> _routeSelector;

    /// <summary>
    /// Creates a selective routing edge strategy.
    /// </summary>
    /// <param name="routeKeyToBlock">Mapping of route keys to target blocks.
    /// Each route key corresponds to one target block that will receive items matching that key.</param>
    /// <param name="routeSelector">Function that extracts the route key from an item.
    /// This function is called for each item to determine which route it should be sent to.</param>
    /// <param name="bufferMode">Buffering mode for channels (default: Bounded)</param>
    /// <param name="bufferCapacity">Capacity of channels (default: 100)</param>
    public SelectiveRoutingEdgeStrategy(
        Dictionary<string, IBlock> routeKeyToBlock,
        Func<TItem, string> routeSelector,
        BufferMode bufferMode = BufferMode.Bounded,
        int bufferCapacity = 100)
        : base(EdgeType.Routed, bufferMode, bufferCapacity)
    {
        _routeKeyToBlock = routeKeyToBlock ?? throw new ArgumentNullException(nameof(routeKeyToBlock));
        _routeSelector = routeSelector ?? throw new ArgumentNullException(nameof(routeSelector));

        if (_routeKeyToBlock.Count == 0)
        {
            throw new ArgumentException("At least one route must be specified", nameof(routeKeyToBlock));
        }
    }

    /// <summary>
    /// Creates typed channels for each route.
    /// Each target block (route) gets its own dedicated channel.
    /// </summary>
    public override (Dictionary<IBlock, object> writers, Dictionary<IBlock, object> readers) CreateTypedChannels(
        Type dataType,
        IBlock sourceBlock,
        IReadOnlyList<IBlock> targetBlocks)
    {
        var writers = new Dictionary<IBlock, object>();
        var readers = new Dictionary<IBlock, object>();

        // Create a separate channel for each target block (each route)
        foreach (var target in targetBlocks)
        {
            var (writer, reader) = TypedChannelFactory.CreateTypedChannel(
                dataType,
                BufferMode,
                BufferCapacity,
                singleReader: true,  // Each route has one consumer
                singleWriter: false); // Source may write from multiple threads
            
            writers[target] = writer;
            readers[target] = reader;
        }

        return (writers, readers);
    }

    /// <summary>
    /// Routes an item to the channel corresponding to its route key.
    /// The item is sent ONLY to the matching route - no broadcasting occurs.
    /// </summary>
    /// <typeparam name="T">The type of items (should match TItem)</typeparam>
    /// <param name="item">The item to route</param>
    /// <param name="typedWriters">Dictionary of typed channel writers for each route</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="InvalidOperationException">Thrown when route key doesn't match any configured route</exception>
    public override async Task RouteTypedItemAsync<T>(
        T item,
        Dictionary<IBlock, ChannelWriter<T>> typedWriters,
        CancellationToken cancellationToken)
    {
        // Cast item to TItem to apply the route selector
        // This is safe because:
        // 1. The edge is created with a specific source block type
        // 2. The source block outputs items of type TItem
        // 3. The edge strategy is configured for type TItem
        // 4. Therefore T == TItem at runtime
        var typedItem = (TItem)(object)item!;
        
        // Extract route key using user-provided selector function
        var routeKey = _routeSelector(typedItem);
        
        // Find the target block for this route key
        if (_routeKeyToBlock.TryGetValue(routeKey, out var targetBlock))
        {
            // Write to the specific channel for this route
            // This is the ONLY channel that receives this item - no broadcasting
            if (typedWriters.TryGetValue(targetBlock, out var writer))
            {
                await writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            // Route not found - this is a configuration error
            // In production, this could be configurable (drop item, dead letter queue, etc.)
            throw new InvalidOperationException(
                $"Route '{routeKey}' not found. Available routes: {string.Join(", ", _routeKeyToBlock.Keys)}. " +
                $"Item type: {typeof(TItem).Name}");
        }
    }
}
