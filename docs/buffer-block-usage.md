# BufferBlock Usage Guide

## Overview

`BufferBlock<T>` is a block that accepts items from multiple upstream producers and distributes them to multiple downstream consumers using a **competing consumer pattern**. Each item is delivered to exactly **ONE** downstream consumer, making it ideal for load balancing and merging multiple data streams.

## Key Characteristics

- **Multiple Producers**: Can accept items from multiple upstream blocks
- **Competing Consumers**: Multiple downstream blocks compete for items
- **Load Distribution**: Each item goes to exactly one consumer (not all)
- **Buffering**: Uses `MonitoredChannel<T>` internally for efficient buffering with metrics
- **Backpressure**: Automatically handles backpressure via bounded channels

## When to Use BufferBlock

Use `BufferBlock<T>` when you need:

1. **Load Balancing**: Distribute work across multiple parallel consumers
2. **Branch Merging**: Combine outputs from multiple upstream branches
3. **Competing Consumers**: Ensure each item is processed by only one consumer
4. **Multi-Producer Scenarios**: Accept items from multiple sources into a single buffer

## Basic Usage

### Single Producer, Multiple Consumers

```csharp
var builder = new StructuredDataFlowBuilder(sp, "LoadBalancing");

// Producer
builder.AddProducer<int>("source", sp => new NumberProducer(1, 100));

// Buffer for competing consumers
builder.AddBuffer<int>("buffer")
    .ReceiveFrom("source");

// Multiple consumers compete for items
for (int i = 0; i < 3; i++)
{
    builder.AddProcessor<int>($"consumer-{i}", sp => new DataProcessor())
        .ReceiveFrom("buffer");
}

var flow = builder.Build();
await flow.ExecuteAsync(context);
```

**Result**: Each of the 100 items is processed by exactly one of the 3 consumers (load distribution).

### Multiple Producers, Single Consumer

```csharp
var builder = new StructuredDataFlowBuilder(sp, "Merging");

// Multiple producers
builder.AddProducer<int>("source1", sp => new NumberProducer(1, 50));
builder.AddProducer<int>("source2", sp => new NumberProducer(51, 100));
builder.AddProducer<int>("source3", sp => new NumberProducer(101, 150));

// Buffer accepts from all sources
builder.AddBuffer<int>("merge-buffer")
    .ReceiveFrom("source1")
    .ReceiveFrom("source2")
    .ReceiveFrom("source3");

// Single consumer processes merged stream
builder.AddProcessor<int>("consumer", sp => new DataProcessor())
    .ReceiveFrom("merge-buffer");
```

**Result**: All 150 items from three producers are merged and processed by a single consumer.

### Multiple Producers, Multiple Consumers

```csharp
var builder = new StructuredDataFlowBuilder(sp, "ComplexBuffer");

// Multiple producers
builder.AddProducer<int>("source1", sp => new NumberProducer(1, 50));
builder.AddProducer<int>("source2", sp => new NumberProducer(51, 100));

// Buffer accepts and distributes
builder.AddBuffer<int>("buffer", new BlockOptions { Capacity = 100 })
    .ReceiveFrom("source1")
    .ReceiveFrom("source2");

// Multiple competing consumers
for (int i = 0; i < 2; i++)
{
    builder.AddProcessor<int>($"consumer-{i}", sp => new DataProcessor())
        .ReceiveFrom("buffer");
}
```

**Result**: 100 items from two producers are distributed across two consumers (each item to one consumer).

## Branch Merge Pattern

BufferBlock is ideal for merging outputs from multiple branches:

```csharp
var builder = new StructuredDataFlowBuilder(sp, "BranchMerge");

// Source
builder.AddProducer<int>("source", sp => new NumberProducer(1, 20));

// Broadcast to branches
builder.AddBroadcast<int>("fanout")
    .ReceiveFrom("source");

// Create 3 branches with different transformations
for (int i = 0; i < 3; i++)
{
    var branchId = i;
    var branch = builder.AddBranch($"branch-{i}");
    
    branch.AddTransform<int, string>($"transform-{i}",
        sp => new CustomTransformer(branchId))
        .ReceiveFrom("fanout");
}

// Merge all branch outputs into buffer
builder.AddBuffer<string>("merge-buffer")
    .ReceiveFromBranches("fanout"); // Convenient API for connecting all branches

// Single consumer for merged results
builder.AddProcessor<string>("final-consumer", sp => new ResultProcessor())
    .ReceiveFrom("merge-buffer");
```

**Flow**:
1. Source produces 20 items
2. Broadcast sends each item to all 3 branches (60 items total)
3. Each branch transforms items differently
4. BufferBlock merges 60 transformed items using `ReceiveFromBranches()`
5. Single consumer processes merged results

**Note**: The `ReceiveFromBranches()` method is a convenient way to connect to all branches that were created from a specific parent block (e.g., "fanout"). This eliminates the need to manually call `.ReceiveFrom()` for each branch.

## BufferBlock vs BroadcastBlock

| Feature | BufferBlock | BroadcastBlock |
|---------|------------|----------------|
| **Semantics** | Competing Consumers | Fanout |
| **Item Distribution** | Each item to ONE consumer | Each item to ALL consumers |
| **Use Case** | Load balancing | Data replication |
| **Multiple Producers** | ✅ Yes | ❌ No (single source) |
| **Example** | 10 items → 3 consumers = 10 operations | 10 items → 3 consumers = 30 operations |

### Example Comparison

```csharp
// BufferBlock: Competing consumers
var bufferBuilder = new StructuredDataFlowBuilder(sp, "Buffer");
bufferBuilder.AddProducer<int>("source", sp => new NumberProducer(1, 10));
bufferBuilder.AddBuffer<int>("buffer").ReceiveFrom("source");

for (int i = 0; i < 3; i++)
{
    bufferBuilder.AddProcessor<int>($"consumer-{i}", sp => new Counter())
        .ReceiveFrom("buffer");
}
// Result: 10 total operations (distributed across 3 consumers)

// BroadcastBlock: Fanout
var broadcastBuilder = new StructuredDataFlowBuilder(sp, "Broadcast");
broadcastBuilder.AddProducer<int>("source", sp => new NumberProducer(1, 10));
broadcastBuilder.AddBroadcast<int>("fanout").ReceiveFrom("source");

for (int i = 0; i < 3; i++)
{
    broadcastBuilder.AddProcessor<int>($"consumer-{i}", sp => new Counter())
        .ReceiveFrom("fanout");
}
// Result: 30 total operations (10 per consumer)
```

## Configuration Options

### Capacity

Control the internal buffer size:

```csharp
builder.AddBuffer<int>("buffer", new BlockOptions { Capacity = 500 })
    .ReceiveFrom("source");
```

- **Default**: 100 items
- **Recommendation**: Adjust based on producer/consumer throughput mismatch
- **Higher capacity**: Better for bursty producers
- **Lower capacity**: Better backpressure control

## Performance Characteristics

- **Backpressure**: Automatic via bounded channel (when buffer is full, producers wait)
- **Metrics**: Full monitoring via `MonitoredChannel<T>` (queue depth, throughput)
- **Concurrency**: Multiple producers write concurrently; multiple consumers read concurrently
- **Ordering**: No ordering guarantees when multiple consumers are present

## Best Practices

1. **Use BufferBlock for Load Balancing**: When you want to distribute work across multiple workers
2. **Use BroadcastBlock for Replication**: When you need multiple independent operations on the same data
3. **Adjust Capacity**: Set based on your throughput requirements and memory constraints
4. **Monitor Metrics**: Use the built-in metrics to observe buffer utilization
5. **Branch Merging**: Use BufferBlock to merge outputs from parallel branches back into a single stream

## Common Patterns

### Pattern 1: Load-Balanced Processing Pipeline

```csharp
Source → Buffer → [Consumer1, Consumer2, Consumer3]
```
Each item is processed by one consumer.

### Pattern 2: Multi-Source Aggregation

```csharp
[Source1, Source2, Source3] → Buffer → Consumer
```
Items from multiple sources are merged and processed sequentially.

### Pattern 3: Branch-Merge Pipeline

```csharp
Source → Broadcast → [Branch1, Branch2] → Buffer → Consumer
```
Items are replicated to branches, processed differently, then merged.

## See Also

- [BroadcastBlock Usage Guide](broadcast-block-usage.md)
- [Branch Concurrency](BRANCH_CONCURRENCY.md)
- [Competing vs Broadcast Consumers Test](../src/Tests/DataFlow/CompetingVsBroadcastConsumersTests.cs)
