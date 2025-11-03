# Transaction Boundaries

## What are Transaction Boundaries?

A **transaction boundary** is a point in pipeline execution where it's safe to commit persistent state changes. In epoch-based pipelines, transaction boundaries align with **global epoch alignment** to ensure consistency across all processing stages.

## The Safety Problem

Consider an ETL pipeline with database writes:

```
Source → Transform → DbWriter
```

**Question:** When should `DbWriter` commit its transaction?

**Naive approach** - commit after processing each item:
- ❌ High overhead (many small transactions)
- ❌ Partial failures leave inconsistent state
- ❌ Can't rollback a logical batch

**Batch approach** - commit after N items:
- ❌ Arbitrary boundaries don't align with logical work units
- ❌ Recovery must track partial batches
- ❌ Doesn't coordinate with other sinks

**Epoch-aligned approach** - commit at global alignment:
- ✅ Logical boundaries tied to data structure
- ✅ All blocks coordinate on the same boundary
- ✅ Clean checkpoint semantics
- ✅ Exactly-once processing guarantees

## Safe vs. Unsafe Boundaries

### Unsafe: Per-Block Completion

```csharp
public ValueTask OnEpochCompletedAsync(EpochVector epoch, IBlockContext block, CancellationToken ct)
{
    // ⚠️ THIS BLOCK finished processing the epoch
    // ⚠️ Other blocks may still be processing it
    // ⚠️ NOT SAFE for committing!
    
    return ValueTask.CompletedTask;
}
```

**Problem:**
```
Block A completes Epoch 5 → commits transaction
Block B crashes while processing Epoch 5
→ Block A's commit is lost or inconsistent with Block B ❌
```

### Safe: Global Alignment

```csharp
public async ValueTask OnGlobalEpochAlignedAsync(EpochVector watermark, CancellationToken ct)
{
    // ✅ ALL blocks finished processing epochs ≤ watermark
    // ✅ No in-flight data for these epochs
    // ✅ SAFE to commit!
    
    var ready = _contexts.Where(kvp => kvp.Key.IsLessThanOrEqual(watermark));
    foreach (var (epoch, ctx) in ready)
    {
        await ctx.SaveChangesAsync(ct);
    }
}
```

**Guarantee:**
- All blocks have completed processing
- No partial state across blocks
- Recovery can resume from this point

## Multi-Sink Coordination

### Problem: Independent Commits

```
                ┌→ DbWriter A (commits at Epoch 5)
Source → Split ─┤
                └→ DbWriter B (commits at Epoch 3)
```

**Risk:**
- Writer A commits Epoch 5
- System crashes
- Writer B only has up to Epoch 3
- **Inconsistent state across databases** ❌

### Solution: Coordinated Alignment

```
                ┌→ DbWriter A (completes Epoch 5, waits)
Source → Split ─┤
                └→ DbWriter B (completes Epoch 3) ← bottleneck

Global Watermark: Epoch 3
→ Both writers commit up to Epoch 3 ✅
→ Consistent state across databases
```

## Transaction Patterns

### Pattern 1: Transactional Tracking Block

Manage per-epoch database contexts, committing at alignment:

```csharp
public class EntityTrackingBlock<T, TContext> : IEpochLifecycleParticipant
    where T : class
    where TContext : DbContext
{
    private readonly ConcurrentDictionary<EpochVector, TContext> _epochContexts = new();
    
    // Create context per epoch
    public async IAsyncEnumerable<T> ProcessAsync(IAsyncEnumerable<IEpochStream<T>> input)
    {
        await foreach (var epochStream in input)
        {
            var ctx = _contextFactory.CreateDbContext();
            _epochContexts[epochStream.Epoch] = ctx;
            
            await foreach (var item in epochStream.Items)
            {
                ctx.Attach(item);
                yield return item;
            }
        }
    }
    
    // Commit at global alignment
    public async ValueTask OnGlobalEpochAlignedAsync(EpochVector watermark, CancellationToken ct)
    {
        var ready = _epochContexts.Where(kvp => kvp.Key.IsLessThanOrEqual(watermark));
        
        foreach (var (epoch, ctx) in ready)
        {
            await using (ctx)
            {
                await ctx.SaveChangesAsync(ct);  // Safe transaction boundary ✅
            }
            _epochContexts.TryRemove(epoch, out _);
        }
    }
}
```

### Pattern 2: Checkpoint Manager

Save application state at alignment boundaries:

```csharp
public class CheckpointManager : IEpochLifecycleParticipant
{
    public async ValueTask OnGlobalEpochAlignedAsync(EpochVector watermark, CancellationToken ct)
    {
        // Safe to checkpoint - all blocks aligned
        var checkpoint = new Checkpoint
        {
            Watermark = watermark,
            Timestamp = DateTime.UtcNow,
            BlockStates = CollectBlockStates()
        };
        
        await _storage.SaveCheckpointAsync(checkpoint, ct);
    }
}
```

### Pattern 3: Multi-Database Coordinator

Coordinate commits across multiple databases:

```csharp
public class MultiDbCoordinator : IEpochLifecycleParticipant
{
    private readonly OrderDbContext _orderCtx;
    private readonly InventoryDbContext _inventoryCtx;
    
    public async ValueTask OnGlobalEpochAlignedAsync(EpochVector watermark, CancellationToken ct)
    {
        // Both contexts are aligned - safe for coordinated commit
        using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        
        await _orderCtx.SaveChangesAsync(ct);
        await _inventoryCtx.SaveChangesAsync(ct);
        
        transaction.Complete();  // Atomic commit across both databases ✅
    }
}
```

## Recovery and Exactly-Once Semantics

### Checkpoint-Based Recovery

```
1. System running, processes epochs 1-10
2. Watermark reaches Epoch 7 → checkpoint saved
3. System crashes while processing Epoch 10
4. Recovery: Load checkpoint (Epoch 7)
5. Replay from Epoch 8 onwards
```

**Guarantee:**
- Epochs 1-7: Fully processed and committed ✅
- Epochs 8-10: Safely reprocessed (idempotent or de-duplicated)
- No lost data, no duplicate commits

### Idempotent Operations

For exactly-once semantics, operations at transaction boundaries should be idempotent:

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
            continue;
        }
        
        // Commit and record
        await ctx.SaveChangesAsync(ct);
        await _checkpointStore.MarkCommittedAsync(epoch, ct);
    }
}
```

## Performance Considerations

### Commit Frequency

Transaction boundaries occur at global alignment, which depends on the slowest block:

**Low-frequency alignment:**
- Fewer commits (less overhead)
- Longer-lived transactions
- More in-flight state to manage

**High-frequency alignment:**
- More commits (more overhead)
- Shorter transactions
- Less in-flight state

**Tuning:**
- Adjust epoch size to control alignment frequency
- Profile slow blocks and optimize
- Use appropriate buffer sizes

### Context Lifetime

Per-epoch contexts live until global alignment:

```
Context for Epoch N created at: T₀
Context committed at: T₀ + alignment_latency
```

**Memory implications:**
- More unaligned epochs = more contexts in memory
- Slow blocks delay alignment → contexts accumulate
- Monitor unaligned epoch count

**Mitigation:**
```csharp
// Option 1: Intermediate commits for large contexts
if (ctx.ChangeTracker.Entries().Count() > 5000)
{
    await ctx.SaveChangesAsync(ct);
    ctx = _contextFactory.CreateDbContext();
}

// Option 2: Track watermark lag
var lag = _newestEpoch.SequenceFor(sourceId) - _watermark.SequenceFor(sourceId);
if (lag > 100)
{
    _logger.LogWarning("Alignment lag: {Lag} epochs behind", lag);
}
```

## Design Principles

### Separation of Concerns

**Sources:** Emit data, don't manage transactions
```csharp
// ✅ Good: Source is stateless
public class MySource : SourceActorBase<T>
{
    public override async IAsyncEnumerable<IEpochStream<T>> ProduceEpochsAsync(...)
    {
        // Just yield data, no transaction logic
    }
}
```

**Tracking Blocks:** Manage transactions downstream
```csharp
// ✅ Good: Transaction logic is separate, composable
public class EntityTracker : IEpochLifecycleParticipant
{
    // Commits at safe boundaries
}
```

### Flexibility

Downstream transaction blocks enable:
- Multiple sinks with different transaction scopes
- Transformation/filtering before persistence
- Branching to different databases
- Testing without real databases

## Common Pitfalls

### ❌ Committing on Per-Block Completion

```csharp
public async ValueTask OnEpochCompletedAsync(EpochVector epoch, ...)
{
    await _ctx.SaveChangesAsync(ct);  // ❌ UNSAFE - other blocks may not be done!
}
```

### ❌ Committing Without Alignment

```csharp
public async IAsyncEnumerable<T> ProcessAsync(...)
{
    await foreach (var epochStream in input)
    {
        var ctx = CreateContext();
        
        await foreach (var item in epochStream.Items)
        {
            ctx.Attach(item);
        }
        
        await ctx.SaveChangesAsync();  // ❌ UNSAFE - no coordination with other blocks!
    }
}
```

### ✅ Correct: Wait for Global Alignment

```csharp
public async ValueTask OnGlobalEpochAlignedAsync(EpochVector watermark, ...)
{
    var ready = _contexts.Where(kvp => kvp.Key.IsLessThanOrEqual(watermark));
    foreach (var (epoch, ctx) in ready)
    {
        await ctx.SaveChangesAsync(ct);  // ✅ SAFE - all blocks aligned!
    }
}
```

## Related Concepts

- [Epochs](./epochs.md) - Core epoch concept
- [Global Alignment](./global-alignment.md) - Computing safe boundaries
- [Lifecycle Events](./lifecycle-events.md) - Reacting to alignment
- [Checkpoint Recovery](./checkpoint-recovery.md) - Recovery using checkpoints

## References

- Phase 5: Global epoch alignment infrastructure
- Phase 6: Composable transaction block pattern
