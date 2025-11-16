# Checkpoint Architecture Design

## Overview

This document outlines the design for a checkpointing mechanism that enables point-in-time recovery in DataFlow, building on the existing epoch system.

**Updated Approach** (Based on design review feedback): Checkpoints are created as **epoch properties** rather than through an external coordinator. This solves the "state ahead" problem where blocks may have advanced beyond the epoch being checkpointed.

## Core Concepts

### 1. Checkpoint as Epoch Property (Revised Approach)

**Key Insight**: Checkpoints should be part of the epoch lifecycle, allowing blocks to contribute state at the exact epoch boundary.

**The "State Ahead" Problem**:
When blocks process asynchronously, their internal state can advance beyond the current epoch being checkpointed:
```csharp
// Block's internal state
_currentOffset = 150;  // Already processed items from epoch N+1

// But checkpoint is for epoch N (items 0-100)  
// Asking block for state NOW returns 150 (wrong!)
```

**Solution - Checkpoint as Epoch Property**:
- Epoch coordinator flags epochs for checkpointing based on strategy
- Epoch carries an optional `Checkpoint` property
- Blocks check `epoch.Checkpoint` when queuing operations
- Blocks contribute state for THAT specific epoch, ensuring temporal consistency
- OnCommitEpoch hook can persist checkpoint atomically with transaction OR best-effort after

**Benefits**:
- ✅ **Temporal consistency**: Blocks contribute state at exact epoch boundary
- ✅ **Atomic persistence option**: Checkpoint can be part of epoch transaction
- ✅ **Simpler architecture**: No external checkpoint coordinator needed
- ✅ **Block-driven**: Blocks control when they snapshot during epoch operations

### 2. Checkpoint Data Model

```csharp
/// <summary>
/// Represents a checkpoint containing state from multiple blocks.
/// Accessed only within serialized epoch operations for thread safety.
/// </summary>
public interface ICheckpoint
{
    /// <summary>
    /// Unique identifier for this checkpoint.
    /// </summary>
    string CheckpointId { get; }
    
    /// <summary>
    /// The epoch vector at which this checkpoint was taken.
    /// </summary>
    EpochVector EpochVector { get; }
    
    /// <summary>
    /// Timestamp when checkpoint was created.
    /// </summary>
    DateTimeOffset Timestamp { get; }
    
    /// <summary>
    /// Adds block state to this checkpoint.
    /// Called from within serialized epoch operations only.
    /// Throws if blockId already exists (prevents blocks from overwriting each other).
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if blockId already exists</exception>
    void AddBlockState(string blockId, byte[] state);
    
    /// <summary>
    /// Block-contributed state data (read-only view).
    /// Keys are block IDs, values are serialized state.
    /// </summary>
    IReadOnlyDictionary<string, byte[]> BlockStates { get; }
}

/// <summary>
/// Extended IEpoch interface with checkpoint support.
/// </summary>
public interface IEpoch : IAsyncDisposable
{
    EpochVector Vector { get; }
    IServiceProvider ServiceProvider { get; }
    
    // ... existing members ...
    
    /// <summary>
    /// Returns true if this epoch is flagged for checkpointing.
    /// Use this to check if checkpoint contribution is needed.
    /// The actual checkpoint object is only accessible within serialized operations.
    /// </summary>
    bool IsCheckpointing { get; }
}

/// <summary>
/// Context provided to serialized epoch operations when checkpointing is enabled.
/// </summary>
public interface IEpochOperationContext
{
    /// <summary>
    /// The checkpoint for this epoch (only available when IsCheckpointing is true).
    /// Null when epoch is not being checkpointed.
    /// </summary>
    ICheckpoint? Checkpoint { get; }
}
```

**Design Decision**: Use a container-based checkpoint with byte[] values for flexibility.
- **Pros**: Blocks can serialize their state however they want (JSON, protobuf, etc.)
- **Cons**: No compile-time type safety for state data
- **Alternative**: Strongly-typed checkpoint per dataflow would require generic constraints throughout

**Concurrency Safety**:
- ✅ **Outside operations**: Blocks can only check `epoch.IsCheckpointing` flag (read-only, no race conditions)
- ✅ **Inside operations**: Checkpoint accessed via operation context (serialized by epoch processor)
- ✅ **Block isolation**: `AddBlockState` throws on duplicate keys (prevents interference)
- ✅ **No concurrent mutations**: All checkpoint updates happen serially in epoch operations queue

**Checkpoint Lifecycle**:
1. Epoch coordinator creates checkpoint on flagged epochs and sets `IsCheckpointing = true`
2. Blocks check `epoch.IsCheckpointing` during processing
3. Within serialized operations, blocks access `context.Checkpoint.AddBlockState(blockId, state)`
4. OnCommitEpoch hook persists the populated checkpoint

### 3. Block Contribution to Checkpoints

Blocks contribute state to checkpoints **within serialized epoch operations** for thread safety and isolation:

```csharp
public class MessageQueueSource : IProducer<Message>
{
    private long _currentOffset = 0;
    private readonly string _blockId;
    
    public async IAsyncEnumerable<Message> ProduceAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var epochStream in _epochSource.GetEpochStreamsAsync(cancellationToken))
        {
            var epoch = epochStream.EpochScope;
            
            // Process items for this epoch
            await foreach (var message in epochStream.Items)
            {
                yield return message;
                _currentOffset++;
            }
            
            // After processing epoch, contribute checkpoint if flagged
            // IMPORTANT: Contribution happens within serialized operation
            if (epoch.IsCheckpointing)
            {
                await epoch.QueueSerializedOperationAsync<IEpochOperationContext>(async ctx =>
                {
                    var state = Encoding.UTF8.GetBytes(_currentOffset.ToString());
                    ctx.Checkpoint?.AddBlockState(_blockId, state);
                }, cancellationToken);
            }
        }
    }
    
    public Task RestoreFromCheckpointAsync(ICheckpoint checkpoint)
    {
        if (checkpoint.BlockStates.TryGetValue(_blockId, out var state))
        {
            _currentOffset = long.Parse(Encoding.UTF8.GetString(state));
        }
        return Task.CompletedTask;
    }
}
```

**Concurrency Safety Pattern**:
1. **Check outside operations**: `if (epoch.IsCheckpointing)` - safe, read-only flag
2. **Access inside operations**: `ctx.Checkpoint?.AddBlockState()` - serialized by epoch processor
3. **Write-once semantics**: `AddBlockState` throws on duplicate `blockId` - prevents interference

**Alternative Pattern** (for blocks already using operations):
```csharp
// Block already queuing operations to epoch
await epoch.QueueSerializedOperationAsync<DbContext>(async (db, ctx) =>
{
    // Process data
    await db.Items.AddAsync(item);
    
    // Contribute checkpoint if this epoch is checkpointing
    if (ctx.Checkpoint != null)
    {
        var state = SerializeCurrentState();
        ctx.Checkpoint.AddBlockState(_blockId, state);
    }
}, cancellationToken);
```

**When to Contribute**:
- **Deterministic progress**: Contribute at start of epoch processing (block knows final state upfront)
- **Discovered progress**: Contribute at end of epoch processing (after processing all items)
- **Atomic operations**: Contribute within same operation that processes data

**Key Benefits**:
- ✅ **Thread-safe**: All checkpoint mutations happen serially
- ✅ **Isolated**: Blocks cannot overwrite each other's state (throws on duplicate)
- ✅ **Simple**: No complex locking or concurrent data structures needed

**Examples of checkpoint contributions:**
- **Source blocks**: Current offset/cursor position
- **Batch blocks**: Partial batch state or drain state
- **Stateful transforms**: Accumulated state for this epoch

### 4. Checkpoint Strategy

Strategy pattern to control when checkpoints are created:

```csharp
public interface ICheckpointStrategy
{
    /// <summary>
    /// Determines if a checkpoint should be created for the given epoch.
    /// </summary>
    bool ShouldCreateCheckpoint(EpochVector epochVector);
}

// Example strategies:
public class EveryNEpochsStrategy : ICheckpointStrategy
{
    private readonly int _n;
    private int _counter = 0;
    
    public EveryNEpochsStrategy(int n) => _n = n;
    
    public bool ShouldCreateCheckpoint(EpochVector epochVector)
    {
        _counter++;
        if (_counter >= _n)
        {
            _counter = 0;
            return true;
        }
        return false;
    }
}

public class TimeBasedStrategy : ICheckpointStrategy
{
    private readonly TimeSpan _interval;
    private DateTimeOffset _lastCheckpoint = DateTimeOffset.MinValue;
    
    public TimeBasedStrategy(TimeSpan interval) => _interval = interval;
    
    public bool ShouldCreateCheckpoint(EpochVector epochVector)
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastCheckpoint >= _interval)
        {
            _lastCheckpoint = now;
            return true;
        }
        return false;
    }
}
```

### 5. Checkpoint Persistence

**Two Modes**: Atomic (checkpoint with transaction) OR Best-effort (checkpoint after transaction)

```csharp
/// <summary>
/// Abstraction for checkpoint persistence.
/// Applications can implement to persist checkpoints however they want.
/// </summary>
public interface ICheckpointStore
{
    /// <summary>
    /// Persists a checkpoint.
    /// </summary>
    Task SaveCheckpointAsync(ICheckpoint checkpoint, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves the latest checkpoint, or null if none exists.
    /// </summary>
    Task<ICheckpoint?> GetLatestCheckpointAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves a specific checkpoint by ID.
    /// </summary>
    Task<ICheckpoint?> GetCheckpointAsync(string checkpointId, CancellationToken cancellationToken = default);
}
```

**Mode 1: Atomic Checkpointing** (Checkpoint as part of epoch transaction)

```csharp
var hooks = new EpochHooks
{
    OnBeginEpoch = async (epoch, ct) =>
    {
        await epoch.QueueSerializedOperationAsync<DbContext>(async db =>
        {
            await db.Database.BeginTransactionAsync(ct);
        }, ct);
    },
    
    OnCommitEpoch = async (epoch, ct) =>
    {
        await epoch.QueueSerializedOperationAsync<DbContext>(async (db, ctx) =>
        {
            await db.SaveChangesAsync(ct);
            
            // Include checkpoint in same transaction (accessed via context)
            if (ctx.Checkpoint != null)
            {
                // Application-specific persistence
                db.Checkpoints.Add(new CheckpointEntity
                {
                    Id = ctx.Checkpoint.CheckpointId,
                    EpochVector = ctx.Checkpoint.EpochVector.ToString(),
                    BlockStates = ctx.Checkpoint.BlockStates
                });
            }
            
            await db.Database.CommitTransactionAsync(ct);
        }, ct);
    }
};
```

**Mode 2: Best-Effort Checkpointing** (Checkpoint after transaction)

```csharp
var hooks = new EpochHooks
{
    OnCommitEpoch = async (epoch, ct) =>
    {
        ICheckpoint? checkpoint = null;
        
        // First: commit epoch transaction and capture checkpoint
        await epoch.QueueSerializedOperationAsync<DbContext>(async (db, ctx) =>
        {
            await db.SaveChangesAsync(ct);
            await db.Database.CommitTransactionAsync(ct);
            checkpoint = ctx.Checkpoint;  // Capture for use outside operation
        }, ct);
        
        // Then: persist checkpoint (best-effort, outside serialized operations)
        if (checkpoint != null)
        {
            try
            {
                await checkpointStore.SaveCheckpointAsync(checkpoint, ct);
            }
            catch (Exception ex)
            {
                // Log but don't fail epoch - checkpoint is best-effort
                _logger.LogWarning(ex, "Failed to persist checkpoint");
            }
        }
    }
};
```

**Trade-offs**:
- **Atomic**: Stronger guarantees, checkpoint and epoch as unit, but checkpoint failure fails epoch
- **Best-effort**: Checkpoint failure doesn't affect epoch, but checkpoint/epoch may be inconsistent

### 6. Epoch Coordinator Integration

Epoch coordinator creates checkpoints based on strategy when yielding epochs:

```csharp
public class EpochCoordinator
{
    private readonly ICheckpointStrategy? _checkpointStrategy;
    
    internal async ValueTask<IEpoch> GetOrCreateEpochAsync(
        string sourceId,
        EpochVector vector,
        CancellationToken cancellationToken = default)
    {
        // ... existing epoch creation logic ...
        
        var epoch = new Epoch(vector, scope);
        
        // Flag epoch for checkpointing based on strategy
        if (_checkpointStrategy?.ShouldCreateCheckpoint(vector) == true)
        {
            var checkpoint = new Checkpoint
            {
                CheckpointId = $"checkpoint-{vector}-{DateTimeOffset.UtcNow.Ticks}",
                EpochVector = vector,
                Timestamp = DateTimeOffset.UtcNow
            };
            
            epoch.SetCheckpoint(checkpoint);  // Internal method sets IsCheckpointing = true
        }
        
        return epoch;
    }
}
```

**Updated IEpochOperation Signature**:
```csharp
// Operations now receive context with checkpoint access
public interface IEpochOperation
{
    Task ExecuteAsync(IServiceProvider serviceProvider, IEpochOperationContext context, CancellationToken ct);
}

// QueueSerializedOperationAsync updated signature
Task QueueSerializedOperationAsync<TService>(
    Func<TService, IEpochOperationContext, Task> operation,
    CancellationToken cancellationToken = default)
    where TService : notnull;
```

**Key Points**:
- Coordinator creates checkpoint object based on strategy
- Epoch internally sets `IsCheckpointing` flag
- Blocks check flag outside operations, access checkpoint inside operations
- Epoch processor provides context to all operations
- No direct checkpoint access outside serialized operations

### 7. Recovery Flow

Recovery loads checkpoint and restores block state before dataflow execution:

```csharp
/// <summary>
/// Configuration for dataflow recovery from checkpoint.
/// </summary>
public class RecoveryConfiguration
{
    public string? CheckpointId { get; set; }
    public bool RecoverFromLatest { get; set; }
}

// Before execution: restore from checkpoint
var checkpoint = await checkpointStore.GetLatestCheckpointAsync();
if (checkpoint != null)
{
    // Restore each block's state
    foreach (var (blockId, state) in checkpoint.BlockStates)
    {
        var block = GetBlockById(blockId);
        if (block != null)
        {
            await block.RestoreFromCheckpointAsync(checkpoint);
        }
    }
}

// Execute dataflow - blocks start from restored state
await dataFlow.ExecuteAsync();
```

**Recovery Process**:
1. Load latest checkpoint from store
2. Restore each block's state by calling their restore methods
3. Blocks initialize internal state (e.g., source sets starting offset)
4. Execute dataflow - processing continues from checkpoint position

## Summary

**Revised Architecture**:
- ✅ Checkpoint as epoch property (not external coordinator)
- ✅ Blocks contribute during epoch processing (temporal consistency)
- ✅ Flexible persistence (atomic OR best-effort)
- ✅ Simpler integration (epoch coordinator flags epochs)

## Concurrency Considerations

### Block State Contribution

**Q: How do blocks safely contribute to checkpoint from concurrent epoch operations?**

**A: Checkpoint accessed only within serialized epoch operations:**

**Outside Operations** (concurrent code):
- ✅ Blocks can check `epoch.IsCheckpointing` - read-only flag, thread-safe
- ❌ Blocks cannot access checkpoint object - prevents race conditions

**Inside Operations** (serialized by epoch processor):
- ✅ Checkpoint accessed via `IEpochOperationContext`
- ✅ All operations execute serially - no concurrent mutations
- ✅ `AddBlockState` throws on duplicate keys - prevents block interference

**Example**:
```csharp
// Outside operation: Safe read-only check
if (epoch.IsCheckpointing)
{
    // Inside operation: Safe serialized access
    await epoch.QueueSerializedOperationAsync<MyService>(async (svc, ctx) =>
    {
        var state = GetCurrentState();
        ctx.Checkpoint?.AddBlockState(_blockId, state);  // Serial, safe
    }, ct);
}
```

**Q: Is checkpoint persistence transactional?**

**A: Application decides - two modes supported:**

**Atomic Mode** (recommended for strong consistency):
- Checkpoint included in epoch transaction
- Checkpoint and epoch commit as atomic unit
- Checkpoint failure fails the epoch
- Stronger guarantees but less tolerant of checkpoint issues

**Best-Effort Mode** (recommended for availability):
- Checkpoint persisted after epoch transaction
- Checkpoint failure logged but doesn't fail epoch
- Weaker guarantees but more tolerant of checkpoint issues

### Block Isolation

**Write-Once Semantics**: `AddBlockState` enforces block isolation by throwing if a blockId already exists:

```csharp
public void AddBlockState(string blockId, byte[] state)
{
    if (_blockStates.ContainsKey(blockId))
    {
        throw new InvalidOperationException(
            $"Block '{blockId}' has already contributed to this checkpoint. " +
            "Each block can only contribute once per checkpoint.");
    }
    _blockStates[blockId] = state;
}
```

**Benefits**:
- ✅ Prevents blocks from overwriting each other
- ✅ Catches configuration errors (duplicate block IDs)
- ✅ Clear error messages for debugging

## Open Questions (for Implementation)

1. **Block ID Tracking**: How do we identify blocks for checkpoint state mapping?
   - Recommendation: Builder-assigned IDs for consistency

2. **Checkpoint Retention**: How long to keep old checkpoints?
   - Configurable retention policy (keep last N checkpoints)

3. **Multi-Source Scenarios**: 
   - Epoch vector captures all source positions
   - Each source block saves its own offset
   - Works naturally with epoch coordination
