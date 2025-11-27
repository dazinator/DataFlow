# Option 2: Intelligent Edge Unwrap/Wrap

**Date**: 2025-11-27  
**Status**: Design & Prototyping

---

## Overview

**Core Concept**: Preserve `IAsyncEnumerable<IEpochStream<T>>` block signatures but make edges intelligent about unwrapping epoch streams to route individual items, then re-wrapping items into new epoch streams for each downstream consumer.

---

## Architecture

### High-Level Model

```
┌─────────────┐  
│ Source Block│ outputs: IAsyncEnumerable<IEpochStream<int>>
└──────┬──────┘
       │ Yields: IEpochStream<int> {vector={A=1}, items=[1,2,3,4,5]}
       ▼
  ┌──────────────────┐
  │ Intelligent Edge │
  │ (Unwrap Logic)   │
  └────┬──────┬──────┘
       │      │
       │      │ Unwraps: 1, 2, 3, 4, 5 (individual items)
       │      │ Routes each item based on strategy
       │      │ Re-wraps into new IEpochStream per consumer
       │      │
       ▼      ▼
   ┌─────┐ ┌─────┐
   │  A  │ │  B  │ Each receives IEpochStream<int> {vector={A=1}, items=...}
   └─────┘ └─────┘
       │      │
       │      └─── Enumerates: [1,2,3,4,5] ✓
       └────────── Enumerates: [1,2,3,4,5] ✓
```

### Key Insight

**Edges become epoch-aware**:
- Detect when routing `IEpochStream<T>` (container type)
- Unwrap to enumerate individual items `T`
- Route items using normal edge strategy logic
- Re-wrap routed items into new epoch streams per downstream block
- Propagate epoch vector information through wrapping

---

## Key Components

### 1. Epoch-Aware Edge Router

**Current Routing** (Problem):
```csharp
private static async Task EnumerateAndRouteTypedStreamGenericAsync<T>(
    object typedStream,
    List<ITypedEdgeRouter> routers,
    CancellationToken cancellationToken)
{
    var stream = (IAsyncEnumerable<T>)typedStream;
    await foreach (var item in stream.WithCancellation(cancellationToken))
    {
        // For IAsyncEnumerable<IEpochStream<int>>, T = IEpochStream<int>
        // Routes CONTAINERS ❌
        await router.RouteTypedItemAsync(item, cancellationToken);
    }
}
```

**New Routing** (Solution):
```csharp
private static async Task EnumerateAndRouteTypedStreamGenericAsync<T>(
    object typedStream,
    List<ITypedEdgeRouter> routers,
    CancellationToken cancellationToken)
{
    var stream = (IAsyncEnumerable<T>)typedStream;
    
    // CHECK: Is T an IEpochStream<TItem>?
    if (typeof(T).IsGenericType && 
        typeof(T).GetGenericTypeDefinition() == typeof(IEpochStream<>))
    {
        // UNWRAP PATH: Route items within epoch streams
        await EnumerateAndRouteEpochStreamAsync(stream, routers, cancellationToken);
    }
    else
    {
        // NORMAL PATH: Route items directly
        await foreach (var item in stream.WithCancellation(cancellationToken))
        {
            await router.RouteTypedItemAsync(item, cancellationToken);
        }
    }
}
```

### 2. Epoch Stream Unwrap/Wrap Logic

**Unwrap and Route Items**:
```csharp
private static async Task EnumerateAndRouteEpochStreamAsync<T>(
    IAsyncEnumerable<IEpochStream<T>> epochStreamSource,
    List<ITypedEdgeRouter> routers,
    CancellationToken cancellationToken)
{
    await foreach (var epochStream in epochStreamSource.WithCancellation(cancellationToken))
    {
        // For each epoch stream container:
        // 1. Extract epoch metadata
        var epochVector = epochStream.EpochVector;
        var epoch = epochStream.Epoch;
        
        // 2. Create downstream epoch streams (one per target block)
        var downstreamEpochStreams = CreateDownstreamEpochStreams<T>(
            routers, 
            epochVector, 
            epoch);
        
        // 3. Enumerate items from source epoch stream
        await foreach (var item in epochStream.Items.WithCancellation(cancellationToken))
        {
            // 4. Route each item using edge strategy
            //    Each router writes item to its downstream epoch stream channel
            await RouteItemToDownstreamEpochStreamsAsync(
                item, 
                routers, 
                downstreamEpochStreams, 
                cancellationToken);
        }
        
        // 5. Signal epoch stream completion to downstream
        await CompleteDownstreamEpochStreamsAsync(downstreamEpochStreams);
    }
}
```

### 3. Downstream Epoch Stream Creation

**Strategy-Specific Channel Management**:

**Broadcast Strategy**:
```csharp
// Each downstream block gets its own channel
// Items are DUPLICATED to each channel
var downstreamChannels = new Dictionary<IBlock, Channel<T>>();
foreach (var targetBlock in targetBlocks)
{
    downstreamChannels[targetBlock] = Channel.CreateBounded<T>(bufferSize);
}

// Wrap each channel as IEpochStream
var downstreamEpochStreams = downstreamChannels.Select(kvp => 
    new ChannelBackedEpochStream<T>(
        epochVector, 
        epoch, 
        kvp.Value.Reader.ReadAllAsync()));
```

**Competing Strategy**:
```csharp
// All downstream blocks share ONE channel
// Items are DISTRIBUTED across consumers
var sharedChannel = Channel.CreateBounded<T>(bufferSize);

// Wrap shared channel as IEpochStream for all blocks
var downstreamEpochStreams = targetBlocks.Select(block => 
    new ChannelBackedEpochStream<T>(
        epochVector, 
        epoch, 
        sharedChannel.Reader.ReadAllAsync()));
```

**Selective Strategy**:
```csharp
// Each route gets its own channel
// Items go to channels based on predicate
var routeChannels = new Dictionary<string, Channel<T>>();
foreach (var route in routes)
{
    routeChannels[route.Name] = Channel.CreateBounded<T>(bufferSize);
}

// Wrap each route channel as IEpochStream
var downstreamEpochStreams = routes.Select(route => 
    new ChannelBackedEpochStream<T>(
        epochVector, 
        epoch, 
        routeChannels[route.Name].Reader.ReadAllAsync()));
```

### 4. Channel-Backed Epoch Stream

**New Type**:
```csharp
public class ChannelBackedEpochStream<T> : IEpochStream<T>
{
    private readonly Channel<T> _channel;
    
    public EpochVector EpochVector { get; }
    public IEpoch Epoch { get; }
    public IAsyncEnumerable<T> Items => _channel.Reader.ReadAllAsync();
    
    public ChannelBackedEpochStream(
        EpochVector epochVector,
        IEpoch epoch,
        Channel<T> channel)
    {
        EpochVector = epochVector;
        Epoch = epoch;
        _channel = channel;
    }
    
    // Internal API for edge routing infrastructure only
    internal ChannelWriter<T> GetWriter() => _channel.Writer;
    
    internal void CompleteWriting() => _channel.Writer.Complete();
}
```

---

## Edge Strategy Adaptations

### Broadcast Strategy

**Unwrap/Wrap Logic**:
```csharp
public override async Task RouteEpochStreamAsync<T>(
    IEpochStream<T> sourceEpochStream,
    Dictionary<IBlock, ChannelBackedEpochStream<T>> downstreamEpochStreams,
    CancellationToken cancellationToken)
{
    // Enumerate source epoch stream items
    await foreach (var item in sourceEpochStream.Items.WithCancellation(cancellationToken))
    {
        // Write item to ALL downstream epoch stream channels
        var writeTasks = downstreamEpochStreams.Values
            .Select(stream => stream.GetWriter().WriteAsync(item, cancellationToken).AsTask());
        
        await Task.WhenAll(writeTasks);
    }
    
    // Complete all downstream channels
    foreach (var stream in downstreamEpochStreams.Values)
    {
        stream.CompleteWriting();
    }
}
```

### Selective Strategy

**Unwrap/Wrap Logic**:
```csharp
public override async Task RouteEpochStreamAsync<T>(
    IEpochStream<T> sourceEpochStream,
    Dictionary<string, ChannelBackedEpochStream<T>> routeEpochStreams,
    CancellationToken cancellationToken)
{
    // Enumerate source epoch stream items
    await foreach (var item in sourceEpochStream.Items.WithCancellation(cancellationToken))
    {
        // Determine route based on item
        var routeName = _routeSelector(item);
        
        // Write to selected route's epoch stream channel
        if (routeEpochStreams.TryGetValue(routeName, out var stream))
        {
            await stream.GetWriter().WriteAsync(item, cancellationToken);
        }
    }
    
    // Complete all route channels
    foreach (var stream in routeEpochStreams.Values)
    {
        stream.CompleteWriting();
    }
}
```

### Competing Strategy

**Unwrap/Wrap Logic**:
```csharp
public override async Task RouteEpochStreamAsync<T>(
    IEpochStream<T> sourceEpochStream,
    ChannelBackedEpochStream<T> sharedEpochStream,
    CancellationToken cancellationToken)
{
    // Enumerate source epoch stream items
    await foreach (var item in sourceEpochStream.Items.WithCancellation(cancellationToken))
    {
        // Write to SHARED epoch stream channel
        // Downstream consumers compete for items
        await sharedEpochStream.GetWriter().WriteAsync(item, cancellationToken);
    }
    
    // Complete shared channel
    sharedEpochStream.CompleteWriting();
}
```

---

## Epoch Vector Propagation

**Key Requirement**: Downstream epoch streams must maintain correlation with source epoch stream.

**Approach**: Copy epoch vector and epoch reference when creating downstream streams.

```csharp
// Source epoch stream
var sourceEpochStream = new EpochStream<int>(
    epochVector: new EpochVector(new Dictionary<string, int> { ["A"] = 1 }),
    epoch: epochInstance,
    items: sourceItems);

// Downstream epoch streams (broadcast example)
var downstreamStreamA = new ChannelBackedEpochStream<int>(
    epochVector: sourceEpochStream.EpochVector, // SAME vector
    epoch: sourceEpochStream.Epoch,             // SAME epoch instance
    channel: channelA);

var downstreamStreamB = new ChannelBackedEpochStream<int>(
    epochVector: sourceEpochStream.EpochVector, // SAME vector
    epoch: sourceEpochStream.Epoch,             // SAME epoch instance
    channel: channelB);
```

**Result**: Epoch correlation is preserved through the graph.

---

## Advantages

### 1. Efficient Epoch Boundary Detection

✅ Blocks detect epoch changes via stream boundaries  
✅ No per-item epoch checks needed  
✅ Preserves IEpochStream semantic benefits  

### 2. Single Long-Running Execution

✅ No graph re-instantiation overhead  
✅ Blocks run continuously  
✅ Natural for long-running pipelines  

### 3. Natural Backpressure

✅ Backpressure flows through channels naturally  
✅ Channel capacity limits apply per downstream stream  
✅ Upstream waits if downstream is slow  

### 4. Works with All Topologies

✅ Broadcast: Duplicate items to each channel  
✅ Selective: Route items based on predicates  
✅ Competing: Shared channel for load balancing  

---

## Challenges

### 1. Edge Complexity

**Concern**: Edge logic becomes significantly more complex

**Analysis**:
- Need to detect epoch stream types
- Need to create and manage downstream channels
- Need to handle completion signals correctly
- Need to propagate epoch metadata

**Mitigation**:
- Encapsulate unwrap/wrap logic in helper classes
- Provide base classes for common patterns
- Good testing coverage

### 2. Reasoning About Data Flow

**Concern**: Harder to understand what edges are doing

**Example**:
```
Source outputs: IAsyncEnumerable<IEpochStream<int>>
Edge unwraps, routes items, re-wraps
Downstream receives: IAsyncEnumerable<IEpochStream<int>>

But the IEpochStream instances are DIFFERENT objects
  backed by DIFFERENT channels
  with the SAME epoch metadata
```

**Mitigation**:
- Clear documentation with diagrams
- Naming conventions (ChannelBackedEpochStream)
- Good examples for all topologies

### 3. Performance Overhead

**Potential Overheads**:
- Type checking (`typeof(T).IsGenericType...`)
- Channel creation per downstream block
- Unwrap/wrap coordination
- Additional async/await overhead

**Questions**:
- Is overhead amortized over many items?
- Can we optimize hot paths?
- How does it compare to graph re-instantiation?

### 4. Channel Management

**Complexity**:
- Different strategies need different channel setups
- Broadcast: N channels (one per block)
- Competing: 1 shared channel
- Selective: M channels (one per route)

**Risk**: Easy to get wrong, causing subtle bugs

**Mitigation**:
- Strategy-specific channel factory methods
- Clear contracts and tests
- Validation in debug builds

---

## Performance Analysis

### Overhead Sources

1. **Type Detection**: Checking if T is IEpochStream<>
2. **Channel Creation**: Creating downstream channels
3. **Item Unwrapping**: Enumerating source epoch stream
4. **Item Routing**: Writing to downstream channels
5. **Item Re-Wrapping**: Downstream consuming from channels

### Baseline Comparison

| Approach | Per-Epoch Overhead | Per-Item Overhead |
|----------|-------------------|-------------------|
| Current (epoch blocks) | Low | High (container sharing) |
| Option 2 (edge unwrap/wrap) | Medium (channel setup) | Low (item routing) |
| Graph-level (plain blocks) | Low | Medium (epoch checks) |

### Key Questions

1. Can we cache type detection results?
2. Can we reuse channels across epochs?
3. How does channel overhead compare to graph instantiation?

**Hypothesis**: For epochs with many items, channel setup overhead is amortized, and per-item routing is efficient.

---

## Test Scenarios

### Functional Tests

1. **Broadcast with Epoch Streams**
   - Source emits epoch stream with 100 items
   - Broadcast to 3 downstream blocks
   - Each block receives all 100 items
   - Epoch vectors match

2. **Selective Routing with Epoch Streams**
   - Source emits epoch stream with mixed items
   - Route even/odd items to different blocks
   - Each block receives correct items
   - Epoch vectors match

3. **Competing Consumers with Epoch Streams**
   - Source emits epoch stream with 100 items
   - 3 competing consumers
   - All items processed exactly once
   - Load distribution is reasonable

4. **Fan-In with Epoch Streams**
   - Two sources emit epoch streams
   - Fan-in merges streams
   - Downstream receives all items from both
   - Epoch vectors indicate both sources

5. **Epoch Vector Propagation**
   - Source A emits epoch {A=1}
   - Source B emits epoch {B=1}
   - After fan-in: epoch {A=1, B=1}
   - Epoch metadata preserved through graph

### Performance Tests

1. **Unwrap/Wrap Overhead**
   - Measure channel creation time
   - Measure item routing time
   - Compare with direct routing

2. **Throughput**
   - Process 1M items across 10 epochs
   - Measure total time
   - Compare with current approach

3. **Backpressure**
   - Slow downstream consumer
   - Verify upstream is throttled
   - No deadlocks or data loss

4. **Memory Profiling**
   - Track channel buffer usage
   - Check for leaks
   - Measure GC pressure

---

## Open Questions

1. **Channel Reuse**: Can we reuse channels across epochs to reduce overhead?
2. **Type Caching**: Can we cache type detection to avoid repeated reflection?
3. **Buffering Strategy**: What buffer sizes are optimal for downstream channels?
4. **Error Handling**: If downstream fails, how to propagate back through channels?
5. **Cancellation**: How to cancel epoch stream routing mid-flight?
6. **Partial Consumption**: What if downstream doesn't consume all items?

---

## Next Steps

### Design Phase
- [ ] Finalize edge routing algorithm
- [ ] Design ChannelBackedEpochStream implementation
- [ ] Design strategy-specific channel management
- [ ] Design error handling for channel failures

### Prototype Phase
- [ ] Implement epoch-aware edge detection
- [ ] Implement unwrap/wrap logic
- [ ] Implement ChannelBackedEpochStream
- [ ] Adapt broadcast, selective, competing strategies

### Validation Phase
- [ ] Run functional tests for all topologies
- [ ] Run performance benchmarks
- [ ] Validate backpressure behavior
- [ ] Compare with baseline

---

## Success Criteria

✅ All topology patterns work correctly  
✅ Epoch boundaries detected via stream boundaries  
✅ Per-item overhead <10% vs current  
✅ Backpressure works without deadlocks  
✅ Epoch vector propagation is correct  
✅ No container sharing bugs  

---

## References

- [Research Plan](../research-plan.md)
- [Context Analysis](../notes/context-analysis.md)
- [Architectural Mismatch Research](../../architectural-mismatch-epoch-routing/README.md)
- [Edge Routing Implementation](/poc/DataFlow.POC/Core/ReflectionHelper.cs)
- [Edge Strategies](/poc/DataFlow.POC/Core/EdgeStrategy.cs)
