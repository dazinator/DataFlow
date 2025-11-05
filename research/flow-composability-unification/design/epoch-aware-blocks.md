# Epoch-Aware Blocks: Processing Within Epoch Boundaries

## Overview

Epoch-aware blocks enable processing of items within epoch streams while preserving epoch boundaries. These blocks bridge the gap between plain data processing logic and epoch-structured streams, enabling full composability in DataFlow pipelines.

### Key Features

- **Epoch Preservation**: Process items while maintaining epoch boundaries and metadata
- **Composability**: Use standard processing logic (transform, batch, process) within epoch streams
- **Type Safety**: Strongly-typed epoch stream processing
- **No Epoch Spanning**: Operations respect epoch boundaries (e.g., batches don't span epochs)
- **Reusability**: Apply plain transformation/processing logic within epoch contexts

## Block Types

### 1. EpochTransformerBlock

Applies transformations to items within epoch boundaries, supporting 1-to-1, 1-to-many, or filtering (1-to-0) transformations.

#### Signature

```csharp
public class EpochTransformerBlock<TIn, TOut> : 
    BlockBase<IEpochStream<TIn>, IEpochStream<TOut>>
```

#### Usage

```csharp
// Create transformer
var transformer = new EpochTransformerBlock<Order, OrderDto>(
    "order-transformer",
    async (order, ctx) => 
    {
        var dto = await _mapper.MapAsync(order, ctx.CancellationToken);
        yield return dto;
    });

// Pipeline: PlainSource → Segmenter → Transformer → Downstream
var source = new PlainSourceBlock<Order, OrderSource>("source", scopeFactory);
var segmenter = new EpochSegmenterBlock<Order>("segmenter", 
    EpochSegmentationPolicy.ByCount(100, "orders"));
var pipeline = transformer.ExecuteAsync(
    segmenter.ExecuteAsync(
        source.ExecuteAsync(emptyInput, context), 
        context), 
    context);
```

#### 1-to-Many Transformation

```csharp
var exploder = new EpochTransformerBlock<OrderBatch, Order>(
    "batch-exploder",
    async (batch, ctx) => 
    {
        foreach (var order in batch.Orders)
        {
            yield return order;
        }
    });
```

#### Filtering

```csharp
var filter = new EpochTransformerBlock<Order, Order>(
    "active-only-filter",
    async (order, ctx) => 
    {
        if (order.IsActive)
        {
            yield return order;
        }
        // Filter out inactive orders (yield nothing)
    });
```

### 2. SimpleEpochTransformerBlock

A simplified 1-to-1 transformer using synchronous functions for common mapping scenarios.

#### Signature

```csharp
public class SimpleEpochTransformerBlock<TIn, TOut> : 
    BlockBase<IEpochStream<TIn>, IEpochStream<TOut>>
```

#### Usage

```csharp
// Simple synchronous mapping
var transformer = new SimpleEpochTransformerBlock<int, string>(
    "int-to-string",
    item => $"Item-{item}");

// Use in pipeline
var pipeline = transformer.ExecuteAsync(epochStreams, context);
```

### 3. EpochProcessorBlock

Terminal block that consumes items and performs side effects within epoch boundaries.

#### Signature

```csharp
public class EpochProcessorBlock<T> : 
    BlockBase<IEpochStream<T>, IEpochStream<object>>
```

#### Usage

```csharp
// Create processor
var processor = new EpochProcessorBlock<Order>(
    "order-processor",
    async (order, ctx) => 
    {
        await _orderService.ProcessOrderAsync(order, ctx.CancellationToken);
    });

// Terminal block - no output
await foreach (var _ in processor.ExecuteAsync(epochStreams, context))
{
    // Processor produces no output
}
```

#### With Side Effects

```csharp
var logger = new EpochProcessorBlock<Order>(
    "order-logger",
    async (order, ctx) => 
    {
        _logger.LogInformation("Processing order {OrderId}", order.Id);
        await Task.CompletedTask;
    });
```

### 4. EpochBatchBlock

Accumulates items into batches within epoch boundaries. **Important**: Batches never span across epochs.

#### Signature

```csharp
public class EpochBatchBlock<T> : 
    BlockBase<IEpochStream<T>, IEpochStream<T[]>>
```

#### Usage

```csharp
// Size-based batching
var batcher = new EpochBatchBlock<Order>(
    "order-batcher",
    maxBatchSize: 50);

// Time-based batching
var timeBatcher = new EpochBatchBlock<Order>(
    "time-batcher",
    maxBatchSize: 100,
    windowPeriod: TimeSpan.FromSeconds(5));

// Use in pipeline
var batchedEpochs = batcher.ExecuteAsync(epochStreams, context);

// Process batches
await foreach (var epochStream in batchedEpochs)
{
    await foreach (var batch in epochStream.Items)
    {
        // batch is T[], all items belong to same epoch
        await ProcessBatchAsync(batch);
    }
}
```

#### Epoch Boundary Behavior

```csharp
// Input epochs: [1,2,3] [4,5,6,7] [8,9]
// Batch size: 5
// Output: [[1,2,3]] [[4,5,6,7]] [[8,9]]
//
// Batches DO NOT span epochs!
// Even though batch size is 5, we get batches of size 3, 4, and 2
// to respect epoch boundaries
```

## Architecture

### Data Flow

```
┌──────────────────┐
│ PlainSourceBlock │
└────────┬─────────┘
         │ IAsyncEnumerable<T>
         ▼
┌─────────────────────┐
│ EpochSegmenterBlock │
└────────┬────────────┘
         │ IAsyncEnumerable<IEpochStream<T>>
         ▼
┌──────────────────────┐
│ EpochTransformerBlock│ ◄── Epoch-aware processing
└────────┬─────────────┘
         │ IAsyncEnumerable<IEpochStream<TOut>>
         ▼
┌─────────────────┐
│ EpochBatchBlock │
└────────┬────────┘
         │ IAsyncEnumerable<IEpochStream<TOut[]>>
         ▼
┌──────────────────────┐
│ EpochProcessorBlock  │ ◄── Terminal
└──────────────────────┘
```

### Component Integration

```
┌────────────────────────────────────────────────────┐
│        Epoch Stream Pipeline                       │
│                                                     │
│  Epoch 1 [Items: a,b,c]                           │
│     ↓                                               │
│  Transform: a→A, b→B, c→C                          │
│     ↓                                               │
│  Batch: [A,B], [C]                                 │
│     ↓                                               │
│  Process: handle [A,B], handle [C]                 │
│                                                     │
│  Epoch 2 [Items: d,e]                              │
│     ↓                                               │
│  Transform: d→D, e→E                               │
│     ↓                                               │
│  Batch: [D,E]  ← New epoch = new batch            │
│     ↓                                               │
│  Process: handle [D,E]                             │
└────────────────────────────────────────────────────┘
```

## Composability Patterns

### Pattern 1: Full Epoch Pipeline

```csharp
// Source → Segment → Transform → Batch → Process
var source = new PlainSourceBlock<Data, DataSource>("source", scopeFactory);
var segmenter = new EpochSegmenterBlock<Data>("segment", 
    EpochSegmentationPolicy.ByCount(1000, "data"));
var transformer = new SimpleEpochTransformerBlock<Data, DataDto>(
    "transform", data => MapToDto(data));
var batcher = new EpochBatchBlock<DataDto>("batch", maxBatchSize: 100);
var processor = new EpochProcessorBlock<DataDto[]>(
    "process", async (batch, ctx) => await SaveBatchAsync(batch));

// Chain the pipeline
var items = source.ExecuteAsync(emptyInput, context);
var epochs = segmenter.ExecuteAsync(items, context);
var transformed = transformer.ExecuteAsync(epochs, context);
var batched = batcher.ExecuteAsync(transformed, context);
await foreach (var _ in processor.ExecuteAsync(batched, context))
{
    // Processing complete
}
```

### Pattern 2: Mixed Plain and Epoch Blocks

```csharp
// When you need epochs for some processing but not all:

// Pipeline 1: With epochs (for DbContext scoping)
var source1 = new PlainSourceBlock<Order, OrderSource>("source1", scopeFactory);
var segmenter = new EpochSegmenterBlock<Order>("segment", 
    EpochSegmentationPolicy.ByCount(100, "orders"));
var processor = new EpochProcessorBlock<Order>("save", 
    async (order, ctx) => await SaveWithDbContextAsync(order));

// Pipeline 2: Without epochs (stateless transformation)
var source2 = new PlainSourceBlock<Order, OrderSource>("source2", scopeFactory);
var transformer = new TransformerBlock<Order, OrderDto>("map", 
    (order, ctx) => MapToDtoAsync(order));
```

### Pattern 3: Complex Transformation Chain

```csharp
// Transform → Filter → Batch → Transform → Process
var transformer1 = new SimpleEpochTransformerBlock<Order, EnrichedOrder>(
    "enrich", order => EnrichOrder(order));
    
var filter = new EpochTransformerBlock<EnrichedOrder, EnrichedOrder>(
    "filter", async (order, ctx) => 
    {
        if (order.IsValid)
            yield return order;
    });
    
var batcher = new EpochBatchBlock<EnrichedOrder>("batch", maxBatchSize: 50);

var transformer2 = new EpochTransformerBlock<EnrichedOrder[], OrderSummary>(
    "summarize", async (batch, ctx) => 
    {
        yield return CreateSummary(batch);
    });
    
var processor = new EpochProcessorBlock<OrderSummary>(
    "save-summary", async (summary, ctx) => await SaveAsync(summary));
```

## Design Principles

### 1. Epoch Boundary Respect

All epoch-aware blocks strictly respect epoch boundaries:

```csharp
// Input: Epoch1[1,2,3] Epoch2[4,5]
// Batch size: 10

// CORRECT (EpochBatchBlock):
// Output: Epoch1[[1,2,3]] Epoch2[[4,5]]
// Batches respect epoch boundaries

// INCORRECT (would not happen):
// Output: [[1,2,3,4,5]]  ← This would violate epoch boundaries
```

### 2. Streaming Processing

Blocks maintain streaming semantics - no buffering beyond what's necessary for the operation:

```csharp
// EpochTransformerBlock: Streams items through
await foreach (var epochStream in transformer.ExecuteAsync(input, ctx))
{
    // Each epoch is yielded as it's produced
    await foreach (var item in epochStream.Items)
    {
        // Items stream through - not buffered
    }
}
```

### 3. Lazy Evaluation

Epoch streams use deferred execution:

```csharp
// Pipeline definition (no execution yet)
var pipeline = batcher.ExecuteAsync(
    transformer.ExecuteAsync(
        segmenter.ExecuteAsync(source, ctx), 
        ctx), 
    ctx);

// Execution starts when enumerated
await foreach (var epochStream in pipeline)
{
    // Items flow through the pipeline as they're consumed
}
```

## Best Practices

### 1. Choose the Right Block

- **EpochTransformerBlock**: For item-level transformations, filtering, or explosions
- **SimpleEpochTransformerBlock**: For simple 1-to-1 synchronous mappings
- **EpochProcessorBlock**: For terminal processing with side effects
- **EpochBatchBlock**: For accumulating items into batches within epochs

### 2. Composition Strategies

```csharp
// Good: Segment early, process with epochs
PlainSource → Segmenter → EpochTransformer → EpochProcessor

// Good: Process without epochs when not needed
PlainSource → Transformer → Processor

// Avoid: Converting back and forth
PlainSource → Segmenter → UnwrapEpochs → Segmenter  // Inefficient
```

### 3. Error Handling

```csharp
var transformer = new EpochTransformerBlock<Order, OrderDto>(
    "transformer",
    async (order, ctx) => 
    {
        try
        {
            var dto = await MapAsync(order);
            yield return dto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to map order {Id}", order.Id);
            // Decide: rethrow, skip, or yield error marker
            throw;
        }
    });
```

### 4. Resource Management

```csharp
// EpochProcessorBlock is ideal for epoch-scoped resources
var processor = new EpochProcessorBlock<Order>(
    "db-saver",
    async (order, ctx) => 
    {
        // DbContext is scoped to actor, which is scoped per invocation
        // Perfect for epoch-based transactional boundaries
        await _dbContext.Orders.AddAsync(order);
        await _dbContext.SaveChangesAsync();
    });
```

## Performance Considerations

### 1. Batch Size Trade-offs

```csharp
// Small batches: More frequent I/O, less memory
var batcher1 = new EpochBatchBlock<Order>("small", maxBatchSize: 10);

// Large batches: Fewer I/O operations, more memory
var batcher2 = new EpochBatchBlock<Order>("large", maxBatchSize: 1000);

// Consider epoch size when choosing batch size:
// If epochs have 100 items and batch size is 1000,
// you'll always get batches of ~100 (epoch boundary)
```

### 2. Transformation Complexity

```csharp
// Lightweight transformations: Use SimpleEpochTransformerBlock
var simple = new SimpleEpochTransformerBlock<int, string>(
    "format", i => i.ToString());

// Complex async transformations: Use EpochTransformerBlock
var complex = new EpochTransformerBlock<Order, EnrichedOrder>(
    "enrich", async (order, ctx) => 
    {
        var customer = await _customerRepo.GetAsync(order.CustomerId);
        yield return new EnrichedOrder(order, customer);
    });
```

### 3. Epoch Sizing

```csharp
// Small epochs (10-100 items): Good for frequent commits, low latency
var segmenter1 = new EpochSegmenterBlock<Order>("small",
    EpochSegmentationPolicy.ByCount(50, "orders"));

// Large epochs (1000-10000 items): Better throughput, higher latency
var segmenter2 = new EpochSegmenterBlock<Order>("large",
    EpochSegmentationPolicy.ByCount(5000, "orders"));
```

## Testing

### Unit Testing Epoch-Aware Blocks

```csharp
[Fact]
public async Task EpochTransformerBlock_Should_PreserveEpochs()
{
    // Arrange
    var transformer = new SimpleEpochTransformerBlock<int, int>(
        "multiply", i => i * 2);
    
    var input = CreateMockEpochStream(
        new[] { (epoch: 1, items: new[] {1, 2, 3}) });
    
    // Act
    var output = await transformer.ExecuteAsync(input, ctx).ToListAsync();
    
    // Assert
    output.Count.ShouldBe(1);
    output[0].Epoch.GetSequence("test").ShouldBe(1);
    var items = await output[0].Items.ToListAsync();
    items.ShouldBe(new[] { 2, 4, 6 });
}
```

### Integration Testing

```csharp
[Fact]
public async Task FullPipeline_Should_ProcessCorrectly()
{
    // Arrange
    var source = new PlainSourceBlock<int, TestSource>("source", scopeFactory);
    var segmenter = new EpochSegmenterBlock<int>("segment",
        EpochSegmentationPolicy.ByCount(3, "test"));
    var transformer = new SimpleEpochTransformerBlock<int, int>(
        "double", i => i * 2);
    var processor = new EpochProcessorBlock<int>("process",
        async (i, ctx) => processedItems.Add(i));
    
    // Act
    var pipeline = processor.ExecuteAsync(
        transformer.ExecuteAsync(
            segmenter.ExecuteAsync(
                source.ExecuteAsync(empty, ctx), 
                ctx), 
            ctx), 
        ctx);
    
    await foreach (var _ in pipeline) { }
    
    // Assert
    processedItems.ShouldBe(new[] { 0, 2, 4, 6, 8, 10, 12, 14, 16, 18 });
}
```

## Related Documentation

- [PlainSourceBlock](./plain-source-block.md) - Epoch-agnostic data sources
- [EpochSegmenterBlock](./epoch-segmenter-block.md) - External epoch segmentation
- [Epoch Design](../epochs.md) - Overall epoch architecture

## Summary

Epoch-aware blocks complete the composability story for DataFlow pipelines:

- **PlainSourceBlock**: Data sources without epoch knowledge
- **EpochSegmenterBlock**: External epoch segmentation policy
- **Epoch-Aware Blocks**: Process data within epoch boundaries
- **Plain Blocks**: Process data without epoch structure

This architecture enables:
- ✅ Full composability between epoch and non-epoch pipelines
- ✅ Reusable sources across different segmentation strategies
- ✅ Standard processing patterns (transform, batch, process) within epochs
- ✅ Clean separation of concerns
- ✅ Flexible pipeline construction
