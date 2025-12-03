# Epoch Buffer Blocks - User Guide

This guide provides practical examples and best practices for using epoch buffer blocks in your data flow pipelines.

## Table of Contents

1. [When to Use Epoch Buffer Blocks](#when-to-use-epoch-buffer-blocks)
2. [Basic Usage](#basic-usage)
3. [Common Patterns](#common-patterns)
4. [Configuration](#configuration)
5. [Best Practices](#best-practices)
6. [Troubleshooting](#troubleshooting)

## When to Use Epoch Buffer Blocks

Use `EpochBufferBlock<T>` when:

- ✅ You need to buffer **epoch streams** (`IEpochStream<T>`)
- ✅ Multiple producers feed a single consumer (fan-in)
- ✅ You want to smooth out rate differences between producer and consumer
- ✅ You need to decouple producer from consumer processing

**Do not use** for:
- ❌ Plain types (use `BufferNode<T>` instead)
- ❌ Side channels (`IDataEnvelope` - use `BufferNode<T>`)

## Basic Usage

### Simple Buffer

The most common usage is adding a buffer between two blocks:

```csharp
using DataFlow.POC.Builder;
using DataFlow.POC.Registry;

var services = new ServiceCollection();
var provider = services.BuildServiceProvider();
var registry = new BlockTypeRegistry();

var builder = new DataFlowGraphBuilder("my-flow", provider, registry);

// Add buffer with capacity of 100 items per epoch
builder.AddEpochBuffer<int>("buffer", capacity: 100);
```

### Complete Pipeline Example

Here's a complete example with source, buffer, and consumer:

```csharp
// 1. Configure services
var services = new ServiceCollection();
services.AddTransient<MyEpochSource>();
services.AddTransient<MyEpochProcessor>();
var provider = services.BuildServiceProvider();

// 2. Build graph
var registry = new BlockTypeRegistry();
var builder = new DataFlowGraphBuilder("example", provider, registry);

// Register blocks (implementation depends on your DI setup)
// ... register MyEpochSource as "source"
// ... register MyEpochProcessor as "processor"

// 3. Add buffer between source and processor
builder.AddEpochBuffer<int>("buffer", capacity: 100);

// 4. Build and execute
var graph = builder.Build();
await graph.ExecuteAsync(cancellationToken);
```

## Common Patterns

### Pattern 1: Multiple Producers (Fan-In)

When multiple sources need to send data to a single consumer:

```csharp
var builder = new DataFlowGraphBuilder("fan-in", provider, registry);

// Multiple sources produce epoch streams
// ... register "source1"
// ... register "source2"

// Buffer collects from all sources
builder.AddEpochBuffer<int>("buffer", capacity: 200);

// Single consumer processes buffered data
// ... register "processor"

// Note: BufferNode would be used to connect multiple producers to one consumer
// in the traditional sense, but for epoch streams, the routing handles this
```

### Pattern 2: Rate Smoothing

When producer is faster than consumer:

```csharp
var builder = new DataFlowGraphBuilder("rate-smoothing", provider, registry);

// Fast producer
// ... register "fast-producer"

// Buffer absorbs bursts
builder.AddEpochBuffer<int>("buffer", capacity: 500);

// Slow consumer gets backpressure when buffer full
// ... register "slow-processor"
```

**How it works**:
1. Fast producer fills buffer quickly
2. Buffer reaches capacity (500 items)
3. Producer blocks on next write
4. Consumer drains buffer
5. Producer resumes when space available

### Pattern 3: Pipeline Decoupling

Decouple processing stages:

```csharp
var builder = new DataFlowGraphBuilder("decoupled", provider, registry);

// Source produces data
// ... register "source"

// First buffer for input smoothing
builder.AddEpochBuffer<int>("input-buffer", capacity: 100);

// Transform stage
// ... register "transformer"

// Second buffer for output smoothing
builder.AddEpochBuffer<string>("output-buffer", capacity: 100);

// Final sink
// ... register "sink"
```

### Pattern 4: Testing with Buffers

Use buffers to simplify testing:

```csharp
[Fact]
public async Task TestEpochProcessing()
{
    // Arrange
    var segmenter = BlockHelpers.CreateEpochSegmenter<int>(
        "segmenter", 
        EpochSegmentationPolicy.ByCount(3, "test-source"));

    var bufferConfig = new BufferConfiguration(capacity: 10);
    var bufferContext = new BlockContext("buffer");
    var buffer = new EpochBufferBlock<int>(bufferContext, bufferConfig);

    var context = new TestExecutionContext();

    // Act
    var plainItems = ProducePlainItems(10);
    var epochStreams = segmenter.ExecuteAsync(plainItems, context);
    var bufferedEpochs = buffer.ExecuteAsync(epochStreams, context);

    // Assert
    var epochs = await ConsumeEpochs(bufferedEpochs);
    Assert.Equal(4, epochs.Count);
}
```

## Configuration

### Capacity Selection

Choose buffer capacity based on your workload:

```csharp
// Small capacity (10-50) - Low latency, tight coupling
builder.AddEpochBuffer<int>("buffer", capacity: 10);

// Medium capacity (100-500) - Balanced
builder.AddEpochBuffer<int>("buffer", capacity: 100);

// Large capacity (1000+) - High throughput, loose coupling
builder.AddEpochBuffer<int>("buffer", capacity: 1000);
```

**Guidelines**:
- **Latency-sensitive**: Use smaller buffers (10-50)
- **Throughput-sensitive**: Use larger buffers (500-1000+)
- **Balanced**: Start with 100 and adjust based on metrics

### Custom Configuration

For advanced scenarios, create custom configuration:

```csharp
// Create configuration instance
var config = new BufferConfiguration(capacity: 200);

// Use with buffer
builder.AddEpochBuffer<int>("buffer", config);
```

## Best Practices

### 1. Choose Appropriate Capacity

```csharp
// ❌ Too small - frequent blocking
builder.AddEpochBuffer<int>("buffer", capacity: 1);

// ❌ Too large - excessive memory usage
builder.AddEpochBuffer<int>("buffer", capacity: 1_000_000);

// ✅ Right-sized for workload
builder.AddEpochBuffer<int>("buffer", capacity: 100);
```

### 2. Monitor Backpressure

If you see performance issues:

```csharp
// Check if buffer is frequently full
// Use metrics/logging to monitor buffer utilization
// Adjust capacity based on observations
```

### 3. Use Meaningful Names

```csharp
// ❌ Generic names
builder.AddEpochBuffer<int>("buffer1", capacity: 100);

// ✅ Descriptive names
builder.AddEpochBuffer<int>("order-intake-buffer", capacity: 100);
builder.AddEpochBuffer<int>("validation-queue", capacity: 50);
```

### 4. Consider Memory Impact

```csharp
// Each buffer consumes capacity × item-size bytes
// For large items, use smaller capacity:
builder.AddEpochBuffer<LargeObject>("buffer", capacity: 10);

// For small items, larger capacity is fine:
builder.AddEpochBuffer<int>("buffer", capacity: 1000);
```

### 5. Test with Realistic Workloads

```csharp
// Test with:
// - Typical epoch sizes
// - Expected throughput
// - Peak loads
// - Error conditions
```

## Troubleshooting

### Issue: Pipeline Hangs

**Symptom**: Pipeline stops processing, appears to be stuck.

**Possible Cause**: Buffer capacity too small for epoch size.

**Solution**:
```csharp
// If epoch has 200 items but buffer capacity is 50,
// the epoch cannot fit in the buffer
// Increase capacity:
builder.AddEpochBuffer<int>("buffer", capacity: 500);
```

### Issue: High Memory Usage

**Symptom**: Memory consumption grows during processing.

**Possible Cause**: Buffer capacity too large.

**Solution**:
```csharp
// Reduce capacity:
builder.AddEpochBuffer<int>("buffer", capacity: 50);

// Or process epochs in smaller batches
```

### Issue: Slow Processing

**Symptom**: Overall throughput is low.

**Possible Cause**: Backpressure from downstream consumer.

**Solution**:
```csharp
// The buffer is working correctly - backpressure is intentional
// To improve throughput:
// 1. Optimize the consumer
// 2. Increase consumer concurrency
// 3. Use larger buffer (but this only delays the problem)
```

### Issue: Wrong Buffer Type

**Symptom**: Compiler error when using `BufferNode<IEpochStream<T>>`.

**Cause**: `BufferNode` is for plain types only.

**Solution**:
```csharp
// ❌ Wrong - BufferNode doesn't support epoch streams
var buffer = new BufferNode<IEpochStream<int>>(capacity: 100);

// ✅ Correct - Use EpochBufferBlock
builder.AddEpochBuffer<int>("buffer", capacity: 100);
```

## Advanced Topics

### Understanding Epoch Boundaries

Epoch buffer blocks preserve epoch boundaries:

```
Input:
  Epoch 1: [1, 2, 3]
  Epoch 2: [4, 5, 6]

Buffer Processing:
  1. Create channel for Epoch 1
  2. Write [1, 2, 3] to channel
  3. Create output stream from channel
  4. Yield output stream
  5. Wait for Epoch 1 to complete
  6. Repeat for Epoch 2

Output:
  Epoch 1: [1, 2, 3]  ✅ Boundary preserved
  Epoch 2: [4, 5, 6]  ✅ Boundary preserved
```

### Backpressure Flow

Understanding backpressure helps with tuning:

```
Producer              Buffer (capacity: 3)       Consumer
   |                        |                       |
Write(1) ──────────────> Channel: [1]              |
Write(2) ──────────────> Channel: [1,2]            |
Write(3) ──────────────> Channel: [1,2,3]          |
Write(4) ──────────────> FULL - BLOCKS             |
   |                        |                Read() <──────
   |                        |                       |
   |                        |                       1
   |                   Channel: [2,3]               |
Write(4) ──────────────> Channel: [2,3,4]          |
   |                        |                       |
```

## Examples

### Example 1: Order Processing

```csharp
var builder = new DataFlowGraphBuilder("order-processing", provider, registry);

// Receive orders in epochs
// ... register "order-source"

// Buffer orders to handle bursts
builder.AddEpochBuffer<Order>("order-buffer", capacity: 200);

// Validate orders
// ... register "order-validator"

// Buffer validated orders
builder.AddEpochBuffer<ValidatedOrder>("validated-buffer", capacity: 100);

// Process orders
// ... register "order-processor"
```

### Example 2: Data Import Pipeline

```csharp
var builder = new DataFlowGraphBuilder("data-import", provider, registry);

// Read records from file (in epochs)
// ... register "file-reader"

// Buffer raw records
builder.AddEpochBuffer<RawRecord>("raw-buffer", capacity: 1000);

// Transform records
// ... register "transformer"

// Buffer transformed records
builder.AddEpochBuffer<TransformedRecord>("transformed-buffer", capacity: 500);

// Write to database
// ... register "db-writer"
```

## See Also

- [Architecture Documentation](../architecture/epoch-buffer-blocks.md) - Deep dive into design
- [Buffer Block Usage](../buffer-block-usage.md) - BufferNode<T> for plain types
- [Getting Started](GETTING_STARTED.md) - DataFlow basics
