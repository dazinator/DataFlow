# Epoch-based Checkpointing

This document describes the epoch-based checkpointing mechanism for point-in-time recovery in DataFlow.

## Overview

Checkpointing enables fault recovery without full pipeline restart by leveraging epoch boundaries for consistent snapshots. The system allows blocks to save lightweight state (offsets, cursors, etc.) at specific epoch boundaries, which can later be restored on recovery.

## Key Features

- ✅ **Temporal consistency**: Blocks contribute state at exact epoch boundaries
- ✅ **Thread safety**: Checkpoint only accessible within serialized epoch operations
- ✅ **Block isolation**: Write-once semantics prevent interference between blocks
- ✅ **Flexible persistence**: Checkpoints can be persisted atomically or best-effort
- ✅ **Low overhead**: < 1% performance impact on epoch completion

## Core Concepts

### Checkpoint

A checkpoint is a container that holds block states at a specific epoch boundary. Each checkpoint has:

- **CheckpointId**: Unique identifier
- **EpochVector**: Position in the stream
- **Timestamp**: When the checkpoint was created
- **BlockStates**: Dictionary of block IDs to JsonElement (serialized as human-readable JSON)

Block states are stored as `JsonElement`, which provides:
- **Human-readable JSON**: When checkpoint is serialized, produces readable JSON output
- **Efficient**: JsonElement is a readonly struct with minimal overhead
- **Type-safe**: Use `ctx.Checkpoint?.SetState()` helper or `JsonSerializer.SerializeToElement()` to create, native JSON deserialization to read

### Checkpoint Strategy

Determines which epochs should create checkpoints. Built-in strategies:

- **EveryNEpochsStrategy**: Checkpoint every N epochs
- **TimeBasedStrategy**: Checkpoint at time intervals

Custom strategies can be implemented via `ICheckpointStrategy`.

### Epoch Operation Context

Provides access to the checkpoint within serialized operations. This ensures thread-safe checkpoint access:

```csharp
await epoch.QueueSerializedOperationAsync<MyService>(async (svc, ctx) =>
{
    // ctx.Checkpoint is available here (null if not checkpointing)
    if (ctx.Checkpoint != null)
    {
        ctx.Checkpoint.AddBlockState("my-block", state);
    }
});
```

## Usage

### 1. Configure Checkpoint Strategy

Create a checkpoint strategy when configuring epochs:

```csharp
graph.ConfigureEpochs(config =>
{
    config.AddProcessor("processor1");
    
    // Set checkpoint strategy
    config.SetCheckpointStrategy(new EveryNEpochsStrategy(10)); // Checkpoint every 10 epochs
    
    config.OnCommitEpoch(async (epoch, ct) =>
    {
        // Persist checkpoint if present
    });
}, strategy => new EpochCoordinator(
    serviceProvider.GetRequiredService<IServiceScopeFactory>(),
    checkpointStrategy: strategy
));
```

Or use time-based strategy:

```csharp
config.SetCheckpointStrategy(new TimeBasedStrategy(TimeSpan.FromMinutes(5)));
```

### 2. Check if Epoch is Checkpointing

Blocks can check the `IsCheckpointing` flag to determine if the current epoch is being checkpointed:

```csharp
if (epoch.IsCheckpointing)
{
    // This epoch should be checkpointed
    // Contribute block state within serialized operation
}
```

### 3. Contribute Block State

Use the new `QueueSerializedOperationAsync` overload that accepts context:

```csharp
await epoch.QueueSerializedOperationAsync<MyService>(async (svc, ctx) =>
{
    // Use SetState helper for canonical, ergonomic API
    ctx.Checkpoint?.SetState(_blockId, new { 
        offset = _currentOffset,
        lastProcessedId = _lastId
    });
    
    await Task.CompletedTask;
});
```

**Alternative (manual)**: You can also call `AddBlockState` directly if you already have a `JsonElement`:
```csharp
var state = JsonSerializer.SerializeToElement(new { offset = _currentOffset });
ctx.Checkpoint?.AddBlockState(_blockId, state);
```

**Important**: Each block can only contribute once per checkpoint. Attempting to add state with the same blockId twice will throw `InvalidOperationException`.

### 4. Persist Checkpoint (Application-Specific)

Checkpoint persistence is application-specific. A common pattern is to save checkpoints in `OnCommitEpoch` hook:

```csharp
var hooks = new EpochHooks
{
    OnCommitEpoch = async (epoch, ct) =>
    {
        await epoch.QueueSerializedOperationAsync<DbContext>(async (db, ctx) =>
        {
            // Commit transaction
            await db.SaveChangesAsync(ct);
            
            // Save checkpoint if present
            if (ctx.Checkpoint != null)
            {
                db.Checkpoints.Add(new CheckpointEntity
                {
                    CheckpointId = ctx.Checkpoint.CheckpointId,
                    EpochVector = SerializeVector(ctx.Checkpoint.EpochVector),
                    BlockStates = SerializeStates(ctx.Checkpoint.BlockStates),
                    Timestamp = ctx.Checkpoint.Timestamp
                });
            }
            
            await db.Database.CommitTransactionAsync(ct);
        }, ct);
    }
};
```

### 5. Restore from Checkpoint

**Recommended Approach**: Use execution context for natural recovery

```csharp
// Configure recovery hook to load checkpoint
var hooks = new EpochHooks
{
    OnLoadRecoveryCheckpoint = async (ct) =>
    {
        // Load latest checkpoint from storage
        return await checkpointStore.GetLatestCheckpointAsync(ct);
    },
    
    OnCommitEpoch = async (epoch, ct) =>
    {
        await epoch.QueueSerializedOperationAsync<DummyService>(
            async (svc, ctx) =>
            {
                if (ctx.Checkpoint != null)
                {
                    await checkpointStore.SaveCheckpointAsync(ctx.Checkpoint, ct);
                }
            }, ct);
    }
};

// Blocks access recovery checkpoint from execution context
public class ResumableQueueSource
{
    private long _currentOffset;
    private readonly string _blockId;
    private readonly IExecutionContext _context;
    
    public ResumableQueueSource(string blockId, IExecutionContext context)
    {
        _blockId = blockId;
        _context = context;
        
        // Restore from checkpoint if available
        if (context.RecoveryCheckpoint?.TryGetBlockState(blockId, out var state) == true)
        {
            _currentOffset = state.GetProperty("offset").GetInt64();
        }
    }
    
    // Block continues from restored offset naturally
    public async IAsyncEnumerable<Message> ProduceAsync(IEpoch epoch, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            yield return await FetchMessageAsync(_currentOffset);
            _currentOffset++;
            
            // Save state to checkpoint
            if (epoch.IsCheckpointing)
            {
                await epoch.QueueSerializedOperationAsync<DummyService>(
                    async (svc, ctx) =>
                    {
                        ctx.Checkpoint?.SetState(_blockId, new { offset = _currentOffset });
                    }, ct);
            }
        }
    }
}
```

**Alternative Approach**: Manual restoration (for backward compatibility)

```csharp
// 1. Load latest checkpoint from storage
var checkpoint = await checkpointStore.GetLatestCheckpointAsync();

// 2. Create blocks and restore their state from checkpoint
var sourceBlock = new CheckpointAwareQueueSource("source-block");
if (checkpoint != null && checkpoint.TryGetBlockState("source-block", out var sourceState))
{
    sourceBlock.RestoreFromCheckpoint(checkpoint);
}

// 3. Configure and start dataflow with restored blocks
var coordinator = new EpochCoordinator(scopeFactory, checkpointStrategy: strategy);
// ... configure and execute dataflow
```

## End-to-End Recovery Example

Here's a complete example showing how to save and restore from checkpoints:

```csharp
// ===== CHECKPOINT PERSISTENCE =====

// Define a simple checkpoint store (application-specific)
public class InMemoryCheckpointStore
{
    private ICheckpoint? _latestCheckpoint;
    
    public Task SaveCheckpointAsync(ICheckpoint checkpoint)
    {
        _latestCheckpoint = checkpoint;
        return Task.CompletedTask;
    }
    
    public Task<ICheckpoint?> GetLatestCheckpointAsync()
    {
        return Task.FromResult(_latestCheckpoint);
    }
}

// ===== CHECKPOINT-AWARE BLOCK =====

public class ResumableQueueSource
{
    private long _currentOffset;
    private readonly string _blockId;
    
    public ResumableQueueSource(string blockId, long initialOffset = 0)
    {
        _blockId = blockId;
        _currentOffset = initialOffset;
    }
    
    // Restore state from checkpoint before execution
    public void RestoreFromCheckpoint(ICheckpoint checkpoint)
    {
        if (checkpoint.BlockStates.TryGetValue(_blockId, out var state))
        {
            _currentOffset = state.GetProperty("offset").GetInt64();
        }
    }
    
    // Save state to checkpoint during execution
    public async IAsyncEnumerable<Message> ProduceAsync(
        IEpoch epoch,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            yield return await FetchMessageAsync(_currentOffset);
            _currentOffset++;
            
            if (epoch.IsCheckpointing)
            {
                await epoch.QueueSerializedOperationAsync<DummyService>(
                    async (svc, ctx) =>
                    {
                        ctx.Checkpoint?.SetState(_blockId, new { offset = _currentOffset });
                        await Task.CompletedTask;
                    }, ct);
            }
        }
    }
}

// ===== APPLICATION INITIALIZATION =====

public class DataFlowApp
{
    private readonly InMemoryCheckpointStore _checkpointStore = new();
    private readonly IServiceScopeFactory _scopeFactory;
    
    public async Task RunAsync(bool recoverFromCheckpoint = false)
    {
        // 1. Create checkpoint-aware blocks
        var sourceBlock = new ResumableQueueSource("queue-source");
        
        // 2. Restore from checkpoint if recovering
        if (recoverFromCheckpoint)
        {
            var checkpoint = await _checkpointStore.GetLatestCheckpointAsync();
            if (checkpoint != null)
            {
                Console.WriteLine($"Recovering from checkpoint {checkpoint.CheckpointId}");
                sourceBlock.RestoreFromCheckpoint(checkpoint);
            }
        }
        
        // 3. Configure checkpointing via epoch configuration
        // (In real usage, this would be done when building the dataflow graph)
        var coordinator = new EpochCoordinator(
            _scopeFactory, 
            checkpointStrategy: new EveryNEpochsStrategy(10)); // Checkpoint every 10 epochs
        
        // 4. Configure checkpoint persistence
        var hooks = new EpochHooks
        {
            OnCommitEpoch = async (epoch, ct) =>
            {
                // Save checkpoint after epoch commits
                await epoch.QueueSerializedOperationAsync<DummyService>(
                    async (svc, ctx) =>
                    {
                        if (ctx.Checkpoint != null)
                        {
                            await _checkpointStore.SaveCheckpointAsync(ctx.Checkpoint);
                            Console.WriteLine($"Saved checkpoint {ctx.Checkpoint.CheckpointId}");
                        }
                        await Task.CompletedTask;
                    }, ct);
            }
        };
        
        // 5. Execute dataflow (sourceBlock will resume from restored offset)
        // ... configure epoch processor with hooks and execute
    }
}

// ===== USAGE =====

// First run - start from beginning
await app.RunAsync(recoverFromCheckpoint: false);

// After failure - resume from last checkpoint
await app.RunAsync(recoverFromCheckpoint: true);
```

## Example: Checkpoint-Aware Source Block

```csharp
public class CheckpointAwareQueueSource
{
    private long _currentOffset = 0;
    private readonly string _blockId;

    public async IAsyncEnumerable<int> ProduceAsync(
        IEpoch epoch, 
        CancellationToken ct = default)
    {
        for (int i = 0; i < _totalItems; i++)
        {
            yield return FetchItem(_currentOffset);
            _currentOffset++;
            
            // Checkpoint current offset if epoch is checkpointing
            if (epoch.IsCheckpointing)
            {
                await epoch.QueueSerializedOperationAsync<DummyService>(
                    async (svc, ctx) =>
                    {
                        ctx.Checkpoint?.SetState(_blockId, new { offset = _currentOffset });
                        await Task.CompletedTask;
                    }, ct);
            }
        }
    }

    public void RestoreFromCheckpoint(ICheckpoint checkpoint)
    {
        if (checkpoint.BlockStates.TryGetValue(_blockId, out var state))
        {
            _currentOffset = state.GetProperty("offset").GetInt64();
        }
    }
}
```

## Thread Safety

The checkpoint mechanism is designed to be thread-safe:

1. **IsCheckpointing flag**: Can be safely read from any thread
2. **Checkpoint access**: Only available within serialized operations via `IEpochOperationContext`
3. **AddBlockState**: Access is serialized by design - operations execute serially, so a regular Dictionary is used for efficiency

All checkpoint mutations must occur within serialized epoch operations to ensure thread safety.

## Performance

The checkpointing mechanism has minimal overhead:

- **Checkpoint creation**: < 1ms for typical workloads (5-10 blocks)
- **Overhead on epoch completion**: < 1% 
- **Recovery time**: < 10ms for typical checkpoints

These measurements are based on in-memory checkpoints. Persistence overhead depends on the storage backend.

## API Reference

### ICheckpoint

```csharp
public interface ICheckpoint
{
    string CheckpointId { get; }
    EpochVector EpochVector { get; }
    DateTimeOffset Timestamp { get; }
    void AddBlockState(string blockId, JsonElement state);
    bool TryGetBlockState(string blockId, out JsonElement state);
    IReadOnlyDictionary<string, JsonElement> BlockStates { get; }
}
```

### IEpochOperationContext

```csharp
public interface IEpochOperationContext
{
    ICheckpoint? Checkpoint { get; }
}
```

### ICheckpointStrategy

```csharp
public interface ICheckpointStrategy
{
    bool ShouldCreateCheckpoint(EpochVector epochVector);
}
```

### IEpoch Extensions

```csharp
public interface IEpoch
{
    // Existing members...
    
    bool IsCheckpointing { get; }
    
    Task QueueSerializedOperationAsync<TService>(
        Func<TService, IEpochOperationContext, Task> operation,
        CancellationToken cancellationToken = default)
        where TService : notnull;
}
```

## Design Rationale

### Why Epoch Boundaries?

Epoch boundaries provide natural alignment points where all blocks have completed processing a specific set of data. This makes them ideal for creating consistent snapshots without complex coordination.

### Why Write-Once Semantics?

Write-once semantics (each block contributes exactly once per checkpoint) prevent:
- Accidental state overwrites
- Race conditions between blocks
- Unclear ownership of state

### Why JsonElement?

Using `JsonElement` for state storage provides the best balance:

**Human Readable**: When checkpoint is serialized, produces readable JSON:
```json
{
  "checkpointId": "checkpoint-123",
  "blockStates": {
    "queue-source": {"offset": 12345, "lastId": "abc-123"},
    "processor-1": {"processedCount": 5000, "lastTimestamp": "2025-01-15T12:30:00Z"}
  }
}
```

**Performance**: JsonElement is a readonly struct with minimal overhead:
- No boxing allocations
- Efficient memory representation
- Fast serialization/deserialization via System.Text.Json

**Developer Experience**:
- Type-safe access via `GetProperty()` methods
- No manual encoding/decoding
- Easy debugging - inspect checkpoints in any text editor
- Transparent state - see exactly where pipeline stopped

**Usage**:
```csharp
// Create state (recommended - use SetState helper)
ctx.Checkpoint?.SetState(blockId, new { 
    offset = 123, 
    lastId = "abc" 
});

// Alternative - manual approach
var state = JsonSerializer.SerializeToElement(new { 
    offset = 123, 
    lastId = "abc" 
});
ctx.Checkpoint?.AddBlockState(blockId, state);

// Read state
var offset = state.GetProperty("offset").GetInt64();
var lastId = state.GetProperty("lastId").GetString();
```

### Why Serialized Operations?

Requiring checkpoint access within serialized operations ensures:
- Thread-safe checkpoint mutations
- No concurrent access conflicts
- Predictable execution order

## Backward Compatibility

The checkpoint mechanism is fully backward compatible:

1. **Optional feature**: Checkpointing is opt-in via checkpoint strategy
2. **Existing API preserved**: Old `QueueSerializedOperationAsync` signature still works
3. **No breaking changes**: All existing tests continue to pass

## See Also

- [Checkpoint Examples](../Examples/CheckpointingExamples.cs)
- [Checkpoint Tests](../../DataFlow.POC.Tests/Checkpointing/)
- [Research Documentation](/research/checkpointing-recovery/README.md)
