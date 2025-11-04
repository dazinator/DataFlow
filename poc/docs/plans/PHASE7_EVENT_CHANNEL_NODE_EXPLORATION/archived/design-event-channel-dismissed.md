# ❌ EventChannelNode Design (DISMISSED)

> **⚠️ ARCHIVED**: This design was explored during Phase 7 but was NOT ADOPTED.  
> **Pivot Date**: 2025-11-03  
> **Reason**: Channel-based approach required significant graph execution refactoring (~10h), couldn't guarantee event ordering, and was over-engineered for actual use cases.  
> **Alternative Adopted**: Hybrid EpochLifecycleNode wrapping coordinator with implicit auto-registration (~100 LOC vs 450 LOC).  
> **See**: `/poc/docs/plans/PHASE7_EVENT_CHANNEL_NODE_EXPLORATION/archived/README.md` for full pivot rationale.

---

## Status
🚧 **Draft** - ~~Under POC Exploration (Phase 7)~~ **DISMISSED**

## Overview

`EventChannelNode` is a proposed graph-native mechanism for propagating events (particularly epoch lifecycle events) through the DataFlow graph using channels. Unlike `IBlock` which transforms data, `EventChannelNode` is a pure routing and broadcasting construct similar to `BufferNode`.

## Problem Statement

### Current Approach: Centralized Coordinator

The current `EpochLifecycleCoordinator` uses a centralized observer pattern:

```csharp
public sealed class EpochLifecycleCoordinator
{
    private readonly List<IEpochLifecycleParticipant> _participants = new();
    private readonly object _lock = new();
    
    public void RegisterParticipant(IEpochLifecycleParticipant participant)
    {
        lock (_lock) { _participants.Add(participant); }
    }
    
    public async ValueTask NotifyEpochCreatedAsync(EpochVector epoch, IBlockContext block, CT ct)
    {
        IEpochLifecycleParticipant[] participants;
        lock (_lock) { participants = _participants.ToArray(); }
        
        foreach (var participant in participants)
            await participant.OnEpochCreatedAsync(epoch, block, ct);
    }
}
```

**Issues:**
1. **Centralized Locking**: Requires `lock` for thread-safe participant access
2. **Implicit Wiring**: Not visible in graph topology
3. **Sequential Only**: No natural way to broadcast in parallel
4. **Limited Composability**: Can't route or transform events through blocks
5. **Hidden Dependencies**: Blocks' event dependencies not apparent in graph

### Proposed Approach: Graph-Native Event Channels

Model events as first-class graph nodes with explicit connections:

```csharp
// Define event channel in graph
var epochEvents = builder.EventChannel<EpochLifecycleEvent>(capacity: 100, name: "epoch-events");

// Blocks connect explicitly to event channel
builder.Connect(epochEvents, trackingBlock);
builder.Connect(epochEvents, metricsBlock);

// Events can be routed through transforms
builder.AddTransform("filter-aligned", e => e.Type == EventType.GlobalAligned ? e : null)
    .Connect(epochEvents, "filter-aligned")
    .Connect("filter-aligned", checkpointBlock);
```

**Benefits:**
1. **No Locking**: Async channels handle concurrency naturally
2. **Graph-Native**: Connections visible in graph topology
3. **Flexible Delivery**: Sequential or broadcast via edge strategies
4. **Composable**: Events flow through standard blocks (transform, route, etc.)
5. **Explicit**: Block event dependencies clear from graph structure

## Architecture

### EventChannelNode Structure

```csharp
public class EventChannelNode
{
    public EventChannelNode(Type eventType, int capacity, string? name = null)
    {
        EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
        Capacity = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));
        Name = name;
    }
    
    public string? Name { get; }
    public Type EventType { get; }
    public int Capacity { get; }
    
    public string GetName() => !string.IsNullOrEmpty(Name) ? Name : "<unnamed-event-channel>";
    
    public override string ToString()
        => $"EventChannelNode({GetName()}, {EventType.Name}, Capacity={Capacity})";
}

public class EventChannelNode<TEvent> : EventChannelNode
{
    public EventChannelNode(int capacity, string? name = null)
        : base(typeof(TEvent), capacity, name)
    {
    }
}
```

**Design Notes:**
- Similar to `BufferNode` but specialized for events
- Generic variant `EventChannelNode<TEvent>` for type safety
- Non-block node type (no `IBlock` interface)
- Channel-backed (created by graph during wiring)

### Integration with Graph

EventChannelNode integrates with existing graph infrastructure:

```
┌──────────────────────────────────────────────┐
│          DataFlowGraph                       │
├──────────────────────────────────────────────┤
│  - Blocks (IBlock)                           │
│  - BufferNodes (data buffering)              │
│  - EventChannelNodes (event distribution) ← NEW
│  - Edges (connections)                       │
└──────────────────────────────────────────────┘
```

### Event Routing Strategies

#### Sequential Delivery (EventEdgeStrategy)

Events delivered one at a time in order:

```csharp
public class SequentialEventEdgeStrategy : EdgeStrategy
{
    public override EdgeType EdgeType => EdgeType.Event;
    
    public override async Task RouteAsync<T>(
        IAsyncEnumerable<T> source,
        IReadOnlyList<Channel<T>> targetChannels,
        CancellationToken ct)
    {
        // Write to each target sequentially
        await foreach (var item in source.WithCancellation(ct))
        {
            foreach (var channel in targetChannels)
            {
                await channel.Writer.WriteAsync(item, ct);
            }
        }
        
        // Complete all targets
        foreach (var channel in targetChannels)
        {
            channel.Writer.Complete();
        }
    }
}
```

**Use Case:** Epoch lifecycle events requiring ordered processing

#### Broadcast Delivery (BroadcastEventEdgeStrategy)

Events sent to all targets in parallel:

```csharp
public class BroadcastEventEdgeStrategy : EdgeStrategy
{
    public override EdgeType EdgeType => EdgeType.BroadcastEvent;
    
    public override async Task RouteAsync<T>(
        IAsyncEnumerable<T> source,
        IReadOnlyList<Channel<T>> targetChannels,
        CancellationToken ct)
    {
        // Write to all targets simultaneously
        await foreach (var item in source.WithCancellation(ct))
        {
            var tasks = targetChannels.Select(ch => ch.Writer.WriteAsync(item, ct).AsTask());
            await Task.WhenAll(tasks);
        }
        
        foreach (var channel in targetChannels)
        {
            channel.Writer.Complete();
        }
    }
}
```

**Use Case:** Metrics collection, logging, observability

### Graph Builder API

```csharp
public class DataFlowGraphBuilder
{
    // Create event channel
    public EventChannelNode<TEvent> EventChannel<TEvent>(
        int capacity = 100, 
        string? name = null)
    {
        var node = new EventChannelNode<TEvent>(capacity, name);
        _eventChannels.Add(node);
        return node;
    }
    
    // Connect block to event channel (block receives events)
    public DataFlowGraphBuilder ConnectEvents<TEvent>(
        EventChannelNode<TEvent> eventChannel,
        IBlock<TEvent, TOut> block,
        EventDeliveryMode mode = EventDeliveryMode.Sequential)
    {
        var strategy = mode == EventDeliveryMode.Sequential
            ? new SequentialEventEdgeStrategy(...)
            : new BroadcastEventEdgeStrategy(...);
            
        var edge = new Edge(eventChannel, block, strategy);
        _edges.Add(edge);
        return this;
    }
    
    // Emit events from block to event channel
    public DataFlowGraphBuilder EmitEvents<TEvent>(
        IBlock<TIn, TEvent> block,
        EventChannelNode<TEvent> eventChannel)
    {
        Connect(block, eventChannel);
        return this;
    }
}
```

## Event Types

### Approach A: Separate Channels per Event Type

Each lifecycle event has its own channel:

```csharp
public record EpochCreatedEvent(EpochVector Epoch, IBlockContext Block);
public record EpochCompletedEvent(EpochVector Epoch, IBlockContext Block);
public record GlobalAlignmentEvent(EpochVector Watermark);

// In graph builder
var createdEvents = builder.EventChannel<EpochCreatedEvent>(name: "epoch-created");
var completedEvents = builder.EventChannel<EpochCompletedEvent>(name: "epoch-completed");
var alignedEvents = builder.EventChannel<GlobalAlignmentEvent>(name: "global-aligned");

// Blocks subscribe to specific event types
builder.ConnectEvents(createdEvents, trackingBlock);
builder.ConnectEvents(alignedEvents, checkpointBlock);
```

**Pros:**
- Type-safe at compile time
- Clear which blocks receive which events
- No filtering needed

**Cons:**
- More graph nodes (3 channels vs 1)
- Need to emit to multiple channels

### Approach B: Single Channel with Event Base Class

One channel for all lifecycle events:

```csharp
public abstract record EpochLifecycleEvent(EpochVector Epoch);

public record EpochCreatedEvent(EpochVector Epoch, IBlockContext Block) 
    : EpochLifecycleEvent(Epoch);
    
public record EpochCompletedEvent(EpochVector Epoch, IBlockContext Block)
    : EpochLifecycleEvent(Epoch);
    
public record GlobalAlignmentEvent(EpochVector Epoch)
    : EpochLifecycleEvent(Epoch);

// Single channel
var lifecycleEvents = builder.EventChannel<EpochLifecycleEvent>(name: "lifecycle");

// Blocks filter events they care about
builder.AddTransform("filter-created", 
    e => e is EpochCreatedEvent ? e : null)
    .ConnectEvents(lifecycleEvents, "filter-created")
    .ConnectEvents("filter-created", trackingBlock);
```

**Pros:**
- Single channel to manage
- Easy to add new event types
- Can process all events in one place

**Cons:**
- Runtime type checking needed
- Less type safety
- Null handling for filtered events

### Approach C: Configuration-Based Filtering

Channel with subscription configuration:

```csharp
public enum EventType
{
    Created = 1,
    Completed = 2,
    Aligned = 4
}

public record EpochLifecycleEvent(EventType Type, EpochVector Epoch, ...);

// Connect with filter
builder.ConnectEvents(lifecycleEvents, trackingBlock, 
    filter: EventType.Created | EventType.Aligned);
```

**Pros:**
- Flexible subscription
- Single channel
- Type-safe event delivery

**Cons:**
- Configuration complexity
- Less discoverable
- Custom filtering logic needed

### Recommended Approach

**Recommendation: Approach A (Separate Channels)**

**Rationale:**
1. **Type Safety**: Compile-time guarantees for event types
2. **Clarity**: Explicit which blocks receive which events
3. **Performance**: No runtime filtering needed
4. **Simplicity**: Standard graph semantics, no special filtering

**Trade-off Accepted:** Slightly more graph nodes, but clearer architecture.

## Usage Examples

### Basic Event Channel

```csharp
// Create event channel for epoch creation events
var epochCreated = builder.EventChannel<EpochCreatedEvent>(capacity: 100, name: "epoch-created");

// Tracking block receives epoch created events
builder.ConnectEvents(epochCreated, trackingBlock);

// Metrics block also receives events (broadcast)
builder.ConnectEvents(epochCreated, metricsBlock);

// Source emits events
builder.AddSource<EpochCreatedEvent>("epoch-source", ...)
    .EmitEvents("epoch-source", epochCreated);
```

### Event Routing and Filtering

```csharp
var lifecycleEvents = builder.EventChannel<EpochLifecycleEvent>();

// Route only specific epochs to expensive processing block
builder.AddRouter("route-by-epoch", event => 
    event.Epoch.GetSequenceFor("main") % 10 == 0 ? "checkpoint" : "skip")
    .ConnectEvents(lifecycleEvents, "route-by-epoch")
    .ConnectEvents("route-by-epoch", checkpointBlock, route: "checkpoint");
```

### Event Transformation

```csharp
// Transform events before delivery
builder.AddTransform("enrich-event", event => 
    new EnrichedEvent(event, metadata: CollectMetadata()))
    .ConnectEvents(rawEvents, "enrich-event")
    .ConnectEvents("enrich-event", consumerBlock);
```

### Multiple Event Consumers

```csharp
var alignmentEvents = builder.EventChannel<GlobalAlignmentEvent>();

// Sequential delivery to critical blocks
builder.ConnectEvents(alignmentEvents, trackingBlock1, EventDeliveryMode.Sequential);
builder.ConnectEvents(alignmentEvents, trackingBlock2, EventDeliveryMode.Sequential);

// Broadcast to monitoring blocks
builder.ConnectEvents(alignmentEvents, metricsCollector, EventDeliveryMode.Broadcast);
builder.ConnectEvents(alignmentEvents, logger, EventDeliveryMode.Broadcast);
```

## EF Core Integration Example

### Current Approach

```csharp
// Coordinator pattern
var coordinator = new EpochLifecycleCoordinator();
var trackingBlock = new EntityTrackingBlock<Product, AppDbContext>(...);
coordinator.RegisterParticipant(trackingBlock);

// Hidden event wiring
await coordinator.NotifyEpochCreatedAsync(epoch, block, ct);
```

### EventChannelNode Approach

```csharp
// Explicit event wiring in graph
var epochCreated = builder.EventChannel<EpochCreatedEvent>(name: "epoch-created");
var epochCompleted = builder.EventChannel<EpochCompletedEvent>(name: "epoch-completed");
var globalAligned = builder.EventChannel<GlobalAlignmentEvent>(name: "global-aligned");

// Tracking block connects to events it needs
var trackingBlock = builder.AddBlock<EpochCreatedEvent, Unit>("tracker", 
    sp => new EntityTrackingBlock<Product, AppDbContext>(...));

builder.ConnectEvents(epochCreated, trackingBlock);
builder.ConnectEvents(globalAligned, trackingBlock);

// Graph emits events at appropriate times
// (Integration with GlobalEpochAlignment)
```

### Updated EntityTrackingBlock

```csharp
public class EntityTrackingBlock<T, TContext> : IBlock<EpochLifecycleEvent, Unit>
    where T : class
    where TContext : DbContext
{
    // ProcessAsync receives events through channel
    public async IAsyncEnumerable<Unit> ProcessAsync(
        IAsyncEnumerable<EpochLifecycleEvent> events,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var evt in events.WithCancellation(ct))
        {
            switch (evt)
            {
                case EpochCreatedEvent e:
                    await OnEpochCreatedAsync(e.Epoch, e.Block, ct);
                    break;
                    
                case GlobalAlignmentEvent e:
                    await OnGlobalAlignedAsync(e.Watermark, ct);
                    break;
            }
            
            yield return Unit.Value; // Event processed
        }
    }
}
```

## Performance Considerations

### Channel Overhead

**Channel Operations:**
- `WriteAsync()`: ~50-100ns (bounded channel)
- `ReadAsync()`: ~50-100ns
- Total per event: ~100-200ns

**Current Approach:**
- Method invocation: ~5-10ns
- Lock acquisition: ~20-50ns (uncontended)
- Total per participant: ~25-60ns

**Analysis:**
- Channel-based: ~2-4x slower per participant
- But: No locking contention with multiple threads
- Async benefits: Better concurrency, natural backpressure

### Backpressure

Bounded channels provide natural backpressure:
```csharp
// If event channel is full, producer waits
await eventChannel.Writer.WriteAsync(event, ct); // Blocks if capacity reached
```

**Capacity Guidelines:**
- Small (10-50): Fast synchronization, tighter coupling
- Medium (100-500): Balanced performance
- Large (1000+): Decoupled but higher memory

### Memory Allocation

**Per Event:**
- Event object: ~40-80 bytes (record type)
- Channel overhead: ~24 bytes (internal buffer slot)
- Total: ~64-104 bytes per event

**Per EventChannelNode:**
- Channel instance: ~200 bytes
- Graph node overhead: ~100 bytes
- Total: ~300 bytes per event channel

**Acceptable?** Yes, for <100 event channels per graph.

## Error Handling

### Event Delivery Failures

If a block throws during event processing:

```csharp
public async Task RouteAsync<T>(...)
{
    try
    {
        await foreach (var item in source.WithCancellation(ct))
        {
            foreach (var channel in targetChannels)
            {
                await channel.Writer.WriteAsync(item, ct);
            }
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Event delivery failed");
        
        // Complete remaining channels
        foreach (var channel in targetChannels)
        {
            channel.Writer.Complete(ex);
        }
        
        throw; // Propagate to graph executor
    }
}
```

**Policy Options:**
1. **Fail Fast** (Default): Stop entire graph on first error
2. **Continue Others**: Complete remaining channels, log error
3. **Circuit Breaker**: Skip failing block after N failures

### Consumer Failures

If event consumer block throws:

```csharp
// In block's ProcessAsync
try
{
    await foreach (var evt in events)
    {
        await HandleEventAsync(evt);
        yield return result;
    }
}
catch (Exception ex)
{
    _logger.LogError(ex, "Event consumer failed");
    throw; // Propagate to graph
}
```

**Recommendation:** Fail fast for critical events (lifecycle), log and continue for non-critical (metrics).

## Ordering Guarantees

### Within Single Channel

**Guarantee:** Events delivered in order they are written.

**Reason:** Channels maintain FIFO ordering.

### Across Multiple Channels

**No Guarantee:** Events from different channels may arrive out of order.

**Example:**
```csharp
// Time 0: Write Created event
await createdChannel.Writer.WriteAsync(created, ct);

// Time 1: Write Completed event  
await completedChannel.Writer.WriteAsync(completed, ct);

// Consumer may receive in any order if reading from both channels
```

**Mitigation:** Use single channel with event base class if strict ordering across event types is required.

### Sequential Delivery

Sequential edge strategy guarantees order within channel:

```csharp
// Events delivered to each block in order
foreach (var channel in targetChannels) // Sequential!
{
    await channel.Writer.WriteAsync(item, ct);
}
```

## Lifecycle Management

### EventChannelNode Lifecycle

1. **Creation**: `builder.EventChannel<T>(...)`
2. **Wiring**: `builder.ConnectEvents(...)`
3. **Initialization**: Graph creates backing channel
4. **Execution**: Events flow through channel
5. **Completion**: Channel completed when source completes
6. **Disposal**: Channel disposed by graph cleanup

### Resource Cleanup

```csharp
public async ValueTask DisposeAsync()
{
    // Complete all event channels
    foreach (var eventChannel in _eventChannels)
    {
        eventChannel.Writer.Complete();
    }
    
    // Wait for consumers to drain
    await Task.WhenAll(_consumers.Select(c => c.Completion));
    
    // Dispose resources
    foreach (var channel in _eventChannels)
    {
        // Channels are ValueTask-completed, no explicit disposal needed
    }
}
```

## Testing Strategy

### Unit Tests

1. **EventChannelNode Creation**
   - Verify properties set correctly
   - Test generic variant type safety
   - Validate capacity constraints

2. **Sequential Delivery**
   - Single consumer receives events in order
   - Multiple consumers receive events sequentially
   - Order preserved across consumers

3. **Broadcast Delivery**
   - All consumers receive all events
   - Events delivered in parallel
   - No ordering guarantee between consumers

4. **Error Handling**
   - Consumer throws exception
   - Channel write fails
   - Partial completion scenarios

### Integration Tests

5. **EF Core Integration**
   - Tracking block receives lifecycle events
   - Transaction boundaries correct
   - No events lost or duplicated

6. **Composability**
   - Events flow through TransformBlock
   - Events routed by RouterBlock
   - Events filtered conditionally

7. **Performance**
   - Compare vs coordinator baseline
   - Measure latency per event
   - Validate memory usage

### Benchmark Tests

8. **Event Delivery Latency**
   - 1, 3, 10 participants
   - Sequential vs broadcast
   - Compare with current approach

9. **Throughput**
   - Events per second
   - Under various concurrency levels
   - Impact of backpressure

10. **Memory Allocation**
    - Per event allocation
    - Channel overhead
    - GC pressure

## Migration Path

### Phase 1: Introduce EventChannelNode (Opt-In)

Keep existing coordinator, add EventChannelNode as alternative:

```csharp
// Option A: Use coordinator (existing)
coordinator.RegisterParticipant(trackingBlock);

// Option B: Use event channels (new)
builder.ConnectEvents(epochEvents, trackingBlock);
```

### Phase 2: Update Examples

Convert EpochAnchoringDemo to use EventChannelNode:
- Demonstrate new approach
- Validate real-world usage
- Collect feedback

### Phase 3: Deprecate Coordinator (If Successful)

After validation:
1. Mark `EpochLifecycleCoordinator` as `[Obsolete]`
2. Provide automatic migration tool
3. Update all documentation

### Phase 4: Remove Coordinator (Future)

Eventually remove coordinator if EventChannelNode proves superior.

## Open Questions

1. **Hybrid Approach?** Support both coordinator and channels?
2. **Event Priority?** Should some events skip queue?
3. **Dead Letter Channel?** Where do failed events go?
4. **Event Replay?** Support for event replay/reprocessing?
5. **Cross-Graph Events?** Events across multiple graph instances?

## Alternatives Considered

### Alternative 1: Keep Coordinator, Add Async

Add async channels to coordinator without graph integration:

**Pros:** Less invasive change
**Cons:** Still centralized, not graph-native

### Alternative 2: Hybrid Observer + Channel

Use coordinator to broadcast, channels for delivery:

**Pros:** Backward compatible
**Cons:** Complexity of two systems

### Alternative 3: Event Bus Pattern

Separate event bus outside graph:

**Pros:** Decoupled from graph
**Cons:** Not graph-native, hidden dependencies

**Decision:** Full EventChannelNode approach for graph-native benefits.

## References

- [BufferNode Implementation](../../DataFlow.POC/Core/BufferNode.cs)
- [EdgeStrategy Patterns](../../DataFlow.POC/Core/EdgeStrategy.cs)
- [Lifecycle Events](./lifecycle-events.md)
- [Phase 6: Epoch Lifecycle](../plans/PHASE6_EPOCH_LIFECYCLE.md)

## Change Log

- **2025-11-03**: Initial design document created
- **Status**: Draft, under POC exploration

---

**Next Steps:**
1. Review and validate design with stakeholders
2. Create ADR for event type handling decision
3. Begin core implementation (Phase 7.2)
4. Test with EF Core integration
