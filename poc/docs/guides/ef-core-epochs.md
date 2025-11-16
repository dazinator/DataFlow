# Using Entity Framework Core with Epochs

## Overview

This guide demonstrates how to use **Entity Framework Core DbContext** with the formalized epoch system for transactional data processing. The epoch system provides automatic transaction management and DbContext scoping for safe, concurrent database operations.

## Key Concepts

### Epoch-Scoped DbContext

Each epoch has its own DI scope, which means:
- ✅ All operations in an epoch share the same `DbContext` instance
- ✅ The `DbContext` is automatically disposed when the epoch completes
- ✅ Multiple blocks can access the same `DbContext` within an epoch
- ✅ No MSDTC escalation (operations execute serially)

### Transaction Lifecycle

The epoch system manages the complete transaction lifecycle:

1. **OnBeginEpoch**: Begin transaction before any operations
2. **Operation Queue**: All DB operations execute serially
3. **OnCommitEpoch**: SaveChanges and commit transaction
4. **OnEpochError**: Rollback on errors

## Basic Setup

### 1. Register DbContext as Scoped Service

```csharp
var services = new ServiceCollection();

// Register DbContext as scoped (one per epoch)
services.AddDbContext<MyDbContext>(options =>
    options.UseSqlServer(connectionString),
    ServiceLifetime.Scoped);

var serviceProvider = services.BuildServiceProvider();
```

### 2. Configure Epoch System with Transaction Hooks

```csharp
var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
var coordinator = new EpochCoordinator(scopeFactory);
var source = new EpochSourceNode(coordinator);

var hooks = new EpochHooks
{
    OnBeginEpoch = async (epoch, ct) =>
    {
        // Begin transaction at epoch start
        await epoch.QueueSerializedOperationAsync<MyDbContext>(async db =>
        {
            await db.Database.BeginTransactionAsync(ct);
        }, ct);
    },
    
    OnCommitEpoch = async (epoch, ct) =>
    {
        // Save changes and commit at epoch end
        await epoch.QueueSerializedOperationAsync<MyDbContext>(async db =>
        {
            await db.SaveChangesAsync(ct);
            await db.Database.CommitTransactionAsync(ct);
        }, ct);
    },
    
    OnEpochError = async (epoch, error, ct) =>
    {
        // Rollback on error
        await epoch.QueueSerializedOperationAsync<MyDbContext>(async db =>
        {
            if (db.Database.CurrentTransaction != null)
            {
                await db.Database.RollbackTransactionAsync(ct);
            }
        }, ct);
    }
};

var processor = new EpochProcessorNode(source, hooks);
```

## Usage Patterns

### Pattern 1: Simple Entity Processing

Queue operations to add/update entities:

```csharp
public class OrderProcessor
{
    public async Task ProcessOrderAsync(Order order, IEpoch epoch, CancellationToken ct)
    {
        // Queue the operation - it executes later in serial order
        await epoch.QueueSerializedOperationAsync<MyDbContext>(async db =>
        {
            db.Orders.Add(order);
            // Don't call SaveChanges - OnCommitEpoch handles it
        }, ct);
    }
}
```

### Pattern 2: Related Entity Operations

All operations within an epoch see the same DbContext:

```csharp
public async Task ProcessOrderWithItemsAsync(
    Order order, 
    IEpoch epoch, 
    CancellationToken ct)
{
    // Single operation: Add order and all related items
    await epoch.QueueSerializedOperationAsync<MyDbContext>(async db =>
    {
        // Add the order
        db.Orders.Add(order);
        
        // Add all order items in the same operation
        foreach (var item in order.Items)
        {
            // Same DbContext sees the order added above
            db.OrderItems.Add(item);
        }
        // SaveChanges happens in OnCommitEpoch
    }, ct);
}
```

### Pattern 3: Query and Update

Query data within the epoch scope:

```csharp
public async Task UpdateInventoryAsync(
    string productId, 
    int quantity,
    IEpoch epoch, 
    CancellationToken ct)
{
    await epoch.QueueSerializedOperationAsync<MyDbContext>(async db =>
    {
        var product = await db.Products
            .FirstOrDefaultAsync(p => p.Id == productId, ct);
        
        if (product != null)
        {
            product.Quantity -= quantity;
            product.LastModified = DateTime.UtcNow;
        }
        // SaveChanges happens in OnCommitEpoch
    }, ct);
}
```

### Pattern 4: Bulk Operations

Process multiple items in the same epoch:

```csharp
public async Task ProcessBatchAsync(
    IEnumerable<Order> orders,
    IEpoch epoch,
    CancellationToken ct)
{
    // Single operation: Add all orders at once
    await epoch.QueueSerializedOperationAsync<MyDbContext>(async db =>
    {
        foreach (var order in orders)
        {
            db.Orders.Add(order);
        }
        // All orders committed together in OnCommitEpoch
    }, ct);
}
```

## Complete Example

### Full DataFlow Pipeline with EF Core

This example shows typical usage with the DataFlow builder:

```csharp
public class OrderProcessingPipeline
{
    public async Task RunAsync(CancellationToken ct)
    {
        // 1. Setup DI with scoped DbContext
        var services = new ServiceCollection();
        services.AddDbContext<MyDbContext>(options =>
            options.UseSqlServer(connectionString),
            ServiceLifetime.Scoped);
        
        // Register your blocks and actors
        services.AddScoped<OrderValidationActor>();
        services.AddScoped<OrderPersistenceActor>();
        
        var serviceProvider = services.BuildServiceProvider();
        
        // 2. Build the DataFlow pipeline
        var dataFlow = new DataFlowBuilder()
            .UseServiceProvider(serviceProvider)
            
            // Configure epoch system with transaction hooks
            .ConfigureEpochs(config =>
            {
                config.SetPolicy(EpochPolicy.ByCount(100)); // Batch every 100 items
                config.AddProcessor("processor1"); // Serial processing
                
                config.OnBeginEpoch(async (epoch, ct) =>
                {
                    await epoch.QueueSerializedOperationAsync<MyDbContext>(
                        async db => await db.Database.BeginTransactionAsync(ct), ct);
                });
                
                config.OnCommitEpoch(async (epoch, ct) =>
                {
                    await epoch.QueueSerializedOperationAsync<MyDbContext>(async db =>
                    {
                        await db.SaveChangesAsync(ct);
                        await db.Database.CommitTransactionAsync(ct);
                    }, ct);
                });
                
                config.OnEpochError(async (epoch, error, ct) =>
                {
                    await epoch.QueueSerializedOperationAsync<MyDbContext>(async db =>
                    {
                        if (db.Database.CurrentTransaction != null)
                            await db.Database.RollbackTransactionAsync(ct);
                    }, ct);
                });
            })
            
            // Define the pipeline
            .AddProducer<Order>("order-source", sp => sp.GetRequiredService<OrderSourceBlock>())
            .AddActor<Order, Order>("validator", sp => sp.GetRequiredService<OrderValidationActor>())
            .ReceiveFrom("order-source")
            .AddActor<Order, Order>("persister", sp => sp.GetRequiredService<OrderPersistenceActor>())
            .ReceiveFrom("validator")
            
            .Build();
        
        // 3. Execute the pipeline
        await dataFlow.ExecuteAsync(ct);
        
        // 4. Cleanup
        await serviceProvider.DisposeAsync();
    }
}

// Example actor that persists orders
public class OrderPersistenceActor : IStreamActor<Order, Order>
{
    private readonly IEpochCoordinator _coordinator;
    
    public OrderPersistenceActor(IEpochCoordinator coordinator)
    {
        _coordinator = coordinator;
    }
    
    public async IAsyncEnumerable<Order> RunAsync(
        IAsyncEnumerable<Order> input,
        IActorExecutionContext context)
    {
        await foreach (var order in input.WithCancellation(context.CancellationToken))
        {
            // Get or create epoch for this item
            var epoch = await _coordinator.GetOrCreateEpochAsync(
                context.BlockName, 
                order.EpochVector);
            
            // Queue database operation
            await epoch.QueueSerializedOperationAsync<MyDbContext>(async db =>
            {
                db.Orders.Add(order);
                // SaveChanges happens in OnCommitEpoch
            }, context.CancellationToken);
            
            yield return order;
        }
    }
}
```

## Best Practices

### 1. Always Use Scoped Lifetime

```csharp
// ✅ CORRECT
services.AddDbContext<MyDbContext>(options => ..., ServiceLifetime.Scoped);

// ❌ WRONG - Singleton causes concurrency issues
services.AddDbContext<MyDbContext>(options => ..., ServiceLifetime.Singleton);
```

### 2. Don't Call SaveChanges in Operations (Usually)

Let the `OnCommitEpoch` hook handle SaveChanges:

```csharp
// ✅ CORRECT - Let OnCommitEpoch handle it
await epoch.QueueSerializedOperationAsync<MyDbContext>(async db =>
{
    db.Orders.Add(order);
});

// ⚠️ EXCEPTION - If you need database-generated IDs for subsequent operations
await epoch.QueueSerializedOperationAsync<MyDbContext>(async db =>
{
    db.Orders.Add(order);
    await db.SaveChangesAsync(); // Get the ID
    
    // Now use the ID for related records
    var detail = new OrderDetail { OrderId = order.Id };
    db.OrderDetails.Add(detail);
});
```

**Important**: If you call `SaveChanges` within operations, ensure you have transaction management:
- Call `BeginTransaction` in `OnBeginEpoch`
- Call `CommitTransaction` in `OnCommitEpoch`
- This ensures multiple `SaveChanges` calls are rolled into a single transaction

### 3. Handle Connection Pooling

EF Core handles connection pooling automatically. Each epoch:
- Gets a scoped `DbContext` from the service provider
- Opens a connection from the pool when needed
- Returns the connection to the pool when disposed
- No MSDTC escalation (serial execution ensures one connection at a time)

### 4. Monitor Transaction Size

Large epochs with many operations can:
- Hold transactions open longer
- Increase memory usage
- Risk transaction timeout

**Recommendation**: Keep epochs reasonably sized (100-1000 operations)

```csharp
// Good: Batch size of 100
var policy = EpochPolicy.ByCount(100);

// Risk: Very large batches
var policy = EpochPolicy.ByCount(100000); // May timeout!
```

### 5. Use Async All the Way

```csharp
// ✅ CORRECT - Async operations
await epoch.QueueSerializedOperationAsync<MyDbContext>(async db =>
{
    await db.SaveChangesAsync(ct);
});

// ❌ WRONG - Blocking sync calls
await epoch.QueueSerializedOperationAsync<MyDbContext>(async db =>
{
    db.SaveChanges(); // Blocks thread pool!
});
```

## Error Handling

### Automatic Rollback

The `OnEpochError` hook automatically rolls back on errors:

```csharp
OnEpochError = async (epoch, error, ct) =>
{
    await epoch.QueueSerializedOperationAsync<MyDbContext>(async db =>
    {
        if (db.Database.CurrentTransaction != null)
        {
            await db.Database.RollbackTransactionAsync(ct);
        }
    }, ct);
    
    // Log the error
    _logger.LogError(error, "Epoch {Epoch} failed", epoch.Vector);
}
```

### Retry Logic

Implement retry at the epoch level:

```csharp
public async Task ProcessWithRetryAsync(IEpoch epoch, int maxRetries = 3)
{
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            await source.PublishEpochAsync(epoch);
            await processor.CompletionTask;
            return; // Success
        }
        catch (Exception ex) when (attempt < maxRetries)
        {
            _logger.LogWarning(ex, "Epoch failed, retry {Attempt}/{Max}", 
                attempt, maxRetries);
            await Task.Delay(TimeSpan.FromSeconds(attempt * 2)); // Exponential backoff
        }
    }
}
```

## Performance Considerations

### 1. Processor Count

Configure based on your workload:

```csharp
// Serial: Deterministic order, lower throughput
var processor = new EpochProcessorNode(source, hooks);

// Parallel: Higher throughput, order non-deterministic
var processors = Enumerable.Range(0, 4)
    .Select(_ => new EpochProcessorNode(source, hooks))
    .ToList();

await Task.WhenAll(processors.Select(p => p.CompletionTask));
```

### 2. Batch Size

Balance transaction size vs throughput:

```csharp
// Small batches: Frequent commits, lower transaction risk
var policy = EpochPolicy.ByCount(50);

// Large batches: Higher throughput, longer transactions
var policy = EpochPolicy.ByCount(500);
```

### 3. Change Tracking

Disable change tracking for read-only queries:

```csharp
await epoch.QueueSerializedOperationAsync<MyDbContext>(async db =>
{
    var products = await db.Products
        .AsNoTracking() // Better performance for read-only
        .ToListAsync(ct);
});
```

## See Also

- [Using Epochs Guide](./using-epochs.md) - General epoch system overview
- [Epoch Vectors](../design/epoch-vectors.md) - Multi-source coordination
- [Transaction Boundaries](../design/transaction-boundaries.md) - Transaction semantics
