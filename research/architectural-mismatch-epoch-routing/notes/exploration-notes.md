# Exploration Notes: Architectural Mismatch Investigation

## Date: 2025-11-26

### Code Analysis Findings

#### 1. Edge Routing Mechanism (CONFIRMED)

**Location**: `DataFlowGraph.cs` - `EnumerateAndRouteTypedStreamAsync`

```csharp
private static async Task EnumerateAndRouteTypedStreamGenericAsync<T>(
    object typedStream,
    List<ITypedEdgeRouter> routers,
    CancellationToken cancellationToken)
{
    var stream = (IAsyncEnumerable<T>)typedStream;
    await foreach (var item in stream.WithCancellation(cancellationToken))
    {
        // Route each item individually
        await router.RouteTypedItemAsync(item, cancellationToken);
    }
}
```

**Key Insight**: 
- The generic parameter `T` is the block's output item type
- If block outputs `IAsyncEnumerable<IEpochStream<int>>`, then `T = IEpochStream<int>`
- Each iteration routes ONE epoch stream container, not individual data items

#### 2. Broadcast Strategy (CONFIRMED)

**Location**: `EdgeStrategy.cs` - `BroadcastEdgeStrategy.RouteTypedItemAsync`

```csharp
public override async Task RouteTypedItemAsync<T>(
    T item,
    Dictionary<IBlock, ChannelWriter<T>> typedWriters,
    CancellationToken cancellationToken)
{
    // Broadcasts the ITEM (which could be an IEpochStream<T>)
    // Multiple writers receive the SAME object reference
}
```

**Key Insight**:
- Broadcast writes the same item reference to all channels
- If item is `IEpochStream<int>`, all downstream blocks receive the SAME container
- Multiple blocks enumerating the same epoch stream would conflict!

#### 3. Competing Strategy (CONFIRMED)

**Location**: `EdgeStrategy.cs` - `CompetingEdgeStrategy.RouteTypedItemAsync`

```csharp
public override async Task RouteTypedItemAsync<T>(
    T item,
    Dictionary<IBlock, ChannelWriter<T>> typedWriters,
    CancellationToken cancellationToken)
{
    // Writes item to shared channel
    var writer = typedWriters.Values.First();
    await writer.WriteAsync(item, cancellationToken);
}
```

**Key Insight**:
- Competing writes ONE item to shared channel
- If item is `IEpochStream<int>`, only ONE consumer gets the entire epoch
- This might actually work correctly for epoch containers (whole epoch to one consumer)

#### 4. No Special Epoch Handling in Edges (CONFIRMED)

**Checked**:
- `EnvelopeEdgeStrategy` - Only handles control signals (IDataEnvelope)
- `EnvelopeStreamMerger` - Only merges envelope streams
- No epoch-specific unwrapping logic found in edge strategies

**Conclusion**: Edges treat `IEpochStream<T>` as opaque containers.

### Architectural Mismatch Hypothesis

**Current Behavior (as designed)**:
```
Block: IAsyncEnumerable<IEpochStream<int>>
       ↓ Yields one container
Edge:  Routes IEpochStream<int> (container)
       ↓
Downstream blocks receive: IEpochStream<int> (container)
```

**Problem Scenarios**:

1. **Broadcast with Epoch Containers**:
   ```
   Source → [Broadcast] → Consumer A
                       → Consumer B
   
   Issue: Both consumers get SAME IEpochStream<int> reference
   Result: Enumerating .Items conflicts - items consumed only once!
   ```

2. **Selective Routing with Epoch Containers**:
   ```
   Source → [Selective by key] → Consumer A (even keys)
                               → Consumer B (odd keys)
   
   Issue: Routing logic operates on CONTAINER, not items inside
   Result: Entire epoch routed to one consumer, not individual items
   ```

3. **Buffer Block with Epoch Containers**:
   ```
   Source → [Buffer] → Consumer
   
   Issue: Buffer stores epoch containers, not data items
   Result: Buffer semantics unclear (buffering containers vs items)
   ```

### Alternative Architecture: ConfigureEpochs

**Location**: `SingleEpochExtensions.cs`

```csharp
public static async IAsyncEnumerable<IEpochStream<T>> WrapInSingleEpoch<T>(
    this IAsyncEnumerable<T> source,
    string sourceName,
    CancellationToken cancellationToken = default)
{
    var epochVector = EpochVector.FromSingleSource(sourceName, 1);
    yield return new EpochStream<T>(epochVector, source);
}
```

**How it works**:
- Takes plain `IAsyncEnumerable<T>` 
- Wraps entire stream in ONE epoch container
- Returns `IAsyncEnumerable<IEpochStream<T>>` with single element

**Same Problem**:
- If used with broadcast edge, still routes the container
- Downstream blocks still receive same container reference

### Key Questions to Answer with Tests

1. ✅ **Do edges route containers or items?** → CONTAINERS (confirmed via code)
2. ⚠️ **Does broadcast with epoch streams cause conflicts?** → NEEDS TESTING
3. ⚠️ **Can selective routing work with epoch streams?** → NEEDS TESTING
4. ⚠️ **Are there workarounds in the existing codebase?** → NEEDS INVESTIGATION

### Next Steps

1. **Create test: Broadcast with epoch streams**
   - Two consumers reading same epoch stream container
   - Validate if conflicts occur

2. **Create test: Selective routing with epoch streams**
   - Route based on key selector
   - Validate if container-level routing works

3. **Review existing epoch tests**
   - How do they handle broadcast/routing?
   - Do they avoid the problem somehow?

4. **Check graph integration**
   - How does `ConfigureEpochs` get used in real flows?
   - Are there epoch-specific graph configurations?

### Preliminary Conclusion

**The architectural mismatch is REAL**:
- Edges route epoch stream **containers**, not data items
- This breaks routing semantics when multiple consumers are involved
- Broadcast and selective routing cannot work correctly with epoch containers

**Possible Solutions**:
1. **Unwrap epochs at edge boundary** - Add epoch-aware routing
2. **Change block outputs** - Blocks output items, infrastructure manages epochs
3. **Dual-mode support** - Both approaches for different use cases
4. **Document limitation** - Epoch streams only work with 1:1 connections

**Recommendation**: Need to validate with tests before deciding on solution.
