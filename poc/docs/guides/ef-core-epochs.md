# Using Entity Framework Core with Epochs

**Audience**: DataFlow users familiar with the [Getting Started guide](./getting-started.md)  
**Prerequisites**: An existing DataFlow pipeline registered with `AddDataFlows`  
**Goal**: Add epoch support so that EF Core `DbContext` is isolated per epoch and each epoch runs inside its own database transaction

---

## Overview

Epochs in DataFlow are logical batch boundaries. Each epoch has its own **DI scope**, which means a `DbContext` registered as `Scoped` is automatically isolated per epoch — different epochs get different `DbContext` instances, and the instance is disposed when the epoch ends.

This gives you:
- ✅ Fresh `DbContext` (no change-tracker bloat) for each epoch
- ✅ Per-epoch database transactions aligned with processing boundaries
- ✅ Automatic rollback when an epoch fails
- ✅ No MSDTC escalation — serial execution within a single epoch uses one connection

---

## How It Works

```
Epoch 1  →  [DI scope 1 → DbContext 1 → Transaction 1]  ✓ Committed
Epoch 2  →  [DI scope 2 → DbContext 2 → Transaction 2]  ✓ Committed
Epoch 3  →  [DI scope 3 → DbContext 3 → Transaction 3]  ✗ Rolled back (error)
```

Each epoch creates a new DI scope. When your actor processes items from that epoch, it gets a fresh `DbContext` instance from that scope. When the actor finishes the epoch, the scope is disposed and the `DbContext` is released.

---

## Step 1 — Register DbContext as Scoped

`DbContext` must be **Scoped**, not Singleton. This is the EF Core default when you call `AddDbContext`.

```csharp
// Program.cs / Startup.cs
services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlServer(connectionString));
// ServiceLifetime.Scoped is the default — one instance per DI scope (= one per epoch)
```

---

## Step 2 — Create a Source Actor

The source actor defines epoch boundaries. Extend `SourceActorBase<T>` and call `CreateEpochStreamAsync` once per epoch. Each call to `CreateEpochStreamAsync` creates a new DI scope through the epoch coordinator.

```csharp
using DataFlow.POC.Core;
using System.Runtime.CompilerServices;

public class PendingOrderSourceActor : SourceActorBase<Order>
{
    private readonly OrderDbContext _dbContext;

    public PendingOrderSourceActor(OrderDbContext dbContext)
        : base("pending-order-source")
    {
        _dbContext = dbContext;
    }

    public override async IAsyncEnumerable<IEpochStream<Order>> ProduceEpochsAsync(
        IActorExecutionContext context)
    {
        // Read-only query — use AsNoTracking for efficiency
        var orders = _dbContext.Orders
            .Where(o => o.Status == "Pending")
            .OrderBy(o => o.Id)
            .AsNoTracking()
            .AsAsyncEnumerable();

        // All pending orders go into a single epoch
        yield return await CreateEpochStreamAsync(
            context,
            sequence: 1,
            items: orders,
            context.CancellationToken);
    }
}
```

> **Tip**: For large datasets, split into multiple smaller epochs — see the [Multiple Epochs (Batching)](#multiple-epochs-batching) section below.

---

## Step 3 — Create a Processor Actor

The processor actor receives items from the epoch and writes to the database. Because `EpochActorBlock` creates a **new DI scope per epoch**, the `DbContext` injected via the constructor is always fresh for each epoch.

Begin the transaction when the actor starts, accumulate changes while processing items, and commit when the actor finishes the epoch.

```csharp
using DataFlow.POC.Core;
using Microsoft.EntityFrameworkCore;

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
        // Begin transaction for this epoch
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(context.CancellationToken);

        var processedCount = 0;

        try
        {
            await foreach (var order in input.WithCancellation(context.CancellationToken))
            {
                // Apply your business logic
                order.Status = "Processed";
                order.ProcessedAt = DateTime.UtcNow;

                _dbContext.Orders.Update(order);
                processedCount++;

                yield return order; // Pass item downstream
            }

            // Commit all changes for this epoch in one transaction
            await _dbContext.SaveChangesAsync(context.CancellationToken);
            await transaction.CommitAsync(context.CancellationToken);

            _logger.LogInformation(
                "Epoch committed {Count} orders", processedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Epoch failed after {Count} orders — rolling back", processedCount);
            await transaction.RollbackAsync(context.CancellationToken);
            throw; // Re-throw so the epoch is marked as failed
        }
    }
}
```

> **Important**: `yield return` must come before `SaveChangesAsync`. The actor streams items downstream while accumulating changes, then commits after the stream is exhausted.

---

## Step 4 — Register Everything with AddDataFlows

```csharp
// Register actors as Scoped — one instance per DI scope (= one per epoch)
services.AddScoped<PendingOrderSourceActor>();
services.AddScoped<OrderProcessorActor>();

services.AddDataFlows("orders", df =>
{
    // Source: produces epoch streams from the database
    df.AddSourceBlock<Order, PendingOrderSourceActor>("order-source");

    // Processor: transforms and persists items within each epoch
    df.AddActorBlock<Order, Order, OrderProcessorActor>("order-processor");

    // Graph: connect source → processor
    df.AddGraph("process-pending", g =>
    {
        g.Connect("order-source", "order-processor");
    });
});
```

`AddDataFlows` automatically registers `IEpochCoordinator` as a scoped service — no manual registration needed.

---

## Step 5 — Execute the Graph

```csharp
var serviceProvider = services.BuildServiceProvider();

// Retrieve the compiled graph
var graph = serviceProvider.GetKeyedService<DataFlowGraph>("orders:process-pending");

// Create an execution context and run
var context = new ExecutionContext(serviceProvider, CancellationToken.None);
await graph!.ExecuteAsync(context);
```

---

## What Happens Under the Hood

When `ExecuteAsync` runs:

1. `EpochSourceBlock` creates a DI scope and resolves `PendingOrderSourceActor` from it.
2. `PendingOrderSourceActor.ProduceEpochsAsync` calls `CreateEpochStreamAsync`, which asks the `EpochCoordinator` to create a new epoch — a new DI scope is allocated for that epoch.
3. The epoch stream flows to `EpochActorBlock`, which creates **its own** DI scope for the actor and resolves `OrderProcessorActor`.
4. `OrderProcessorActor.RunAsync` receives the item stream, opens a transaction, processes items, and commits.
5. When `RunAsync` returns, the actor's DI scope is disposed — the `DbContext` is released.

---

## Multiple Epochs (Batching)

For large datasets, split into multiple epochs so each transaction is bounded in size:

```csharp
public class BatchedOrderSourceActor : SourceActorBase<Order>
{
    private readonly OrderDbContext _dbContext;
    private const int BatchSize = 500;

    public BatchedOrderSourceActor(OrderDbContext dbContext)
        : base("batched-order-source")
    {
        _dbContext = dbContext;
    }

    public override async IAsyncEnumerable<IEpochStream<Order>> ProduceEpochsAsync(
        IActorExecutionContext context)
    {
        long sequence = 1;
        int lastProcessedId = 0;

        while (true)
        {
            var batch = await _dbContext.Orders
                .Where(o => o.Status == "Pending" && o.Id > lastProcessedId)
                .OrderBy(o => o.Id)
                .Take(BatchSize)
                .AsNoTracking()
                .ToListAsync(context.CancellationToken);

            if (batch.Count == 0)
                break;

            // Signal readiness to advance before yielding the next epoch
            if (sequence > 1)
                SignalReadyForNext(context, sequence - 1, sequence);

            lastProcessedId = batch[^1].Id;

            yield return await CreateEpochStreamAsync(
                context,
                sequence,
                batch.ToAsyncEnumerable(),
                context.CancellationToken);

            sequence++;
        }
    }
}
```

This results in:
```
Epoch 1  →  Orders 1–500    → Transaction 1  ✓ Committed
Epoch 2  →  Orders 501–1000 → Transaction 2  ✓ Committed
Epoch 3  →  Orders 1001–1500 → Transaction 3 ✓ Committed
```

---

## Lifecycle Hooks (Optional)

If you need to begin a transaction before any items are processed (e.g., to wrap the entire epoch in one `BEGIN TRANSACTION` and let the actor omit the explicit begin/commit), configure `EpochHooks` via `ConfigureEpochs` on a `DataFlowGraphBuilder`.

> **Note**: `ConfigureEpochs` applies to the low-level `EpochSourceNode`/`EpochProcessorNode` pipeline and is most useful when multiple blocks share the same epoch scope via `epochStream.EpochScope`. For the typical actor block pattern described in Steps 1–5, managing transactions inside the actor is simpler and recommended.

```csharp
var builder = new DataFlowGraphBuilder("orders");

builder.ConfigureEpochs(config =>
{
    config.SetPolicy(EpochPolicy.ByCount(500));
    config.AddProcessor("processor1");

    config.OnBeginEpoch(async (epoch, ct) =>
    {
        var db = epoch.GetService<OrderDbContext>();
        await db.Database.BeginTransactionAsync(ct);
    });

    config.OnCommitEpoch(async (epoch, ct) =>
    {
        var db = epoch.GetService<OrderDbContext>();
        await db.SaveChangesAsync(ct);
        await db.Database.CommitTransactionAsync(ct);
    });

    config.OnEpochError(async (epoch, ex, ct) =>
    {
        var db = epoch.GetService<OrderDbContext>();
        if (db.Database.CurrentTransaction != null)
            await db.Database.RollbackTransactionAsync(ct);
    });
});
```

---

## Best Practices

### 1. Register DbContext as Scoped

```csharp
// ✅ CORRECT — one instance per DI scope (per epoch)
services.AddDbContext<OrderDbContext>(options => ...);

// ❌ WRONG — shared across all epochs, causes concurrency bugs
services.AddSingleton<OrderDbContext>(...);
```

### 2. Manage Transactions Inside the Actor

Open the transaction at the start of `RunAsync`, commit after the stream is exhausted, and roll back in the `catch`. This co-locates the transaction lifecycle with the code that uses it.

### 3. Use AsNoTracking for Read-Only Queries

When the source actor only reads data to produce the stream, disable change tracking:

```csharp
_dbContext.Orders
    .Where(o => o.Status == "Pending")
    .AsNoTracking() // No change tracking for read-only queries
    .AsAsyncEnumerable();
```

### 4. Keep Epoch Size Reasonable

Large epochs hold transactions open longer, increasing lock contention and memory usage.

| Epoch Size | Tradeoff |
|------------|----------|
| 50–200 | Low lock contention, frequent commits |
| 500–1000 | Good throughput, moderate transaction size |
| >5000 | High throughput but risk of timeout or memory pressure |

### 5. Always Propagate CancellationToken

```csharp
// ✅ CORRECT
await _dbContext.SaveChangesAsync(context.CancellationToken);

// ❌ WRONG — ignores cancellation
await _dbContext.SaveChangesAsync();
```

### 6. Never Call SaveChanges Synchronously

```csharp
// ✅ CORRECT
await _dbContext.SaveChangesAsync(context.CancellationToken);

// ❌ WRONG — blocks the thread pool
_dbContext.SaveChanges();
```

---

## Error Handling

The actor is responsible for its own error handling. Throwing an exception from `RunAsync` propagates up through the `EpochActorBlock` and the graph, which cancels the pipeline.

```csharp
public async IAsyncEnumerable<Order> RunAsync(
    IAsyncEnumerable<Order> input,
    IActorExecutionContext context)
{
    await using var transaction = await _dbContext.Database
        .BeginTransactionAsync(context.CancellationToken);

    try
    {
        await foreach (var order in input.WithCancellation(context.CancellationToken))
        {
            // ... process ...
            yield return order;
        }

        await _dbContext.SaveChangesAsync(context.CancellationToken);
        await transaction.CommitAsync(context.CancellationToken);
    }
    catch (OperationCanceledException)
    {
        // Cancellation — rollback and re-throw
        await transaction.RollbackAsync(CancellationToken.None);
        throw;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Epoch processing failed — rolling back");
        await transaction.RollbackAsync(CancellationToken.None);
        throw;
    }
}
```

> **Note**: Use `CancellationToken.None` for the rollback call so it is not itself cancelled before it completes.

---

## Shared DbContext Across Multiple Blocks in the Same Epoch

In some advanced scenarios you may want multiple blocks to share the same `DbContext` instance within an epoch (e.g., one block reads and another writes, and you need them to see each other's un-committed changes).

This is possible by accessing the epoch scope directly via `epochStream.EpochScope`:

```csharp
// In a custom block (not using EpochActorBlock):
await foreach (var epochStream in input.WithCancellation(cancellationToken))
{
    var db = epochStream.EpochScope?.GetService<OrderDbContext>()
        ?? throw new InvalidOperationException("EpochScope required");

    // db is the same instance shared across all blocks in this epoch
    await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

    await foreach (var item in epochStream.Items.WithCancellation(cancellationToken))
    {
        // process item using db ...
    }

    await db.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
}
```

> This pattern requires writing a custom block class rather than using the `IStreamActor<TIn, TOut>` interface with `df.AddActorBlock`. For most pipelines the actor-level transaction approach (Steps 1–5) is simpler and sufficient.

---

## See Also

- [Getting Started](./getting-started.md) — First-time setup with `AddDataFlows`
- [Using Epochs](./using-epochs.md) — Epoch system overview and `ConfigureEpochs`
- [Epoch Actor Block](./epoch-actor-block.md) — DI scope rotation and memory management
- [EpochAnchoringDemo](../../EpochAnchoringDemo/README.md) — Runnable end-to-end EF Core + epoch demo
