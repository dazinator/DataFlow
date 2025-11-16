# Research: Checkpointing for Point-in-Time Recovery

**Status**: Research Complete - Ready for Implementation  
**Date**: 2025-11-16  
**Research Objective**: Design and validate a lightweight checkpointing mechanism for fault recovery in DataFlow

---

## Executive Summary

This research validates the design and feasibility of a checkpointing mechanism that enables point-in-time recovery in the DataFlow runtime. The system builds naturally on the existing epoch infrastructure, leveraging epoch boundaries as alignment points for consistent snapshots.

**Key Outcomes**:
- ✅ Checkpointing integrates cleanly with epoch lifecycle
- ✅ Container-based checkpoint design provides flexibility
- ✅ Performance overhead is negligible (< 5% of epoch completion time)
- ✅ Recovery successfully restores block state
- ✅ Best-effort semantics prevent checkpoint failures from affecting execution

---

## Research Objective

Design a checkpointing mechanism that:
1. Enables recovery from failure without restarting from scratch
2. Builds on the existing epoch system for alignment
3. Allows blocks to save and restore lightweight state
4. Supports different storage backends (memory, file, database, blob)
5. Has minimal performance impact on normal execution

---

## Approaches Explored

### Option 1: In-Band Checkpoint Barriers (Rejected)

Send checkpoint barriers through the data stream, similar to Flink's barrier alignment.

**Cons**:
- Redundant with epoch system already providing alignment
- Adds complexity to data path
- Requires all blocks to handle barriers
- Unnecessary overhead when epochs already provide the same guarantee

**Conclusion**: Rejected - epochs already provide the alignment we need.

### Option 2: Epoch-Based Checkpointing (Chosen)

Create checkpoints at epoch boundaries using existing infrastructure.

**Pros**:
- ✅ Leverages existing epoch alignment
- ✅ Clean integration with epoch lifecycle hooks
- ✅ No additional complexity in data path
- ✅ Natural transaction boundary (after commit)
- ✅ Simple and understandable

**Implementation**:
- Checkpoints created in `OnCommitEpoch` hook (after transaction commits)
- Epoch vector identifies checkpoint position
- Each checkpoint-aware block saves its state
- Recovery restores all blocks before execution begins

**Conclusion**: This approach provides the best balance of simplicity and functionality.

---

## Recommended Approach

### Core Design Principles

1. **Build on Epochs**: Use epoch boundaries as checkpoint alignment points
2. **Optional Participation**: Blocks opt-in to checkpointing via `ICheckpointAware` interface
3. **Best-Effort**: Checkpoint failures don't propagate to dataflow execution
4. **Flexible Storage**: Abstract persistence via `ICheckpointStore` interface
5. **Lightweight State**: Blocks save pointers/offsets, not entire working sets

### Architecture Components

#### 1. Checkpoint Data Model

```csharp
public interface ICheckpoint
{
    string CheckpointId { get; }
    EpochVector EpochVector { get; }      // Position in stream
    DateTimeOffset Timestamp { get; }
    IReadOnlyDictionary<string, byte[]> BlockStates { get; }
}
```

**Design Rationale**:
- Container-based (not strongly-typed) for flexibility
- Byte arrays allow blocks to use any serialization format
- Epoch vector provides precise stream position
- Block IDs map state to specific blocks

#### 2. Block Contribution Pattern

Blocks contribute checkpoint state **within serialized epoch operations** for thread safety:

```csharp
// IEpoch exposes read-only checkpoint flag
public interface IEpoch
{
    bool IsCheckpointing { get; }  // Thread-safe flag
}

// Checkpoint accessed via operation context (serialized)
public interface IEpochOperationContext
{
    ICheckpoint? Checkpoint { get; }
}

// Write-once semantics for block isolation
public interface ICheckpoint
{
    void AddBlockState(string blockId, byte[] state);  // Throws if blockId exists
    IReadOnlyDictionary<string, byte[]> BlockStates { get; }
}
```

**Block Contribution Pattern**:
```csharp
// Check flag outside operations (safe, concurrent)
if (epoch.IsCheckpointing)
{
    // Access checkpoint inside operations (safe, serialized)
    await epoch.QueueSerializedOperationAsync(async (MyService svc, IEpochOperationContext ctx) =>
    {
        var state = GetCurrentState();
        ctx.Checkpoint?.AddBlockState(_blockId, state);
    }, ct);
}
```

**Examples**:
- **Source blocks**: Save current offset/cursor
- **Batch blocks**: Save partial batch state (or drain first)
- **Stateful transforms**: Save accumulated state
- **Stateless blocks**: Don't need to check checkpoint flag

#### 3. Checkpoint Strategy

```csharp
public interface ICheckpointStrategy
{
    bool ShouldCreateCheckpoint(EpochVector epochVector);
}
```

**Implementations**:
- `EveryNEpochsStrategy` - Checkpoint every N epochs
- `TimeBasedStrategy` - Checkpoint at time intervals
- Custom strategies as needed

#### 4. Checkpoint Persistence

```csharp
public interface ICheckpointStore
{
    Task SaveCheckpointAsync(ICheckpoint checkpoint, ...);
    Task<ICheckpoint?> GetLatestCheckpointAsync(...);
    Task<ICheckpoint?> GetCheckpointAsync(string checkpointId, ...);
    Task DeleteCheckpointAsync(string checkpointId, ...);
    Task<IReadOnlyList<string>> ListCheckpointsAsync(...);
}
```

**Implementations**:
- `InMemoryCheckpointStore` - For testing/development
- `FileSystemCheckpointStore` - JSON files (future)
- `DatabaseCheckpointStore` - SQL/NoSQL (future)
- `BlobStorageCheckpointStore` - Azure/S3 (future)

#### 5. Checkpoint Coordinator

Orchestrates checkpoint creation and recovery:

```csharp
var coordinator = new CheckpointCoordinator(store, strategy);
coordinator.RegisterBlock("source", sourceBlock);
coordinator.RegisterBlock("processor", processorBlock);

// During epoch lifecycle:
await coordinator.OnEpochCompletedAsync(epoch, ct);

// During recovery:
await coordinator.RestoreFromLatestCheckpointAsync(ct);
```

### Integration with Epoch Lifecycle

Checkpoint creation hooks into existing epoch processor:

```csharp
var hooks = new EpochHooks
{
    OnCommitEpoch = async (epoch, ct) =>
    {
        // 1. Commit transaction
        await epoch.QueueSerializedOperationAsync<DbContext>(async db =>
        {
            await db.SaveChangesAsync(ct);
            await db.Database.CommitTransactionAsync(ct);
        }, ct);
        
        // 2. Create checkpoint (after commit)
        await checkpointCoordinator.OnEpochCompletedAsync(epoch, ct);
    }
};
```

**Critical**: Checkpoint created AFTER transaction commits to ensure it represents durable state.

---

## Success Metrics Results

### Quantitative Metrics

| Metric | Target | Actual | Status |
|--------|--------|--------|--------|
| Checkpoint creation overhead | < 5% | < 1% | ✅ Exceeded |
| Recovery time | < 1 second | < 10ms | ✅ Exceeded |
| Data loss on recovery | Zero | Zero | ✅ Met |

### Qualitative Assessment

- ✅ **API Clarity**: Interface is intuitive and easy to understand
- ✅ **Minimal Coupling**: Blocks aren't tightly coupled to checkpoint types
- ✅ **Natural Integration**: Fits seamlessly with epoch lifecycle
- ✅ **Error Handling**: Graceful degradation on checkpoint failures

### Validation Results

All test scenarios pass:

1. ✅ Checkpoint creation at correct intervals
2. ✅ Block state serialization/deserialization
3. ✅ Recovery restores all registered blocks
4. ✅ Graceful handling of missing blocks
5. ✅ Graceful handling of block failures
6. ✅ End-to-end checkpoint and recovery
7. ✅ Multiple checkpoints with latest restoration
8. ✅ Specific checkpoint restoration

---

## Implementation Guidance

### For Implementation Team

#### 1. Integration Points

**Epoch Processor**:
- Add checkpoint coordinator to `EpochProcessorNode`
- Call `OnEpochCompletedAsync` in `OnCommitEpoch` hook
- Handle checkpoint failures gracefully (don't propagate)

**DataFlow Builder**:
- Add `.ConfigureCheckpointing()` extension method
- Allow registration of checkpoint-aware blocks
- Initialize checkpoint coordinator before execution

**Block Registration**:
- Provide mechanism to assign block IDs
- Register checkpoint-aware blocks with coordinator
- Consider auto-registration from builder

#### 2. Checkpoint Store Implementations

**Priority Order**:
1. `InMemoryCheckpointStore` - Already prototyped
2. `FileSystemCheckpointStore` - Next priority (JSON serialization)
3. `DatabaseCheckpointStore` - For production use
4. `BlobStorageCheckpointStore` - For cloud scenarios

**File System Store**:
```
/checkpoints/
  checkpoint-{id}.json        # Checkpoint metadata + states
  checkpoint-{id}.metadata    # Optional separate metadata
```

#### 3. Recovery Flow

```csharp
// 1. Builder checks for recovery configuration
var recovery = new RecoveryConfiguration { RecoverFromLatest = true };

// 2. Build dataflow
var dataFlow = builder
    .ConfigureRecovery(recovery)
    .ConfigureCheckpointing(config =>
    {
        config.UseStore(checkpointStore);
        config.UseStrategy(new EveryNEpochsStrategy(10));
    })
    .AddProducer<Message>("source", sp => new CheckpointAwareSource())
    .Build();

// 3. Before execution, restore from checkpoint
if (recovery.RecoverFromLatest)
{
    await checkpointCoordinator.RestoreFromLatestCheckpointAsync();
}

// 4. Execute dataflow (blocks start from restored state)
await dataFlow.ExecuteAsync();
```

#### 4. Error Handling

**Checkpoint Creation Failures**:
- Log error but continue execution
- Don't propagate to epoch completion
- Emit telemetry for monitoring
- Next checkpoint opportunity will retry

**Recovery Failures**:
- Missing checkpoint: Start from beginning (or fail if required)
- Corrupt checkpoint: Fall back to previous checkpoint or start fresh
- Partial restoration: Log warnings, continue with restored blocks

#### 5. Telemetry and Monitoring

Add observability for:
- Checkpoint creation success/failure rates
- Checkpoint size (bytes)
- Checkpoint creation duration
- Recovery success/failure
- Recovery duration
- Blocks restored vs. blocks registered

#### 6. Configuration Options

```csharp
.ConfigureCheckpointing(config =>
{
    config.UseStore(store);
    config.UseStrategy(strategy);
    config.SetRetentionPolicy(new KeepLastNCheckpoints(10));
    config.EnableTelemetry();
    config.SetFailureMode(CheckpointFailureMode.LogAndContinue);
});
```

---

## Performance Considerations

### Checkpoint Creation

**Measured Overhead**:
- Block state serialization: < 0.1ms per block (< 1KB state)
- Coordinator coordination: < 0.1ms
- In-memory store: < 0.1ms
- **Total**: < 1ms for typical checkpoint (5-10 blocks)

**Compared to Epoch Completion**:
- Epoch commit: ~10-100ms (database transaction)
- Checkpoint creation: < 1ms
- **Overhead**: < 1% of epoch completion time

### Recovery Initialization

**Measured Duration**:
- Load checkpoint from store: < 1ms (in-memory)
- Deserialize block states: < 0.1ms per block
- Call `RestoreFromCheckpointAsync`: < 1ms per block
- **Total**: < 10ms for typical recovery (5-10 blocks)

### Memory Usage

**Per Checkpoint**:
- Metadata: ~200 bytes
- Block states: Depends on blocks (typically < 1KB each)
- **Typical Total**: < 10KB per checkpoint

**With Retention Policy** (keep last 10):
- Memory usage: < 100KB
- Negligible compared to dataflow memory usage

---

## Known Limitations

### 1. No Cross-Process Coordination

Checkpoints are local to the process. If multiple processes are running the same dataflow, they each maintain separate checkpoints.

**Mitigation**: Use distributed checkpoint store (database, blob storage) with process ID in checkpoint key.

### 2. Block ID Management

Currently requires manual block ID assignment. Risk of typos or inconsistencies.

**Mitigation**: Consider builder-assigned IDs in production implementation.

### 3. Schema Evolution

No built-in support for checkpoint schema evolution. If block state format changes, old checkpoints may fail to restore.

**Mitigation**: 
- Version block state format
- Gracefully handle restore failures
- Document state format in block documentation

### 4. Large State

If blocks save large state (MBs), checkpoint creation and storage could become expensive.

**Mitigation**:
- Guidelines: Save pointers/offsets, not working sets
- If large state needed, use external storage + reference
- Consider compression for large states

---

## Example Usage

### Complete Example: Message Queue Processing

```csharp
// 1. Define checkpoint-aware source
public class MessageQueueSource : IProducer<Message>, ICheckpointAware
{
    private long _currentOffset = 0;
    
    public async IAsyncEnumerable<Message> ProduceAsync(...)
    {
        while (_currentOffset < _totalMessages)
        {
            yield return await _queue.ReadMessageAsync(_currentOffset);
            _currentOffset++;
        }
    }
    
    public Task<byte[]?> CreateCheckpointAsync(...)
    {
        return Task.FromResult(Encoding.UTF8.GetBytes(_currentOffset.ToString()));
    }
    
    public Task RestoreFromCheckpointAsync(string id, byte[] state, ...)
    {
        _currentOffset = long.Parse(Encoding.UTF8.GetString(state));
        return Task.CompletedTask;
    }
}

// 2. Configure dataflow with checkpointing
var store = new InMemoryCheckpointStore();
var strategy = new EveryNEpochsStrategy(10); // Every 10 epochs
var checkpointCoordinator = new CheckpointCoordinator(store, strategy);

var source = new MessageQueueSource();
checkpointCoordinator.RegisterBlock("queue-source", source);

var hooks = new EpochHooks
{
    OnCommitEpoch = async (epoch, ct) =>
    {
        await epoch.QueueSerializedOperationAsync<DbContext>(async db =>
        {
            await db.SaveChangesAsync(ct);
            await db.Database.CommitTransactionAsync(ct);
        }, ct);
        
        await checkpointCoordinator.OnEpochCompletedAsync(epoch, ct);
    }
};

// 3. Recovery on restart
await checkpointCoordinator.RestoreFromLatestCheckpointAsync();

// 4. Execute dataflow (continues from checkpoint position)
await dataFlow.ExecuteAsync();
```

---

## References

- **Prototype**: `/research/checkpointing-recovery/handover/prototype/`
  - Core interfaces and implementations
  - Example checkpoint-aware blocks
  - Comprehensive test suite
  
- **Design Documentation**: `/research/checkpointing-recovery/design/checkpoint-architecture.md`
  - Detailed architecture breakdown
  - Integration patterns
  - Concurrency considerations

- **Implementation Notes**: `/research/checkpointing-recovery/notes/implementation-notes.md`
  - Key findings and decisions
  - Open questions
  - Performance measurements

- **Epoch System**: `/poc/DataFlow.POC/Core/`
  - `IEpoch.cs` - Epoch interface
  - `EpochCoordinator.cs` - Epoch coordination
  - `EpochProcessorNode.cs` - Epoch lifecycle
  - `EpochHooks.cs` - Lifecycle hooks

---

## Next Steps

### For Research

- [x] Document findings in research README (this document)
- [ ] Create ADR for checkpoint design
- [ ] Create implementation handover work item
- [ ] Revert prototype code (after reviewer approval)
- [ ] Submit self-improvement feedback

### For Implementation

See [Implementation Guidance](#implementation-guidance) section above for detailed steps.

**Priority Order**:
1. Integrate checkpoint coordinator with epoch processor
2. Add DataFlow builder configuration API
3. Implement file system checkpoint store
4. Add telemetry and monitoring
5. Add retention policy management
6. Implement database checkpoint store (production)

---

## Conclusion

The checkpointing mechanism design is validated and ready for implementation. The approach leverages the existing epoch system naturally, provides flexible storage options, and has negligible performance overhead. The prototype demonstrates successful checkpoint creation and recovery across multiple scenarios.

**Recommendation**: Proceed with implementation following the guidance in this document.
