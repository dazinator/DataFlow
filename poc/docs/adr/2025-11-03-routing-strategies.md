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

### Edge Strategy Pattern (NEW - Implemented)

The POC now formalizes delivery semantics through the **Edge Strategy Pattern**. This addresses the expert feedback to "promote Edge strategies (broadcast, competing, cloning, routed)."

#### Edge Strategy Types

All edge strategies inherit from `EdgeStrategy` base class and define how items flow from source to target(s):

```csharp
public enum EdgeType
{
    Broadcast,   // All targets get all items (independent channels)
    Competing,   // Each item consumed once (shared channel)
    Cloning,     // All targets get independent clones
    Routed       // Items routed based on criteria
}
```

#### 1. BroadcastEdgeStrategy (Default)

Multiple edges from one source → Each edge gets its own channel.

**How it works:**
- Creates one channel per target block
- Writes each item to ALL target channels
- No competition between consumers
- Each consumer reads at its own pace (independent backpressure)

```csharp
var producer = new ProducerBlock<int>("producer", ...);
var consumer1 = new ProcessorBlock<int>("consumer1", ...);
var consumer2 = new ProcessorBlock<int>("consumer2", ...);

// Broadcast edge - both consumers get all items
var broadcastEdge = new Edge(
    producer,
    new[] { consumer1, consumer2 },
    new BroadcastEdgeStrategy(BufferMode.Bounded, 100));

graph.AddEdge(broadcastEdge);

// Result: consumer1 and consumer2 both receive ALL items
```

**Alternative syntax using separate edges (equivalent):**
```csharp
graph.Connect(producer, consumer1);  // Edge 1 with Channel A
graph.Connect(producer, consumer2);  // Edge 2 with Channel B
// Graph writes each item to BOTH Channel A and Channel B
```

#### 2. CompetingEdgeStrategy (NEW - Enables Concurrent Processing)

Multiple blocks read from the SAME shared channel → True competing consumers.

**How it works:**
- Creates ONE shared channel for all targets
- Writes each item ONCE to the shared channel
- First available consumer gets the item
- Natural load balancing across consumers

```csharp
var producer = new ProducerBlock<int>("producer", ...);
var processor1 = new ProcessorBlock<int>("processor1", ...);
var processor2 = new ProcessorBlock<int>("processor2", ...);
var processor3 = new ProcessorBlock<int>("processor3", ...);

// Competing edge - processors compete for items
var competingEdge = new Edge(
    producer,
    new[] { processor1, processor2, processor3 },
    new CompetingEdgeStrategy(BufferMode.Bounded, 50));

graph.AddEdge(competingEdge);

// Result: Each item processed by ONE processor (competing consumers)
// This provides concurrent processing without ConcurrentProcessorBlock!
```

**This replaces ConcurrentProcessorBlock:**
```csharp
// OLD WAY (still works but transitional):
var concurrentProc = new ConcurrentProcessorBlock<int>(
    "processor", 
    ProcessItem, 
    maxConcurrency: 4);

// NEW WAY (better separation of concerns):
var proc1 = new ProcessorBlock<int>("proc1", ProcessItem);
var proc2 = new ProcessorBlock<int>("proc2", ProcessItem);
var proc3 = new ProcessorBlock<int>("proc3", ProcessItem);
var proc4 = new ProcessorBlock<int>("proc4", ProcessItem);

var competingEdge = new Edge(
    source,
    new[] { proc1, proc2, proc3, proc4 },
    new CompetingEdgeStrategy());
```

**Benefits:**
- ✅ Concurrency moved to edge layer (graph responsibility)
- ✅ Processors remain simple (no internal concurrency logic)
- ✅ Parallelism explicit in topology
- ✅ Easy to tune concurrency (add/remove processor blocks)

#### 3. CloningEdgeStrategy (NEW)

Similar to broadcast but clones items for each target.

**How it works:**
- Creates one channel per target block
- Clones each item before writing to each channel
- Each consumer gets an independent copy (mutations don't affect others)
- Requires items to implement `ICloneable` or be value types

```csharp
var producer = new ProducerBlock<DataItem>("producer", ...);
var processor1 = new ProcessorBlock<DataItem>("processor1", ...);
var processor2 = new ProcessorBlock<DataItem>("processor2", ...);

// Cloning edge - each processor gets independent clones
var cloningEdge = new Edge(
    producer,
    new[] { processor1, processor2 },
    new CloningEdgeStrategy(BufferMode.Bounded, 100));

graph.AddEdge(cloningEdge);

// Result: Both processors get clones - mutations are isolated
```

**Use case:** When downstream consumers mutate items and need isolation.

**Legacy note:** This replaces the `shouldClone` parameter from legacy `BroadcastBlock`:
```csharp
// OLD: new BroadcastBlock<T>(name, shouldClone: item => item.Clone())
// NEW: CloningEdgeStrategy
```

### How Broadcasting Works in POC

The POC achieves broadcasting through **edge-level fanout** (now formalized as `BroadcastEdgeStrategy`):

1. **Multiple edges from one source** → Each edge gets its own channel (or single edge with multiple targets)
2. **Graph writes to all channels** → No competition between consumers
3. **Each consumer reads at its own pace** → Independent backpressure per path

Example:
```csharp
// Using BroadcastEdgeStrategy (explicit)
var broadcastEdge = new Edge(
    producer,
    new[] { consumer1, consumer2 },
    new BroadcastEdgeStrategy());
graph.AddEdge(broadcastEdge);

// Or using separate Connect calls (same result)
graph.Connect(producer, consumer1);  // Edge 1 with Channel A
graph.Connect(producer, consumer2);  // Edge 2 with Channel B

// Graph writes each item to BOTH Channel A and Channel B
// consumer1 and consumer2 are NOT competing - each gets all items
```

### Competing Consumers vs Broadcasting (NOW SUPPORTED)

**Status: ✅ FULLY IMPLEMENTED via CompetingEdgeStrategy**

The model now natively supports **both** patterns through edge strategies:

#### Broadcasting Pattern (BroadcastEdgeStrategy)
Multiple targets, each with own channel:
```csharp
// Each consumer gets ALL items
var broadcastEdge = new Edge(
    source,
    new[] { target1, target2 },
    new BroadcastEdgeStrategy());
// Item written to both channels → broadcast
```

#### Competing Consumers Pattern (CompetingEdgeStrategy - NEW)
Multiple targets share ONE channel:
```csharp
// Targets compete for items - each item consumed once
var competingEdge = new Edge(
    source,
    new[] { target1, target2 },
    new CompetingEdgeStrategy());
// Item written to shared channel → only one consumer gets it
```

### True Competing Consumers (✅ NOW SUPPORTED)

Implemented via `CompetingEdgeStrategy`:

```csharp
var source = new ProducerBlock<int>("source", ...);
var proc1 = new ProcessorBlock<int>("proc1", ...);
var proc2 = new ProcessorBlock<int>("proc2", ...);

// All target blocks share ONE channel
var competingEdge = new Edge(
    source,
    new[] { proc1, proc2 },
    new CompetingEdgeStrategy(BufferMode.Bounded, 100));

graph.AddEdge(competingEdge);

// Only one consumer gets each item - true competing consumers!
```

**Implementation details:**
- `CompetingEdgeStrategy.CreateChannels()` creates ONE shared channel
- All targets get the SAME `ChannelReader` instance
- First available consumer wins each item
- Natural load balancing and concurrency

**Current Status:** ✅ Fully implemented and tested. See `EdgeStrategyTests.cs` for examples.

### Broadcast Item Copying (✅ NOW SUPPORTED)

Legacy BroadcastBlock supported optional item copying:
```csharp
new BroadcastBlock<T>(name, shouldClone: item => item.Clone())
```

**In POC:** ✅ NOW SUPPORTED via `CloningEdgeStrategy`

```csharp
var cloningEdge = new Edge(
    source,
    new[] { target1, target2 },
    new CloningEdgeStrategy(BufferMode.Bounded, 100));

// Graph clones items before writing to each target's channel
// Each target gets an independent copy
```

**Implementation:**
- Items must implement `ICloneable`, be value types, or be immutable (like strings)
- Each target channel receives a cloned copy
- Mutations in one consumer don't affect others
- Perfect for scenarios where downstream blocks modify items

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

### ConcurrentProcessorBlock - Still Needed?

**Status: ✅ SOLVED - CompetingEdgeStrategy provides better alternative**

**Legacy Design:** ProcessorBlock supports max concurrency via competing consumers reading from one channel.

**POC Design:** Two options for concurrent processing:

#### Option 1: Multiple Processor Blocks with CompetingEdgeStrategy (RECOMMENDED)
```csharp
var proc1 = new ProcessorBlock<int>("proc1", ProcessItem);
var proc2 = new ProcessorBlock<int>("proc2", ProcessItem);
var proc3 = new ProcessorBlock<int>("proc3", ProcessItem);
var proc4 = new ProcessorBlock<int>("proc4", ProcessItem);

var competingEdge = new Edge(
    source,
    new[] { proc1, proc2, proc3, proc4 },
    new CompetingEdgeStrategy(BufferMode.Bounded, 100));

graph.AddEdge(competingEdge);

// Each processor competes for items - true concurrent processing!
// Concurrency level = number of processor blocks (4 in this example)
```

**Benefits:**
- ✅ Concurrency is a graph/orchestration concern (not block concern)
- ✅ Processors remain simple - just business logic
- ✅ Easy to tune concurrency (add/remove blocks)
- ✅ Explicit parallelism in topology
- ✅ Natural load balancing

#### Option 2: ConcurrentProcessorBlock (TRANSITIONAL - Use Option 1 Instead)
```csharp
var concurrentProc = new ConcurrentProcessorBlock<int>(
    "processor", 
    ProcessItem, 
    maxConcurrency: 4);

// Block internally manages concurrent processing
```

**Verdict:** 
- ✅ CompetingEdgeStrategy is NOW the recommended approach
- ⚠️ ConcurrentProcessorBlock remains for backward compatibility but is TRANSITIONAL
- 📝 Documentation updated to guide users toward CompetingEdgeStrategy

**Key Insight:** With CompetingEdgeStrategy, concurrency becomes an orchestration feature, not a block implementation detail. This aligns with the expert recommendation: "Once CompetingEdge exists, you can drop ConcurrentProcessorBlock and ConcurrentTransformerBlock because parallelism becomes an orchestration concern."

### ConcurrentTransformerBlock - Channels in Blocks?

**Status: ✅ SOLVED - CompetingEdgeStrategy provides better alternative**

Similar situation to ConcurrentProcessorBlock: ConcurrentTransformerBlock manages internal channel for concurrent work.

**Question:** "Shouldn't channels be managed via edges?"

**Answer:** Yes! Now they can be via CompetingEdgeStrategy.

**Recommended approach:**
```csharp
// Create multiple simple transformer instances
var transform1 = new SimpleTransformerBlock<int, string>("t1", Transform);
var transform2 = new SimpleTransformerBlock<int, string>("t2", Transform);
var transform3 = new SimpleTransformerBlock<int, string>("t3", Transform);
var transform4 = new SimpleTransformerBlock<int, string>("t4", Transform);

// Input edge: competing among transformers
var competingInput = new Edge(
    source,
    new[] { transform1, transform2, transform3, transform4 },
    new CompetingEdgeStrategy());

graph.AddEdge(competingInput);

// Output edges: merge results to downstream
graph.Connect(transform1, downstream);
graph.Connect(transform2, downstream);
graph.Connect(transform3, downstream);
graph.Connect(transform4, downstream);

// Result: Concurrent transformation with competing consumers!
```

**Benefits:**
- ✅ Transformers remain simple (just transformation logic)
- ✅ Concurrency explicit in topology
- ✅ Easy to add/remove workers
- ✅ No internal channel management in blocks

**Verdict:** 
- ✅ CompetingEdgeStrategy is NOW the recommended approach
- ⚠️ ConcurrentTransformerBlock remains for backward compatibility but is TRANSITIONAL
- 📝 Better separation of concerns - edges handle concurrency, blocks handle logic

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

### Loss of Strong Typing in Processing Loop

**Concern:** Using `object` types instead of generic types in the main processing paths may impact performance in a high-performance library.

**Areas Where Strong Typing Is Lost:**

1. **Channel Types** (`EdgeStrategy.CreateChannels`)
   - Channels are created as `Channel<object>` instead of `Channel<T>`
   - Location: `EdgeStrategy.CreateChannels()` returns `ChannelWriter<object>` and `ChannelReader<object>`
   - Impact: Boxing for value types, loss of compile-time type safety

2. **Item Routing** (`EdgeStrategy.RouteItemAsync`)
   - Items are passed as `object` parameter in the hot path
   - Location: `RouteItemAsync(object item, ...)` 
   - Impact: Boxing/unboxing overhead for value types, runtime type checks

3. **Clone Function** (`BroadcastEdgeStrategy._cloneFunc`)
   - Clone function signature: `Func<object, object>?`
   - Location: `BroadcastEdgeStrategy` constructor and `RouteItemAsync` 
   - Impact: Boxing when cloning value types, no compile-time type checking

4. **Block Execution** (`IBlock.ExecuteAsync`)
   - Block input/output streams use `IAsyncEnumerable<object>`
   - Location: Untyped `IBlock` interface
   - Impact: Boxing/unboxing at block boundaries

**Performance Implications:**
- **Boxing overhead**: Value types (int, double, struct, etc.) are boxed when written to channels and unboxed when read
- **GC pressure**: Each box operation allocates on the heap, increasing garbage collection frequency
- **Cache locality**: Boxed values have worse cache performance due to indirection

**Mitigation Options:**
1. **Preserve type information for future optimization**: Edge already has `DataType` property that preserves the original type information
   - Could use reflection with `Edge.DataType` to create properly typed `Channel<T>` instances at runtime
   - EdgeStrategy could store type info for typed channel creation
   - This would eliminate boxing for value types while maintaining flexibility
   - **ACTION ITEM**: Document as high-priority optimization path for production use
   
2. **Typed fast path**: Add generic overloads for common scenarios while keeping object-based path for heterogeneous graphs
   - Common value types (int, long, double) could have specialized implementations
   - Fallback to object-based approach for less common types
   
3. **Benchmark first**: Measure actual performance impact before optimizing - may be acceptable for POC and many real-world scenarios
   - Focus on value type throughput benchmarks
   - Measure GC pressure under realistic loads

**Action Required:** Performance testing should focus on:
- Value type throughput (int, long, struct payloads)
- GC allocation rates under load
- Comparison with strongly-typed alternatives
- **Validating type-preserving optimization approach using Edge.DataType**

**Current Status:** Acceptable tradeoff for POC validating architecture patterns. **Type information is preserved in Edge.DataType** for future optimization. Should be prioritized if moving beyond POC for production use.

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

### ✅ Completed - Edge Strategy Pattern Implementation

#### Phase 1: Initial POC (Previous Commit)
- ✅ Added `AutoConnect()` API for refactor-friendly chaining
- ✅ Added `ConnectMany()` API for multiple connections
- ✅ Removed Unbounded buffer mode
- ✅ Enforced bounded buffers in builder API
- ✅ Added comprehensive documentation of routing/broadcasting behavior
- ✅ Explained RouteOutput method

#### Phase 2: Edge Strategy Pattern (Current Commit)
- ✅ **Implemented EdgeStrategy abstraction** - Base class for delivery semantics
- ✅ **Implemented BroadcastEdgeStrategy** - All targets get all items (existing behavior formalized)
- ✅ **Implemented CompetingEdgeStrategy** - Each item consumed once (TRUE competing consumers!)
- ✅ **Implemented CloningEdgeStrategy** - All targets get independent clones
- ✅ **Updated DataFlowGraph** - Routes items via edge strategies
- ✅ **Added comprehensive tests** - 4 new tests covering all strategies (14 tests total, all passing)
- ✅ **Documented concurrent block deprecation** - ConcurrentProcessorBlock and ConcurrentTransformerBlock now transitional
- ✅ **Updated DESIGN_DECISIONS.md** - Complete edge strategy documentation

### Key Achievements

**Competing Consumers:** ✅ FULLY IMPLEMENTED
- No longer "future work"
- CompetingEdgeStrategy provides true competing consumers
- Replaces need for ConcurrentProcessorBlock/ConcurrentTransformerBlock
- Moves concurrency from block concern to orchestration concern

**Item Cloning:** ✅ FULLY IMPLEMENTED
- CloningEdgeStrategy provides item cloning
- Supports ICloneable types, value types, and immutable types
- Each consumer gets independent copy

**Edge Strategies:** ✅ ARCHITECTURE IMPROVEMENT
- Formalizes delivery semantics at edge level
- Aligns with expert recommendation: "Promote Edge strategies (broadcast, competing, cloning, routed)"
- Clean separation: blocks = logic, edges = delivery, graph = orchestration

## Buffer Nodes (✅ Phase 3 - NEW)

### Overview

Buffer Nodes are first-class representations of shared channels in the graph, providing explicit visibility and control for **fan-in** (multiple producers → single buffer) and **fan-out** (single buffer → multiple competing consumers) scenarios. Unlike regular edges that connect blocks directly, Buffer Nodes are configurable channel buffers that can be referenced and connected to multiple upstream producers and downstream consumers.

**Note:** Block-to-block connections (without BufferNode) continue to use edge-level stream merging for fan-in as before. BufferNode is specifically for scenarios requiring explicit buffer representation and control.

### Terminology

- **Fan-in**: Multiple producers writing to a single shared buffer
- **Fan-out**: Multiple consumers competing to read from a single shared buffer (each item consumed once)
- When producers and consumers are balanced (e.g., 3 producers → buffer → 3 consumers), both fan-in and fan-out occur, maintaining overall concurrency

### Motivation

Before Buffer Nodes, connecting multiple producers to multiple consumers was difficult and required workarounds:

**Without BufferNode** (complex, unclear topology):
```csharp
// Trying to connect 2 producers to 2 consumers
// Option 1: Connect each producer to each consumer (4 edges, duplicated processing)
builder.Connect(producer1, consumer1);
builder.Connect(producer1, consumer2);
builder.Connect(producer2, consumer1);
builder.Connect(producer2, consumer2);
// Problem: Each consumer receives ALL items from BOTH producers (duplication)

// Option 2: Use CompetingEdgeStrategy (only works for single source)
var competingEdge = new Edge(producer1, new[] { consumer1, consumer2 }, new CompetingEdgeStrategy());
// Problem: Can only connect ONE producer, not multiple

// Option 3: Manual intermediate block to consolidate
// Problem: Requires custom block logic, not explicit in graph
```

**With BufferNode** (clean, explicit topology):
```csharp
var buffer = builder.Buffer<int>(capacity: 100);  // name optional
builder.Connect(producer1, buffer);
builder.Connect(producer2, buffer);
builder.Connect(buffer, consumer1);
builder.Connect(buffer, consumer2);
// Clear: 2 producers write to shared buffer, 2 consumers compete for items
```

### Graph Node Types

The POC now has three first-class node types:

| Node Type | Purpose | Multiple Inputs | Multiple Outputs | Transformation Logic |
|-----------|---------|-----------------|------------------|---------------------|
| **Block** (`IBlock`) | Data transformation | ❌ Single source | ✅ Multiple (via edges) | ✅ Yes (`ExecuteAsync`) |
| **Buffer** (`BufferNode`) | Shared channel buffer | ✅ Multiple producers | ✅ Multiple consumers | ❌ No (pure buffering) |
| **Edge** | Connection between nodes | Single source | Single/Multiple targets | ❌ No (routing only) |

**Why BufferNode isn't IBlock:** IBlock is designed for single-input transformation pipelines. It doesn't have semantics to:
- Accept input from multiple independent source streams
- Provide multiple independent output streams (it provides one output that edges can fan out)
- Represent a stateful buffer that consolidates and redistributes items

### Design

**BufferNode** is a separate entity from `IBlock`:
- Does not have transformation logic (no `ExecuteAsync`)
- Backed by a single `Channel<T>` with configurable capacity
- Enables multiple upstream producers (fan-in) and multiple downstream consumers (fan-out with competition)

### Usage Example

```csharp
var builder = new DataFlowGraphBuilder("fan-in-fan-out-flow");

// Create a shared buffer with capacity 100 (name is optional)
var buffer = builder.Buffer<int>(capacity: 100, name: "shared-buffer");

// Connect multiple producers to buffer (fan-in)
builder.Connect(producer1, buffer);
builder.Connect(producer2, buffer);

// Connect buffer to multiple consumers (fan-out - consumers compete)
builder.Connect(buffer, consumer1);
builder.Connect(buffer, consumer2);

var graph = builder.Build();
```

### What BufferNode Enables Beyond Standard Channels

Since BufferNode is backed by `Channel<T>`, it inherits backpressure control, bounded capacity, and concurrent read/write support. What makes BufferNode unique:

1. **Explicit Fan-In/Fan-Out Topology**
   - Makes multi-producer/multi-consumer patterns visible in the graph
   - No need for workarounds like all-to-all connections or intermediate blocks

2. **Automatic Producer Completion Tracking**
   - Channel closes only when ALL producers complete (not just one)
   - Handles complex coordination automatically

3. **Channel Optimization**
   - Detects single reader/writer scenarios and optimizes accordingly
   - Applied transparently based on actual connections

### Implementation Details

**Builder API:**
```csharp
// Generic version for compile-time type safety
public BufferNode<T> Buffer<T>(string name, int capacity = 100)

// Non-generic version for runtime type specification
public BufferNode Buffer(string name, Type dataType, int capacity = 100)

// Connect block to buffer
public DataFlowGraphBuilder Connect(IBlock source, BufferNode target)

// Connect buffer to block
public DataFlowGraphBuilder Connect(BufferNode source, IBlock target)
```

**Graph Execution:**
- Buffer nodes create a single typed `Channel<T>` during pipeline building
- Writers are shared among all producer blocks
- Readers are shared among all consumer blocks
- Channel completion is coordinated - closes only when all producers complete
- Uses `TypedBufferNodeRouter` for zero-boxing routing

### Comparison with Edge Strategies

| Feature | BufferNode | CompetingEdgeStrategy |
|---------|------------|----------------------|
| **Fan-In (Multiple Producers)** | ✅ Yes | ❌ No (single source) |
| **Fan-Out (Multiple Consumers)** | ✅ Yes | ✅ Yes |
| **Explicit Channel Reference** | ✅ Yes (named node) | ❌ No (implicit in edge) |
| **Configurable Capacity** | ✅ Yes (per buffer) | ✅ Yes (per edge) |
| **Graph Visibility** | ✅ High (visible as node) | ⚠️ Medium (edge metadata) |

**Use CompetingEdgeStrategy when:**
- Single producer with multiple competing consumers
- Don't need to reference the shared channel by name

**Use BufferNode when:**
- Multiple producers need to feed the same buffer (fan-in)
- Need explicit visibility of buffer in graph topology
- Want to reference the buffer by name for clarity
- Complex multi-producer, multi-consumer scenarios

### Status

✅ **FULLY IMPLEMENTED**
- BufferNode class with type safety
- Builder API with `Buffer()` and `Connect()` methods
- Graph execution with proper channel management
- Producer completion tracking for multi-producer scenarios
- 7 comprehensive tests covering all scenarios:
  - Single producer to single consumer
  - Multiple producers to single consumer
  - Single producer to multiple consumers (competing)
  - Multiple producers to multiple consumers
  - Backpressure enforcement
  - Type compatibility validation

### Future Work (Remaining Items)
- ⏭️ Dynamic routing support (requires graph-level route creation)
- ⏭️ Graph-level producer concurrency management (ProducerGroup pattern)
- ⏭️ Performance benchmarking (RouterBlock, BatchBlock, BufferNode)
- ⏭️ Consider struct vs record for RoutedItem<T>

### Transitional Components
- ⚠️ ConcurrentProcessorBlock - Kept for backward compatibility, use CompetingEdgeStrategy instead
- ⚠️ ConcurrentTransformerBlock - Kept for backward compatibility, use CompetingEdgeStrategy instead
- ℹ️ Both documented with migration guidance
