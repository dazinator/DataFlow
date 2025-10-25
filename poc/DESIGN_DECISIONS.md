# POC Design Decisions and Clarifications

## Routing: Static vs Dynamic

### Current Implementation: Static Routing
The current POC implements **static routing** where all routes must be defined upfront when building the graph. This is achieved through:

1. **RouterBlock** - Tags items with route keys
2. **RouteFilterBlock** - Filters items based on route keys
3. **Graph structure** - All paths (filter blocks) are created at build time

Example:
```csharp
var router = new RouterBlock<int>("router", i => i % 2 == 0 ? "even" : "odd");
var evenFilter = new RouteFilterBlock<int>("even-filter", "even");
var oddFilter = new RouteFilterBlock<int>("odd-filter", "odd");

graph.AddBlock(router)
    .AddBlock(evenFilter)
    .AddBlock(oddFilter)
    .Connect(router, evenFilter)
    .Connect(router, oddFilter);
```

### Dynamic Routing: Future Design

To support **dynamic routing** (like the legacy StructuredRoutingBlock), we would need:

#### Option 1: Dynamic Route Creation at Graph Level
```csharp
public class DynamicRoutingEdge<T> : Edge
{
    private readonly Func<string, IBlock> _routeFactory;
    private readonly ConcurrentDictionary<string, IBlock> _routes = new();
    
    // Graph would detect new routes at runtime and wire up channels dynamically
}
```

**Pros:**
- Keeps blocks pure
- Graph still owns topology

**Cons:**
- Adds complexity to graph execution
- Topology not fully known upfront

#### Option 2: Self-Contained Dynamic Router Block
```csharp
public class DynamicRouterBlock<T> : BlockBase<T, object>
{
    private readonly ConcurrentDictionary<string, Channel<T>> _routeChannels = new();
    private readonly Func<string, IDataFlow> _routeFlowFactory;
    
    // Block manages its own downstream channels and dataflows
}
```

**Pros:**
- Simpler to implement
- Self-contained

**Cons:**
- Violates pure block principle
- Channel management in block, not graph

#### Recommendation
For dynamic routing support, **Option 1** better aligns with the POC's separation of concerns. However, it requires:
1. Graph to support dynamic edge creation at runtime
2. Mechanism for graph to be notified of new routes
3. Safe concurrent channel creation and wiring

This is feasible but was excluded from the initial POC to keep the design simple.

## Broadcasting and Competing Consumers

### How Broadcasting Works in POC

The POC achieves broadcasting through **edge-level fanout**:

1. **Multiple edges from one source** → Each edge gets its own channel
2. **Graph writes to all channels** → No competition between consumers
3. **Each consumer reads at its own pace** → Independent backpressure per path

Example:
```csharp
graph.AddBlock(producer)
    .AddBlock(consumer1)
    .AddBlock(consumer2)
    .Connect(producer, consumer1)  // Edge 1 with Channel A
    .Connect(producer, consumer2); // Edge 2 with Channel B

// Graph writes each item to BOTH Channel A and Channel B
// consumer1 and consumer2 are NOT competing - each gets all items
```

### Competing Consumers vs Broadcasting

The model naturally supports **both** patterns:

#### Broadcasting Pattern
Multiple edges from same source, each with its own channel:
```csharp
// Each consumer gets ALL items
Connect(source, target1);  // Channel A
Connect(source, target2);  // Channel B
// Item written to both channels → broadcast
```

#### Competing Consumers Pattern
Multiple blocks read from the SAME upstream source:
```csharp
// Multiple processor blocks as targets
var processor1 = new ProcessorBlock<int>("proc1", ...);
var processor2 = new ProcessorBlock<int>("proc2", ...);

Connect(source, processor1);  // Channel A
Connect(source, processor2);  // Channel B

// Still broadcasting! Each processor gets all items.
```

**Wait, that's still broadcasting!** To get true competing consumers, you need:

#### True Competing Consumers (Not Currently Supported)
This would require a special edge type or block that manages a shared channel:

```csharp
// Hypothetical competing consumer edge
public class CompetingConsumerEdge : Edge
{
    // All target blocks share ONE channel
    // Only one consumer gets each item
}
```

**Current Status:** POC only supports broadcasting. Competing consumers would need to be added as a feature.

### Broadcast Item Copying

Legacy BroadcastBlock supported optional item copying:
```csharp
new BroadcastBlock<T>(name, shouldClone: item => item.Clone())
```

**In POC:** This is NOT currently supported. All broadcasts pass the same reference.

**To Add Item Copying:**
```csharp
public class CloningEdge<T> : Edge where T : ICloneable
{
    public bool ShouldClone { get; init; }
    
    // Graph would clone items before writing to this edge's channel
}
```

This could be added as an edge-level feature without changing block semantics.

## Routing and Competing Consumers

### RouteFilterBlock Behavior

**Key Question:** "Multiple filter blocks might be competing consumers where if an item was filtered out, the other consumer won't get it?"

**Answer:** No - They are NOT competing consumers. Here's why:

```csharp
router → [evenFilter]  // Edge 1 → Channel A
      → [oddFilter]    // Edge 2 → Channel B

// Graph writes each RoutedItem<int> to BOTH Channel A and Channel B
// evenFilter reads from Channel A, filters, outputs matching items
// oddFilter reads from Channel B, filters, outputs matching items
```

Each filter has its **own channel**, so:
- evenFilter filters Channel A (sees all items, passes even ones)
- oddFilter filters Channel B (sees all items, passes odd ones)
- No competition - they're reading from different channels

## Concurrent Processing

### ConcurrentProcessorBlock - Needed?

**Legacy Design:** ProcessorBlock supports max concurrency via competing consumers reading from one channel.

**POC Design:** Two options for concurrent processing:

#### Option 1: Multiple Processor Blocks (Currently Used)
```csharp
var proc1 = new ProcessorBlock<int>("proc1", ProcessItem);
var proc2 = new ProcessorBlock<int>("proc2", ProcessItem);

Connect(source, proc1);
Connect(source, proc2);

// Each processor gets ALL items (broadcasting)
// To get competing consumers, need special edge type
```

**Problem:** This is broadcasting, not concurrent processing of the same stream.

#### Option 2: ConcurrentProcessorBlock (As Implemented)
```csharp
var concurrentProc = new ConcurrentProcessorBlock<int>(
    "processor", 
    ProcessItem, 
    maxConcurrency: 4);

// Block internally manages concurrent processing
```

**Verdict:** ConcurrentProcessorBlock IS needed because:
- Without competing consumer edges, can't achieve concurrency via multiple blocks
- Alternative requires adding competing consumer feature to graph

**Keep ConcurrentProcessorBlock** for now, but document that it's a workaround until competing consumer edges are implemented.

### ConcurrentTransformerBlock - Channels in Blocks?

Similar situation: ConcurrentTransformerBlock manages internal channel for concurrent work.

**Question:** "Shouldn't channels be managed via edges?"

**Answer:** Ideally yes, but without competing consumer support:
- Can't connect multiple transformer blocks and have them compete
- Need internal channel for parallel work coordination

**Options:**
1. Keep ConcurrentTransformerBlock (pragmatic)
2. Add competing consumer edges (better separation of concerns)
3. Remove concurrent variants (limits functionality)

**Recommendation:** Keep for now, document as limitation to address in future versions.

## Concurrent Producers

**Question:** "Can we add separate producer blocks, connect them to the same target, with max X running concurrently?"

**Use Case:** 3 database producers, max 2 concurrent.

**Current POC:** ConcurrentProducerBlock handles this internally.

**Better Design:** Graph-level producer concurrency management:

```csharp
public class ProducerGroup
{
    public List<IBlock> Producers { get; init; }
    public int MaxConcurrency { get; init; }
}

// Graph would use Parallel.ForEachAsync with MaxDegreeOfParallelism
```

**Recommendation:** This is a graph orchestration concern, not a block concern. Should be added to DataFlowGraph:

```csharp
graph.AddProducerGroup(
    maxConcurrency: 2,
    producer1, producer2, producer3);
```

## Performance Concerns

### RouterBlock - Record Creation

**Concern:** "Creating a new record per item might cause perf issues with high volumes"

**Analysis:**
- `record RoutedItem<T>` creates a new allocation per item
- For millions of items, this could cause GC pressure

**Alternatives:**
1. **Struct instead of record** - Stack allocation, but mutable concerns
2. **Object pooling** - Complex, might not be worth it
3. **Benchmark first** - Measure before optimizing

**Action:** Need to benchmark against legacy router block.

### BatchBlock - Performance Comparison

**Action Required:** Benchmark POC BatchBlock vs legacy BatchBlock to compare:
- Throughput
- Memory allocation
- CPU usage
- Batching accuracy (size and time-window)

## Type System - Untyped IBlock

**Question:** "Do we really need the untyped version?"

**Answer:** Yes, for graph orchestration:

```csharp
// Graph needs to store blocks without knowing their types
private readonly List<IBlock> _blocks;

// Can't use List<IBlock<TIn, TOut>> because each block has different types
```

**Alternative:** Use reflection and dynamic types, but more complex and slower.

**Verdict:** Keep untyped interface for graph management, typed interface for compile-time safety.

## Summary of Actions

### Immediate (Done in this commit)
- ✅ Added `AutoConnect()` API for refactor-friendly chaining
- ✅ Added `ConnectMany()` API for multiple connections
- ✅ Removed Unbounded buffer mode
- ✅ Enforced bounded buffers in builder API
- ✅ Added comprehensive documentation of routing/broadcasting behavior
- ✅ Explained RouteOutput method

### Future Work (Documented for Discussion)
- ⏭️ Dynamic routing support (requires graph-level route creation)
- ⏭️ Competing consumer edges (alternative to concurrent block variants)
- ⏭️ Item cloning for broadcast edges
- ⏭️ Graph-level producer concurrency management
- ⏭️ Performance benchmarking (RouterBlock, BatchBlock)
- ⏭️ Consider struct vs record for RoutedItem<T>

### Won't Do
- ❌ Remove ConcurrentProcessorBlock (needed without competing consumers)
- ❌ Remove ConcurrentTransformerBlock (needed without competing consumers)
- ❌ Remove untyped IBlock (needed for graph management)
