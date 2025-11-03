# Creating Tracking Blocks

## Overview

An **Entity Tracking Block** is a composable pattern for managing per-epoch database transactions in epoch-based pipelines. It separates data production (sources) from transaction management (downstream blocks), enabling flexible pipeline topologies and multi-sink scenarios.

## Why Downstream Transaction Blocks?

### The Separation Principle

**Sources should be stateless** - they emit data without managing transactions:
```csharp
✅ Source: Pure data production (read-only, stateless)
✅ Transform: Business logic (stateless)
✅ Tracking Block: Transaction management (per-epoch state)
✅ Sink: Final output (stateless)
```

**Benefits:**
- Flexible routing: Transform, filter, branch before persisting
- Multi-sink support: Different blocks manage different databases
- Composable: Works with any pipeline topology
- Testable: Mock transaction blocks independently

### What This Enables

```
                    ┌→ Transform → TrackingBlock A → DB1
Source (stateless) ─┤
                    └→ Filter → TrackingBlock B → DB2
```

Both tracking blocks coordinate via global alignment, committing at the same safe boundary.

## Basic Pattern

### Step 1: Define the Tracking Block

```csharp
public class EntityTrackingBlock<T, TContext> : IEpochLifecycleParticipant
    where T : class
    where TContext : DbContext
{
    private readonly IDbContextFactory<TContext> _contextFactory;
    private readonly ConcurrentDictionary<EpochVector, TContext> _epochContexts = new();
    private readonly ILogger<EntityTrackingBlock<T, TContext>> _logger;

    public EntityTrackingBlock(
        IDbContextFactory<TContext> contextFactory,
        ILogger<EntityTrackingBlock<T, TContext>> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    // Process items and track in per-epoch DbContext
    public async IAsyncEnumerable<T> ProcessAsync(
        IAsyncEnumerable<IEpochStream<T>> input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var epochStream in input.WithCancellation(cancellationToken))
        {
            // Get or create context for this epoch
            var ctx = _epochContexts.GetOrAdd(
                epochStream.Epoch,
                _ => _contextFactory.CreateDbContext());

            await foreach (var item in epochStream.Items.WithCancellation(cancellationToken))
            {
                // Track the entity
                ctx.Attach(item);
                
                yield return item;
            }
        }
    }

    // Lifecycle Events
    
    public ValueTask OnEpochCreatedAsync(EpochVector epoch, IBlockContext block, CancellationToken ct)
    {
        _logger.LogInformation("Epoch {Epoch} created in block {Block}", epoch, block.BlockName);
        return ValueTask.CompletedTask;
    }

    public ValueTask OnEpochCompletedAsync(EpochVector epoch, IBlockContext block, CancellationToken ct)
    {
        // Epoch done locally, but don't commit yet
        _logger.LogInformation("Block {Block} completed epoch {Epoch}", block.BlockName, epoch);
        return ValueTask.CompletedTask;
    }

    public async ValueTask OnGlobalEpochAlignedAsync(EpochVector watermark, CancellationToken ct)
    {
        // ALL blocks completed - safe to commit
        var ready = _epochContexts
            .Where(kvp => kvp.Key.IsLessThanOrEqual(watermark))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var epoch in ready)
        {
            if (_epochContexts.TryRemove(epoch, out var ctx))
            {
                try
                {
                    await using (ctx)
                    {
                        await ctx.SaveChangesAsync(ct);
                        _logger.LogInformation("Committed transaction for epoch {Epoch}", epoch);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to commit epoch {Epoch}", epoch);
                    throw;
                }
            }
        }
    }
}
```

### Step 2: Register Services

```csharp
services.AddDbContextFactory<MyDbContext>(options =>
    options.UseSqlServer(connectionString));

services.AddSingleton<EntityTrackingBlock<MyEntity, MyDbContext>>();
```

### Step 3: Add to Pipeline

```csharp
var pipeline = new DataFlowGraphBuilder()
    .AddSource<MyEntity, MySource>("source")
    
    .AddTransform<MyEntity, EnrichedEntity>("enricher",
        entity => new EnrichedEntity(entity))
    
    .AddBlock<IEpochStream<EnrichedEntity>, EnrichedEntity>("tracker",
        sp => sp.GetRequiredService<EntityTrackingBlock<EnrichedEntity, MyDbContext>>())
    
    .AddProcessor<EnrichedEntity>("logger",
        entity => Console.WriteLine($"Processed: {entity}"))
    
    .Build();
```

### Step 4: Register as Lifecycle Participant

```csharp
var trackingBlock = serviceProvider.GetRequiredService<EntityTrackingBlock<EnrichedEntity, MyDbContext>>();
coordinator.RegisterParticipant(trackingBlock);
```

### Step 5: Execute

```csharp
await pipeline.ExecuteAsync(cancellationToken);
```

## Advanced: Handling Merges

When multiple sources converge, epoch vectors merge using element-wise max. To avoid creating duplicate contexts, detect and promote ancestor contexts:

```csharp
public async IAsyncEnumerable<T> ProcessAsync(
    IAsyncEnumerable<IEpochStream<T>> input,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    await foreach (var epochStream in input.WithCancellation(cancellationToken))
    {
        TContext ctx;
        
        if (!_epochContexts.TryGetValue(epochStream.Epoch, out ctx))
        {
            // Fast path: Single-source linear progression
            if (epochStream.Epoch.Sequences.Count == 1)
            {
                ctx = _contextFactory.CreateDbContext();
                _epochContexts[epochStream.Epoch] = ctx;
            }
            else
            {
                // Merge scenario: check for reusable ancestor
                var ancestor = epochStream.Epoch.FindMostSpecificAncestor(_epochContexts.Keys);
                
                if (ancestor != null && _epochContexts.TryRemove(ancestor, out var ancestorCtx))
                {
                    // Promote the ancestor's context
                    ctx = ancestorCtx;
                    _epochContexts[epochStream.Epoch] = ctx;
                    _logger.LogDebug("Promoted context from {Ancestor} to {Merged}",
                        ancestor, epochStream.Epoch);
                }
                else
                {
                    // Create new context
                    ctx = _contextFactory.CreateDbContext();
                    _epochContexts[epochStream.Epoch] = ctx;
                }
            }
        }
        
        // Track entities in the context
        await foreach (var item in epochStream.Items.WithCancellation(cancellationToken))
        {
            ctx.Attach(item);
            yield return item;
        }
    }
}
```

### Why Context Promotion Matters

Without ancestor detection:
```
Receive: EpochVector[source1=1] → Create Context1
Receive: EpochVector[source2=1] → Create Context2
Receive: EpochVector[source1=1, source2=1] → Create Context3 ❌

Result: Entities fragmented across 3 contexts
```

With ancestor detection:
```
Receive: EpochVector[source1=1] → Create Context1
Receive: EpochVector[source2=1] → Create Context2
Receive: EpochVector[source1=1, source2=1] → Promote Context1 ✅

Result: All entities in single context, atomic commit
```

## Multi-Sink Example

Track entities in multiple databases with coordinated commits:

```csharp
// Order tracking block
var orderTracker = new EntityTrackingBlock<Order, OrderDbContext>(
    orderContextFactory, logger);

// Inventory tracking block
var inventoryTracker = new EntityTrackingBlock<InventoryItem, InventoryDbContext>(
    inventoryContextFactory, logger);

// Both participate in lifecycle coordination
coordinator.RegisterParticipant(orderTracker);
coordinator.RegisterParticipant(inventoryTracker);

// Build pipeline
var pipeline = new DataFlowGraphBuilder()
    .AddSource<Transaction, TransactionSource>("source")
    
    // Branch 1: Orders
    .AddTransform<Transaction, Order>("order-extractor", tx => tx.Order)
    .AddBlock("order-tracker", sp => orderTracker)
    
    // Branch 2: Inventory
    .AddTransform<Transaction, InventoryItem>("inventory-extractor", tx => tx.InventoryItem)
    .AddBlock("inventory-tracker", sp => inventoryTracker)
    
    .Build();

await pipeline.ExecuteAsync(ct);
```

Both tracking blocks commit at the same global alignment boundary, ensuring consistency.

## Custom Entity Modifications

Extend the tracking block to add custom logic:

```csharp
public class TimestampedTrackingBlock<T, TContext> : EntityTrackingBlock<T, TContext>
    where T : class, ITimestamped
    where TContext : DbContext
{
    public override async IAsyncEnumerable<T> ProcessAsync(
        IAsyncEnumerable<IEpochStream<T>> input,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in base.ProcessAsync(input, ct))
        {
            // Add timestamps before tracking
            item.LastModified = DateTime.UtcNow;
            item.ModifiedBy = "DataFlow";
            
            yield return item;
        }
    }
}
```

## Performance Considerations

### Context Lifetime

Contexts live from creation until global alignment:
- Fast alignment → short-lived contexts
- Slow alignment → long-lived contexts with more entities

**Monitoring:**
```csharp
public ValueTask OnGlobalEpochAlignedAsync(EpochVector watermark, CancellationToken ct)
{
    var unalignedCount = _epochContexts.Count;
    _metrics.RecordUnalignedEpochCount(unalignedCount);
    
    if (unalignedCount > 50)
    {
        _logger.LogWarning("High unaligned epoch count: {Count}", unalignedCount);
    }
    
    // ... commit logic ...
}
```

### Entity Accumulation

In continuous merge scenarios, a single context can accumulate many entities:
```
Epoch[source1=1] → Context1 (100 entities)
Epoch[source1=1, source2=1] → Promotes Context1 (200 entities)
Epoch[source1=1, source2=1, source3=1] → Promotes Context1 (300 entities)
```

**Mitigation:**
```csharp
// Option 1: Intermediate commits for large contexts
if (ctx.ChangeTracker.Entries().Count() > 5000)
{
    await ctx.SaveChangesAsync(ct);
    ctx = _contextFactory.CreateDbContext();
}

// Option 2: Monitor and alert
if (ctx.ChangeTracker.Entries().Count() > 10000)
{
    _logger.LogWarning("Context has {Count} tracked entities", 
        ctx.ChangeTracker.Entries().Count());
}
```

For most workloads, global alignment happens frequently enough that this is not an issue.

### DbContext Thread Safety

**Important:** `DbContext` is NOT thread-safe. However, this pattern is safe because:
- Each epoch has its own isolated context instance
- Each epoch stream is processed sequentially
- Concurrent processing of different epochs uses different contexts

## Error Handling

### Commit Failures

```csharp
public async ValueTask OnGlobalEpochAlignedAsync(EpochVector watermark, CancellationToken ct)
{
    var ready = GetReadyContexts(watermark);
    
    foreach (var (epoch, ctx) in ready)
    {
        try
        {
            await using (ctx)
            {
                await ctx.SaveChangesAsync(ct);
            }
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Failed to commit epoch {Epoch}", epoch);
            
            // Options:
            // 1. Throw (fail-fast, pipeline stops)
            throw;
            
            // 2. Retry with exponential backoff
            // await RetryCommit(ctx, epoch, ct);
            
            // 3. Move to dead-letter queue
            // await _deadLetterQueue.AddAsync(epoch, ex, ct);
        }
        finally
        {
            _epochContexts.TryRemove(epoch, out _);
        }
    }
}
```

### Idempotent Recovery

For exactly-once semantics during recovery:

```csharp
public async ValueTask OnGlobalEpochAlignedAsync(EpochVector watermark, CancellationToken ct)
{
    var ready = GetReadyContexts(watermark);
    
    foreach (var (epoch, ctx) in ready)
    {
        // Check if already committed (recovery scenario)
        if (await _checkpointStore.IsCommittedAsync(epoch, ct))
        {
            _logger.LogInformation("Epoch {Epoch} already committed, skipping", epoch);
            _epochContexts.TryRemove(epoch, out _);
            continue;
        }
        
        // Commit and mark as committed
        await using (ctx)
        {
            await ctx.SaveChangesAsync(ct);
        }
        
        await _checkpointStore.MarkCommittedAsync(epoch, ct);
        _epochContexts.TryRemove(epoch, out _);
    }
}
```

## Testing

### Unit Test

```csharp
[Test]
public async Task TrackingBlock_CommitsOnGlobalAlignment()
{
    // Arrange
    var contextFactory = new InMemoryDbContextFactory<TestDbContext>();
    var logger = new Mock<ILogger<EntityTrackingBlock<Product, TestDbContext>>>();
    var trackingBlock = new EntityTrackingBlock<Product, TestDbContext>(
        contextFactory.Object, logger.Object);
    
    var epoch1 = EpochVector.FromSingleSource("test", 1);
    var products = new[] { new Product { Id = 1, Name = "Test" } };
    
    // Act: Process items
    var epochStream = new TestEpochStream<Product>(epoch1, products.ToAsyncEnumerable());
    var result = trackingBlock.ProcessAsync(new[] { epochStream }.ToAsyncEnumerable());
    await result.ToListAsync();
    
    // Act: Trigger alignment
    await trackingBlock.OnGlobalEpochAlignedAsync(epoch1, CancellationToken.None);
    
    // Assert: Verify commit
    var ctx = contextFactory.LastCreatedContext;
    Assert.That(ctx.SaveChangesCalled, Is.True);
    Assert.That(ctx.Products.Count(), Is.EqualTo(1));
}
```

### Integration Test

```csharp
[Test]
public async Task Pipeline_CommitsAtGlobalAlignment()
{
    // Build pipeline with tracking block
    var pipeline = BuildTestPipeline();
    var trackingBlock = GetTrackingBlock();
    coordinator.RegisterParticipant(trackingBlock);
    
    // Execute
    await pipeline.ExecuteAsync(CancellationToken.None);
    
    // Assert: Verify database state
    using var verifyContext = new TestDbContext(_options);
    var committed = await verifyContext.Products.ToListAsync();
    Assert.That(committed.Count, Is.EqualTo(expectedCount));
}
```

## Common Pitfalls

### ❌ Committing Too Early

```csharp
// ❌ WRONG: Committing on per-block completion
public async ValueTask OnEpochCompletedAsync(EpochVector epoch, IBlockContext block, CancellationToken ct)
{
    await _context.SaveChangesAsync(ct);  // Other blocks may not be done!
}
```

```csharp
// ✅ CORRECT: Commit on global alignment
public async ValueTask OnGlobalEpochAlignedAsync(EpochVector watermark, CancellationToken ct)
{
    var ready = _contexts.Where(kvp => kvp.Key.IsLessThanOrEqual(watermark));
    foreach (var (epoch, ctx) in ready)
    {
        await ctx.SaveChangesAsync(ct);
    }
}
```

### ❌ Forgetting to Register Participant

```csharp
// ❌ WRONG: Tracking block created but not registered
var trackingBlock = new EntityTrackingBlock<Product, AppDbContext>(...);
// Lifecycle events will never fire!
```

```csharp
// ✅ CORRECT: Register with coordinator
var trackingBlock = new EntityTrackingBlock<Product, AppDbContext>(...);
coordinator.RegisterParticipant(trackingBlock);  // Events will fire
```

### ❌ Reusing Contexts Across Epochs

```csharp
// ❌ WRONG: Single context for all epochs
private readonly DbContext _sharedContext;

public async IAsyncEnumerable<T> ProcessAsync(...)
{
    await foreach (var epochStream in input)
    {
        await foreach (var item in epochStream.Items)
        {
            _sharedContext.Attach(item);  // Wrong! Mixes epochs
        }
    }
}
```

```csharp
// ✅ CORRECT: Context per epoch
private readonly ConcurrentDictionary<EpochVector, TContext> _epochContexts;

public async IAsyncEnumerable<T> ProcessAsync(...)
{
    await foreach (var epochStream in input)
    {
        var ctx = _epochContexts.GetOrAdd(epochStream.Epoch, 
            _ => _contextFactory.CreateDbContext());
        
        await foreach (var item in epochStream.Items)
        {
            ctx.Attach(item);  // Correct: isolated per epoch
        }
    }
}
```

## Related Guides

- [Handling Merges](./handling-merges.md) (Coming soon) - Fan-in strategies
- [EF Core Integration](./ef-core-integration.md) (Coming soon) - Best practices for EF Core
- [Multi-Sink Pipelines](./multi-sink-pipelines.md) (Coming soon) - Multiple tracking blocks

## Related Concepts

- [Transaction Boundaries](../concepts/transaction-boundaries.md) - When to commit safely
- [Lifecycle Events](../concepts/lifecycle-events.md) - Event-driven coordination
- [Global Alignment](../concepts/global-alignment.md) - Computing safe boundaries

## References

- Phase 6: EntityTrackingBlock pattern and examples
- Phase 5: Global alignment infrastructure
