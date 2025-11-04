# ❌ ADR: Event Type Handling in EventChannelNode (DISMISSED)

> **⚠️ ARCHIVED**: This ADR was explored during Phase 7 but was NOT ADOPTED.  
> **Pivot Date**: 2025-11-03  
> **Reason**: Separate channels couldn't guarantee strict event ordering across types, which is critical for epoch lifecycle correctness.  
> **Alternative Adopted**: Hybrid EpochLifecycleNode wrapping coordinator with implicit auto-registration.  
> **See**: `/poc/docs/plans/PHASE7_EVENT_CHANNEL_NODE_EXPLORATION/archived/README.md` for full pivot rationale.

---

**Date:** 2025-11-03  
**Status:** ~~Proposed~~ **DISMISSED**  
**Context:** Phase 7 - EventChannelNode Exploration

## Context

EventChannelNode needs a mechanism to handle multiple types of epoch lifecycle events:
- `EpochCreatedEvent` (when epoch starts)
- `EpochCompletedEvent` (when block completes epoch)
- `GlobalAlignmentEvent` (when all blocks complete epoch)

We need to decide how blocks subscribe to specific event types they care about.

## Decision Drivers

1. **Type Safety**: Compile-time guarantees reduce runtime errors
2. **Clarity**: Easy to understand which blocks receive which events
3. **Performance**: Minimal overhead for event routing
4. **Composability**: Works well with standard DataFlow blocks (transform, route, etc.)
5. **Extensibility**: Easy to add new event types in future
6. **Graph Visualization**: Clear representation in graph diagrams

## Options Considered

### Option A: Separate Channels per Event Type

Create distinct EventChannelNode for each event type:

```csharp
// Three separate channels
var epochCreated = builder.EventChannel<EpochCreatedEvent>(name: "epoch-created");
var epochCompleted = builder.EventChannel<EpochCompletedEvent>(name: "epoch-completed");
var globalAligned = builder.EventChannel<GlobalAlignmentEvent>(name: "global-aligned");

// Blocks connect to specific event types
builder.ConnectEvents(epochCreated, trackingBlock);
builder.ConnectEvents(globalAligned, checkpointBlock);
```

**Event Type Definitions:**
```csharp
public record EpochCreatedEvent(EpochVector Epoch, IBlockContext Block);
public record EpochCompletedEvent(EpochVector Epoch, IBlockContext Block);
public record GlobalAlignmentEvent(EpochVector Watermark);
```

**Pros:**
- ✅ **Strong Type Safety**: Compile-time guarantees
- ✅ **Explicit Subscriptions**: Clear which blocks get which events
- ✅ **No Runtime Filtering**: Direct delivery, better performance
- ✅ **Independent Channels**: Each event type has independent capacity and backpressure
- ✅ **Graph Visualization**: Three nodes clearly show three event types
- ✅ **Simple Implementation**: Standard channel semantics, no special handling

**Cons:**
- ⚠️ **More Graph Nodes**: 3 nodes instead of 1 (but still manageable)
- ⚠️ **Multiple Emitters**: Need to write to multiple channels at source
- ⚠️ **Coordination Overhead**: Must manage lifecycle of 3 channels instead of 1

### Option B: Single Channel with Event Base Class

Use inheritance with single channel:

```csharp
// Event hierarchy
public abstract record EpochLifecycleEvent(EpochVector Epoch);

public record EpochCreatedEvent(EpochVector Epoch, IBlockContext Block) 
    : EpochLifecycleEvent(Epoch);
    
public record EpochCompletedEvent(EpochVector Epoch, IBlockContext Block)
    : EpochLifecycleEvent(Epoch);
    
public record GlobalAlignmentEvent(EpochVector Epoch)
    : EpochLifecycleEvent(Epoch);

// Single channel
var lifecycleEvents = builder.EventChannel<EpochLifecycleEvent>(name: "lifecycle");

// Blocks filter events using transforms
builder.AddTransform("filter-created", 
    e => e is EpochCreatedEvent created ? created : null)
    .ConnectEvents(lifecycleEvents, "filter-created")
    .ConnectEvents("filter-created", trackingBlock);
```

**Pros:**
- ✅ **Single Channel**: One node to manage
- ✅ **Easy Extension**: Add new event types without new channels
- ✅ **Unified Processing**: Can handle all events in one block if needed

**Cons:**
- ❌ **Runtime Type Checks**: Pattern matching adds overhead
- ❌ **Null Handling**: Filtered events return null, need null checks
- ❌ **Less Type Safety**: Easy to miss event types in switch statements
- ❌ **Verbose Filtering**: Need transform blocks for each filter
- ❌ **Hidden Subscriptions**: Not obvious from graph which blocks get which events

**Performance Impact:**
- Pattern matching: ~5-10ns per event
- Null propagation: Memory allocation for filtered nulls
- Additional transform blocks: ~50-100ns overhead per event

### Option C: Configuration-Based Filtering

Channel with subscription flags:

```csharp
[Flags]
public enum EventType
{
    None = 0,
    Created = 1,
    Completed = 2,
    Aligned = 4,
    All = Created | Completed | Aligned
}

public record EpochLifecycleEvent(
    EventType Type,
    EpochVector Epoch,
    IBlockContext? Block,
    EpochVector? Watermark);

// Connect with filter configuration
builder.ConnectEvents(lifecycleEvents, trackingBlock, 
    filter: EventType.Created | EventType.Aligned);
    
builder.ConnectEvents(lifecycleEvents, metricsBlock,
    filter: EventType.All);
```

**Pros:**
- ✅ **Single Channel**: One node to manage
- ✅ **Flexible Subscription**: Easy to subscribe to multiple types
- ✅ **Type Safe Filtering**: Enum-based, compile-time checks

**Cons:**
- ❌ **Custom Implementation**: Need custom filtering logic in graph
- ❌ **Runtime Filtering Overhead**: Check flags for each event/block
- ❌ **Unified Event Structure**: All events share same structure (nullable fields)
- ❌ **Less Discoverable**: Filter configuration not obvious from graph topology
- ❌ **Breaking Standard Semantics**: Custom behavior vs standard channel routing

**Implementation Complexity:**
```csharp
// Custom routing logic needed in graph
await foreach (var evt in eventSource)
{
    foreach (var (block, filter) in subscribers)
    {
        if ((evt.Type & filter) != EventType.None)
        {
            await block.Channel.Writer.WriteAsync(evt, ct);
        }
    }
}
```

### Option D: Dynamic Subscription API

Runtime subscription with event handlers:

```csharp
var lifecycleEvents = builder.EventChannel<EpochLifecycleEvent>();

// Subscribe to specific event types at runtime
trackingBlock.Subscribe<EpochCreatedEvent>(lifecycleEvents);
trackingBlock.Subscribe<GlobalAlignmentEvent>(lifecycleEvents);
```

**Pros:**
- ✅ **Flexible**: Easy to subscribe/unsubscribe at runtime
- ✅ **Single Channel**: One node to manage

**Cons:**
- ❌ **Runtime Behavior**: Not visible in graph structure
- ❌ **No Static Analysis**: Can't validate subscriptions at build time
- ❌ **Memory Overhead**: Need subscription registry
- ❌ **Complexity**: Custom subscription management outside graph

## Decision

**Selected: Option A - Separate Channels per Event Type**

## Rationale

### Primary Reasoning

1. **Type Safety is Critical**
   - Epoch lifecycle events control transaction boundaries
   - Compile-time guarantees prevent subtle bugs
   - Wrong event delivery could corrupt data

2. **Clarity Trumps Convenience**
   - Three channels make dependencies explicit
   - Graph visualization shows exact event flow
   - No hidden filtering or runtime logic

3. **Performance Matters**
   - Lifecycle events fired frequently (per epoch)
   - Zero overhead for filtering (direct delivery)
   - Independent channels allow independent tuning

4. **Aligns with DataFlow Principles**
   - Standard channel semantics (no custom behavior)
   - Composable with existing blocks
   - Follows separation of concerns

5. **Scalability**
   - Three channels is manageable overhead
   - Can optimize each channel independently
   - Easy to add more event types (just add channel)

### Addressing Concerns

**Concern:** "More graph nodes (3 vs 1)"
- **Response:** Negligible. Even complex graphs have 50-100+ nodes. Three extra nodes for crucial lifecycle events is acceptable.
- **Mitigation:** Group in graph visualization under "Lifecycle Events" category.

**Concern:** "Must emit to multiple channels"
- **Response:** Source emits naturally to different channels at different times:
  ```csharp
  // Different lifecycle phases
  await createdChannel.Writer.WriteAsync(new EpochCreatedEvent(...), ct);
  // ... processing ...
  await completedChannel.Writer.WriteAsync(new EpochCompletedEvent(...), ct);
  // ... alignment ...
  await alignedChannel.Writer.WriteAsync(new GlobalAlignmentEvent(...), ct);
  ```
- **Reality:** These events occur at distinct phases, not simultaneously.

**Concern:** "Coordination overhead"
- **Response:** Minimal. Graph already coordinates many channels. Three more is marginal.
- **Benefit:** Independent capacity and backpressure tuning per event type.

## Consequences

### Positive

1. **✅ Type-Safe Event Delivery**
   - Compiler enforces correct event types
   - No runtime type checks or casting
   - Reduced possibility of bugs

2. **✅ Explicit Graph Topology**
   - Clear which blocks receive which events
   - Easy to trace event flow in diagrams
   - Better debugging and understanding

3. **✅ Zero Filtering Overhead**
   - Direct delivery to interested blocks
   - No pattern matching or conditionals
   - Better performance

4. **✅ Independent Configuration**
   - Each event type has own capacity
   - Independent backpressure handling
   - Tune per event type characteristics

5. **✅ Simple Implementation**
   - Standard channel semantics throughout
   - No custom filtering logic
   - Reuse existing graph infrastructure

### Negative

1. **⚠️ Slightly More Graph Nodes**
   - Mitigation: Group in visualizations, document clearly
   - Impact: Negligible for typical graphs

2. **⚠️ Multiple Channels to Wire**
   - Mitigation: Provide helper methods in builder
   - Example: `builder.ConnectLifecycleEvents(trackingBlock)`

3. **⚠️ Coordination Complexity**
   - Mitigation: Graph handles coordination automatically
   - Impact: Marginal increase in graph management

### Neutral

1. **Adding New Event Types**
   - Requires new channel (not just new class)
   - But: Makes new events explicit in graph (good for visibility)
   - Process: Define event, create channel, wire connections

## Implementation Guidelines

### Creating Event Channels

```csharp
// Standard pattern for lifecycle events
var epochCreated = builder.EventChannel<EpochCreatedEvent>(
    capacity: 100, 
    name: "epoch-created");
    
var epochCompleted = builder.EventChannel<EpochCompletedEvent>(
    capacity: 50,  // Smaller capacity - less critical
    name: "epoch-completed");
    
var globalAligned = builder.EventChannel<GlobalAlignmentEvent>(
    capacity: 10,  // Very small - infrequent but critical
    name: "global-aligned");
```

### Connecting Blocks

```csharp
// Explicit connections
builder.ConnectEvents(epochCreated, trackingBlock);
builder.ConnectEvents(epochCompleted, metricsBlock);
builder.ConnectEvents(globalAligned, trackingBlock);
builder.ConnectEvents(globalAligned, checkpointBlock);
```

### Helper Method for Common Pattern

```csharp
public static class LifecycleEventExtensions
{
    public static DataFlowGraphBuilder ConnectLifecycleEvents(
        this DataFlowGraphBuilder builder,
        EventChannelNode<EpochCreatedEvent> created,
        EventChannelNode<EpochCompletedEvent> completed,
        EventChannelNode<GlobalAlignmentEvent> aligned,
        IBlock block,
        bool receiveCreated = true,
        bool receiveCompleted = false,
        bool receiveAligned = true)
    {
        if (receiveCreated)
            builder.ConnectEvents(created, block);
        if (receiveCompleted)
            builder.ConnectEvents(completed, block);
        if (receiveAligned)
            builder.ConnectEvents(aligned, block);
            
        return builder;
    }
}

// Usage
builder.ConnectLifecycleEvents(
    epochCreated, epochCompleted, globalAligned,
    trackingBlock,
    receiveCreated: true,
    receiveCompleted: false,
    receiveAligned: true);
```

## Validation

This decision will be validated through:

1. **Implementation**: Build EventChannelNode with separate channels
2. **Testing**: Convert EF Core tracking block to use event channels
3. **Benchmarking**: Compare performance vs current coordinator
4. **Feedback**: Review with team, document lessons learned

Success criteria:
- Type-safe event delivery ✓
- Clear graph topology ✓
- Performance within 10% of baseline ✓
- Positive developer feedback ✓

## Alternatives for Future Consideration

If separate channels prove problematic, we could:

1. **Hybrid Approach**: Separate channels for critical events (Created, Aligned), combined channel for metrics events
2. **Channel Groups**: Syntactic sugar to manage related channels as a group
3. **Event Families**: Higher-level abstraction over multiple channels

But: Start with simple separate channels, evolve if needed.

## References

- [EventChannelNode Design](../design/event-channel-node.md)
- [Phase 7 Plan](../plans/PHASE7_EVENT_CHANNEL_NODE_EXPLORATION.md)
- [Lifecycle Events Design](../design/lifecycle-events.md)
- [BufferNode Implementation](../../DataFlow.POC/Core/BufferNode.cs)

## Related ADRs

- None yet (this is the first ADR for EventChannelNode)

## Review Notes

- **Review Date**: TBD
- **Reviewed By**: TBD
- **Outcome**: TBD (Approved / Rejected / Modified)

---

**Status:** Proposed (awaiting implementation and validation)
