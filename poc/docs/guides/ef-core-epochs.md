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
- ✅ No MSDTC escalation — all DB operations within an epoch are serialised through a single queue and a single connection

---

## How It Works

```
Epoch 1  →  [DI scope 1 → DbContext 1 → Transaction 1]  ✓ Committed
Epoch 2  →  [DI scope 2 → DbContext 2 → Transaction 2]  ✓ Committed
Epoch 3  →  [DI scope 3 → DbContext 3 → Transaction 3]  ✗ Rolled back (error)
```

Each epoch has a DI scope. Database writes are **queued** to that scope as serialised operations and executed strictly in order. When the epoch completes successfully, `OnCommitEpoch` calls `SaveChangesAsync` and commits the transaction. If anything throws, `OnEpochError` rolls back.

The queue guarantees that only one operation touches the database connection at a time, which avoids MSDTC escalation and concurrent-access bugs.

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

## Step 2 — Configure Transaction Lifecycle Hooks

`EpochHooks` own the transaction boundary. The begin/commit/rollback calls are themselves queued as serialised operations so they execute in the correct order relative to your data writes.

```csharp
var hooks = new EpochHooks
{
    OnBeginEpoch = async (epoch, ct) =>
    {
        // Queue opening the transaction — runs before any data operations
        await epoch.QueueSerializedOperationAsync<OrderDbContext>(async db =>
        {
            await db.Database.BeginTransactionAsync(ct);
        }, ct);
    },

    OnCommitEpoch = async (epoch, ct) =>
    {
        // Queue SaveChanges + commit — runs after all data operations
        await epoch.QueueSerializedOperationAsync<OrderDbContext>(async db =>
        {
            await db.SaveChangesAsync(ct);
            await db.Database.CommitTransactionAsync(ct);
        }, ct);
    },

    OnEpochError = async (epoch, ex, ct) =>
    {
        // Queue rollback — runs when any queued operation throws
        await epoch.QueueSerializedOperationAsync<OrderDbContext>(async db =>
        {
            if (db.Database.CurrentTransaction != null)
                await db.Database.RollbackTransactionAsync(CancellationToken.None);
        }, ct);
    }
};
```

> **Why `CancellationToken.None` in `RollbackTransactionAsync`?** The outer `QueueSerializedOperationAsync` call still receives the hook's `ct` so the *queuing* can be cancelled. Once the operation starts executing, the rollback itself must succeed even if `ct` has already been cancelled, so `CancellationToken.None` is passed specifically to `RollbackTransactionAsync`.

---

## Step 3 — Create an Epoch Source

Use `EpochCoordinator` to create epochs. Each epoch gets its own DI scope so the `OrderDbContext` resolved inside it is a fresh, isolated instance.

```csharp
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;

// Resolved from DI — IServiceScopeFactory is automatically available
var coordinator = new EpochCoordinator(
    serviceProvider.GetRequiredService<IServiceScopeFactory>());

var epochSourceNode = new EpochSourceNode();
var epochProcessor  = new EpochProcessorNode(epochSourceNode, hooks);
```

---

## Step 4 — Queue Data Operations and Publish Epochs

For each epoch, read the data, queue every write as a serialised operation, and then publish the epoch. **Do not call `SaveChanges` inside the operation** — `OnCommitEpoch` does that after all writes have been queued.

```csharp
long sequence   = 1;
int  lastId     = 0;
const int BatchSize = 500;

while (true)
{
    // Load the next batch (read-only, no tracking needed)
    var batch = await dbContext.Orders
        .Where(o => o.Status == "Pending" && o.Id > lastId)
        .OrderBy(o => o.Id)
        .Take(BatchSize)
        .AsNoTracking()
        .ToListAsync(cancellationToken);

    if (batch.Count == 0)
        break;

    // Create an epoch — this allocates a new DI scope with a fresh OrderDbContext
    var vector = EpochVector.FromSingleSource("orders", sequence);
    var epoch  = await coordinator.GetOrCreateEpochAsync("orders", vector, cancellationToken);

    // Queue one write operation per order
    foreach (var order in batch)
    {
        await epoch.QueueSerializedOperationAsync<OrderDbContext>(async db =>
        {
            order.Status      = "Processed";
            order.ProcessedAt = DateTime.UtcNow;
            db.Orders.Update(order);
            // Do NOT call SaveChanges here — OnCommitEpoch handles it
        }, cancellationToken);
    }

    // Publish the epoch: EpochProcessorNode will drain the queue and run hooks
    await epochSourceNode.PublishEpochAsync(epoch);

    lastId = batch[^1].Id;
    sequence++;
}

// Signal that no more epochs will arrive
epochSourceNode.SignalCompletion();

// Wait for all epoch processing (drain + commit/rollback) to finish
await epochProcessor.CompletionTask;

await epochProcessor.DisposeAsync();
await coordinator.DisposeAsync();
```

This produces:

```
Epoch 1  →  Orders 1–500    → Transaction 1  ✓ Committed
Epoch 2  →  Orders 501–1000 → Transaction 2  ✓ Committed
Epoch 3  →  Orders 1001–1500 → Transaction 3 ✗ Rolled back (error)
```

---

## Step 5 — Wire Up with Dependency Injection

For hosted-service scenarios, inject `IServiceScopeFactory` and build the infrastructure in your service:

```csharp
public class OrderProcessingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderProcessingService> _logger;

    public OrderProcessingService(
        IServiceScopeFactory scopeFactory,
        ILogger<OrderProcessingService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var coordinator = new EpochCoordinator(_scopeFactory);

        var epochSourceNode = new EpochSourceNode();
        var hooks = BuildHooks();
        var epochProcessor = new EpochProcessorNode(epochSourceNode, hooks);

        // Use a short-lived read scope for the source query
        await using var sourceScope = _scopeFactory.CreateAsyncScope();
        var sourceDb = sourceScope.ServiceProvider.GetRequiredService<OrderDbContext>();

        long sequence = 1;
        int  lastId   = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            var batch = await sourceDb.Orders
                .Where(o => o.Status == "Pending" && o.Id > lastId)
                .OrderBy(o => o.Id)
                .Take(500)
                .AsNoTracking()
                .ToListAsync(stoppingToken);

            if (batch.Count == 0)
                break;

            var vector = EpochVector.FromSingleSource("orders", sequence);
            var epoch  = await coordinator.GetOrCreateEpochAsync("orders", vector, stoppingToken);

            foreach (var order in batch)
            {
                await epoch.QueueSerializedOperationAsync<OrderDbContext>(async db =>
                {
                    order.Status      = "Processed";
                    order.ProcessedAt = DateTime.UtcNow;
                    db.Orders.Update(order);
                }, stoppingToken);
            }

            await epochSourceNode.PublishEpochAsync(epoch);

            lastId = batch[^1].Id;
            sequence++;
        }

        epochSourceNode.SignalCompletion();
        await epochProcessor.CompletionTask;
        await epochProcessor.DisposeAsync();
    }

    private static EpochHooks BuildHooks() => new EpochHooks
    {
        OnBeginEpoch = async (epoch, ct) =>
            await epoch.QueueSerializedOperationAsync<OrderDbContext>(
                async db => await db.Database.BeginTransactionAsync(ct), ct),

        OnCommitEpoch = async (epoch, ct) =>
            await epoch.QueueSerializedOperationAsync<OrderDbContext>(async db =>
            {
                await db.SaveChangesAsync(ct);
                await db.Database.CommitTransactionAsync(ct);
            }, ct),

        OnEpochError = async (epoch, ex, ct) =>
            await epoch.QueueSerializedOperationAsync<OrderDbContext>(async db =>
            {
                if (db.Database.CurrentTransaction != null)
                    await db.Database.RollbackTransactionAsync(CancellationToken.None);
            }, ct)
    };
}
```

---

## What Happens Under the Hood

For each epoch:

1. `coordinator.GetOrCreateEpochAsync` allocates a new DI scope — `OrderDbContext` is resolved fresh from it.
2. Each `QueueSerializedOperationAsync` call enqueues a work item onto the epoch's bounded channel and returns immediately.
3. `epochSourceNode.PublishEpochAsync` signals to `EpochProcessorNode` that this epoch is ready.
4. `EpochProcessorNode` picks up the epoch and:
   a. Runs `OnBeginEpoch` (queues `BeginTransactionAsync`)
   b. Drains the user-queued operations in FIFO order (the `db.Orders.Update(...)` calls)
   c. Runs `OnCommitEpoch` (queues `SaveChangesAsync` + `CommitTransactionAsync`)
   d. Drains the commit operation
5. The epoch's DI scope is disposed — `OrderDbContext` is released.

Because the channel is single-reader and FIFO, **only one operation touches the database at a time**, preventing MSDTC escalation.

---

## Best Practices

### 1. Register DbContext as Scoped

```csharp
// ✅ CORRECT — one instance per DI scope (per epoch)
services.AddDbContext<OrderDbContext>(options => ...);

// ❌ WRONG — shared across all epochs, causes concurrency bugs
services.AddSingleton<OrderDbContext>(...);
```

### 2. Never Call SaveChanges Inside a Queued Operation

```csharp
// ✅ CORRECT — SaveChanges is handled by OnCommitEpoch
await epoch.QueueSerializedOperationAsync<OrderDbContext>(async db =>
{
    db.Orders.Update(order);
    // no SaveChangesAsync here
});

// ❌ WRONG — SaveChanges here races with OnCommitEpoch
await epoch.QueueSerializedOperationAsync<OrderDbContext>(async db =>
{
    db.Orders.Update(order);
    await db.SaveChangesAsync(); // do not do this
});
```

### 3. Use AsNoTracking for Read-Only Source Queries

The source query only needs to enumerate data; tracking is unnecessary overhead:

```csharp
var batch = await dbContext.Orders
    .Where(o => o.Status == "Pending")
    .AsNoTracking()
    .ToListAsync(cancellationToken);
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
await epoch.QueueSerializedOperationAsync<OrderDbContext>(async db =>
{
    db.Orders.Update(order);
}, cancellationToken);

// ❌ WRONG — ignores cancellation
await epoch.QueueSerializedOperationAsync<OrderDbContext>(async db =>
{
    db.Orders.Update(order);
}); // missing cancellationToken
```

### 6. Use CancellationToken.None for Rollback

The rollback must complete even when the original cancellation token has fired:

```csharp
OnEpochError = async (epoch, ex, ct) =>
    await epoch.QueueSerializedOperationAsync<OrderDbContext>(async db =>
    {
        if (db.Database.CurrentTransaction != null)
            await db.Database.RollbackTransactionAsync(CancellationToken.None); // ✅
    }, ct)
```

---

## Error Handling

When any queued operation throws, `EpochProcessorNode` calls `OnEpochError` before re-throwing:

```csharp
var hooks = new EpochHooks
{
    OnEpochError = async (epoch, ex, ct) =>
    {
        _logger.LogError(ex, "Epoch {Vector} failed — rolling back", epoch.Vector);

        await epoch.QueueSerializedOperationAsync<OrderDbContext>(async db =>
        {
            if (db.Database.CurrentTransaction != null)
                await db.Database.RollbackTransactionAsync(CancellationToken.None);
        }, ct);
    }
};
```

The exception propagates out of `epochProcessor.CompletionTask`, so wrapping the await in a `try/catch` lets you handle or log the failure at the call site.

---

## Sharing DbContext Across Multiple Queued Operations in the Same Epoch

Because every operation in the same epoch is resolved from the **same DI scope**, multiple calls to `QueueSerializedOperationAsync<OrderDbContext>` within one epoch automatically receive **the same `OrderDbContext` instance**. Changes tracked by one operation are visible to subsequent operations in the same epoch.

```csharp
// Operation 1 — adds a new entity
await epoch.QueueSerializedOperationAsync<OrderDbContext>(async db =>
{
    db.AuditLogs.Add(new AuditLog { Message = $"Processing order {order.Id}" });
});

// Operation 2 — updates the order
// db here is the SAME OrderDbContext instance as in Operation 1
await epoch.QueueSerializedOperationAsync<OrderDbContext>(async db =>
{
    db.Orders.Update(order);
});

// OnCommitEpoch will call db.SaveChangesAsync() — persisting both changes in one transaction
```

---

## See Also

- [Getting Started](./getting-started.md) — First-time setup with `AddDataFlows`
- [Using Epochs](./using-epochs.md) — Epoch system overview, `ConfigureEpochs`, and `EpochHooks`
- [Epoch Actor Block](./epoch-actor-block.md) — DI scope rotation and memory management
- [EpochAnchoringDemo](../../EpochAnchoringDemo/README.md) — Runnable end-to-end EF Core + epoch demo
