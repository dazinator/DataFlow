# EpochActorBlock: Epoch-Aware Stream Processing with DI Scope Safety

## Overview

`EpochActorBlock<TIn, TOut, TActor>` is the **recommended** primary block for epoch-aware stream processing. It processes items within epoch boundaries using the actor pattern, providing DI scope isolation and optional scope rotation while preserving epoch structure throughout the pipeline.

### Key Features

- **Epoch Boundary Preservation**: Items processed within epoch boundaries (never mixed across epochs)
- **DI Scope Isolation**: Each actor runs in its own async DI scope
- **Actor-Controlled Rotation**: Actors can request rotation to refresh their DI scope
- **Flexible Patterns**: Supports 1-to-1, 1-to-many, filtering, and processing
- **Type Safety**: Strong typing prevents runtime epoch boundary violations
- **Streaming Semantics**: Lazy evaluation with O(1) memory per item

## Architecture

### Components

```
┌──────────────────────────────────────────────────────────────┐
│         EpochActorBlock<TIn, TOut, TActor>                   │
│                                                               │
│  Input: IAsyncEnumerable<IEpochStream<TIn>>                 │
│         (stream of epoch streams)                            │
│                       │                                       │
│                       ▼                                       │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  For Each Epoch Stream:                                │ │
│  │  ┌──────────────────────────────────────────────────┐ │ │
│  │  │  DI Scope (per actor instance)                   │ │ │
│  │  │  ┌────────────────────────────────────────────┐ │ │ │
│  │  │  │  TActor : IStreamActor<TIn, TOut>          │ │ │ │
│  │  │  │  • Process items from this epoch only      │ │ │ │
│  │  │  │  • Can request rotation (optional)         │ │ │ │
│  │  │  │  • Scoped dependencies isolated            │ │ │ │
│  │  │  └────────────────────────────────────────────┘ │ │ │
│  │  └──────────────────────────────────────────────────┘ │ │
│  └────────────────────────────────────────────────────────┘ │
│                       │                                       │
│                       ▼                                       │
│  Output: IAsyncEnumerable<IEpochStream<TOut>>               │
│          (transformed epoch streams)                         │
└──────────────────────────────────────────────────────────────┘
```

### Data Flow

```
Input Epoch Stream → Actor (in DI scope) → Output Epoch Stream
Epoch{1,2,3} ──────→ Transform each ──────→ Epoch{A,B,C}
                     (within epoch)
```

**Critical Property**: Epoch metadata preserved, items never cross boundaries.

## Actor Lifecycle

1. **Epoch Stream Arrival**: New epoch stream enters the block
2. **Scope Creation**: Fresh DI scope created for this epoch's processing
3. **Actor Resolution**: Actor instance resolved from scope with dependencies
4. **Item Processing**: Actor processes items from this epoch only
5. **Optional Rotation**: Actor can call `context.RequestRotation()` to refresh scope
6. **Epoch Completion**: All items processed, yield output epoch stream
7. **Scope Disposal**: DI scope and actor disposed
8. **Next Epoch**: Process repeats for next epoch stream

## Usage

### Basic Actor Implementation

```csharp
public class OrderTransformActor : IStreamActor<Order, OrderDto>
{
    private readonly ILogger<OrderTransformActor> _logger;
    private readonly IMapper _mapper; // Scoped dependency
    
    public OrderTransformActor(ILogger<OrderTransformActor> logger, IMapper mapper)
    {
        _logger = logger;
        _mapper = mapper;
    }
    
    public async IAsyncEnumerable<OrderDto> RunAsync(
        IAsyncEnumerable<Order> input,
        IActorExecutionContext context)
    {
        await foreach (var order in input.WithCancellation(context.CancellationToken))
        {
            var dto = _mapper.Map<OrderDto>(order);
            yield return dto;
        }
    }
}
```

### Pipeline Configuration

```csharp
// Register services
services.AddTransient<OrderSource>();
services.AddScoped<IMapper, AutoMapper>();
services.AddTransient<OrderTransformActor>();

var provider = services.BuildServiceProvider();
var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

// Create pipeline: Source → Segmenter → EpochActorBlock
var sourceBlock = new PlainSourceBlock<Order, OrderSource>("source", scopeFactory);

var segmenterBlock = new EpochSegmenterBlock<Order>("segmenter",
    EpochSegmentationPolicy.ByCount(100, "orders"));

var transformBlock = new EpochActorBlock<Order, OrderDto, OrderTransformActor>(
    "transform",
    scopeFactory);

// Execute pipeline
var context = new ExecutionContext();
var orders = sourceBlock.ExecuteAsync(EmptyInput(), context);
var epochStreams = segmenterBlock.ExecuteAsync(orders, context);
var transformedEpochs = transformBlock.ExecuteAsync(epochStreams, context);

await foreach (var epochStream in transformedEpochs)
{
    await foreach (var dto in epochStream.Items)
    {
        // Process transformed items
    }
}
```

## Processing Patterns

### 1. One-to-One Transformation

Transform each input item to one output item.

```csharp
public class DoubleActor : IStreamActor<int, int>
{
    public async IAsyncEnumerable<int> RunAsync(
        IAsyncEnumerable<int> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            yield return item * 2;
        }
    }
}
```

**Result**: Each epoch maintains same item count.

### 2. One-to-Many Transformation

Transform each input item to multiple output items.

```csharp
public class SplitActor : IStreamActor<string, string>
{
    public async IAsyncEnumerable<string> RunAsync(
        IAsyncEnumerable<string> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            foreach (var word in item.Split(' '))
            {
                yield return word;
            }
        }
    }
}
```

**Result**: Epochs may have more items than input, all within epoch boundaries.

### 3. Filtering (One-to-Zero-or-One)

Filter items based on criteria.

```csharp
public class FilterEvenActor : IStreamActor<int, int>
{
    public async IAsyncEnumerable<int> RunAsync(
        IAsyncEnumerable<int> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            if (item % 2 == 0)
            {
                yield return item;
            }
        }
    }
}
```

**Result**: Epochs may have fewer items, some may be empty.

### 4. Processing (Terminal/Side Effects)

Process items with side effects, no output.

```csharp
public class LoggingActor : IStreamActor<Order, object>
{
    private readonly ILogger<LoggingActor> _logger;
    
    public LoggingActor(ILogger<LoggingActor> logger)
    {
        _logger = logger;
    }
    
    public async IAsyncEnumerable<object> RunAsync(
        IAsyncEnumerable<Order> input,
        IActorExecutionContext context)
    {
        await foreach (var order in input.WithCancellation(context.CancellationToken))
        {
            _logger.LogInformation("Processing order {OrderId}", order.Id);
            // No yield - terminal processing
        }
        
        // Must yield break to complete the stream
        yield break;
    }
}
```

**Result**: Epoch streams complete without emitting items (terminal block).

## Advanced Features

### Scope Rotation

Actors can request scope rotation to refresh dependencies and prevent memory buildup.

```csharp
public class CachingActor : IStreamActor<Order, OrderSummary>
{
    private readonly ICache _cache; // Scoped cache
    private int _itemsProcessed = 0;
    
    public CachingActor(ICache cache)
    {
        _cache = cache;
    }
    
    public async IAsyncEnumerable<OrderSummary> RunAsync(
        IAsyncEnumerable<Order> input,
        IActorExecutionContext context)
    {
        await foreach (var order in input.WithCancellation(context.CancellationToken))
        {
            var summary = await ProcessWithCache(order);
            yield return summary;
            
            _itemsProcessed++;
            
            // Rotate after processing 1000 items to clear cache
            if (_itemsProcessed >= 1000)
            {
                context.RequestRotation();
                yield break; // Exit, block will create new actor instance
            }
        }
    }
}
```

**Behavior**: 
- Block creates new DI scope after rotation
- New actor instance continues processing remaining items
- Useful for long-running streams within single epoch

### Stateful Processing with Dependencies

```csharp
public class DbWriterActor : IStreamActor<Order, object>
{
    private readonly AppDbContext _dbContext; // Scoped per actor
    
    public DbWriterActor(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async IAsyncEnumerable<object> RunAsync(
        IAsyncEnumerable<Order> input,
        IActorExecutionContext context)
    {
        await foreach (var order in input.WithCancellation(context.CancellationToken))
        {
            _dbContext.Orders.Add(order);
        }
        
        // Save all orders from this epoch as single transaction
        await _dbContext.SaveChangesAsync(context.CancellationToken);
        
        yield break; // Terminal processing
    }
}
```

**Benefit**: Each epoch gets its own `DbContext`, natural transaction boundaries.

## Composability Patterns

### Pattern 1: Transform → Batch → Process

```csharp
// Plain source → Segment → Transform → Batch → Process
var sourceBlock = new PlainSourceBlock<Invoice, InvoiceSource>("source", scopeFactory);

var segmenterBlock = new EpochSegmenterBlock<Invoice>("seg",
    EpochSegmentationPolicy.ByCount(100, "invoices"));

var transformBlock = new EpochActorBlock<Invoice, InvoiceDto, MapperActor>(
    "map", scopeFactory);

var batchBlock = new EpochBatchBlock<InvoiceDto>("batch", maxBatchSize: 50);

var writerBlock = new EpochActorBlock<InvoiceDto[], object, BatchWriterActor>(
    "write", scopeFactory);

// Chain blocks
var invoices = sourceBlock.ExecuteAsync(EmptyInput(), context);
var epochs = segmenterBlock.ExecuteAsync(invoices, context);
var mapped = transformBlock.ExecuteAsync(epochs, context);
var batched = batchBlock.ExecuteAsync(mapped, context);
await foreach (var _ in writerBlock.ExecuteAsync(batched, context)) { }
```

### Pattern 2: Filter → Transform

```csharp
// Filter out invalid items, then transform
var filterBlock = new EpochActorBlock<Order, Order, ValidationActor>(
    "filter", scopeFactory);

var transformBlock = new EpochActorBlock<Order, OrderDto, MapperActor>(
    "transform", scopeFactory);

// Chain: epochs → filter → transform
var filtered = filterBlock.ExecuteAsync(epochStreams, context);
var transformed = transformBlock.ExecuteAsync(filtered, context);
```

## Performance Characteristics

### Memory Overhead

| Component | Memory | Notes |
|-----------|--------|-------|
| Block instance | ~64 bytes | Fixed per block |
| Per epoch | ~0 bytes | No epoch-level state |
| Per item | O(1) | Streaming, no buffering |
| DI scope | Varies | Depends on dependencies |

### Processing Overhead

From benchmarks:
- **Micro-benchmark**: ~10% overhead vs plain blocks
- **Realistic workload**: <2% overhead (with 30ms processing per item)
- **Conclusion**: Overhead negligible in real-world scenarios

### Throughput

- Maintains streaming semantics (lazy evaluation)
- No unnecessary buffering
- Backpressure handled naturally via async enumeration

## Comparison with Other Blocks

### vs. ActorBlock

| Aspect | ActorBlock | EpochActorBlock |
|--------|------------|-----------------|
| Input | `IAsyncEnumerable<T>` | `IAsyncEnumerable<IEpochStream<T>>` |
| Output | `IAsyncEnumerable<T>` | `IAsyncEnumerable<IEpochStream<T>>` |
| Boundaries | None | Epoch boundaries preserved |
| DI Scope | Per actor instance | Per actor instance (same) |
| Use Case | Plain streams | Epoch-structured streams |

**Design**: EpochActorBlock mirrors ActorBlock pattern exactly, just with epoch awareness.

### vs. TransformerBlock/ProcessorBlock

EpochActorBlock **consolidates** these patterns with DI safety:

| Feature | TransformerBlock | ProcessorBlock | EpochActorBlock |
|---------|------------------|----------------|-----------------|
| DI Scope | ❌ No | ❌ No | ✅ Yes |
| Rotation | ❌ No | ❌ No | ✅ Yes |
| 1-to-1 | ✅ Yes | N/A | ✅ Yes |
| 1-to-many | ✅ Yes | N/A | ✅ Yes |
| Filtering | ✅ Yes | N/A | ✅ Yes |
| Processing | N/A | ✅ Yes | ✅ Yes |
| Epochs | ❌ No | ❌ No | ✅ Yes |

**Recommendation**: Use `EpochActorBlock` as the unified pattern for epoch-aware processing.

## Best Practices

### 1. Respect Epoch Boundaries

```csharp
// ✅ Good: Process within epoch
public async IAsyncEnumerable<T> RunAsync(
    IAsyncEnumerable<T> input,
    IActorExecutionContext context)
{
    await foreach (var item in input.WithCancellation(context.CancellationToken))
    {
        yield return Transform(item);
    }
}

// ❌ Bad: Buffering entire epoch
public async IAsyncEnumerable<T> RunAsync(
    IAsyncEnumerable<T> input,
    IActorExecutionContext context)
{
    var allItems = await input.ToListAsync(); // DON'T buffer!
    foreach (var item in allItems)
    {
        yield return Transform(item);
    }
}
```

### 2. Always Pass Cancellation Token

```csharp
// ✅ Good: Respect cancellation
await foreach (var item in input.WithCancellation(context.CancellationToken))
{
    yield return item;
}

// ❌ Bad: Ignoring cancellation
await foreach (var item in input)
{
    yield return item;
}
```

### 3. Use Scope Rotation Wisely

```csharp
// ✅ Good: Rotate to prevent memory buildup
if (accumulatedStateSize > threshold)
{
    context.RequestRotation();
    yield break;
}

// ❌ Bad: Rotating too frequently (overhead)
foreach (var item in ...)
{
    yield return item;
    context.RequestRotation(); // Too frequent!
}
```

### 4. Leverage Scoped Dependencies

```csharp
// ✅ Good: Use scoped dependencies safely
public class MyActor : IStreamActor<T, T>
{
    private readonly DbContext _db; // Scoped - safe!
    
    public MyActor(DbContext db)
    {
        _db = db;
    }
}

// ❌ Bad: Singleton service with state (if concurrent)
public class MyActor : IStreamActor<T, T>
{
    private readonly MySingletonService _service; // Avoid if stateful
}
```

## Troubleshooting

### Issue: Type Mismatch When Connecting Blocks

**Problem:**
```csharp
var plainTransform = new TransformerBlock<Order, OrderDto>(...);
var epochActor = new EpochActorBlock<OrderDto, ...>(...);
// Cannot connect: plainTransform outputs OrderDto, epochActor expects IEpochStream<OrderDto>
```

**Solution:** Use EpochSegmenterBlock to bridge:
```csharp
var segmenter = new EpochSegmenterBlock<OrderDto>("seg", policy);
var plainOutput = plainTransform.ExecuteAsync(...);
var epochs = segmenter.ExecuteAsync(plainOutput, context);
var actorOutput = epochActor.ExecuteAsync(epochs, context);
```

### Issue: Items Crossing Epoch Boundaries

**Problem:** Actor implementation inadvertently mixes epochs.

**Solution:** EpochActorBlock prevents this by design - each actor processes one epoch at a time. Verify actor doesn't cache items across `RunAsync` invocations.

### Issue: Memory Growth in Long Epochs

**Problem:** Large epochs cause memory buildup in scoped dependencies.

**Solution:** Use scope rotation:
```csharp
private int _processedCount = 0;

public async IAsyncEnumerable<T> RunAsync(...)
{
    await foreach (var item in input)
    {
        yield return Process(item);
        
        if (++_processedCount > 10000)
        {
            context.RequestRotation(); // Refresh scope
            yield break;
        }
    }
}
```

## Design Decisions

### Why Consolidate Transformer/Processor?

**Rationale**: Separate blocks (`EpochTransformerBlock`, `EpochProcessorBlock`) would duplicate code and lack DI safety. `EpochActorBlock` provides:

1. **Single Pattern**: One block type for all processing patterns
2. **DI Safety**: Scope isolation prevents concurrent dependency bugs
3. **Flexibility**: Handles 1-to-1, 1-to-many, filtering, processing in one block
4. **Consistency**: Mirrors `ActorBlock` pattern for familiarity

**Trade-off**: Requires implementing `IStreamActor` vs. simple lambda (acceptable for added safety).

### Why Mirror ActorBlock?

**Rationale**: Consistency with existing patterns:
- Developers familiar with `ActorBlock` immediately understand `EpochActorBlock`
- Same DI scope behavior
- Same rotation mechanism
- Only difference: operates on epoch streams

## See Also

- [ActorBlock](./actor-block.md) - Plain stream actor pattern
- [EpochBatchBlock](./epoch-batch-block.md) - Batching within epochs
- [EpochSegmenterBlock](./epoch-segmenter-block.md) - Epoch segmentation
- [PlainSourceBlock](./plain-source-block.md) - Plain data sources
- [Using Epochs Guide](/poc/docs/guides/using-epochs.md) - Epoch usage patterns
