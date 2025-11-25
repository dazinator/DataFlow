# Epoch Actor Block Guide

**Audience**: Advanced DataFlow users  
**Prerequisites**: [Using Epochs](./using-epochs.md), [Working with Blocks](./working-with-blocks.md)  
**Time**: 20 minutes

---

## Table of Contents

1. [What is an Epoch Actor Block?](#what-is-an-epoch-actor-block)
2. [The Scope Rotation Pattern](#the-scope-rotation-pattern)
3. [When to Use Epoch Actor Blocks](#when-to-use-epoch-actor-blocks)
4. [Basic Example](#basic-example)
5. [With Entity Framework Core](#with-entity-framework-core)
6. [Single Epoch vs Multi-Epoch](#single-epoch-vs-multi-epoch)
7. [Performance and Memory Management](#performance-and-memory-management)
8. [Best Practices](#best-practices)

---

## What is an Epoch Actor Block?

An **Epoch Actor Block** is a special type of actor that provides **automatic DI scope rotation** based on epoch boundaries. This solves critical problems with long-running streams and scoped services like `DbContext`.

```
Without Epochs (Problem):
┌─────────────────────────────────────────┐
│ Single DI Scope (entire stream)         │
│ DbContext grows indefinitely            │
│ Memory leak!                            │
└─────────────────────────────────────────┘

With Epoch Actor Block (Solution):
┌─────────┐ ┌─────────┐ ┌─────────┐
│ Scope 1 │ │ Scope 2 │ │ Scope 3 │
│ Epoch 1 │ │ Epoch 2 │ │ Epoch 3 │
│ Disposed│ │ Disposed│ │ Disposed│
└─────────┘ └─────────┘ └─────────┘
```

**Key Benefits**:
- ✅ Automatic scope disposal after each epoch
- ✅ Prevents memory leaks with EF Core change tracking
- ✅ Transaction boundaries align with scopes
- ✅ Works on any stream (single epoch when not configured)

---

## The Scope Rotation Pattern

### The Problem

Without epoch actor blocks, a long-running stream uses a single DI scope:

```csharp
// Problem: Single scope for entire stream
public class OrderProcessorActor : IStreamActor<Order, Order>
{
    private readonly OrderDbContext _dbContext; // Grows indefinitely!
    
    public OrderProcessorActor(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async IAsyncEnumerable<Order> RunAsync(
        IAsyncEnumerable<Order> input,
        IActorExecutionContext context)
    {
        // Process 100,000 orders...
        // DbContext tracks ALL entities - memory leak!
        await foreach (var order in input.WithCancellation(context.CancellationToken))
        {
            _dbContext.Orders.Update(order);
            await _dbContext.SaveChangesAsync(context.CancellationToken);
            yield return order;
        }
    }
}
```

**Result**: After processing 100,000 orders, `DbContext` tracks 100,000 entities. Memory usage explodes! 💥

### The Solution: Scope Rotation

Epoch Actor Block rotates the DI scope after each epoch:

```csharp
// Solution: New scope for each epoch
services.AddDataFlows("global", df =>
{
    df.AddGraph("process-orders", g =>
    {
        var source = BlockHelpers.CreateProducer<Order>("orders", GetOrders);
        
        g.AddBlock(source)
         .ConfigureEpochs(config =>
         {
             // Create epochs every 1000 items
             config.SetPolicy(EpochPolicy.ByCount(1000));
             
             // Processor gets new scope for each epoch
             config.AddProcessor("order-processor");
         },
         sp => new EpochCoordinator(sp.GetRequiredService<IServiceScopeFactory>()));
    });
});
```

**Result**: After each 1000 orders, the scope is disposed, DbContext is released, memory is reclaimed. ✅

---

## When to Use Epoch Actor Blocks

Use Epoch Actor Blocks when:

| Scenario | Why |
|----------|-----|
| **Long-running streams with EF Core** | Prevent change tracker bloat |
| **Transaction boundaries needed** | Align transactions with epochs |
| **Memory management critical** | Dispose scoped resources periodically |
| **Batch processing** | Process in logical batches with isolated scopes |

Don't use when:

| Scenario | Alternative |
|----------|-------------|
| **Short streams (<1000 items)** | Regular actors are fine |
| **No scoped dependencies** | No benefit from scope rotation |
| **Stateless transformations** | Regular transform blocks sufficient |

---

## Basic Example

Let's build a simple example that processes orders with scope rotation.

### Step 1: Define Your Processor Actor

```csharp
using DataFlow.POC.Core;

public class OrderProcessorActor : IStreamActor<Order, Order>
{
    private readonly OrderDbContext _dbContext;
    private readonly ILogger<OrderProcessorActor> _logger;
    
    public OrderProcessorActor(
        OrderDbContext dbContext,
        ILogger<OrderProcessorActor> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }
    
    public async IAsyncEnumerable<Order> RunAsync(
        IAsyncEnumerable<Order> input,
        IActorExecutionContext context)
    {
        var count = 0;
        
        await foreach (var order in input.WithCancellation(context.CancellationToken))
        {
            // Process order
            order.Status = "Processed";
            order.ProcessedDate = DateTime.UtcNow;
            
            _dbContext.Orders.Update(order);
            await _dbContext.SaveChangesAsync(context.CancellationToken);
            
            count++;
            _logger.LogInformation("Processed order {OrderId} (#{Count} in this epoch)", 
                order.Id, count);
            
            yield return order;
        }
        
        _logger.LogInformation("Epoch completed. Processed {Count} orders", count);
    }
}
```

### Step 2: Configure with Epochs

```csharp
using Microsoft.Extensions.DependencyInjection;
using DataFlow.POC.DependencyInjection;

var services = new ServiceCollection();

// Register DbContext
services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlServer(connectionString));

// Register actor
services.AddScoped<OrderProcessorActor>();

// Configure DataFlow with epochs
services.AddDataFlows("orders", df =>
{
    df.AddBlock("order-source", sp =>
        BlockHelpers.CreateProducer<Order>("order-source", GetOrdersFromDatabase));
    
    df.AddGraph("process-orders", g =>
    {
        g.UseBlock("order-source")
         .ConfigureEpochs(config =>
         {
             // Rotate scope every 1000 orders
             config.SetPolicy(EpochPolicy.ByCount(1000));
             
             // Add the processor - gets new scope each epoch
             config.AddProcessor("order-processor");
         },
         sp => new EpochCoordinator(sp.GetRequiredService<IServiceScopeFactory>()));
    });
});
```

### Step 3: Execute

```csharp
var serviceProvider = services.BuildServiceProvider();
var graph = serviceProvider.GetKeyedService<DataFlowGraph>("orders:process-orders");
var context = new ExecutionContext(serviceProvider, CancellationToken.None);

await graph!.ExecuteAsync(context);
```

**What happens**:
1. First 1000 orders processed in Scope 1
2. Scope 1 disposed → DbContext released
3. Next 1000 orders processed in Scope 2  
4. Scope 2 disposed → DbContext released
5. And so on...

---

## With Entity Framework Core

This is the most common use case for epoch actor blocks.

### Complete Example

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DataFlow.POC.Core;

// Domain model
public class Order
{
    public int Id { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime? ProcessedDate { get; set; }
}

// DbContext
public class OrderDbContext : DbContext
{
    public DbSet<Order> Orders { get; set; }
    
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }
}

// Processor with lifecycle hooks
public class OrderBatchProcessorActor : IStreamActor<Order, Order>
{
    private readonly OrderDbContext _dbContext;
    private readonly ILogger<OrderBatchProcessorActor> _logger;
    
    public OrderBatchProcessorActor(
        OrderDbContext dbContext,
        ILogger<OrderBatchProcessorActor> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }
    
    public async IAsyncEnumerable<Order> RunAsync(
        IAsyncEnumerable<Order> input,
        IActorExecutionContext context)
    {
        var batch = new List<Order>();
        
        await foreach (var order in input.WithCancellation(context.CancellationToken))
        {
            // Enrich order
            var customer = await _dbContext.Customers
                .FirstOrDefaultAsync(c => c.Id == order.CustomerId, context.CancellationToken);
            
            order.CustomerName = customer?.Name;
            order.Status = "Processed";
            
            batch.Add(order);
            yield return order;
        }
        
        // Save all at end of epoch
        _dbContext.Orders.UpdateRange(batch);
        await _dbContext.SaveChangesAsync(context.CancellationToken);
        
        _logger.LogInformation("Saved batch of {Count} orders", batch.Count);
        
        // Scope will be disposed after this, releasing DbContext
    }
}

// Setup
var services = new ServiceCollection();

services.AddLogging(builder => builder.AddConsole());
services.AddDbContext<OrderDbContext>(options =>
    options.UseInMemoryDatabase("OrdersDb"));

services.AddScoped<OrderBatchProcessorActor>();

services.AddDataFlows("orders", df =>
{
    df.AddBlock("order-source", sp =>
    {
        return BlockHelpers.CreateProducer<Order>("orders", async ctx =>
        {
            var dbContext = ctx.ServiceProvider.GetRequiredService<OrderDbContext>();
            var orders = await dbContext.Orders
                .Where(o => o.Status == "Pending")
                .AsNoTracking()
                .ToListAsync(ctx.CancellationToken);
            
            foreach (var order in orders)
                yield return order;
        });
    });
    
    df.AddGraph("process", g =>
    {
        g.UseBlock("order-source")
         .ConfigureEpochs(config =>
         {
             config.SetPolicy(EpochPolicy.ByCount(500)); // 500 orders per epoch
             config.AddProcessor("batch-processor");
             
             // Optional: Add lifecycle hooks
             config.SetHooks(new EpochHooks
             {
                 OnBeginEpoch = async (epoch, ct) =>
                 {
                     var db = epoch.ServiceProvider.GetRequiredService<OrderDbContext>();
                     await db.Database.BeginTransactionAsync(ct);
                 },
                 OnCommitEpoch = async (epoch, ct) =>
                 {
                     var db = epoch.ServiceProvider.GetRequiredService<OrderDbContext>();
                     await db.Database.CommitTransactionAsync(ct);
                 },
                 OnEpochError = async (epoch, ex, ct) =>
                 {
                     var db = epoch.ServiceProvider.GetRequiredService<OrderDbContext>();
                     await db.Database.RollbackTransactionAsync(ct);
                 }
             });
         },
         sp => new EpochCoordinator(sp.GetRequiredService<IServiceScopeFactory>()));
    });
});
```

---

## Single Epoch vs Multi-Epoch

### Single Epoch Stream (Default)

When you don't configure epochs, it's treated as a single-epoch stream:

```csharp
// No epoch configuration = single epoch
var graph = GraphHelpers.CreateGraphBuilder("simple-flow")
    .AddBlock(producer)
    .AddBlock(transformer)
    .AddBlock(processor)
    .Connect(producer, transformer)
    .Connect(transformer, processor)
    .Build();

// Entire stream runs in one scope
```

**Use when**: Stream is short, no scope rotation needed.

### Multi-Epoch Stream

Explicitly configure epochs for scope rotation:

```csharp
df.AddGraph("multi-epoch", g =>
{
    g.AddBlock(producer)
     .ConfigureEpochs(config =>
     {
         config.SetPolicy(EpochPolicy.ByCount(1000)); // New epoch every 1000 items
         config.AddProcessor("processor");
     },
     sp => coordinator);
});

// Stream divided into multiple epochs, each with its own scope
```

**Use when**: Long stream, scoped services need periodic disposal.

---

## Performance and Memory Management

### Memory Usage Comparison

#### Without Scope Rotation (Problem)

```
Processing 100,000 orders without scope rotation:

Memory usage over time:
   ^
   │                                    ╱
GB │                            ╱╱╱╱╱╱╱
 3 │                    ╱╱╱╱╱╱╱
 2 │            ╱╱╱╱╱╱╱
 1 │    ╱╱╱╱╱╱╱
   └─────────────────────────────────────▶
     0    20k   40k   60k   80k  100k  Orders

DbContext tracks all 100k entities!
```

#### With Scope Rotation (Solution)

```
Processing 100,000 orders with epoch rotation (1000/epoch):

Memory usage over time:
   ^
   │ ╱╲    ╱╲    ╱╲    ╱╲    ╱╲
GB │╱  ╲  ╱  ╲  ╱  ╲  ╱  ╲  ╱  ╲
0.1│    ╲╱    ╲╱    ╲╱    ╲╱    ╲╱
   └─────────────────────────────────────▶
     0    20k   40k   60k   80k  100k  Orders

Periodic scope disposal keeps memory constant!
```

### Performance Tips

1. **Epoch Size**: Balance between overhead and memory
   ```csharp
   // Too small: overhead from scope creation
   config.SetPolicy(EpochPolicy.ByCount(10)); // ❌
   
   // Too large: memory growth
   config.SetPolicy(EpochPolicy.ByCount(100000)); // ❌
   
   // Just right: sweet spot
   config.SetPolicy(EpochPolicy.ByCount(1000)); // ✅
   ```

2. **Use Transactions Wisely**:
   ```csharp
   // ✅ Good: One transaction per epoch
   OnBeginEpoch: begin transaction
   OnCommitEpoch: commit transaction
   
   // ❌ Bad: Transaction per item (slow!)
   ```

3. **AsNoTracking for Read-Only**:
   ```csharp
   // ✅ Good: No tracking for source queries
   var orders = await dbContext.Orders
       .AsNoTracking()
       .ToListAsync();
   
   // ❌ Bad: Tracking not needed
   var orders = await dbContext.Orders.ToListAsync();
   ```

---

## Best Practices

### 1. Choose Appropriate Epoch Size

```csharp
// ✅ Good: Balance memory and performance
config.SetPolicy(EpochPolicy.ByCount(1000));

// Consider your data:
// - Small items (ints, strings): 1000-5000 per epoch
// - Medium items (orders, customers): 500-1000 per epoch  
// - Large items (with navigations): 100-500 per epoch
```

### 2. Use Lifecycle Hooks for Transactions

```csharp
config.SetHooks(new EpochHooks
{
    OnBeginEpoch = async (epoch, ct) =>
    {
        var db = epoch.ServiceProvider.GetRequiredService<MyDbContext>();
        await db.Database.BeginTransactionAsync(ct);
    },
    OnCommitEpoch = async (epoch, ct) =>
    {
        var db = epoch.ServiceProvider.GetRequiredService<MyDbContext>();
        await db.SaveChangesAsync(ct);
        await db.Database.CommitTransactionAsync(ct);
    },
    OnEpochError = async (epoch, ex, ct) =>
    {
        var db = epoch.ServiceProvider.GetRequiredService<MyDbContext>();
        await db.Database.RollbackTransactionAsync(ct);
    }
});
```

### 3. Don't Mix Epoch Policies

```csharp
// ✅ Good: One policy per graph
config.SetPolicy(EpochPolicy.ByCount(1000));

// ❌ Bad: Don't change policies mid-stream
```

### 4. Register Actors as Scoped

```csharp
// ✅ Good: Scoped registration
services.AddScoped<MyProcessorActor>();

// ❌ Bad: Singleton defeats scope rotation!
services.AddSingleton<MyProcessorActor>();
```

---

## Next Steps

- **[Using Epochs](./using-epochs.md)** - Deep dive into epoch system
- **[Checkpointing](./checkpointing.md)** - Resume processing with checkpoints
- **[EF Core with Epochs](./ef-core-epochs.md)** - Advanced EF Core patterns

---

## Summary

You've learned:

- ✅ What epoch actor blocks are and why they matter
- ✅ The scope rotation pattern for memory management
- ✅ How to use them with Entity Framework Core
- ✅ Single epoch vs multi-epoch streams
- ✅ Performance and memory considerations

**Key Takeaway**: Use epoch actor blocks for long-running streams with scoped services to prevent memory leaks and align transaction boundaries.

---

**Related Guides**:
- [Using Epochs](./using-epochs.md)
- [Working with Blocks](./working-with-blocks.md)
- [EF Core with Epochs](./ef-core-epochs.md)
