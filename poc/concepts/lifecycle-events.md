# Lifecycle Events

## What are Lifecycle Events?

**Lifecycle events** are notifications that fire at key points in an epoch's lifecycle, enabling components to react to epoch creation, per-block completion, and global alignment. This event-driven model decouples epoch tracking from epoch reaction.

## The Participant Interface

```csharp
public interface IEpochLifecycleParticipant
{
    // Called when a block starts processing an epoch
    ValueTask OnEpochCreatedAsync(EpochVector epoch, IBlockContext block, CancellationToken ct);
    
    // Called when a block finishes processing an epoch
    ValueTask OnEpochCompletedAsync(EpochVector epoch, IBlockContext block, CancellationToken ct);
    
    // Called when ALL blocks have completed an epoch (global alignment reached)
    ValueTask OnGlobalEpochAlignedAsync(EpochVector watermark, CancellationToken ct);
}
```

## Event Lifecycle

```mermaid
sequenceDiagram
    participant Block
    participant Coordinator
    participant Participant
    
    Block->>Coordinator: Start processing Epoch 5
    Coordinator->>Participant: OnEpochCreatedAsync(Epoch 5)
    Note over Participant: Initialize resources for epoch
    
    Block->>Block: Process items...
    
    Block->>Coordinator: Completed Epoch 5
    Coordinator->>Participant: OnEpochCompletedAsync(Epoch 5)
    Note over Participant: Per-block cleanup (optional)
    
    Note over Coordinator: Wait for ALL blocks to complete Epoch 5
    
    Coordinator->>Participant: OnGlobalEpochAlignedAsync(watermark=5)
    Note over Participant: Commit transactions, save state
```

## Event Semantics

### OnEpochCreatedAsync

**Trigger:** A block begins processing a new epoch

**When to use:**
- Pre-allocate resources for the epoch
- Initialize per-epoch caches
- Start per-epoch metrics collection
- Log epoch start for debugging

**Example:**
```csharp
public ValueTask OnEpochCreatedAsync(EpochVector epoch, IBlockContext block, CancellationToken ct)
{
    _logger.LogInformation("Block {Block} started epoch {Epoch}", block.BlockName, epoch);
    
    // Pre-create DbContext (alternatively, create lazily in ProcessAsync)
    var ctx = _contextFactory.CreateDbContext();
    _epochContexts[epoch] = ctx;
    
    return ValueTask.CompletedTask;
}
```

**Characteristics:**
- Fires per-block (each block gets its own notification)
- No coordination required
- Lightweight initialization

### OnEpochCompletedAsync

**Trigger:** A single block finishes processing all items in an epoch

**When to use:**
- Per-block metrics (latency, throughput for this block)
- Optional per-block resource cleanup
- Debugging and observability

**⚠️ Important:** This does NOT indicate global alignment. Other blocks may still be processing the same epoch.

**Example:**
```csharp
public ValueTask OnEpochCompletedAsync(EpochVector epoch, IBlockContext block, CancellationToken ct)
{
    _metrics.RecordBlockCompletion(block.BlockName, epoch);
    
    _logger.LogDebug("Block {Block} completed epoch {Epoch}", block.BlockName, epoch);
    
    // ⚠️ DO NOT commit transactions here!
    // Other blocks may not have completed yet.
    
    return ValueTask.CompletedTask;
}
```

**Characteristics:**
- Fires per-block
- **Not a safe transaction boundary**
- Use for observability, not coordination

### OnGlobalEpochAlignedAsync

**Trigger:** ALL blocks have completed processing up to the watermark epoch

**When to use:**
- **Commit transactions** (safe boundary)
- Save checkpoints
- Dispose per-epoch resources
- Record global completion metrics

**Example:**
```csharp
public async ValueTask OnGlobalEpochAlignedAsync(EpochVector watermark, CancellationToken ct)
{
    // Safe to commit all epochs ≤ watermark
    var ready = _epochContexts
        .Where(kvp => kvp.Key.IsLessThanOrEqual(watermark))
        .ToList();
    
    foreach (var (epoch, ctx) in ready)
    {
        await using (ctx)
        {
            await ctx.SaveChangesAsync(ct);
            _logger.LogInformation("Committed epoch {Epoch}", epoch);
        }
        
        _epochContexts.TryRemove(epoch, out _);
    }
}
```

**Characteristics:**
- Fires once per aligned watermark (not per-block)
- **Safe transaction boundary** ✅
- All blocks have completed processing

## Use Cases

### Transaction Management

**Problem:** When to commit database changes?

**Solution:** Commit on global alignment

```csharp
public class EntityTrackingBlock<T, TContext> : IEpochLifecycleParticipant
    where TContext : DbContext
{
    public async ValueTask OnGlobalEpochAlignedAsync(EpochVector watermark, CancellationToken ct)
    {
        // Commit all ready contexts atomically
        var ready = _contexts.Where(kvp => kvp.Key.IsLessThanOrEqual(watermark));
        
        foreach (var (epoch, ctx) in ready)
        {
            await ctx.SaveChangesAsync(ct);
        }
    }
}
```

### Metrics Collection

**Problem:** Track epoch processing latency across the pipeline

**Solution:** Listen to lifecycle events

```csharp
public class EpochMetricsCollector : IEpochLifecycleParticipant
{
    private readonly ConcurrentDictionary<EpochVector, DateTime> _epochStarts = new();
    
    public ValueTask OnEpochCreatedAsync(EpochVector epoch, IBlockContext block, CancellationToken ct)
    {
        _epochStarts.TryAdd(epoch, DateTime.UtcNow);
        return ValueTask.CompletedTask;
    }
    
    public ValueTask OnGlobalEpochAlignedAsync(EpochVector watermark, CancellationToken ct)
    {
        if (_epochStarts.TryRemove(watermark, out var start))
        {
            var duration = DateTime.UtcNow - start;
            _metrics.RecordEpochLatency(watermark, duration);
        }
        
        return ValueTask.CompletedTask;
    }
}
```

### Cache Management

**Problem:** Per-epoch caches need cleanup after commit

**Solution:** Clear caches on alignment

```csharp
public class PerEpochCacheManager : IEpochLifecycleParticipant
{
    private readonly ConcurrentDictionary<EpochVector, Cache<TKey, TValue>> _epochCaches = new();
    
    public ValueTask OnEpochCreatedAsync(EpochVector epoch, IBlockContext block, CancellationToken ct)
    {
        _epochCaches[epoch] = new Cache<TKey, TValue>();
        return ValueTask.CompletedTask;
    }
    
    public ValueTask OnGlobalEpochAlignedAsync(EpochVector watermark, CancellationToken ct)
    {
        var ready = _epochCaches.Where(kvp => kvp.Key.IsLessThanOrEqual(watermark));
        
        foreach (var (epoch, cache) in ready)
        {
            cache.Dispose();
            _epochCaches.TryRemove(epoch, out _);
        }
        
        return ValueTask.CompletedTask;
    }
}
```

### Checkpoint Management

**Problem:** Save application state at safe boundaries

**Solution:** Checkpoint on global alignment

```csharp
public class CheckpointManager : IEpochLifecycleParticipant
{
    public async ValueTask OnGlobalEpochAlignedAsync(EpochVector watermark, CancellationToken ct)
    {
        var checkpoint = new Checkpoint
        {
            Watermark = watermark,
            Timestamp = DateTime.UtcNow,
            ApplicationState = CollectState()
        };
        
        await _storage.SaveAsync(checkpoint, ct);
        _logger.LogInformation("Saved checkpoint at {Watermark}", watermark);
    }
}
```

## Registration and Coordination

### Registering Participants

```csharp
// Create participant
var trackingBlock = new EntityTrackingBlock<Product, AppDbContext>(contextFactory, logger);

// Register with coordinator
coordinator.RegisterParticipant(trackingBlock);
```

### Coordinator Responsibilities

The `EpochLifecycleCoordinator` manages the broadcast:

```csharp
public class EpochLifecycleCoordinator
{
    private readonly List<IEpochLifecycleParticipant> _participants = new();
    
    public void RegisterParticipant(IEpochLifecycleParticipant participant)
    {
        _participants.Add(participant);
    }
    
    public async Task NotifyEpochCreatedAsync(EpochVector epoch, IBlockContext block, CancellationToken ct)
    {
        foreach (var participant in _participants)
        {
            await participant.OnEpochCreatedAsync(epoch, block, ct);
        }
    }
    
    public async Task NotifyGlobalAlignmentAsync(EpochVector watermark, CancellationToken ct)
    {
        foreach (var participant in _participants)
        {
            await participant.OnGlobalEpochAlignedAsync(watermark, ct);
        }
    }
}
```

## Integration with Alignment Tracking

Lifecycle events are layered on top of existing alignment infrastructure:

```
┌─────────────────────────────────────┐
│   EpochLifecycleCoordinator         │
│   (Broadcasts lifecycle events)     │
└──────────────┬──────────────────────┘
               │ listens to
┌──────────────▼──────────────────────┐
│   GlobalEpochAlignment              │
│   (Tracks completion, computes      │
│    watermark)                       │
└─────────────────────────────────────┘
```

**Separation of concerns:**
- **GlobalEpochAlignment:** Tracks progress, computes watermarks
- **EpochLifecycleCoordinator:** Notifies interested parties
- No duplication of alignment logic

## Design Principles

### Event-Driven Decoupling

Components react to events without knowing about each other:

```
Source → Transform → Sink
                      ↓
                    Tracking Block (participant)
                      ↓
                    Metrics Collector (participant)
                      ↓
                    Checkpoint Manager (participant)
```

Each participant is independent and can be added/removed without affecting others.

### Composability

Multiple participants can coexist:

```csharp
coordinator.RegisterParticipant(new EntityTrackingBlock<Order, OrderDbContext>(...));
coordinator.RegisterParticipant(new EntityTrackingBlock<Customer, CustomerDbContext>(...));
coordinator.RegisterParticipant(new EpochMetricsCollector(...));
coordinator.RegisterParticipant(new CheckpointManager(...));
```

All receive the same events and react independently.

### Testability

Participants can be tested in isolation:

```csharp
[Test]
public async Task TrackingBlock_CommitsOnAlignment()
{
    var trackingBlock = new EntityTrackingBlock<Product, TestDbContext>(...);
    
    // Simulate lifecycle
    await trackingBlock.OnEpochCreatedAsync(epoch1, block1, ct);
    // ... process items ...
    await trackingBlock.OnGlobalEpochAlignedAsync(epoch1, ct);
    
    // Assert context was committed
    Assert.That(_dbContext.SaveChangesCalled, Is.True);
}
```

## Common Patterns

### Pattern: Conditional Participation

Participate only for specific epochs:

```csharp
public async ValueTask OnGlobalEpochAlignedAsync(EpochVector watermark, CancellationToken ct)
{
    // Only checkpoint every 10th epoch
    if (watermark.GetSequenceFor("mainSource") % 10 == 0)
    {
        await SaveCheckpoint(watermark, ct);
    }
}
```

### Pattern: Multi-Phase Cleanup

Clean up resources in stages:

```csharp
public ValueTask OnEpochCompletedAsync(EpochVector epoch, IBlockContext block, CancellationToken ct)
{
    // Stage 1: Mark context as ready for commit (per-block)
    _readyContexts.TryAdd(epoch, DateTime.UtcNow);
    return ValueTask.CompletedTask;
}

public async ValueTask OnGlobalEpochAlignedAsync(EpochVector watermark, CancellationToken ct)
{
    // Stage 2: Actually commit (global)
    var ready = _contexts.Where(kvp => kvp.Key.IsLessThanOrEqual(watermark));
    foreach (var (epoch, ctx) in ready)
    {
        await ctx.SaveChangesAsync(ct);
    }
}
```

## Error Handling

### Participant Failures

If a participant throws during event handling:

```csharp
public async Task NotifyGlobalAlignmentAsync(EpochVector watermark, CancellationToken ct)
{
    foreach (var participant in _participants)
    {
        try
        {
            await participant.OnGlobalEpochAlignedAsync(watermark, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Participant {Participant} failed on alignment {Watermark}", 
                participant.GetType().Name, watermark);
            
            // Strategy: Continue or fail-fast?
            // For transactional participants, fail-fast is safer
            throw;
        }
    }
}
```

### Retries on Commit Failures

```csharp
public async ValueTask OnGlobalEpochAlignedAsync(EpochVector watermark, CancellationToken ct)
{
    var ready = GetReadyContexts(watermark);
    
    foreach (var (epoch, ctx) in ready)
    {
        var retries = 0;
        while (retries < 3)
        {
            try
            {
                await ctx.SaveChangesAsync(ct);
                break;
            }
            catch (DbUpdateException ex) when (IsTransient(ex))
            {
                retries++;
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retries)), ct);
            }
        }
    }
}
```

## Related Concepts

- [Epochs](./epochs.md) - Core epoch concept
- [Global Alignment](./global-alignment.md) - Computing safe boundaries
- [Transaction Boundaries](./transaction-boundaries.md) - When to commit safely

## References

- Phase 6: Lifecycle event interfaces and coordinator design
- Phase 5: Global alignment infrastructure
