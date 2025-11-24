# Control Flow Topologies Guide

**Status**: ✅ Current  
**Audience**: POC Users  
**Last Updated**: 2025-11-24

---

## Table of Contents

1. [Overview](#overview)
2. [Available Routing Patterns](#available-routing-patterns)
3. [Broadcast Pattern](#broadcast-pattern)
4. [Competing Consumers Pattern](#competing-consumers-pattern)
5. [Selective Content-Based Routing](#selective-content-based-routing)
6. [When to Use Each Pattern](#when-to-use-each-pattern)
7. [Performance Characteristics](#performance-characteristics)
8. [Common Scenarios](#common-scenarios)
9. [Decision Tree](#decision-tree)
10. [Advanced Topics](#advanced-topics)

---

## Overview

The DataFlow POC provides three primary **control flow topologies** (routing patterns) for managing how data flows between blocks. These patterns are implemented using **Edge Strategies** that define delivery semantics at the edge level, keeping blocks simple and focused on business logic.

### Core Principle: Edge-Level Routing

```
┌─────────────────────────────────────────────────────────┐
│  Blocks = Business Logic (pure transformation)          │
│  Edges  = Delivery Semantics (how items flow)           │
│  Graph  = Orchestration (topology & execution)          │
└─────────────────────────────────────────────────────────┘
```

This separation allows you to:
- ✅ Change routing behavior without modifying block code
- ✅ Test blocks in isolation from topology
- ✅ Build flexible topologies with simple building blocks

### The Three Patterns

| Pattern | Edge Strategy | Use Case | Items Per Route |
|---------|---------------|----------|-----------------|
| **Broadcast** | `BroadcastEdgeStrategy` | Fan-out to all consumers | All items to all targets |
| **Competing Consumers** | `CompetingEdgeStrategy` | Load balancing, concurrency | Each item to one target |
| **Selective Routing** | `SelectiveRoutingEdgeStrategy<T>` | Content-based routing | Each item to matching target |

---

## Available Routing Patterns

### Pattern 1: Broadcast (Fan-Out)

**Purpose**: Send every item to ALL target blocks simultaneously.

**Implementation**: Each target gets its own dedicated channel. Items are written to all channels **concurrently** using `Task.WhenAll`.

```csharp
var producer = BlockHelpers.CreateProducer<int>("source", ctx => GenerateNumbers());
var logger = BlockHelpers.CreateProcessor<int>("logger", async (item, ctx) => 
    Console.WriteLine($"Log: {item}"));
var monitor = BlockHelpers.CreateProcessor<int>("monitor", async (item, ctx) => 
    await RecordMetric(item));

// Create broadcast edge - both targets receive ALL items
var broadcastEdge = new Edge(
    producer,
    new[] { logger, monitor },
    new BroadcastEdgeStrategy(BufferMode.Bounded, 100));

var graph = new DataFlowGraphBuilder("monitoring-flow")
    .AddBlock(producer)
    .AddBlock(logger)
    .AddBlock(monitor)
    .AddEdge(broadcastEdge)
    .Build();
```

**Key Characteristics**:
- ✅ All consumers receive every item
- ✅ Concurrent writes to avoid serialization bottleneck
- ✅ Independent backpressure per consumer
- ⚠️ N concurrent channel writes (N = number of targets)

### Pattern 2: Competing Consumers (Load Balancing)

**Purpose**: Distribute items among multiple workers for concurrent processing. Each item is consumed by exactly ONE target.

**Implementation**: All targets share a single channel. First available consumer gets each item.

```csharp
var producer = BlockHelpers.CreateProducer<Order>("orders", ctx => GetOrders());
var worker1 = BlockHelpers.CreateProcessor<Order>("worker1", ProcessOrder);
var worker2 = BlockHelpers.CreateProcessor<Order>("worker2", ProcessOrder);
var worker3 = BlockHelpers.CreateProcessor<Order>("worker3", ProcessOrder);

// Create competing edge - workers compete for items
var competingEdge = new Edge(
    producer,
    new[] { worker1, worker2, worker3 },
    new CompetingEdgeStrategy(BufferMode.Bounded, 100));

var graph = new DataFlowGraphBuilder("order-processing")
    .AddBlock(producer)
    .AddBlock(worker1)
    .AddBlock(worker2)
    .AddBlock(worker3)
    .AddEdge(competingEdge)
    .Build();
```

**Key Characteristics**:
- ✅ Each item processed exactly once
- ✅ Natural load balancing
- ✅ Easy to scale (add/remove workers)
- ✅ Shared channel for efficiency
- ❌ Cannot route based on item content

### Pattern 3: Selective Content-Based Routing

**Purpose**: Route items to specific targets based on item content inspection. Each item goes ONLY to its matching route.

**Implementation**: Uses a route selector function to determine the destination for each item. Creates dedicated channels per route.

```csharp
var producer = BlockHelpers.CreateProducer<Order>("orders", ctx => GetOrders());
var highPriorityProcessor = BlockHelpers.CreateProcessor<Order>("high-priority", ProcessUrgent);
var normalPriorityProcessor = BlockHelpers.CreateProcessor<Order>("normal-priority", ProcessNormal);

// Define route mapping
var routeMapping = new Dictionary<string, IBlock>
{
    ["high"] = highPriorityProcessor,
    ["normal"] = normalPriorityProcessor
};

// Create selective routing strategy
var routingStrategy = new SelectiveRoutingEdgeStrategy<Order>(
    routeKeyToBlock: routeMapping,
    routeSelector: order => order.Priority == "high" ? "high" : "normal");

var edge = new Edge(
    producer,
    new[] { highPriorityProcessor, normalPriorityProcessor },
    routingStrategy);

var graph = new DataFlowGraphBuilder("priority-routing")
    .AddBlock(producer)
    .AddBlock(highPriorityProcessor)
    .AddBlock(normalPriorityProcessor)
    .AddEdge(edge)
    .Build();
```

**Key Characteristics**:
- ✅ Routes based on item content
- ✅ Zero allocation overhead (no wrapper records)
- ✅ O(1) route lookup
- ✅ Each item sent only to matching route
- ✅ Scales efficiently with route count

---

## Broadcast Pattern

### How Broadcasting Works

The POC implements broadcasting through **concurrent writes** to multiple independent channels:

```csharp
// Simplified implementation concept
public override async Task RouteTypedItemAsync<T>(
    T item,
    Dictionary<IBlock, ChannelWriter<T>> typedWriters,
    CancellationToken cancellationToken)
{
    // Write to all channels concurrently (not sequentially!)
    var writeTasks = typedWriters.Values
        .Select(writer => writer.WriteAsync(item, cancellationToken).AsTask())
        .ToArray();
    
    await Task.WhenAll(writeTasks).ConfigureAwait(false);
}
```

**This means**:
- All target channels receive items **simultaneously** (not round-robin)
- Each consumer processes at its own pace (independent backpressure)
- No sequential delivery - all broadcasts happen in parallel
- If one consumer is slow, it doesn't block others

### When to Use Broadcasting

✅ **Good Use Cases**:
- Logging and auditing (one target processes, another logs)
- Monitoring pipelines (process + metrics collection)
- Multiple independent transformations of same data
- Fan-out scenarios where all targets need every item

❌ **Bad Use Cases**:
- Content-based routing (use `SelectiveRoutingEdgeStrategy` instead)
- Load balancing (use `CompetingEdgeStrategy` instead)
- High-volume scenarios with many targets (broadcasting overhead)

### Broadcasting with Cloning

For scenarios where downstream consumers might mutate items, use `CloningEdgeStrategy`:

```csharp
var cloningEdge = new Edge(
    producer,
    new[] { mutatingProcessor1, mutatingProcessor2 },
    new CloningEdgeStrategy(BufferMode.Bounded, 100));
```

**Requirements**:
- Items must implement `ICloneable`, be value types, or be immutable (like strings)
- Each target receives an independent clone
- Mutations in one consumer don't affect others

---

## Competing Consumers Pattern

### How Competing Works

Multiple consumers share a **single channel**. The channel ensures each item is consumed exactly once:

```csharp
// Simplified implementation concept
public override (Dictionary<IBlock, object> writers, Dictionary<IBlock, object> readers) 
    CreateTypedChannels(Type dataType, IBlock sourceBlock, IReadOnlyList<IBlock> targetBlocks)
{
    var writers = new Dictionary<IBlock, object>();
    var readers = new Dictionary<IBlock, object>();

    // Create ONE shared channel
    var (writer, reader) = TypedChannelFactory.CreateTypedChannel(
        dataType,
        BufferMode,
        BufferCapacity,
        singleReader: false,  // Multiple readers compete
        singleWriter: false);

    // All targets share the same reader
    foreach (var target in targetBlocks)
    {
        writers[target] = writer;
        readers[target] = reader;  // Same reader instance
    }

    return (writers, readers);
}
```

### When to Use Competing Consumers

✅ **Good Use Cases**:
- Concurrent processing with identical workers
- Load balancing across multiple processors
- Scaling throughput by adding more workers
- Scenarios where each item needs processing exactly once

❌ **Bad Use Cases**:
- When items need different handling (use `SelectiveRoutingEdgeStrategy`)
- When all consumers need all items (use `BroadcastEdgeStrategy`)
- When order preservation is critical (competing may interleave)

### Concurrency vs ConcurrentProcessorBlock

**Old Way** (Concurrency inside block):
```csharp
var concurrentProc = new ConcurrentProcessorBlock<int>(
    "processor", 
    ProcessItem, 
    maxConcurrency: 4);
```

**New Way** (Concurrency at topology level):
```csharp
var proc1 = BlockHelpers.CreateProcessor<int>("proc1", ProcessItem);
var proc2 = BlockHelpers.CreateProcessor<int>("proc2", ProcessItem);
var proc3 = BlockHelpers.CreateProcessor<int>("proc3", ProcessItem);
var proc4 = BlockHelpers.CreateProcessor<int>("proc4", ProcessItem);

var competingEdge = new Edge(
    source,
    new[] { proc1, proc2, proc3, proc4 },
    new CompetingEdgeStrategy());
```

**Benefits of New Way**:
- ✅ Concurrency is a graph/orchestration concern (not block concern)
- ✅ Processors remain simple - just business logic
- ✅ Easy to tune concurrency (add/remove blocks)
- ✅ Explicit parallelism in topology
- ✅ Natural load balancing

---

## Selective Content-Based Routing

### How Selective Routing Works

Items are routed to specific targets based on content inspection using a route selector function:

```csharp
public override async Task RouteTypedItemAsync<T>(
    T item,
    Dictionary<IBlock, ChannelWriter<T>> typedWriters,
    CancellationToken cancellationToken)
{
    // Cast item to TItem to apply the route selector
    var typedItem = (TItem)(object)item!;
    
    // Get route key from item content
    var routeKey = _routeSelector(typedItem);
    
    // Look up target block (O(1) dictionary lookup)
    if (!_routeKeyToBlock.TryGetValue(routeKey, out var targetBlock))
    {
        throw new InvalidOperationException($"Unknown route key: {routeKey}");
    }
    
    // Write ONLY to the matching channel (no broadcast!)
    var writer = typedWriters[targetBlock];
    await writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
}
```

### Performance Characteristics

**Zero Overhead Design**:
- **No wrapper records**: Works directly with your item type
- **O(1) lookup**: Dictionary lookup per item
- **Zero broadcast overhead**: Each item written once
- **No filtering**: Items sent only to matching route

**Comparison with Broadcast-and-Filter** (obsolete pattern):
```
Selective Routing:       1 write per item
Broadcast-and-Filter:    N concurrent writes per item (N = route count)
                         + N filter checks (wasted work)
```

### When to Use Selective Routing

✅ **Good Use Cases**:
- Routing by customer ID, product type, priority, region, etc.
- Routes known at build time
- High-volume scenarios (scales efficiently)
- Content-based decisions with 2+ routes

❌ **Bad Use Cases**:
- Dynamic routes created at runtime (not supported in POC)
- Load balancing without content inspection (use `CompetingEdgeStrategy`)
- All consumers need all items (use `BroadcastEdgeStrategy`)

### Example: Multi-Way Routing

```csharp
var producer = BlockHelpers.CreateProducer<Customer>("customers", ctx => GetCustomers());
var vipProcessor = BlockHelpers.CreateProcessor<Customer>("vip", ProcessVIP);
var regularProcessor = BlockHelpers.CreateProcessor<Customer>("regular", ProcessRegular);
var trialProcessor = BlockHelpers.CreateProcessor<Customer>("trial", ProcessTrial);

var routeMapping = new Dictionary<string, IBlock>
{
    ["vip"] = vipProcessor,
    ["regular"] = regularProcessor,
    ["trial"] = trialProcessor
};

var routingStrategy = new SelectiveRoutingEdgeStrategy<Customer>(
    routeKeyToBlock: routeMapping,
    routeSelector: customer => customer.Tier);  // "vip", "regular", or "trial"

var edge = new Edge(
    producer,
    new[] { vipProcessor, regularProcessor, trialProcessor },
    routingStrategy);
```

---

## When to Use Each Pattern

### Quick Reference Table

| Your Need | Pattern | Why |
|-----------|---------|-----|
| All consumers need all items | **Broadcast** | Fan-out with independent processing |
| Load balancing identical workers | **Competing** | Natural distribution, simple |
| Route by item property | **Selective Routing** | Zero overhead, O(1) lookup |
| Logging + processing | **Broadcast** | One processes, one logs |
| Scale throughput | **Competing** | Add more workers easily |
| Priority-based routing | **Selective Routing** | Route by priority field |
| Monitoring pipeline | **Broadcast** | Process + collect metrics |
| Concurrent processing | **Competing** | Topology-level concurrency |

### Decision Flow

```
Do all consumers need all items?
├─ YES → Use BroadcastEdgeStrategy
│        (Fan-out scenarios, monitoring, logging)
│
└─ NO → Do you need content-based routing?
    ├─ YES → Use SelectiveRoutingEdgeStrategy<T>
    │        (Route by customer, priority, region, etc.)
    │
    └─ NO → Use CompetingEdgeStrategy
             (Load balancing, concurrent processing)
```

---

## Performance Characteristics

### Throughput Comparison

| Pattern | Light Load (100/sec) | Medium (10K/sec) | High (100K/sec) | Notes |
|---------|---------------------|------------------|-----------------|-------|
| **Competing** | ~100 | ~10K | ~100K | Channel limit is bottleneck |
| **Selective Routing** | ~100 | ~10K | ~100K | Near optimal scaling |
| **Broadcast (2 targets)** | ~95 | ~9K | ~80K | N concurrent writes overhead |
| **Broadcast (10 targets)** | ~85 | ~7K | ~50K | Overhead increases with targets |

### Memory & CPU Overhead

| Pattern | Allocations Per Item | CPU Per Item | Channels |
|---------|---------------------|--------------|----------|
| **Competing** | 0 (zero overhead) | Minimal (1 write) | 1 shared |
| **Selective Routing** | 0 (zero overhead) | O(1) lookup + 1 write | N (per route) |
| **Broadcast** | 0 (zero overhead) | N concurrent writes | N (per target) |

### Backpressure Behavior

| Pattern | Slow Consumer Impact | Backpressure Scope |
|---------|---------------------|-------------------|
| **Competing** | Affects all (shared channel) | Shared |
| **Selective Routing** | Independent per route | Per-route |
| **Broadcast** | Independent per target | Per-target |

**Important**: 
- With **Competing**, if one consumer is slow, all consumers are affected (shared channel backs up)
- With **Broadcast** or **Selective Routing**, slow consumers only affect their own channel

---

## Common Scenarios

### Scenario 1: Load Balancing Work Items

**Requirement**: Process work items using 4 concurrent workers, each item processed exactly once.

```csharp
var producer = BlockHelpers.CreateProducer<WorkItem>("source", ctx => GetWorkItems());
var worker1 = BlockHelpers.CreateProcessor<WorkItem>("worker1", ProcessWorkItem);
var worker2 = BlockHelpers.CreateProcessor<WorkItem>("worker2", ProcessWorkItem);
var worker3 = BlockHelpers.CreateProcessor<WorkItem>("worker3", ProcessWorkItem);
var worker4 = BlockHelpers.CreateProcessor<WorkItem>("worker4", ProcessWorkItem);

var competingEdge = new Edge(
    producer,
    new[] { worker1, worker2, worker3, worker4 },
    new CompetingEdgeStrategy(BufferMode.Bounded, 100));

var graph = new DataFlowGraphBuilder("work-processing")
    .AddBlock(producer)
    .AddBlock(worker1)
    .AddBlock(worker2)
    .AddBlock(worker3)
    .AddBlock(worker4)
    .AddEdge(competingEdge)
    .Build();
```

**Why Competing**: Selective routing, no wasted work, natural load balancing.

---

### Scenario 2: Route Orders by Priority

**Requirement**: High-priority orders go to express processor, normal orders go to batch processor.

```csharp
var orderProducer = BlockHelpers.CreateProducer<Order>("orders", ctx => GetOrders());
var expressProcessor = BlockHelpers.CreateProcessor<Order>("express", ProcessExpress);
var batchProcessor = BlockHelpers.CreateProcessor<Order>("batch", ProcessBatch);

var routeMapping = new Dictionary<string, IBlock>
{
    ["high"] = expressProcessor,
    ["normal"] = batchProcessor
};

var routingStrategy = new SelectiveRoutingEdgeStrategy<Order>(
    routeKeyToBlock: routeMapping,
    routeSelector: order => order.Priority == "high" ? "high" : "normal");

var edge = new Edge(
    orderProducer,
    new[] { expressProcessor, batchProcessor },
    routingStrategy);

var graph = new DataFlowGraphBuilder("priority-routing")
    .AddBlock(orderProducer)
    .AddBlock(expressProcessor)
    .AddBlock(batchProcessor)
    .AddEdge(edge)
    .Build();
```

**Why Selective Routing**: Content-based routing with simple logic, zero overhead.

---

### Scenario 3: Monitoring Pipeline

**Requirement**: Process data while simultaneously collecting metrics and logging.

```csharp
var dataProducer = BlockHelpers.CreateProducer<DataItem>("source", ctx => GetData());
var processor = BlockHelpers.CreateProcessor<DataItem>("processor", ProcessData);
var metricsCollector = BlockHelpers.CreateProcessor<DataItem>("metrics", CollectMetrics);
var logger = BlockHelpers.CreateProcessor<DataItem>("logger", LogItem);

var broadcastEdge = new Edge(
    dataProducer,
    new[] { processor, metricsCollector, logger },
    new BroadcastEdgeStrategy(BufferMode.Bounded, 100));

var graph = new DataFlowGraphBuilder("monitoring-pipeline")
    .AddBlock(dataProducer)
    .AddBlock(processor)
    .AddBlock(metricsCollector)
    .AddBlock(logger)
    .AddEdge(broadcastEdge)
    .Build();
```

**Why Broadcast**: All consumers need to see all items independently.

---

### Scenario 4: Combined Pattern - Content Routing + Load Balancing

**Requirement**: Route orders by priority, then load balance within each priority tier.

```csharp
var orderProducer = BlockHelpers.CreateProducer<Order>("orders", ctx => GetOrders());

// High priority route with 3 competing workers
var highWorker1 = BlockHelpers.CreateProcessor<Order>("high-worker1", ProcessExpress);
var highWorker2 = BlockHelpers.CreateProcessor<Order>("high-worker2", ProcessExpress);
var highWorker3 = BlockHelpers.CreateProcessor<Order>("high-worker3", ProcessExpress);

// Normal priority route with 2 competing workers
var normalWorker1 = BlockHelpers.CreateProcessor<Order>("normal-worker1", ProcessNormal);
var normalWorker2 = BlockHelpers.CreateProcessor<Order>("normal-worker2", ProcessNormal);

// First: Route by priority using BufferNode to fan in to competing workers
var highBuffer = builder.Buffer<Order>(name: "high-priority-buffer", capacity: 100);
var normalBuffer = builder.Buffer<Order>(name: "normal-priority-buffer", capacity: 100);

var routeMapping = new Dictionary<string, IBlock>
{
    ["high"] = highBuffer,    // Route to buffer
    ["normal"] = normalBuffer  // Route to buffer
};

var routingStrategy = new SelectiveRoutingEdgeStrategy<Order>(
    routeKeyToBlock: routeMapping,
    routeSelector: order => order.Priority == "high" ? "high" : "normal");

var graph = new DataFlowGraphBuilder("priority-load-balanced")
    .AddBlock(orderProducer)
    
    // Selective routing to buffers
    .AddEdge(new Edge(
        orderProducer,
        new[] { highBuffer, normalBuffer },
        routingStrategy))
    
    // High priority: competing workers
    .AddBlock(highWorker1)
    .AddBlock(highWorker2)
    .AddBlock(highWorker3)
    .AddEdge(new Edge(
        highBuffer,
        new[] { highWorker1, highWorker2, highWorker3 },
        new CompetingEdgeStrategy()))
    
    // Normal priority: competing workers
    .AddBlock(normalWorker1)
    .AddBlock(normalWorker2)
    .AddEdge(new Edge(
        normalBuffer,
        new[] { normalWorker1, normalWorker2 },
        new CompetingEdgeStrategy()))
    
    .Build();
```

**Why Combined**: Content-based routing (priority) + load balancing (workers per tier).

---

## Decision Tree

Use this decision tree to quickly determine the right pattern:

```
┌─────────────────────────────────────────────────────┐
│  Do all consumers need to receive ALL items?        │
└─────────────────────────────────────────────────────┘
           │
           ├─ YES → BroadcastEdgeStrategy
           │        Examples:
           │        • Monitoring (process + log + metrics)
           │        • Auditing (process + audit trail)
           │        • Multiple independent transformations
           │
           └─ NO → Do items need different handling based on content?
                    │
                    ├─ YES → SelectiveRoutingEdgeStrategy<T>
                    │        Examples:
                    │        • Route by priority, customer, region
                    │        • Different processors for different item types
                    │        • Route to different pipelines based on business rules
                    │
                    └─ NO → CompetingEdgeStrategy
                             Examples:
                             • Load balancing across identical workers
                             • Concurrent processing
                             • Scale throughput by adding more blocks
```

---

## Advanced Topics

### BufferNode for Fan-In and Fan-Out

When you need multiple producers feeding multiple consumers, use `BufferNode`:

```csharp
var producer1 = BlockHelpers.CreateProducer<int>("source1", ctx => GenerateNumbers(1));
var producer2 = BlockHelpers.CreateProducer<int>("source2", ctx => GenerateNumbers(2));
var consumer1 = BlockHelpers.CreateProcessor<int>("consumer1", ProcessItem);
var consumer2 = BlockHelpers.CreateProcessor<int>("consumer2", ProcessItem);

// Create shared buffer
var buffer = builder.Buffer<int>(name: "shared-buffer", capacity: 100);

var graph = new DataFlowGraphBuilder("fan-in-fan-out")
    .AddBlock(producer1)
    .AddBlock(producer2)
    .AddBlock(consumer1)
    .AddBlock(consumer2)
    
    // Fan-in: Multiple producers to buffer
    .Connect(producer1, buffer)
    .Connect(producer2, buffer)
    
    // Fan-out: Buffer to competing consumers
    .Connect(buffer, consumer1)
    .Connect(buffer, consumer2)
    
    .Build();
```

**Result**: Both producers write to shared buffer, both consumers compete for items.

### Handling Unknown Route Keys

When using `SelectiveRoutingEdgeStrategy`, unknown route keys throw exceptions:

```csharp
var routingStrategy = new SelectiveRoutingEdgeStrategy<Order>(
    routeKeyToBlock: routeMapping,
    routeSelector: order => order.Priority ?? "unknown");  // What if Priority is null?

// If an order has Priority = "unknown" and "unknown" is not in routeMapping:
// InvalidOperationException: "Unknown route key: unknown"
```

**Best Practice**: Provide a default route or validate data upstream.

```csharp
var routeMapping = new Dictionary<string, IBlock>
{
    ["high"] = highProcessor,
    ["normal"] = normalProcessor,
    ["unknown"] = defaultProcessor  // Handle unknown cases
};

var routingStrategy = new SelectiveRoutingEdgeStrategy<Order>(
    routeKeyToBlock: routeMapping,
    routeSelector: order => order.Priority ?? "unknown");  // Safe!
```

### Order Preservation

| Pattern | Order Preserved? | Notes |
|---------|-----------------|-------|
| **Broadcast** | ✅ Per-target | Each target sees items in order |
| **Selective Routing** | ✅ Per-route | Each route sees its items in order |
| **Competing** | ❌ No guarantee | Consumers may interleave |

If order preservation is critical, **avoid CompetingEdgeStrategy** or use a single consumer.

---

## Not Available in POC

The following features are **NOT available in the POC** and exist only in production code (`/src`):

### ❌ Structured Routing (Production Code Only)

**Location**: `/src/DataFlow/Builder/Graph/StructuredRoutingBlockExtensions.cs`

Structured Routing provides:
- Dynamic routes (created at runtime)
- Multi-block routes (routes can contain pipelines)
- Route-level DI scoping
- Template-based route creation

**If you see references to Structured Routing in POC docs, they are stale and should be ignored.**

### POC Routing Capabilities

The POC supports:
- ✅ Static routes (defined at build time)
- ✅ Content-based routing via `SelectiveRoutingEdgeStrategy<T>`
- ✅ Broadcast and competing patterns
- ❌ Dynamic route creation
- ❌ Route templates
- ❌ Multi-block routes with sub-pipelines

---

## Summary

### Key Takeaways

1. **Three patterns**: Broadcast, Competing, Selective Routing
2. **Edge-level routing**: Blocks stay simple, edges define delivery
3. **Performance**: All patterns have zero allocation overhead
4. **Flexibility**: Change routing without changing block code
5. **Testability**: Blocks testable in isolation

### Pattern Selection Guide

| If you need... | Use this pattern |
|----------------|------------------|
| All consumers get all items | `BroadcastEdgeStrategy` |
| Load balancing | `CompetingEdgeStrategy` |
| Content-based routing | `SelectiveRoutingEdgeStrategy<T>` |
| Monitoring + processing | `BroadcastEdgeStrategy` |
| Scale concurrency | `CompetingEdgeStrategy` |

### Next Steps

- **Learn more**: See [ADR: Routing Strategies](../../../docs/adr/poc/2025-11-03-routing-strategies.md) for architectural decisions
- **See examples**: Review tests in `DataFlow.POC.Tests/SelectiveRoutingEdgeStrategyTests.cs`
- **Performance**: Check analysis at [Routing Analysis](../analysis/routing/README.md)
- **Compare**: See [Comparison Tables](../analysis/routing/COMPARISON_TABLES.md)

---

**Document Version**: 1.0  
**Status**: ✅ Current  
**Last Updated**: 2025-11-24
