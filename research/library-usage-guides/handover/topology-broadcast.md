# Broadcast Topology Guide

**Audience**: Developers familiar with DataFlow basics  
**Prerequisites**: [Getting Started](./getting-started.md), [Working with Blocks](./working-with-blocks.md)  
**Time**: 10 minutes

---

## Table of Contents

1. [What is Broadcast?](#what-is-broadcast)
2. [When to Use Broadcast](#when-to-use-broadcast)
3. [Basic Broadcast Example](#basic-broadcast-example)
4. [Extending the Hello World](#extending-the-hello-world)
5. [Broadcast with Cloning](#broadcast-with-cloning)
6. [Performance Characteristics](#performance-characteristics)
7. [Common Use Cases](#common-use-cases)

---

## What is Broadcast?

**Broadcast** (also called **fan-out**) is a topology pattern where every item from a source is sent to **ALL** target blocks simultaneously.

```
                      ┌─────────────┐
                  ┌──▶│ Logger      │
                  │   └─────────────┘
┌──────────┐      │
│ Source   │──────┤
└──────────┘      │   ┌─────────────┐
                  └──▶│ Metrics     │
                      └─────────────┘

Each item goes to BOTH logger and metrics
```

**Key Characteristics**:
- ✅ All consumers receive every item
- ✅ Concurrent writes to all targets
- ✅ Independent backpressure per consumer
- ⚠️ N concurrent channel writes (N = number of targets)

---

## When to Use Broadcast

Use broadcast when you need to:

| Scenario | Example |
|----------|---------|
| **Parallel Processing** | Send data to multiple independent processors |
| **Monitoring** | Log data while also processing it |
| **Metrics Collection** | Send to both processor and metrics collector |
| **Multi-Format Output** | Write to both database and file system |
| **Testing/Debugging** | Tap into stream for debugging without affecting main flow |

**Don't use broadcast when**:
- ❌ You want load balancing (use [Competing Consumers](./topology-competing-consumers.md))
- ❌ You need conditional routing (use [Selective Routing](./topology-selective-routing.md))
- ❌ Each item should only go to one target

---

## Basic Broadcast Example

Let's create a simple broadcast that sends data to both a logger and a metrics collector.

### Step 1: Define Your Actors

```csharp
using DataFlow.POC.Core;

// Logger actor - writes to console
public class LoggerActor : IStreamActor<int, object>
{
    public async IAsyncEnumerable<object> RunAsync(
        IAsyncEnumerable<int> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            Console.WriteLine($"[LOG] Processing item: {item}");
        }
    }
}

// Metrics actor - tracks count
public class MetricsActor : IStreamActor<int, object>
{
    private int _count = 0;
    
    public async IAsyncEnumerable<object> RunAsync(
        IAsyncEnumerable<int> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            _count++;
            Console.WriteLine($"[METRICS] Total items processed: {_count}");
        }
    }
}
```

### Step 2: Build the Graph with Broadcast

```csharp
using DataFlow.POC.Builder;
using DataFlow.POC.Blocks;
using DataFlow.POC.Core;
using DataFlow.POC.Tests.TestHelpers;

// Create blocks
var producer = BlockHelpers.CreateProducer<int>("source", ctx =>
{
    for (int i = 1; i <= 5; i++)
        yield return i;
});

var logger = BlockHelpers.CreateActor<int, object, LoggerActor>(
    "logger",
    new LoggerActor());

var metrics = BlockHelpers.CreateActor<int, object, MetricsActor>(
    "metrics",
    new MetricsActor());

// Create broadcast edge
var broadcastEdge = new Edge(
    producer,
    new[] { logger, metrics },  // Multiple targets
    new BroadcastEdgeStrategy(BufferMode.Bounded, 100));

// Build graph
var graph = new DataFlowGraphBuilder("broadcast-example")
    .AddBlock(producer)
    .AddBlock(logger)
    .AddBlock(metrics)
    .AddEdge(broadcastEdge)
    .Build();

// Execute
var serviceProvider = new ServiceCollection().BuildServiceProvider();
var context = new ExecutionContext(serviceProvider, CancellationToken.None);
await graph.ExecuteAsync(context);
```

**Output**:
```
[LOG] Processing item: 1
[METRICS] Total items processed: 1
[LOG] Processing item: 2
[METRICS] Total items processed: 2
...
```

---

## Extending the Hello World

Let's extend the "hello world" example from the Getting Started guide to add logging and metrics.

**Original Flow**:
```
Console Input → Uppercase → Console Output
```

**With Broadcast**:
```
                          ┌─────────────┐
                      ┌──▶│ Logger      │
                      │   └─────────────┘
Console Input → Uppercase ──┤
                      │   ┌─────────────┐
                      ├──▶│ Metrics     │
                      │   └─────────────┘
                      │   ┌─────────────┐
                      └──▶│ Console Out │
                          └─────────────┘
```

### Implementation

```csharp
using DataFlow.POC.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddDataFlows("global", df =>
{
    // Original actors
    df.AddActorBlock<string, string, UppercaseActor>("uppercase");
    df.AddActorBlock<string, object, ConsoleWriterActor>("writer");
    
    // New monitoring actors
    df.AddActorBlock<string, object, LoggerActor>("logger");
    df.AddActorBlock<string, object, MetricsActor>("metrics");
    
    // Build graph with broadcast
    df.AddGraph("main", g =>
    {
        var producer = BlockHelpers.CreateProducer<string>("input", ReadLinesAsync);
        
        g.AddBlock(producer)
         .UseBlock("uppercase")
         .UseBlock("writer")
         .UseBlock("logger")
         .UseBlock("metrics")
         .Connect(producer, "uppercase")
         // Broadcast from uppercase to all three targets
         .AddEdge(new Edge(
             g.GetBlock("uppercase"),
             new[] { 
                 g.GetBlock("writer"), 
                 g.GetBlock("logger"),
                 g.GetBlock("metrics")
             },
             new BroadcastEdgeStrategy(BufferMode.Bounded, 50)));
    });
});

static async IAsyncEnumerable<string> ReadLinesAsync()
{
    Console.WriteLine("Enter text (type 'quit' to exit):");
    while (true)
    {
        var line = Console.ReadLine();
        if (line == "quit" || string.IsNullOrEmpty(line))
            break;
        yield return line;
    }
}
```

---

## Broadcast with Cloning

**Problem**: What if downstream consumers modify the items?

By default, broadcast sends the **same reference** to all targets. If one consumer modifies the object, all consumers see the change.

**Solution**: Use cloning to send independent copies.

### Example: Broadcast with Cloning

```csharp
public class ClonableBroadcastEdgeStrategy<T> : BroadcastEdgeStrategy
    where T : ICloneable
{
    public ClonableBroadcastEdgeStrategy(BufferMode bufferMode, int capacity)
        : base(bufferMode, capacity)
    {
    }
    
    protected override async Task WriteToTargetsAsync<TItem>(
        TItem item,
        IEnumerable<ChannelWriter<TItem>> targetWriters,
        CancellationToken cancellationToken)
    {
        var writers = targetWriters.ToList();
        var tasks = new List<Task>(writers.Count);
        
        for (int i = 0; i < writers.Count; i++)
        {
            // Clone the item for each target (except the first)
            var itemToWrite = i == 0 ? item : (TItem)(item as ICloneable).Clone();
            tasks.Add(writers[i].WriteAsync(itemToWrite, cancellationToken).AsTask());
        }
        
        await Task.WhenAll(tasks);
    }
}

// Usage
var broadcastEdge = new Edge(
    producer,
    new[] { consumer1, consumer2, consumer3 },
    new ClonableBroadcastEdgeStrategy<Order>(BufferMode.Bounded, 100));
```

**When to use cloning**:
- ✅ Consumers might modify items
- ✅ Items are mutable reference types
- ✅ You need isolation between consumers

**When NOT to clone**:
- ❌ Items are immutable (strings, records, etc.)
- ❌ Items are value types (structs)
- ❌ Performance is critical and you control all consumers

---

## Performance Characteristics

### Throughput

| Setup | Throughput | Notes |
|-------|-----------|-------|
| 1 target | ~1.0x baseline | Same as direct connection |
| 2 targets | ~0.95x baseline | Small overhead for coordination |
| 5 targets | ~0.85x baseline | N concurrent writes |
| 10 targets | ~0.75x baseline | Coordination overhead grows |

**Key**: Broadcast writes concurrently to all targets using `Task.WhenAll`, avoiding serialization bottleneck.

### Backpressure

Each target has **independent backpressure**:

```
Producer → Broadcast → Target A (fast)    ✅ Not blocked
                    → Target B (slow)    ⏳ Blocked, but doesn't affect A
```

**Important**: The broadcast will only write to all targets when **all targets are ready**. If one target is full, broadcast waits for it.

### Memory Usage

Memory usage scales with:
- Number of targets × buffer capacity per target
- Item size (especially with cloning)

**Example**:
```
3 targets × 100 buffer capacity × 1KB per item = ~300KB buffered
```

---

## Common Use Cases

### Use Case 1: Logging and Processing

Process data while also logging it:

```csharp
var edge = new Edge(
    dataSource,
    new[] { processor, logger },
    new BroadcastEdgeStrategy(BufferMode.Bounded, 100));
```

### Use Case 2: Multi-Format Export

Write to both database and file:

```csharp
services.AddDataFlows("export", df =>
{
    df.AddActorBlock<Order, object, DatabaseWriterActor>("db-writer");
    df.AddActorBlock<Order, object, FileWriterActor>("file-writer");
    
    df.AddGraph("export-orders", g =>
    {
        g.UseBlock("order-source")
         .UseBlock("db-writer")
         .UseBlock("file-writer")
         .AddEdge(new Edge(
             g.GetBlock("order-source"),
             new[] { g.GetBlock("db-writer"), g.GetBlock("file-writer") },
             new BroadcastEdgeStrategy(BufferMode.Bounded, 50)));
    });
});
```

### Use Case 3: Testing/Debugging Tap

Tap into a stream for debugging:

```csharp
// Production flow
Producer → Transformer → Processor

// With debug tap
                     ┌─────────────┐
                 ┌──▶│ Debug Tap   │
                 │   └─────────────┘
Producer → Transformer ──┤
                 │   ┌─────────────┐
                 └──▶│ Processor   │
                     └─────────────┘
```

---

## Best Practices

### 1. Use Bounded Buffers

```csharp
// ✅ Good: Bounded buffer with reasonable capacity
new BroadcastEdgeStrategy(BufferMode.Bounded, 100)

// ❌ Bad: Unbounded can lead to memory issues
new BroadcastEdgeStrategy(BufferMode.Unbounded, 0)
```

### 2. Match Buffer Sizes to Slowest Consumer

```csharp
// If you have one slow consumer, size buffer accordingly
// Fast consumer: processes 1000 items/sec
// Slow consumer: processes 100 items/sec
// Buffer size: at least 100-200 to handle variance
new BroadcastEdgeStrategy(BufferMode.Bounded, 200)
```

### 3. Consider Immutability

```csharp
// ✅ Good: Immutable items don't need cloning
public record OrderData(int Id, decimal Total);

// ❌ Risky: Mutable items might need cloning
public class OrderData
{
    public int Id { get; set; }
    public decimal Total { get; set; }
}
```

### 4. Monitor Backpressure

If one consumer is consistently slower, consider:
- Increasing buffer capacity
- Optimizing the slow consumer
- Using competing consumers for the slow path

---

## Troubleshooting

### Issue: Pipeline is slow

**Symptom**: Overall throughput is limited by slowest consumer

**Solution**:
1. Profile consumers to find the bottleneck
2. Increase concurrency for slow consumer
3. Consider using [Competing Consumers](./topology-competing-consumers.md) for the slow path

### Issue: Out of memory

**Symptom**: Memory usage grows with number of targets

**Solution**:
1. Reduce buffer capacity
2. Don't use cloning unless necessary
3. Ensure all consumers are processing items

### Issue: One consumer not receiving items

**Symptom**: Items go to some targets but not others

**Check**:
1. Ensure all targets are properly connected in the edge
2. Verify all target blocks are added to the graph
3. Check for exceptions in the missing consumer

---

## Summary

You've learned:

- ✅ What broadcast topology is and when to use it
- ✅ How to create broadcast edges
- ✅ When and how to use cloning
- ✅ Performance characteristics and trade-offs
- ✅ Common use cases and best practices

**Next Steps**:

- **[Competing Consumers](./topology-competing-consumers.md)** - Learn about load balancing
- **[Selective Routing](./topology-selective-routing.md)** - Content-based routing
- **[Using Epochs](./using-epochs.md)** - Transaction boundaries

---

**Related Guides**:
- [Getting Started](./getting-started.md)
- [Working with Blocks](./working-with-blocks.md)
- [Testing Guide](./testing-guide.md)
