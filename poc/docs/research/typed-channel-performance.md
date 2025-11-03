# Typed Channel Performance Optimization

## Problem

The initial implementation of typed channels (PR #68) successfully replaced `Channel<object>` with `Channel<T>` to reduce boxing overhead. However, boxing was still occurring when writing values to channels because:

1. The `EdgeStrategy.RouteItemAsync` method receives `object item` as a parameter
2. When a value type (e.g., `int`) is passed as `object`, it's already boxed
3. Even though we write to a typed `ChannelWriter<T>`, the value remains boxed because the cast from `object` to `T` doesn't unbox—it just verifies the type

## Example of the Problem

```csharp
// Before optimization
int value = 42;  // Value type on stack
object boxed = value;  // BOXING happens here - allocates on heap

// In EdgeStrategy.RouteItemAsync(object item, ...)
await adapter.WriteAsync(item, cancellationToken);  // item is still boxed

// Inside TypedChannelAdapter.WriteAsync(object item, ...)
await _writeAsyncDelegate(item, cancellationToken);  // Still boxed

// Inside reflection invoke
writeMethod.Invoke(_writer, new[] { item, ct });  // Still boxed - creates object[] too
```

## Solution: TypedEdgeRouter

We introduced `TypedEdgeRouter<T>` which:

1. **Extracts strongly-typed `ChannelWriter<T>`** from the `TypedChannelAdapter`
2. **Casts items once** from `object` to `T` (unboxing value types)
3. **Writes directly to `ChannelWriter<T>`** without boxing

### Architecture

```
┌─────────────────────────────────────────────────────────────┐
│ DataFlowGraph.RouteOutput                                   │
│                                                              │
│  await foreach (var item in output) // item is 'object'     │
│  {                                                           │
│    await router.RouteItemAsync(item, ct);                   │
│  }                                                           │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│ TypedEdgeRouter<T>.RouteItemAsync(object item)              │
│                                                              │
│  var typedItem = (T)item;  // UNBOXING happens here once    │
│  await _routeDelegate(typedItem, _typedWriters, ct);        │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│ RouteBroadcastAsync(T item, ...)                            │
│                                                              │
│  foreach (var writer in writers.Values)                     │
│  {                                                           │
│    await writer.WriteAsync(item, ct); // NO boxing!         │
│  }                                                           │
└─────────────────────────────────────────────────────────────┘
```

### Key Benefits

1. **Single unbox operation**: When `(T)item` is executed, if `item` is a boxed value type, it's unboxed once
2. **Direct channel write**: The unboxed value is passed directly to `ChannelWriter<T>.WriteAsync(T item, ...)`
3. **No reflection overhead**: No `MethodInfo.Invoke()` in the hot path
4. **No additional allocations**: No intermediate `object[]` arrays for reflection

## Performance Impact

### What We Eliminated

For value types (int, struct, etc.) in **channel write operations**:
- **Before**: Boxing on every write to channel (heap allocation + GC pressure)
- **After**: Single unbox operation, then direct typed writes (no re-boxing)

For reference types (string, class, etc.):
- **Before**: Cast to object (no cost since already reference type)
- **After**: Cast to T (no cost since already reference type)

### Remaining Boxing Source

**Important**: Boxing still occurs once per item when blocks output their results. This happens in `BlockBase<TIn, TOut>` where the typed output (`IAsyncEnumerable<TOut>`) is converted to the untyped interface (`IAsyncEnumerable<object>`).

**Current flow for value types:**
```
ProducerBlock<int> → IAsyncEnumerable<int> (typed, no boxing)
                   ↓
BlockBase adapter  → IAsyncEnumerable<object> (BOXING occurs here)
                   ↓
TypedEdgeRouter    → Unboxes once, writes to typed channels (no re-boxing)
```

**Impact:**
- ✅ **Eliminated**: Re-boxing on every channel write (critical for broadcast scenarios)
- ❌ **Remaining**: One boxing allocation per item at block output boundary

### Performance Gains

The optimization is most significant for:
- **Broadcast edges**: Each item written to N channels (before: N boxing operations, after: 0)
- **High-throughput scenarios**: Millions of items with value types
- **Large structs**: 64-byte structs being written to multiple channels
- **GC pressure reduction**: Fewer heap allocations overall

**Example - Broadcast to 4 targets:**
- Before: 1 box + 4 re-boxes = 5 allocations per item
- After: 1 box + 0 re-boxes = 1 allocation per item
- **80% reduction in allocations**

## Implementation Details

### TypedEdgeRouterFactory

Uses compiled expressions to create typed routers efficiently:

```csharp
var factory = _routerFactoryCache.GetOrAdd(dataType, type =>
{
    var routerType = typeof(TypedEdgeRouter<>).MakeGenericType(type);
    var constructor = routerType.GetConstructor(...);
    
    // Compile expression tree to create router
    var lambda = Expression.Lambda<Func<...>>(newExpr, ...);
    return lambda.Compile();
});
```

### Cloning Support

The router supports the broadcast cloning feature:

```csharp
if (_cloneFunc != null)
{
    foreach (var writer in writers.Values)
    {
        var clonedItem = (T)_cloneFunc(item!);  // Clone then cast/unbox
        await writer.WriteAsync(clonedItem, ct);  // Direct typed write
    }
}
```

## Testing

All existing tests pass with the new implementation:
- ✅ BroadcastEdge routing
- ✅ CompetingEdge routing  
- ✅ Cloning in broadcast scenarios
- ✅ Complex multi-edge flows
- ✅ Batch processing

## Future Improvements

### 1. Complete Zero-Boxing Execution (Architecture Defined, Implementation Ready)

**Status**: Foundation implemented, integration pending.

**Architecture** (from senior developer guidance):
The solution uses a layered responsibility model:

| Layer | Role | Generic? | Notes |
|-------|------|----------|-------|
| **Block implementation** | Business logic (`IBlock<TIn, TOut>`) | ✅ Generic | Strongly typed transformation |
| **Graph orchestration** | Stores untyped block references and manages lifecycle | ⚙️ Non-generic | Uses reflection once to wire types |
| **Execution plan (hot path)** | Executes cached typed delegates | ✅ Generic delegate, cached | No reflection, no boxing |

**Key Insight**: Avoid `IAsyncEnumerable<object>` which forces per-item boxing. Instead:
- Use `Task<object> ExecuteUntypedAsync(object input, ...)` where `object` represents the entire typed stream
- Cast happens at control layer (once), not per item (avoiding boxing)
- The `IAsyncEnumerable<T>` remains strongly typed throughout execution

**Implementation Components** (Ready):
1. ✅ `ExecutableBlockAdapter<TIn, TOut>` - wraps typed block execution
2. ✅ `ExecutableBlockFactory` - caches compiled adapters (reflection once at build)
3. ✅ `TypedEdgeRouter<T>.RouteTypedItemAsync()` - internal method for zero-boxing routing
4. ⚠️ DataFlowGraph integration - requires refactoring to use adapters

**Integration Steps** (Remaining):
1. Create typed execution plans during graph build
2. Store adapters and typed routers per block
3. Execute via `adapter.ExecuteUntypedAsync()` returning typed stream as object
4. Cast stream and enumerate/route without per-item boxing
5. Handle multiple outgoing edges with typed routing coordinator

**Expected Outcome**:
- **Zero boxing** for value types end-to-end
- **No reflection** on hot path
- **Polymorphic orchestration** preserved
- **Performance parity** with handwritten `Channel<T>` pipelines

**Example Flow**:
```
Build Phase (cold path):
    Reflection → Type detection → Cached typed delegates → Typed adapters/routers created

Execution Phase (hot path):
    adapter.ExecuteUntypedAsync() → Returns IAsyncEnumerable<T> as object
    → Cast once at control layer → Enumerate typed items
    → Route via TypedEdgeRouter<T>.RouteTypedItemAsync()
    → Write to Channel<T> → Zero boxing
```

**Tradeoffs**:
- ✅ Zero allocations for value types
- ✅ Minimal breaking changes (internal only)
- ⚠️ Requires DataFlowGraph refactoring
- ⚠️ More complex execution pipeline

### 2. AOT Compatibility

Evaluate Source Generators instead of runtime reflection/expression trees for AOT scenarios where reflection is limited or has performance penalties.

### 3. Zero-Allocation Path for Specific Scenarios

Explore `ref struct` and stack-only paths for ultra-hot scenarios where even single allocations matter (e.g., high-frequency trading, real-time systems).

## References

- Issue: uniun-technology/lib-dataflow#67
- Previous work: PR #68 (TypedChannelAdapter)
