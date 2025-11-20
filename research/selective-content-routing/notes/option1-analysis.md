# Option 1: SelectiveRoutingEdgeStrategy Analysis

## Overview

Create a new edge strategy that performs selective content-based routing for any type `T`, not just `RoutedItem<T>`.

## Key Differences from RoutedItemEdgeStrategy

**RoutedItemEdgeStrategy** (existing):
- Works ONLY with `RoutedItem<T>` wrapper type
- Extracts route key via reflection on `RouteKey` property
- Requires wrapping items in a record

**SelectiveRoutingEdgeStrategy** (proposed):
- Works with ANY type `T` directly
- Uses user-provided route selector function: `Func<T, string>`
- No wrapper type needed
- No record allocation overhead

## Design Approach

### Core Idea

Allow users to specify routing logic via a selector function that inspects the item's content:

```csharp
// User provides a function that extracts route key from item
Func<Order, string> routeSelector = order => order.CustomerId;

// Edge strategy uses this function to route items directly
var strategy = new SelectiveRoutingEdgeStrategy<Order>(
    routeKeyToBlock: routeMapping,
    routeSelector: routeSelector
);
```

### Architecture Integration

**Challenge**: EdgeStrategy is non-generic but RouteTypedItemAsync<T> is generic.

**Solution**: Store the route selector as `Func<object, string>` and cast at runtime (one-time cast per item).

### Type Safety

- Route selector is provided at construction time
- Type checking happens when creating the edge
- Runtime cast is safe because edge is created with specific source/target block types

## Prototype Code Structure

```csharp
public class SelectiveRoutingEdgeStrategy<TItem> : EdgeStrategy
{
    private readonly Dictionary<string, IBlock> _routeKeyToBlock;
    private readonly Func<TItem, string> _routeSelector;
    
    public SelectiveRoutingEdgeStrategy(
        Dictionary<string, IBlock> routeKeyToBlock,
        Func<TItem, string> routeSelector,
        BufferMode bufferMode = BufferMode.Bounded,
        int bufferCapacity = 100)
        : base(EdgeType.Routed, bufferMode, bufferCapacity)
    {
        _routeKeyToBlock = routeKeyToBlock;
        _routeSelector = routeSelector;
    }
    
    public override async Task RouteTypedItemAsync<T>(
        T item,
        Dictionary<IBlock, ChannelWriter<T>> typedWriters,
        CancellationToken cancellationToken)
    {
        // Cast item to TItem (safe because edge created with proper types)
        var typedItem = (TItem)(object)item!;
        
        // Extract route key using user-provided selector
        var routeKey = _routeSelector(typedItem);
        
        // Route to matching channel only
        if (_routeKeyToBlock.TryGetValue(routeKey, out var targetBlock))
        {
            if (typedWriters.TryGetValue(targetBlock, out var writer))
            {
                await writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            throw new InvalidOperationException($"Route '{routeKey}' not found");
        }
    }
}
```

## Trade-offs

### Pros
✅ No record allocation overhead (eliminates `RoutedItem<T>`)
✅ Works with any type T
✅ User provides routing logic directly
✅ Consistent with existing EdgeStrategy architecture
✅ Type-safe (compile-time checking of route selector)

### Cons
❌ Requires making EdgeStrategy generic (breaking change?)
❌ One runtime cast per item (TItem -> object -> T)
❌ User must configure edge strategy explicitly
❌ Less discoverable than block-based approach

## Performance Expectations

- **Zero broadcast overhead** - items go only to matching route
- **Zero record allocation** - no `RoutedItem<T>` wrapper needed
- **Minimal runtime overhead** - one cast, one dictionary lookup
- **Scales linearly with routes** - O(1) route lookup regardless of N routes

## Integration with Graph Builder

**Question**: How does user configure this?

**Option A**: Via edge creation
```csharp
var edge = new Edge(
    source: producer,
    targets: new[] { routeA, routeB },
    strategy: new SelectiveRoutingEdgeStrategy<Order>(
        routeKeyToBlock: new Dictionary<string, IBlock> 
        {
            ["customerA"] = routeA,
            ["customerB"] = routeB
        },
        routeSelector: order => order.CustomerId
    )
);
graph.AddEdge(edge);
```

**Option B**: Via builder extension
```csharp
builder
    .AddProducer<Order>("producer", ...)
    .AddProcessor<Order>("routeA", ...)
    .AddProcessor<Order>("routeB", ...)
    .ConnectWithSelectiveRouting(
        from: "producer",
        routeSelector: order => order.CustomerId,
        routes: new Dictionary<string, string>
        {
            ["customerA"] = "routeA",
            ["customerB"] = "routeB"
        }
    );
```

**Recommendation**: Start with Option A (explicit edge creation) for research. Option B (builder extension) can be added in implementation phase for ergonomics.

## Next Steps

1. Implement prototype in `/poc/DataFlow.POC/Core/SelectiveRoutingEdgeStrategy.cs`
2. Create integration test comparing to broadcast-and-filter
3. Create benchmark measuring performance improvement
4. Document findings
