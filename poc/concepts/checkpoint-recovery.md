# Checkpoint and Recovery

## Overview

**Checkpointing** is the process of saving pipeline state at safe boundaries (global epoch alignment) to enable recovery after failures. Epoch-based coordination provides natural checkpoint boundaries with exactly-once processing guarantees.

## Why Checkpointing?

In long-running pipelines, failures are inevitable:
- Process crashes
- Network interruptions
- Database connection failures
- Host machine restarts

Without checkpoints:
- Must reprocess from the beginning ❌
- Risk duplicate processing ❌
- Lost progress ❌

With checkpoints:
- Resume from last saved state ✅
- No duplicate processing ✅
- Minimal reprocessing ✅

## Safe Checkpoint Boundaries

Not all points in execution are safe for checkpointing:

### ❌ Unsafe: Mid-Epoch

```
Source emitting Epoch 5...
Some items processed, some still in flight
CRASH → Unclear state, must reprocess entire epoch
```

### ❌ Unsafe: Per-Block Completion

```
Block A completed Epoch 5
Block B still processing Epoch 5
CRASH → Block A's commit lost, Block B must reprocess
```

### ✅ Safe: Global Alignment

```
ALL blocks completed Epoch 5
Transactions committed
State saved
CRASH → Resume from Epoch 6, no reprocessing needed
```

## Checkpoint Data

A checkpoint contains:

```csharp
public class Checkpoint
{
    // Epoch watermark - safe resume point
    public EpochVector Watermark { get; set; }
    
    // When checkpoint was created
    public DateTime Timestamp { get; set; }
    
    // Application-specific state
    public Dictionary<string, object> ApplicationState { get; set; }
    
    // Block-specific state
    public Dictionary<string, BlockState> BlockStates { get; set; }
}
```

### Example

```json
{
  "watermark": {
    "sequences": {
      "sourceA": 100,
      "sourceB": 85
    }
  },
  "timestamp": "2024-11-03T20:00:00Z",
  "applicationState": {
    "lastProcessedId": 12345,
    "customMetric": 42
  },
  "blockStates": {
    "tracking-block": {
      "pendingContexts": 2,
      "committedEpochs": 100
    }
  }
}
```

## Checkpoint Lifecycle

### 1. Save on Global Alignment

```csharp
public class CheckpointManager : IEpochLifecycleParticipant
{
    public async ValueTask OnGlobalEpochAlignedAsync(
        EpochVector watermark, 
        CancellationToken ct)
    {
        var checkpoint = new Checkpoint
        {
            Watermark = watermark,
            Timestamp = DateTime.UtcNow,
            ApplicationState = CollectApplicationState(),
            BlockStates = CollectBlockStates()
        };
        
        await _storage.SaveCheckpointAsync(checkpoint, ct);
        
        _logger.LogInformation(
            "Checkpoint saved at watermark {Watermark}", 
            watermark);
    }
    
    private Dictionary<string, object> CollectApplicationState()
    {
        return new Dictionary<string, object>
        {
            ["lastProcessedId"] = _lastProcessedId,
            ["customMetric"] = _customMetric
        };
    }
    
    private Dictionary<string, BlockState> CollectBlockStates()
    {
        // Collect state from participating blocks
        return _blocks.ToDictionary(
            b => b.Name,
            b => b.GetState());
    }
}
```

### 2. Load on Startup

```csharp
public async Task<Checkpoint?> LoadLastCheckpointAsync(CancellationToken ct)
{
    var checkpoint = await _storage.GetLatestCheckpointAsync(ct);
    
    if (checkpoint != null)
    {
        _logger.LogInformation(
            "Loaded checkpoint from {Timestamp} with watermark {Watermark}",
            checkpoint.Timestamp,
            checkpoint.Watermark);
    }
    else
    {
        _logger.LogInformation("No checkpoint found, starting from beginning");
    }
    
    return checkpoint;
}
```

### 3. Resume from Checkpoint

```csharp
public async Task ExecuteWithRecoveryAsync(CancellationToken ct)
{
    // Load last checkpoint
    var checkpoint = await LoadLastCheckpointAsync(ct);
    
    EpochVector resumeFrom;
    
    if (checkpoint != null)
    {
        // Resume from saved watermark
        resumeFrom = checkpoint.Watermark;
        RestoreApplicationState(checkpoint.ApplicationState);
        RestoreBlockStates(checkpoint.BlockStates);
    }
    else
    {
        // Start from beginning
        resumeFrom = EpochVector.Zero;
    }
    
    _logger.LogInformation("Resuming from epoch {Epoch}", resumeFrom);
    
    // Execute pipeline from resume point
    await _pipeline.ExecuteAsync(resumeFrom, ct);
}
```

## Recovery Scenarios

### Scenario 1: Clean Recovery

```
Timeline:
1. Process epochs 1-100
2. Checkpoint saved at epoch 100
3. Process epochs 101-110
4. CRASH
5. Recovery: Load checkpoint (epoch 100)
6. Resume from epoch 101
7. Epochs 101-110 reprocessed (idempotent operations)
```

**Result:** No data loss, minimal reprocessing ✅

### Scenario 2: Checkpoint Failure

```
Timeline:
1. Process epochs 1-100
2. Checkpoint save FAILS
3. Continue processing epochs 101-110
4. Checkpoint saved at epoch 110
5. CRASH
6. Recovery: Load checkpoint (epoch 110)
7. Resume from epoch 111
```

**Result:** Epochs 101-110 not reprocessed (already committed) ✅

### Scenario 3: Multiple Source Recovery

```
Checkpoint watermark: EpochVector[sourceA=100, sourceB=85]

Recovery:
- SourceA resumes from epoch 101
- SourceB resumes from epoch 86
- Both sources coordinate via epoch vectors
```

**Result:** Each source resumes from correct position ✅

## Exactly-Once Semantics

### The Challenge

After recovery, some epochs may be reprocessed:

```
Checkpoint saved at: Epoch 100
Crashed while processing: Epochs 101-105
After recovery: Epochs 101-105 reprocessed
```

**Risk:** Duplicate processing if operations are not idempotent

### Solution 1: Idempotent Operations

Design operations to be safely reexecutable:

```csharp
// ❌ Not idempotent
public async Task ProcessOrder(Order order)
{
    _inventory.DecreaseStock(order.ProductId, order.Quantity);
    await _db.SaveChangesAsync();
}

// ✅ Idempotent
public async Task ProcessOrder(Order order)
{
    if (await _db.ProcessedOrders.AnyAsync(o => o.Id == order.Id))
    {
        _logger.LogInformation("Order {Id} already processed, skipping", order.Id);
        return;
    }
    
    _inventory.DecreaseStock(order.ProductId, order.Quantity);
    await _db.ProcessedOrders.AddAsync(new ProcessedOrder { Id = order.Id });
    await _db.SaveChangesAsync();
}
```

### Solution 2: Epoch Commit Tracking

Track which epochs have been committed:

```csharp
public class CommitTracker
{
    private readonly ICheckpointStore _checkpointStore;
    
    public async ValueTask OnGlobalEpochAlignedAsync(
        EpochVector watermark, 
        CancellationToken ct)
    {
        var ready = GetReadyContexts(watermark);
        
        foreach (var (epoch, ctx) in ready)
        {
            // Check if already committed (recovery scenario)
            if (await _checkpointStore.IsCommittedAsync(epoch, ct))
            {
                _logger.LogInformation(
                    "Epoch {Epoch} already committed, skipping", 
                    epoch);
                continue;
            }
            
            // Commit and mark as committed
            await using (ctx)
            {
                await ctx.SaveChangesAsync(ct);
            }
            
            await _checkpointStore.MarkCommittedAsync(epoch, ct);
            
            _logger.LogInformation(
                "Committed and marked epoch {Epoch}", 
                epoch);
        }
    }
}
```

### Solution 3: Source Position Tracking

Sources track their position and skip already-emitted epochs:

```csharp
public class RecoverableSource : SourceActorBase<Data>
{
    public override async IAsyncEnumerable<IEpochStream<Data>> ProduceEpochsAsync(
        IActorExecutionContext context)
    {
        // Load last emitted epoch from checkpoint
        var lastEpoch = await LoadLastEmittedEpochAsync();
        var nextEpoch = lastEpoch + 1;
        
        await foreach (var data in LoadDataAsync(startFrom: nextEpoch))
        {
            yield return CreateEpochStream(
                CreateEpoch(_sourceId, nextEpoch++),
                data.ToAsyncEnumerable());
        }
    }
}
```

## Checkpoint Storage

### In-Memory (Testing)

```csharp
public class InMemoryCheckpointStore : ICheckpointStore
{
    private Checkpoint? _lastCheckpoint;
    
    public Task SaveCheckpointAsync(Checkpoint checkpoint, CancellationToken ct)
    {
        _lastCheckpoint = checkpoint;
        return Task.CompletedTask;
    }
    
    public Task<Checkpoint?> GetLatestCheckpointAsync(CancellationToken ct)
    {
        return Task.FromResult(_lastCheckpoint);
    }
}
```

### File-Based (Simple)

```csharp
public class FileCheckpointStore : ICheckpointStore
{
    private readonly string _checkpointPath;
    
    public async Task SaveCheckpointAsync(Checkpoint checkpoint, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(checkpoint);
        await File.WriteAllTextAsync(_checkpointPath, json, ct);
    }
    
    public async Task<Checkpoint?> GetLatestCheckpointAsync(CancellationToken ct)
    {
        if (!File.Exists(_checkpointPath))
            return null;
        
        var json = await File.ReadAllTextAsync(_checkpointPath, ct);
        return JsonSerializer.Deserialize<Checkpoint>(json);
    }
}
```

### Database-Based (Production)

```csharp
public class DatabaseCheckpointStore : ICheckpointStore
{
    private readonly DbContext _db;
    
    public async Task SaveCheckpointAsync(Checkpoint checkpoint, CancellationToken ct)
    {
        var entity = new CheckpointEntity
        {
            Id = Guid.NewGuid(),
            Watermark = JsonSerializer.Serialize(checkpoint.Watermark),
            Timestamp = checkpoint.Timestamp,
            ApplicationState = JsonSerializer.Serialize(checkpoint.ApplicationState),
            BlockStates = JsonSerializer.Serialize(checkpoint.BlockStates)
        };
        
        _db.Checkpoints.Add(entity);
        await _db.SaveChangesAsync(ct);
    }
    
    public async Task<Checkpoint?> GetLatestCheckpointAsync(CancellationToken ct)
    {
        var entity = await _db.Checkpoints
            .OrderByDescending(c => c.Timestamp)
            .FirstOrDefaultAsync(ct);
        
        if (entity == null)
            return null;
        
        return new Checkpoint
        {
            Watermark = JsonSerializer.Deserialize<EpochVector>(entity.Watermark),
            Timestamp = entity.Timestamp,
            ApplicationState = JsonSerializer.Deserialize<Dictionary<string, object>>(entity.ApplicationState),
            BlockStates = JsonSerializer.Deserialize<Dictionary<string, BlockState>>(entity.BlockStates)
        };
    }
}
```

## Checkpoint Frequency

### Trade-offs

**Frequent checkpoints:**
- ✅ Less reprocessing on recovery
- ❌ Higher I/O overhead
- ❌ More storage space

**Infrequent checkpoints:**
- ✅ Lower overhead
- ✅ Less storage
- ❌ More reprocessing on recovery

### Strategies

**Every N epochs:**
```csharp
public ValueTask OnGlobalEpochAlignedAsync(EpochVector watermark, CancellationToken ct)
{
    if (watermark.GetSequenceFor(_sourceId) % 10 == 0)
    {
        return SaveCheckpointAsync(watermark, ct);
    }
    
    return ValueTask.CompletedTask;
}
```

**Time-based:**
```csharp
private DateTime _lastCheckpoint = DateTime.UtcNow;

public async ValueTask OnGlobalEpochAlignedAsync(EpochVector watermark, CancellationToken ct)
{
    if (DateTime.UtcNow - _lastCheckpoint > TimeSpan.FromMinutes(5))
    {
        await SaveCheckpointAsync(watermark, ct);
        _lastCheckpoint = DateTime.UtcNow;
    }
}
```

**Hybrid:**
```csharp
public async ValueTask OnGlobalEpochAlignedAsync(EpochVector watermark, CancellationToken ct)
{
    var shouldCheckpoint = 
        watermark.GetSequenceFor(_sourceId) % 10 == 0 ||
        DateTime.UtcNow - _lastCheckpoint > TimeSpan.FromMinutes(5);
    
    if (shouldCheckpoint)
    {
        await SaveCheckpointAsync(watermark, ct);
        _lastCheckpoint = DateTime.UtcNow;
    }
}
```

## Testing Recovery

### Simulate Failure

```csharp
[Test]
public async Task Pipeline_RecoverFromCheckpoint()
{
    var checkpointStore = new InMemoryCheckpointStore();
    var pipeline = BuildPipeline(checkpointStore);
    
    // Process some epochs
    var cts = new CancellationTokenSource();
    var task = pipeline.ExecuteAsync(cts.Token);
    
    await Task.Delay(TimeSpan.FromSeconds(5));
    
    // Simulate crash
    cts.Cancel();
    
    try
    {
        await task;
    }
    catch (OperationCanceledException)
    {
        // Expected
    }
    
    // Verify checkpoint was saved
    var checkpoint = await checkpointStore.GetLatestCheckpointAsync(CancellationToken.None);
    Assert.That(checkpoint, Is.Not.Null);
    
    // Resume from checkpoint
    var recoveredPipeline = BuildPipeline(checkpointStore);
    await recoveredPipeline.ExecuteAsync(CancellationToken.None);
    
    // Verify no data loss
    var result = await VerifyDataIntegrity();
    Assert.That(result.MissingItems, Is.Empty);
    Assert.That(result.DuplicateItems, Is.Empty);
}
```

## Best Practices

### ✅ Do

- Checkpoint at global alignment only
- Store checkpoint data transactionally
- Test recovery scenarios regularly
- Monitor checkpoint frequency
- Use idempotent operations
- Track committed epochs

### ❌ Don't

- Checkpoint mid-epoch (unsafe)
- Checkpoint per-block completion (inconsistent)
- Skip checkpoint testing
- Assume operations are idempotent
- Store checkpoints with application data (separate concerns)

## Related Concepts

- [Global Alignment](./global-alignment.md) - Safe checkpoint boundaries
- [Transaction Boundaries](./transaction-boundaries.md) - When to commit
- [Epochs](./epochs.md) - Core epoch concept

## References

- Phase 5: Global alignment infrastructure for checkpointing
- Phase 6: Lifecycle events for checkpoint coordination
