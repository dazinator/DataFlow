# Using Epochs in DataFlow POC

## Overview

Epochs provide transactional boundaries and coordination capabilities in DataFlow pipelines. This guide explains the modern epoch system using graph-level configuration for building transactional data processing pipelines.

## What are Epochs?

An **epoch** represents a logical boundary in the data stream that groups related items together. The epoch system provides:

- **Transaction lifecycle management**: Begin, commit, and rollback transactions via lifecycle hooks
- **Serialized operation execution**: Queue operations that execute serially within epoch scope
- **Multi-processor concurrency**: Configure 1-N processors for serial vs parallel epoch processing
- **DI scope isolation**: Each epoch has its own dependency injection scope
- **MSDTC-safe design**: Fully serial execution prevents distributed transaction escalation

## Key Concepts

### Epoch Architecture

The epoch system consists of two specialized nodes and graph-level configuration:

#### EpochSourceNode
Creates and publishes epochs to an internal stream. Acts as the coordinator for epoch creation.

```csharp
public sealed class EpochSourceNode
{
    public ChannelReader<IEpoch> EpochReader { get; }
    public Task PublishEpochAsync(IEpoch epoch);
    public void SignalCompletion();
}
```

#### EpochProcessorNode
Consumes epochs from the source node and processes them by:
1. Executing lifecycle hooks (OnBeginEpoch, OnCommitEpoch, OnEpochError)
2. Draining serialized operation queues
3. Managing epoch disposal

```csharp
public sealed class EpochProcessorNode : IAsyncDisposable
{
    public EpochProcessorNode(EpochSourceNode source, EpochHooks? hooks = null);
    public Task CompletionTask { get; }
}
```

#### Graph-Level Configuration
Configure epoch segmentation and lifecycle at the graph level using `ConfigureEpochs()`:

```csharp
var builder = new DataFlowGraphBuilder(serviceProvider, "my-graph");

builder.ConfigureEpochs(config =>
{
    // Count-based: Create new epoch every 100 items
    config.SetPolicy(EpochPolicy.ByCount(100));
    
    // Time-based: Create new epoch every 5 seconds
    // config.SetPolicy(EpochPolicy.ByTime(TimeSpan.FromSeconds(5)));
    
    // Both: Create epoch when EITHER condition is met
    // config.SetPolicy(EpochPolicy.ByCountOrTime(100, TimeSpan.FromSeconds(5)));
    
    config.AddProcessor("processor1");
    
    // Lifecycle hooks
    config.OnBeginEpoch(async (epoch, ct) => { /* start transaction */ });
    config.OnCommitEpoch(async (epoch, ct) => { /* commit transaction */ });
    config.OnEpochError(async (epoch, ex, ct) => { /* handle error */ });
});
```

### Epoch Scopes and DI

Each epoch has its own DI scope, enabling scoped services (like `DbContext`) to be shared across all operations within that epoch:

```csharp
public interface IEpoch : IAsyncDisposable
{
    EpochVector Vector { get; }
    IServiceProvider ServiceProvider { get; }
    
    Task QueueSerializedOperationAsync<TService>(
        Func<TService, Task> operation,
        CancellationToken cancellationToken = default)
        where TService : notnull;
}
```

### Serialized Operations

All operations within an epoch execute **fully serially** through a single channel:

```csharp
// All operations queued to this epoch will execute in FIFO order
await epoch.QueueSerializedOperationAsync<DbContext>(async db =>
{
    // Operation executes with epoch-scoped DbContext
    db.Orders.Add(new Order { ... });
    await db.SaveChangesAsync();
});
```

**Key Benefits**:
- ✅ No MSDTC escalation (only one connection active at a time)
- ✅ Thread-safe by design (no concurrent access)
- ✅ Deterministic execution order
- ✅ Automatic service resolution from epoch scope

## When to Use Epochs

### Use Epochs When You Need:

1. **Transactional Database Operations**: Writing to databases where you need transaction lifecycle control
2. **Scoped Service Coordination**: Multiple blocks need to share scoped services (like `DbContext`)
3. **Serialized Execution**: Operations must execute in order without concurrency
4. **Lifecycle Hooks**: Need to execute custom logic at epoch boundaries (begin/commit/error)

### Use Plain Blocks When:

1. **Stateless Transformations**: Simple data mapping without transaction concerns
2. **Continuous Streams**: Unbounded streams without natural boundaries
3. **No Shared State**: Each item processed independently

## Epoch Segmentation Policies

Configure how items are grouped into epochs using `EpochPolicy`:

### Count-Based Segmentation

Create a new epoch every N items:

```csharp
config.SetPolicy(EpochPolicy.ByCount(1000));
```

**Best For**: Bulk operations, batch processing

### Time-Based Segmentation

Create a new epoch after a time window:

```csharp
config.SetPolicy(EpochPolicy.ByTime(TimeSpan.FromSeconds(5)));
```

**Best For**: Time-sensitive processing, periodic checkpoints

### Combined Policy

Create a new epoch when EITHER condition is met:

```csharp
config.SetPolicy(EpochPolicy.ByCountOrTime(100, TimeSpan.FromSeconds(5)));
```

**Best For**: Ensuring both throughput and latency bounds

## Choosing Epoch Granularity

### Coarse Epochs (Many Items per Epoch)

```csharp
config.SetPolicy(EpochPolicy.ByCount(1000));
```

**Pros**:
- Efficient bulk operations (e.g., bulk database inserts)
- Lower overhead per epoch
- Better throughput

**Cons**:
- Less frequent checkpointing
- Larger rollback units on failure

**Use When**: Bulk insert scenarios, high-throughput requirements

### Fine Epochs (Few Items per Epoch)

```csharp
config.SetPolicy(EpochPolicy.ByCount(1));
```

**Pros**:
- Frequent checkpointing
- Small rollback units
- Better failure recovery

**Cons**:
- Higher per-epoch overhead
- May not leverage bulk operations

**Use When**: Critical data, need fine-grained recovery

### Recommendation

Start with **coarse epochs** (hundreds to thousands of items) for efficiency. Refine based on:
- Failure recovery requirements
- Bulk operation capabilities
- Checkpointing frequency needs

## Working with Epoch Sources

### Creating an Epoch Source

Define a source actor that produces epoch streams:

```csharp
public class MySourceActor : ISourceActor<Invoice>
{
    private readonly IDataService _dataService;
    
    public MySourceActor(IDataService dataService)
    {
        _dataService = dataService;
    }
    
    public async IAsyncEnumerable<IEpochStream<Invoice>> ProduceEpochsAsync(
        IActorExecutionContext context)
    {
        var invoices = await _dataService.GetInvoicesAsync();
        yield return invoices.WrapInSingleEpoch("invoice-source", context.CancellationToken);
    }
}

// Usage in graph
builder.AddSource<Invoice, MySourceActor>("invoice-source");
```

### Multi-Source Coordination

When working with multiple sources, each source produces its own epoch streams:

```csharp
// Source 1: Orders
builder.AddSource<Order, OrderSourceActor>("order-source");

// Source 2: Customers  
builder.AddSource<Customer, CustomerSourceActor>("customer-source");

// Both sources can share epoch configuration
builder.ConfigureEpochs(config =>
{
    config.SetPolicy(EpochPolicy.ByCount(100));
    config.AddProcessor("order-processor");
    config.AddProcessor("customer-processor");
});
```

## Lifecycle Hooks

### OnBeginEpoch

Called when an epoch begins, before any operations execute:

```csharp
config.OnBeginEpoch(async (epoch, ct) =>
{
    var db = epoch.ServiceProvider.GetRequiredService<DbContext>();
    await db.Database.BeginTransactionAsync(ct);
});
```

**Use Cases**:
- Start database transactions
- Initialize epoch-scoped resources
- Log epoch boundaries

### OnCommitEpoch

Called after all operations complete successfully:

```csharp
config.OnCommitEpoch(async (epoch, ct) =>
{
    var db = epoch.ServiceProvider.GetRequiredService<DbContext>();
    await db.Database.CommitTransactionAsync(ct);
});
```

**Use Cases**:
- Commit database transactions
- Write checkpoints
- Emit completion events

### OnEpochError

Called when an error occurs during epoch processing:

```csharp
config.OnEpochError(async (epoch, exception, ct) =>
{
    var db = epoch.ServiceProvider.GetRequiredService<DbContext>();
    await db.Database.RollbackTransactionAsync(ct);
    
    // Log error with epoch context
    _logger.LogError(exception, "Epoch {EpochVector} failed", epoch.Vector);
});
```

**Use Cases**:
- Rollback transactions
- Log errors with epoch context
- Trigger alerts

## Examples

### Example 1: Transactional Database Writes

```csharp
public class DatabaseWriteActor : IStreamActor<Invoice, object>
{
    private readonly ILogger<DatabaseWriteActor> _logger;
    
    public async IAsyncEnumerable<object> RunAsync(
        IAsyncEnumerable<Invoice> input,
        IActorExecutionContext context)
    {
        await foreach (var invoice in input.WithCancellation(context.CancellationToken))
        {
            // Queue database write - executes serially within epoch
            await context.EpochCoordinator!.CurrentEpoch!.QueueSerializedOperationAsync<DbContext>(
                async db =>
                {
                    db.Invoices.Add(invoice);
                    await db.SaveChangesAsync();
                });
        }
        
        yield return new object(); // Signal completion
    }
}

// Configure graph
var builder = new DataFlowGraphBuilder(serviceProvider, "invoice-pipeline");

builder.AddSource<Invoice, InvoiceSourceActor>("source");
builder.AddActor<Invoice, object, DatabaseWriteActor>("writer")
    .ReceiveFrom("source");

builder.ConfigureEpochs(config =>
{
    config.SetPolicy(EpochPolicy.ByCount(100));
    config.AddProcessor("writer");
    
    config.OnBeginEpoch(async (epoch, ct) =>
    {
        var db = epoch.ServiceProvider.GetRequiredService<DbContext>();
        await db.Database.BeginTransactionAsync(ct);
    });
    
    config.OnCommitEpoch(async (epoch, ct) =>
    {
        var db = epoch.ServiceProvider.GetRequiredService<DbContext>();
        await db.Database.CommitTransactionAsync(ct);
    });
});

var graph = builder.Build();
await graph.ExecuteAsync(executionContext);
```

### Example 2: ETL Pipeline with Batching

```csharp
// Source: Read data
builder.AddSource<RawRecord, DataSourceActor>("source");

// Transform: Validate and enrich
builder.AddActor<RawRecord, ValidatedRecord, ValidationActor>("validator")
    .ReceiveFrom("source");
    
builder.AddActor<ValidatedRecord, EnrichedRecord, EnrichmentActor>("enricher")
    .ReceiveFrom("validator");

// Batch for efficient writes
builder.AddBatch<EnrichedRecord>("batcher", maxSize: 100)
    .ReceiveFrom("enricher");

// Write batches
builder.AddProcessor<EnrichedRecord[], BatchWriterActor>("writer")
    .ReceiveFrom("batcher");

// Configure epochs for transactional writes
builder.ConfigureEpochs(config =>
{
    config.SetPolicy(EpochPolicy.ByCountOrTime(1000, TimeSpan.FromSeconds(30)));
    config.AddProcessor("writer");
    
    config.OnBeginEpoch(async (epoch, ct) =>
    {
        // Start transaction
    });
    
    config.OnCommitEpoch(async (epoch, ct) =>
    {
        // Commit transaction and checkpoint
    });
});
```

## Best Practices

1. **Respect Epoch Boundaries**: Never mix items from different epochs
2. **Choose Appropriate Granularity**: Balance efficiency vs. checkpointing frequency
3. **Use Lifecycle Hooks**: Leverage hooks for transaction management
4. **Streaming Semantics**: Use `yield return` for lazy evaluation
5. **Handle Cancellation**: Always pass through cancellation tokens
6. **Cleanup Resources**: Use lifecycle hooks for resource disposal

## Common Pitfalls

### ❌ Buffering Entire Epoch

```csharp
// DON'T: Buffers entire epoch in memory
var allItems = await epochStream.Items.ToListAsync();
foreach (var item in allItems)
{
    yield return Transform(item);
}
```

### ✅ Streaming Processing

```csharp
// DO: Stream items one at a time
await foreach (var item in epochStream.Items)
{
    yield return Transform(item);
}
```

### ❌ Ignoring Cancellation

```csharp
// DON'T: Ignores cancellation
await foreach (var item in input)
{
    yield return Transform(item);
}
```

### ✅ Proper Cancellation

```csharp
// DO: Pass cancellation token
await foreach (var item in input.WithCancellation(context.CancellationToken))
{
    context.CancellationToken.ThrowIfCancellationRequested();
    yield return Transform(item);
}
```

## Performance Considerations

### Epoch Overhead

- **Epoch creation**: < 1μs per epoch
- **Operation queuing**: < 10μs per operation  
- **Lifecycle hook execution**: Depends on implementation

### Optimization Tips

1. **Batch operations**: Use larger epochs for better throughput
2. **Minimize hooks**: Only use hooks when necessary
3. **Reuse scopes**: Leverage epoch-scoped DI for shared resources
4. **Parallel processors**: Configure multiple processors for concurrency

## Troubleshooting

### Deadlocks

**Symptom**: Pipeline hangs
**Cause**: Circular dependencies in serialized operations
**Solution**: Ensure operations don't wait on each other

### Memory Growth

**Symptom**: Increasing memory usage
**Cause**: Large epoch sizes or unbounded buffers
**Solution**: Reduce epoch size or add backpressure

### Slow Throughput

**Symptom**: Low items/second
**Cause**: Small epoch sizes or heavy lifecycle hooks
**Solution**: Increase epoch size, optimize hooks

## See Also

- [EF Core with Epochs Guide](./ef-core-epochs.md) - Specific guidance for Entity Framework Core
- [Epoch Vectors](../design/epoch-vectors.md) - Multi-source coordination
- [Transaction Boundaries](../design/transaction-boundaries.md) - Transaction semantics
- [ConfigureEpochs API Reference](../api/configure-epochs.md) - Complete API documentation
