# Competing Consumers Topology Guide

**Last Updated**: 2025-11-25  
**Difficulty**: Intermediate  
**Prerequisites**: [Getting Started](getting-started.md), [Working with Blocks](working-with-blocks.md)

---

## Overview

The **Competing Consumers** pattern allows multiple worker blocks to process items from a single source, with each item being consumed by exactly ONE worker. This enables natural load balancing and easy scaling of concurrent processing without changing block implementations.

### What You'll Learn

- How competing consumers work with shared channels
- When to use competing consumers vs other patterns
- How to scale throughput by adding workers
- Performance characteristics and backpressure behavior
- Real-world examples of load balancing scenarios

---

## Table of Contents

1. [Core Concept](#core-concept)
2. [Your First Competing Consumers Flow](#your-first-competing-consumers-flow)
3. [How It Works](#how-it-works)
4. [Scaling Throughput](#scaling-throughput)
5. [Competing vs ConcurrentProcessorBlock](#competing-vs-concurrentprocessorblock)
6. [Backpressure Behavior](#backpressure-behavior)
7. [When to Use Competing Consumers](#when-to-use-competing-consumers)
8. [Real-World Examples](#real-world-examples)
9. [Advanced Patterns](#advanced-patterns)
10. [Troubleshooting](#troubleshooting)
11. [Next Steps](#next-steps)

---

## Core Concept

### The Problem: Sequential Processing Bottleneck

```
Source → Single Processor → Output
         (bottleneck!)
```

If your processor takes 100ms per item and you receive 1000 items/second, you'll quickly fall behind (100ms × 1000 = 100 seconds of work per second!).

### The Solution: Multiple Competing Workers

```
                  ┌─→ Worker 1 ─┐
Source → Channel ─┼─→ Worker 2 ─┼→ (each item processed once)
                  └─→ Worker 3 ─┘
```

Multiple workers compete for items from a shared channel. The channel ensures each item is consumed exactly once, providing natural load balancing.

### Key Characteristics

- ✅ Each item processed **exactly once**
- ✅ Natural load balancing (faster workers get more items)
- ✅ Easy to scale (add/remove workers)
- ✅ Shared channel for efficiency
- ⚠️ Order may not be preserved across workers
- ❌ Cannot route based on item content (use selective routing for that)

---

## Your First Competing Consumers Flow

Let's process orders with three competing workers:

```csharp
// Step 1: Register the order source and workers
builder.Services.AddDataFlows("orders", df =>
{
    // Register source block
    df.AddSourceBlock<Order, OrderSource>("order-source");
    
    // Register three identical worker blocks
    df.AddActorBlock<Order, Order, OrderProcessor>("worker-1");
    df.AddActorBlock<Order, Order, OrderProcessor>("worker-2");
    df.AddActorBlock<Order, Order, OrderProcessor>("worker-3");
    
    // Build the graph with competing edge
    df.AddGraph("process-orders", g =>
    {
        g.UseBlock("order-source")
         .CompeteWith(new[] { "worker-1", "worker-2", "worker-3" });
    });
});

// Step 2: Implement the source
public class OrderSource : IProducer<Order>
{
    private readonly IOrderRepository _repository;
    
    public OrderSource(IOrderRepository repository)
    {
        _repository = repository;
    }
    
    public async IAsyncEnumerable<Order> ProduceAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var order in _repository.GetPendingOrdersAsync(cancellationToken))
        {
            yield return order;
        }
    }
}

// Step 3: Implement the worker (same logic for all)
public class OrderProcessor : IActor<Order, Order>
{
    private readonly ILogger<OrderProcessor> _logger;
    
    public OrderProcessor(ILogger<OrderProcessor> logger)
    {
        _logger = logger;
    }
    
    public async Task<Order> ProcessAsync(Order order, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing order {OrderId}", order.Id);
        
        // Simulate processing work
        await Task.Delay(100, cancellationToken);
        
        order.Status = "Processed";
        return order;
    }
}

// Step 4: Execute the graph
var graph = serviceProvider
    .GetRequiredKeyedService<DataFlowGraph>(("orders", "process-orders"));

await graph.ExecuteAsync();
```

**What happens**: Orders are distributed across the three workers. If Worker 1 finishes an order quickly, it immediately picks up the next available order from the channel, providing natural load balancing.

---

## How It Works

### Shared Channel Architecture

Unlike broadcast (where each target gets its own channel), competing consumers share a **single channel**:

```csharp
// Conceptual implementation
public class CompetingEdgeStrategy : EdgeStrategy
{
    public override (Writers, Readers) CreateTypedChannels(
        Type dataType, 
        IBlock sourceBlock, 
        IReadOnlyList<IBlock> targetBlocks)
    {
        // Create ONE shared channel
        var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
        {
            SingleReader = false,  // Multiple readers compete!
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
        
        var writer = channel.Writer;
        var reader = channel.Reader;
        
        // All targets share the same reader instance
        var writers = new Dictionary<IBlock, ChannelWriter<T>>();
        var readers = new Dictionary<IBlock, ChannelReader<T>>();
        
        foreach (var target in targetBlocks)
        {
            writers[target] = writer;  // Same writer
            readers[target] = reader;  // Same reader (competition happens here!)
        }
        
        return (writers, readers);
    }
}
```

### Item Routing

```csharp
public override async Task RouteTypedItemAsync<T>(
    T item,
    Dictionary<IBlock, ChannelWriter<T>> typedWriters,
    CancellationToken cancellationToken)
{
    // Write once to shared channel - first available reader wins!
    var sharedWriter = typedWriters.Values.First();
    await sharedWriter.WriteAsync(item, cancellationToken);
}
```

**Key insight**: Because all workers share the same `ChannelReader`, when one worker calls `ReadAsync()`, it gets the next item. Other workers wait for the following items.

---

## Scaling Throughput

### Adding More Workers

The beauty of competing consumers is that scaling is trivial - just add more worker blocks:

```csharp
// 2 workers
df.AddGraph("process-orders", g =>
{
    g.UseBlock("order-source")
     .CompeteWith(new[] { "worker-1", "worker-2" });
});

// 4 workers (2x throughput)
df.AddGraph("process-orders", g =>
{
    g.UseBlock("order-source")
     .CompeteWith(new[] { "worker-1", "worker-2", "worker-3", "worker-4" });
});

// 8 workers (4x throughput)
df.AddGraph("process-orders", g =>
{
    g.UseBlock("order-source")
     .CompeteWith(new[] { 
         "worker-1", "worker-2", "worker-3", "worker-4",
         "worker-5", "worker-6", "worker-7", "worker-8"
     });
});
```

### Throughput Scaling

| Workers | Theoretical Speedup | Actual Speedup | Notes |
|---------|-------------------|----------------|-------|
| 1 | 1x | 1x | Baseline |
| 2 | 2x | ~1.9x | Minor channel overhead |
| 4 | 4x | ~3.7x | Good scaling |
| 8 | 8x | ~7.2x | Diminishing returns |
| 16 | 16x | ~12x | Channel contention |

**Real-world example**: If one worker processes 10 items/sec, 4 workers typically achieve 37-40 items/sec (not perfect 40 due to channel coordination overhead).

---

## Competing vs ConcurrentProcessorBlock

There are two ways to achieve concurrent processing. The modern approach uses competing consumers at the topology level.

### Old Way: Concurrency Inside Block

```csharp
// ❌ Concurrency hidden inside block implementation
var concurrentProcessor = new ConcurrentProcessorBlock<Order>(
    "processor",
    async (order, ct) => await ProcessOrder(order, ct),
    maxConcurrency: 4);
```

**Problems**:
- Concurrency is a block concern (should be orchestration concern)
- Hard to tune (requires code changes)
- Less visible in graph structure
- Block is more complex

### New Way: Competing Consumers (Topology-Level)

```csharp
// ✅ Concurrency at topology level
df.AddActorBlock<Order, Order, OrderProcessor>("worker-1");
df.AddActorBlock<Order, Order, OrderProcessor>("worker-2");
df.AddActorBlock<Order, Order, OrderProcessor>("worker-3");
df.AddActorBlock<Order, Order, OrderProcessor>("worker-4");

df.AddGraph("process-orders", g =>
{
    g.UseBlock("order-source")
     .CompeteWith(new[] { "worker-1", "worker-2", "worker-3", "worker-4" });
});
```

**Benefits**:
- ✅ Blocks remain simple (just business logic)
- ✅ Concurrency visible in graph topology
- ✅ Easy to tune (add/remove blocks in registration)
- ✅ Explicit parallelism
- ✅ Natural load balancing

---

## Backpressure Behavior

### Shared Channel Backpressure

Because workers share a channel, slow consumers affect ALL workers:

```csharp
Source (fast) → [Bounded Channel: capacity 100] ← Workers (some slow)
                         ↓
                  Channel fills up
                         ↓
              Source blocks waiting
```

**Scenario**: If you have 4 workers and one is slow:
- The slow worker still gets items (it's competing fairly)
- The slow worker fills the channel more slowly
- Eventually the channel reaches capacity
- Source blocks until channel space is available

### Independent Backpressure (Compare with Broadcast)

With broadcast, each target has its own channel:

```
Source → Worker 1's Channel [####    ] ← Worker 1 (fast)
      ↓
      → Worker 2's Channel [########] ← Worker 2 (slow - only blocks its channel)
```

With competing, there's ONE channel:

```
Source → Shared Channel [########] ← Worker 1 (fast)
                                   ← Worker 2 (slow - blocks everyone)
```

---

## When to Use Competing Consumers

### ✅ Good Use Cases

1. **Load Balancing Identical Workers**
   ```csharp
   // Process work items with 5 workers
   g.UseBlock("work-source")
    .CompeteWith(new[] { "worker-1", "worker-2", "worker-3", "worker-4", "worker-5" });
   ```

2. **Scaling Throughput**
   ```csharp
   // Need more throughput? Add more workers!
   g.UseBlock("order-source")
    .CompeteWith(new[] { 
        "worker-1", "worker-2", "worker-3", "worker-4",
        "worker-5", "worker-6"  // Added for 50% more capacity
    });
   ```

3. **CPU-Bound Processing**
   ```csharp
   // Image processing with worker count matching CPU cores
   var workerCount = Environment.ProcessorCount;
   var workers = Enumerable.Range(1, workerCount)
       .Select(i => $"image-processor-{i}")
       .ToArray();
   
   g.UseBlock("image-source")
    .CompeteWith(workers);
   ```

4. **Each Item Needs Processing Exactly Once**
   ```csharp
   // Payment processing - no duplicates allowed!
   g.UseBlock("payment-source")
    .CompeteWith(new[] { "payment-processor-1", "payment-processor-2" });
   ```

### ❌ Bad Use Cases

1. **Content-Based Routing**
   ```csharp
   // ❌ Wrong: Can't route high-priority items differently
   g.UseBlock("order-source")
    .CompeteWith(new[] { "worker-1", "worker-2" });
   
   // ✅ Better: Use selective routing
   g.UseBlock("order-source")
    .RouteBy(order => order.Priority)
    .To("high-priority-processor", priority => priority == "high")
    .To("normal-processor", priority => priority == "normal");
   ```

2. **All Consumers Need All Items**
   ```csharp
   // ❌ Wrong: Only one worker will log each item!
   g.UseBlock("data-source")
    .CompeteWith(new[] { "processor", "logger", "metrics" });
   
   // ✅ Better: Use broadcast
   g.UseBlock("data-source")
    .BroadcastTo(new[] { "processor", "logger", "metrics" });
   ```

3. **Order Preservation Critical**
   ```csharp
   // ❌ Risky: Workers may complete out of order
   g.UseBlock("sequence-source")
    .CompeteWith(new[] { "worker-1", "worker-2" });
   
   // ✅ Better: Use single worker if order matters
   g.UseBlock("sequence-source")
    .ProcessWith("single-worker");
   ```

---

## Real-World Examples

### Example 1: E-Commerce Order Processing

Process incoming orders with 3 workers for peak traffic:

```csharp
builder.Services.AddDataFlows("ecommerce", df =>
{
    df.AddSourceBlock<Order, OrderQueueSource>("order-queue");
    df.AddActorBlock<Order, ProcessedOrder, ValidateOrderActor>("validator-1");
    df.AddActorBlock<Order, ProcessedOrder, ValidateOrderActor>("validator-2");
    df.AddActorBlock<Order, ProcessedOrder, ValidateOrderActor>("validator-3");
    df.AddActorBlock<ProcessedOrder, Order, SaveOrderActor>("saver");
    
    df.AddGraph("process-orders", g =>
    {
        // Orders compete among validators
        g.UseBlock("order-queue")
         .CompeteWith(new[] { "validator-1", "validator-2", "validator-3" })
         .ProcessWith("saver");  // Single saver to maintain database consistency
    });
});

public class ValidateOrderActor : IActor<Order, ProcessedOrder>
{
    private readonly IValidator<Order> _validator;
    
    public async Task<ProcessedOrder> ProcessAsync(Order order, CancellationToken ct)
    {
        var result = await _validator.ValidateAsync(order, ct);
        
        return new ProcessedOrder
        {
            Order = order,
            IsValid = result.IsValid,
            ValidationErrors = result.Errors
        };
    }
}
```

### Example 2: Log File Processing

Process large log files with parallel workers:

```csharp
builder.Services.AddDataFlows("logs", df =>
{
    df.AddSourceBlock<LogEntry, LogFileSource>("log-source");
    
    // 8 workers for CPU-bound parsing
    for (int i = 1; i <= 8; i++)
    {
        df.AddActorBlock<LogEntry, ParsedLog, LogParserActor>($"parser-{i}");
    }
    
    df.AddActorBlock<ParsedLog, ParsedLog, LogAggregatorActor>("aggregator");
    
    df.AddGraph("parse-logs", g =>
    {
        var parsers = Enumerable.Range(1, 8).Select(i => $"parser-{i}").ToArray();
        
        g.UseBlock("log-source")
         .CompeteWith(parsers)
         .ProcessWith("aggregator");
    });
});
```

### Example 3: API Request Handler

Handle API requests with competing workers for load balancing:

```csharp
builder.Services.AddDataFlows("api", df =>
{
    df.AddSourceBlock<ApiRequest, RequestQueueSource>("request-queue");
    df.AddActorBlock<ApiRequest, ApiResponse, RequestHandlerActor>("handler-1");
    df.AddActorBlock<ApiRequest, ApiResponse, RequestHandlerActor>("handler-2");
    df.AddActorBlock<ApiRequest, ApiResponse, RequestHandlerActor>("handler-3");
    df.AddActorBlock<ApiRequest, ApiResponse, RequestHandlerActor>("handler-4");
    df.AddActorBlock<ApiResponse, ApiResponse, ResponseCacheActor>("cache");
    
    df.AddGraph("handle-requests", g =>
    {
        g.UseBlock("request-queue")
         .CompeteWith(new[] { "handler-1", "handler-2", "handler-3", "handler-4" })
         .ProcessWith("cache");
    });
});
```

---

## Advanced Patterns

### Pattern 1: Combining Content Routing + Competing

Route by priority, then compete within each tier:

```csharp
builder.Services.AddDataFlows("orders", df =>
{
    df.AddSourceBlock<Order, OrderSource>("orders");
    df.AddBufferBlock<Order>("high-buffer", capacity: 100);
    df.AddBufferBlock<Order>("normal-buffer", capacity: 100);
    
    // High priority: 3 workers
    df.AddActorBlock<Order, Order, ExpressProcessor>("high-worker-1");
    df.AddActorBlock<Order, Order, ExpressProcessor>("high-worker-2");
    df.AddActorBlock<Order, Order, ExpressProcessor>("high-worker-3");
    
    // Normal priority: 2 workers
    df.AddActorBlock<Order, Order, StandardProcessor>("normal-worker-1");
    df.AddActorBlock<Order, Order, StandardProcessor>("normal-worker-2");
    
    df.AddGraph("priority-load-balanced", g =>
    {
        // Route to buffers by priority
        g.UseBlock("orders")
         .RouteBy(order => order.Priority)
         .To("high-buffer", p => p == "high")
         .To("normal-buffer", p => p == "normal");
        
        // Compete within each tier
        g.UseBlock("high-buffer")
         .CompeteWith(new[] { "high-worker-1", "high-worker-2", "high-worker-3" });
        
        g.UseBlock("normal-buffer")
         .CompeteWith(new[] { "normal-worker-1", "normal-worker-2" });
    });
});
```

### Pattern 2: Dynamic Worker Scaling

Adjust worker count based on configuration:

```csharp
public class DataFlowConfiguration
{
    public int WorkerCount { get; set; } = 4;
}

builder.Services.Configure<DataFlowConfiguration>(
    builder.Configuration.GetSection("DataFlow"));

builder.Services.AddDataFlows("scalable", df =>
{
    var config = df.ServiceProvider.GetRequiredService<IOptions<DataFlowConfiguration>>().Value;
    
    df.AddSourceBlock<WorkItem, WorkSource>("work-source");
    
    // Register workers based on configuration
    var workers = new List<string>();
    for (int i = 1; i <= config.WorkerCount; i++)
    {
        var workerName = $"worker-{i}";
        df.AddActorBlock<WorkItem, WorkItem, WorkProcessor>(workerName);
        workers.Add(workerName);
    }
    
    df.AddGraph("process-work", g =>
    {
        g.UseBlock("work-source")
         .CompeteWith(workers.ToArray());
    });
});
```

---

## Troubleshooting

### Problem: Items Processed Out of Order

**Symptom**: Item 1, 2, 3 go in, but you see 1, 3, 2 in the output.

**Cause**: Workers complete at different speeds.

**Solutions**:
1. If order matters, use a single worker (no competing)
2. Add sequence numbers and reorder downstream
3. Use a batch block to group and then reorder

```csharp
// Option 1: Single worker
g.UseBlock("source")
 .ProcessWith("single-worker");

// Option 2: Add sequence tracking
public class SequencedItem<T>
{
    public long Sequence { get; set; }
    public T Item { get; set; }
}
```

### Problem: One Slow Worker Blocks Everything

**Symptom**: One worker is much slower, throughput drops.

**Cause**: Shared channel - slow worker fills capacity, blocking others.

**Solutions**:
1. Investigate why one worker is slow (logging, profiling)
2. Ensure workers are truly identical (no hidden state)
3. Consider increasing channel capacity
4. Use broadcast instead if workers do different things

```csharp
// Increase buffer capacity
var competingEdge = new Edge(
    source,
    workers,
    new CompetingEdgeStrategy(BufferMode.Bounded, capacity: 1000));  // Larger buffer
```

### Problem: Workers Not Getting Equal Distribution

**Symptom**: Worker 1 processes 80%, Worker 2 processes 20%.

**Cause**: Worker 2 is slower OR source produces in bursts.

**Solutions**:
1. Profile workers to ensure equal performance
2. Check for hidden I/O or database access differences
3. Use smaller batches to allow better distribution

```csharp
// Check if workers have equal processing time
var stopwatch = Stopwatch.StartNew();
await ProcessAsync(item, ct);
_logger.LogInformation("Worker {Id} took {Ms}ms", _workerId, stopwatch.ElapsedMilliseconds);
```

---

## Next Steps

### Learn More Patterns

- **[Broadcast Topology](topology-broadcast.md)** - Fan-out to multiple consumers
- **[Selective Routing](topology-selective-routing.md)** - Content-based routing
- **[Control Flow Topologies](control-flow-topologies.md)** - Complete topology reference

### Advanced Features

- **[Working with Blocks](working-with-blocks.md)** - Deep dive on block types
- **[Epoch Actor Block](epoch-actor-block.md)** - Scope rotation and epochs
- **[Using Epochs](using-epochs.md)** - Transaction boundaries

### Related Topics

- **[Testing Guide](testing-guide.md)** - Test competing consumer flows
- **[Dependency Injection](dependency-injection-registration.md)** - DI patterns

---

## Summary

### Key Takeaways

- **Competing consumers** = Multiple workers share one channel
- Each item processed **exactly once** by first available worker
- **Natural load balancing** without extra logic
- **Easy scaling** by adding more worker blocks
- **Topology-level concurrency** keeps blocks simple
- Order may **not be preserved** across workers
- **Shared backpressure** - slow worker affects all

### When to Use

| Use Competing When... | Don't Use Competing When... |
|-----------------------|-----------------------------|
| ✅ Load balancing identical workers | ❌ Content-based routing needed |
| ✅ Scaling throughput | ❌ All consumers need all items |
| ✅ Each item processed once | ❌ Order preservation critical |
| ✅ CPU-bound processing | ❌ Workers do different things |

---

**Document Version**: 1.0  
**Status**: ✅ Current  
**Last Updated**: 2025-11-25
