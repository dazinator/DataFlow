namespace DataFlow.POC.Blocks;

using DataFlow.POC.Core;

/// <summary>
/// Router block that routes items to different outputs based on a selector function.
/// In the new design, routing is handled by the edge layer - this block just
/// tags items with routing information that edges can use.
/// </summary>
public class RouterBlock<T> : BlockBase<T, RoutedItem<T>>
{
    private readonly Func<T, string> _routeSelector;

    /// <summary>
    /// Legacy constructor for inline graph building.
    /// Prefer using the constructor with IBlockContext via DI registration.
    /// </summary>
    [Obsolete("Use the constructor with IBlockContext parameter via services.AddDataFlows(). This constructor will be removed in a future version.")]
    public RouterBlock(string name, Func<T, string> routeSelector)
        : base(name)
    {
        _routeSelector = routeSelector ?? throw new ArgumentNullException(nameof(routeSelector));
    }

    /// <summary>
    /// Constructor with IBlockContext for proper dependency injection.
    /// All dependencies are passed via constructor.
    /// </summary>
    public RouterBlock(IBlockContext context, Func<T, string> routeSelector)
        : base(context)
    {
        _routeSelector = routeSelector ?? throw new ArgumentNullException(nameof(routeSelector));
    }

    public override async IAsyncEnumerable<RoutedItem<T>> ExecuteAsync(
        IAsyncEnumerable<T> input,
        IExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            var routeKey = _routeSelector(item);
            yield return new RoutedItem<T>(item, routeKey);
        }
    }
}

/// <summary>
/// Represents an item with routing information.
/// </summary>
public record RoutedItem<T>(T Item, string RouteKey);

/// <summary>
/// Specialized edge for routing that filters items based on route key.
/// This demonstrates how edge logic can handle routing at the edge level.
/// </summary>
public class RoutingEdge : Edge
{
    public RoutingEdge(
        IBlock sourceBlock,
        IBlock targetBlock,
        string routeKey,
        BufferMode bufferMode = BufferMode.Bounded,
        int bufferCapacity = 100)
        : base(sourceBlock, targetBlock, bufferMode, bufferCapacity)
    {
        RouteKey = routeKey ?? throw new ArgumentNullException(nameof(routeKey));
    }

    /// <summary>
    /// The route key that this edge accepts.
    /// </summary>
    public string RouteKey { get; }

    /// <summary>
    /// Checks if an item should be routed through this edge.
    /// </summary>
    public bool Matches<T>(RoutedItem<T> item)
    {
        return item.RouteKey == RouteKey;
    }
}

/// <summary>
/// Adapter block that filters routed items for a specific route.
/// This shows how routing logic can be separated from the core router block.
/// </summary>
public class RouteFilterBlock<T> : BlockBase<RoutedItem<T>, T>
{
    private readonly string _routeKey;

    /// <summary>
    /// Legacy constructor for inline graph building.
    /// Prefer using the constructor with IBlockContext via DI registration.
    /// </summary>
    [Obsolete("Use the constructor with IBlockContext parameter via services.AddDataFlows(). This constructor will be removed in a future version.")]
    public RouteFilterBlock(string name, string routeKey)
        : base(name)
    {
        _routeKey = routeKey ?? throw new ArgumentNullException(nameof(routeKey));
    }

    /// <summary>
    /// Constructor with IBlockContext for proper dependency injection.
    /// All dependencies are passed via constructor.
    /// </summary>
    public RouteFilterBlock(IBlockContext context, string routeKey)
        : base(context)
    {
        _routeKey = routeKey ?? throw new ArgumentNullException(nameof(routeKey));
    }

    public override async IAsyncEnumerable<T> ExecuteAsync(
        IAsyncEnumerable<RoutedItem<T>> input,
        IExecutionContext context)
    {
        await foreach (var routedItem in input.WithCancellation(context.CancellationToken))
        {
            if (routedItem.RouteKey == _routeKey)
            {
                yield return routedItem.Item;
            }
        }
    }
}
