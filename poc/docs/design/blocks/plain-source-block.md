# PlainSourceBlock: Epoch-Agnostic Data Sources

## Overview

`PlainSourceBlock<T, TActor>` is a source block that hosts plain source actors producing continuous data streams without epoch knowledge. This enables separation of concerns where sources focus solely on data production while epoch segmentation is handled externally.

### Key Features

- **Epoch-Agnostic**: Sources emit plain `IAsyncEnumerable<T>` without epoch boundaries
- **Reusability**: Same source can be used with different epoch segmentation strategies
- **DI Scope Management**: Each source actor runs in its own `IServiceScope`
- **Simplified Testing**: Source logic can be tested independently of epoch concerns
- **Optional Epochs**: Sources can bypass epoch infrastructure entirely for non-epoch pipelines

## Architecture

### Components

```
┌────────────────────────────────────────────────────────┐
│           PlainSourceBlock<T, TActor>                  │
│                                                         │
│  ┌──────────────────────────────────────────────┐    │
│  │  DI Scope                                     │    │
│  │  ┌────────────────────────────────────────┐ │    │
│  │  │  TActor : IPlainSourceActor<T>          │ │    │
│  │  │  • ProduceAsync() → IAsyncEnumerable<T> │ │    │
│  │  │  • No epoch knowledge                    │ │    │
│  │  │  • Focus on data production only         │ │    │
│  │  └────────────────────────────────────────┘ │    │
│  └──────────────────────────────────────────────┘    │
│                       │                                │
│                       ▼                                │
│              IAsyncEnumerable<T>                      │
│         (plain data stream, no epochs)                │
└────────────────────────────────────────────────────────┘
```

### Data Flow

```
PlainSourceBlock → IAsyncEnumerable<T> → EpochSegmenterBlock (optional)
                                      ↘
                                        TransformerBlock (non-epoch pipeline)
```

## Usage

### Basic Source Implementation

```csharp
public class OrderSource : PlainSourceActorBase<Order>
{
    private readonly IOrderRepository _repository;
    
    public OrderSource(IOrderRepository repository)
    {
        _repository = repository;
    }
    
    public override async IAsyncEnumerable<Order> ProduceAsync(
        IActorExecutionContext context)
    {
        await foreach (var order in _repository.StreamOrdersAsync(context.CancellationToken))
        {
            yield return order;
        }
    }
}
```

### Pipeline Configuration

```csharp
// Register services
services.AddScoped<IOrderRepository, OrderRepository>();
services.AddTransient<OrderSource>();

// Create pipeline
var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
var source = new PlainSourceBlock<Order, OrderSource>("orders", scopeFactory);

// Option 1: With epoch segmentation
var segmenter = new EpochSegmenterBlock<Order>("segmenter",
    EpochSegmentationPolicy.ByKey<Order, DateTime>(o => o.Date.Date, "orders"));
// Pipeline: source → segmenter → downstream

// Option 2: Without epochs (direct processing)
var transformer = new TransformerBlock<Order, Invoice>("toInvoice", ...);
// Pipeline: source → transformer → downstream
```

## Integration Patterns

### Pattern 1: With Epoch Segmentation

```csharp
var source = new PlainSourceBlock<T, MySource>("source", scopeFactory);
var segmenter = new EpochSegmenterBlock<T>("segmenter",
    EpochSegmentationPolicy.ByCount(100, "source"));

// source → segmenter → epoch-aware downstream blocks
```

### Pattern 2: Multiple Segmentation Strategies

```csharp
var source = new PlainSourceBlock<Order, OrderSource>("source", scopeFactory);

// Pipeline A: Daily epochs
var dailySegmenter = new EpochSegmenterBlock<Order>("daily",
    EpochSegmentationPolicy.ByKey<Order, DateTime>(o => o.Date.Date, "source"));

// Pipeline B: Customer-based epochs
var customerSegmenter = new EpochSegmenterBlock<Order>("customer",
    EpochSegmentationPolicy.ByKey<Order, int>(o => o.CustomerId, "source"));

// Same source, different epoch strategies
```

### Pattern 3: No Epoch Infrastructure

```csharp
var source = new PlainSourceBlock<Order, OrderSource>("source", scopeFactory);
var transform = new TransformerBlock<Order, OrderDto>("transform", ...);
var sink = new ProcessorBlock<OrderDto>("sink", ...);

// source → transform → sink (no epochs needed)
```

## Design Benefits

### Separation of Concerns

**Before (Source-Centric):**
```csharp
public class OrderSource : SourceActorBase<Order>
{
    public override async IAsyncEnumerable<IEpochStream<Order>> ProduceEpochsAsync(...)
    {
        // Source must handle both data production AND epoch segmentation
        var data = await FetchOrders();
        await foreach (var epoch in EpochSegmenter.SegmentByKey(data, ...))
            yield return epoch;
    }
}
```

**After (Decoupled):**
```csharp
public class OrderSource : PlainSourceActorBase<Order>
{
    public override async IAsyncEnumerable<Order> ProduceAsync(...)
    {
        // Source only handles data production
        return await FetchOrders();
    }
}

// Epoch segmentation is external configuration
var segmenter = new EpochSegmenterBlock<Order>("seg",
    EpochSegmentationPolicy.ByKey<Order, DateTime>(o => o.Date, "orders"));
```

### Reusability

The same source can be used in multiple contexts:

```csharp
var source = new PlainSourceBlock<Order, OrderSource>("source", scopeFactory);

// Use case 1: Real-time processing (no epochs)
realTimePipeline.AddSource(source);

// Use case 2: Batch processing (daily epochs)
batchPipeline.AddSource(source)
    .WithSegmentation(EpochSegmentationPolicy.ByKey(...));

// Use case 3: Testing (no epochs)
testPipeline.AddSource(source);
```

## Comparison with EpochSourceBlock

| Feature | PlainSourceBlock | EpochSourceBlock |
|---------|------------------|------------------|
| **Output** | `IAsyncEnumerable<T>` | `IAsyncEnumerable<IEpochStream<T>>` |
| **Epoch Knowledge** | None | Full |
| **Segmentation** | External (via EpochSegmenterBlock) | Internal (in source) |
| **Reusability** | High (works with/without epochs) | Limited (always produces epochs) |
| **Testing** | Simple (test data production only) | Complex (must handle epochs) |
| **Flexibility** | High (change strategy via config) | Low (change requires source modification) |

## Implementation Details

### Actor Interface

```csharp
public interface IPlainSourceActor<T>
{
    IAsyncEnumerable<T> ProduceAsync(IActorExecutionContext context);
}

public abstract class PlainSourceActorBase<T> : IPlainSourceActor<T>
{
    public abstract IAsyncEnumerable<T> ProduceAsync(IActorExecutionContext context);
}
```

### Lifecycle

1. **Initialization**: Block creates DI scope
2. **Actor Resolution**: Actor instance resolved from scope
3. **Production**: Actor's `ProduceAsync()` called to produce data stream
4. **Stream Output**: Items yielded directly to downstream blocks
5. **Cleanup**: Scope disposed when stream completes

### Error Handling

```csharp
public class ResilientOrderSource : PlainSourceActorBase<Order>
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<ResilientOrderSource> _logger;
    
    public override async IAsyncEnumerable<Order> ProduceAsync(
        [EnumeratorCancellation] IActorExecutionContext context)
    {
        while (!context.CancellationToken.IsCancellationRequested)
        {
            Order? order = null;
            try
            {
                order = await _repository.GetNextOrderAsync(context.CancellationToken);
                if (order == null) break;
                
                yield return order;
            }
            catch (Exception ex) when (order != null)
            {
                _logger.LogError(ex, "Error processing order {OrderId}", order.Id);
                // Continue with next order
            }
        }
    }
}
```

## Best Practices

### 1. Keep Sources Focused

```csharp
// ✅ Good: Source focuses on data access
public class OrderSource : PlainSourceActorBase<Order>
{
    public override async IAsyncEnumerable<Order> ProduceAsync(...)
    {
        return _repository.StreamOrdersAsync();
    }
}

// ❌ Bad: Source handles epochs and business logic
public class OrderSource : PlainSourceActorBase<Order>
{
    public override async IAsyncEnumerable<Order> ProduceAsync(...)
    {
        // Don't segment or transform here - keep sources pure
        var orders = _repository.GetOrders();
        foreach (var order in orders.GroupBy(o => o.Date))
            foreach (var o in order) yield return o;
    }
}
```

### 2. Use Appropriate Dependencies

```csharp
// ✅ Good: Scoped dependencies for data access
public class OrderSource : PlainSourceActorBase<Order>
{
    private readonly DbContext _dbContext; // Scoped
    private readonly ILogger _logger;      // Singleton (injected)
    
    public OrderSource(DbContext dbContext, ILogger<OrderSource> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }
}
```

### 3. Respect Cancellation

```csharp
public override async IAsyncEnumerable<Order> ProduceAsync(
    [EnumeratorCancellation] IActorExecutionContext context)
{
    await foreach (var order in _repository.StreamOrdersAsync(context.CancellationToken))
    {
        // Check cancellation periodically for long operations
        context.CancellationToken.ThrowIfCancellationRequested();
        yield return order;
    }
}
```

## Migration from EpochSourceBlock

To migrate from `EpochSourceBlock` to `PlainSourceBlock`:

### Before

```csharp
public class MySource : SourceActorBase<T>
{
    public override async IAsyncEnumerable<IEpochStream<T>> ProduceEpochsAsync(...)
    {
        var data = await GetData();
        await foreach (var epoch in EpochSegmenter.SegmentByKey(data, ...))
            yield return epoch;
    }
}

var source = new EpochSourceBlock<T, MySource>("source", scopeFactory);
```

### After

```csharp
public class MySource : PlainSourceActorBase<T>
{
    public override async IAsyncEnumerable<T> ProduceAsync(...)
    {
        return await GetData(); // Just return data, no segmentation
    }
}

var source = new PlainSourceBlock<T, MySource>("source", scopeFactory);
var segmenter = new EpochSegmenterBlock<T>("segmenter",
    EpochSegmentationPolicy.ByKey<T, TKey>(keySelector, "source"));
```

## See Also

- [EpochSegmenterBlock](./epoch-segmenter-block.md) - External epoch segmentation
- [EpochSourceBlock](./epoch-source-block.md) - Source-centric epoch approach (legacy)
- [ActorBlock](./actor-block.md) - General actor-based block pattern
- [Epoch Segmentation Design](/research/epoch-stream-separation/design/decoupled-epoch-architecture.md)
