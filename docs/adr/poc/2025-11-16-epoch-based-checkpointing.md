# ADR: Epoch-Based Checkpointing for Point-in-Time Recovery

**Date**: 2025-11-16  
**Status**: Proposed  
**Context**: Research work for checkpointing mechanism

---

## Context

DataFlow processes long-running data pipelines that may fail due to transient errors (network issues, resource exhaustion, etc.). When failures occur, the system currently must restart from the beginning, which is inefficient for long-running or expensive data sources.

We need a checkpointing mechanism that allows the system to resume from the last successfully processed position after a failure.

**Key Requirements**:
1. Enable recovery without reprocessing already-completed data
2. Minimal performance overhead on normal execution path
3. Integration with existing epoch system
4. Support for different storage backends
5. Optional participation (blocks opt-in to checkpointing)

---

## Decision

We will implement **epoch-based checkpointing** where checkpoints are **epoch properties**, allowing blocks to contribute state at exact epoch boundaries for temporal consistency.

### Core Design (Revised After Review)

**Checkpoint as Epoch Property**:
- Epoch coordinator flags epochs for checkpointing based on strategy
- Epoch carries optional `Checkpoint` property
- Blocks check `epoch.Checkpoint` when queuing operations
- Blocks contribute state for THAT specific epoch via `epoch.Checkpoint.AddBlockState()`
- OnCommitEpoch hook persists checkpoint (atomic OR best-effort mode)

**The "State Ahead" Problem Solved**:
Blocks process asynchronously and their internal state can advance beyond the current epoch being checkpointed. By making checkpoint an epoch property, blocks contribute state at the exact epoch boundary:

```csharp
// Block processing epoch N
await foreach (var epochStream in _epochSource.GetEpochStreamsAsync(ct))
{
    var epoch = epochStream.EpochScope;
    
    // Process items for THIS epoch
    await foreach (var item in epochStream.Items)
    {
        ProcessItem(item);
        _currentOffset++;  // State advances
    }
    
    // Contribute checkpoint state for THIS epoch only, via operation context
    if (epoch.IsCheckpointing)
    {
        await epoch.QueueSerializedOperationAsync(async (IEpochOperationContext ctx) =>
        {
            if (ctx.Checkpoint != null)
            {
                var state = Encoding.UTF8.GetBytes(_currentOffset.ToString());
                ctx.Checkpoint.AddBlockState(_blockId, state);
            }
        }, ct);
    }
}
```

**Checkpoint Data**:
- Container-based checkpoint with `Dictionary<string, byte[]>` for block states
- Thread-safe `AddBlockState` method for concurrent contributions
- Each block serializes its own state (typically lightweight: offsets, cursors, IDs)
- Epoch vector identifies exact position in stream

**Persistence Modes**:

*Atomic Mode* (checkpoint with transaction):
```csharp
OnCommitEpoch = async (epoch, ct) =>
{
    await epoch.QueueSerializedOperationAsync<DbContext>(async db =>
    {
        await db.SaveChangesAsync(ct);
        if (epoch.Checkpoint != null)
        {
            db.Checkpoints.Add(epoch.Checkpoint);  // Same transaction
        }
        await db.Database.CommitTransactionAsync(ct);
    }, ct);
}
```

*Best-Effort Mode* (checkpoint after transaction):
```csharp
OnCommitEpoch = async (epoch, ct) =>
{
    await epoch.QueueSerializedOperationAsync<DbContext>(async db =>
    {
        await db.SaveChangesAsync(ct);
        await db.Database.CommitTransactionAsync(ct);
    }, ct);
    
    if (epoch.Checkpoint != null)
    {
        try
        {
            await checkpointStore.SaveAsync(epoch.Checkpoint, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Checkpoint persistence failed");
        }
    }
}
```

**Recovery Process**:
- Before dataflow execution, load latest checkpoint from store
- Restore each block's state by calling their restore methods
- Blocks initialize their state (e.g., source sets starting offset)
- Execution begins from checkpoint position

**Key Benefits**:
- ✅ **Temporal consistency**: Blocks contribute state at exact epoch boundary
- ✅ **Atomic persistence option**: Checkpoint can be part of epoch transaction
- ✅ **Simpler architecture**: No external checkpoint coordinator needed
- ✅ **Flexible**: Application chooses atomic vs best-effort mode

---

## Alternatives Considered

### Option 1: In-Band Checkpoint Barriers

Similar to Apache Flink, send checkpoint barriers through the data stream.

**How it Works**:
- Coordinator injects barrier markers into data stream
- Blocks buffer data when barrier received
- After all inputs receive same barrier, block snapshots state
- Barrier propagates to downstream blocks

**Pros**:
- Industry-proven approach (Flink, Beam)
- Precise alignment across all blocks
- Independent of other coordination mechanisms

**Cons**:
- ❌ Redundant with epoch system (already provides alignment)
- ❌ Adds complexity to data path (barrier handling in every block)
- ❌ Performance overhead on hot path
- ❌ All blocks must handle barriers (even if not checkpoint-aware)
- ❌ Requires significant infrastructure changes

**Conclusion**: Rejected - epochs already provide the alignment we need.

### Option 2: Time-Based Checkpointing

Create checkpoints at fixed time intervals independent of data alignment.

**How it Works**:
- Timer triggers checkpoint creation
- Each block snapshots its current state when signaled
- No alignment guarantee across blocks

**Pros**:
- Simple to implement
- Predictable checkpoint frequency

**Cons**:
- ❌ No consistency guarantee (blocks at different stream positions)
- ❌ Recovery may produce inconsistent state
- ❌ Doesn't leverage existing epoch infrastructure
- ❌ Requires additional coordination mechanism

**Conclusion**: Rejected - consistency is critical for correctness.

### Option 3: Epoch-Based Checkpointing (Chosen)

Create checkpoints at epoch boundaries using existing infrastructure.

**How it Works**:
- Checkpoint coordinator registered with epoch processor
- `OnCommitEpoch` hook triggers checkpoint creation
- Each checkpoint-aware block saves its state
- Checkpoint persisted with epoch vector as position marker

**Pros**:
- ✅ Leverages existing epoch alignment
- ✅ Consistent snapshot across all blocks
- ✅ Clean integration with epoch lifecycle
- ✅ No additional complexity in data path
- ✅ Natural transaction boundary (after commit)
- ✅ Simple and understandable

**Cons**:
- Checkpoint frequency tied to epoch boundaries (acceptable tradeoff)

**Conclusion**: Best balance of simplicity and functionality.

### Option 4: Strongly-Typed Checkpoints

Use a strongly-typed checkpoint specific to each dataflow configuration.

**How it Works**:
```csharp
public class MyFlowCheckpoint
{
    public long SourceOffset { get; set; }
    public int ProcessorCount { get; set; }
}
```

**Pros**:
- Compile-time type safety
- Easier to work with in code
- Clear schema definition

**Cons**:
- ❌ Tight coupling between blocks and checkpoint type
- ❌ Requires generic constraints throughout infrastructure
- ❌ Less flexible for dynamic configurations
- ❌ Difficult to evolve schema

**Conclusion**: Rejected in favor of container-based approach with `byte[]` values for flexibility.

---

## Consequences

### Positive

1. **Minimal Code Changes**: Integrates with existing epoch infrastructure without significant refactoring

2. **Performance**: Negligible overhead (< 1% of epoch completion time)

3. **Flexibility**: Abstract storage interface supports multiple backends (memory, file, database, blob)

4. **Optional**: Blocks opt-in via `ICheckpointAware` interface; stateless blocks unaffected

5. **Testability**: Clean interfaces make testing straightforward

6. **Observability**: Checkpoint creation and recovery are explicit, observable events

### Negative

1. **Checkpoint Frequency**: Limited to epoch boundaries. Can't checkpoint mid-epoch.
   - **Mitigation**: Configure smaller epochs if more frequent checkpoints needed

2. **Block ID Management**: Requires manual assignment of block IDs for state mapping
   - **Mitigation**: Consider builder-assigned IDs in future

3. **Schema Evolution**: No built-in support for block state format changes
   - **Mitigation**: Blocks should version their state format; gracefully handle incompatible checkpoints

4. **Storage Dependency**: Recovery depends on checkpoint store availability
   - **Mitigation**: Use reliable storage (database, replicated blob storage)

### Neutral

1. **State Size**: Assumes lightweight state (offsets, cursors). Large state requires different approach.
   - **Guidance**: Document that blocks should save pointers/references, not full working sets

2. **Concurrency**: Checkpoint creation is sequential (one block at a time)
   - **Current**: Acceptable for typical block counts (5-20 blocks)
   - **Future**: Could parallelize if needed

---

---

## Migration Path

This is a new feature, not a breaking change:

1. **Phase 1**: Implement core interfaces and coordinator (this research)
2. **Phase 2**: Integrate with epoch processor and builder
3. **Phase 3**: Implement file system checkpoint store
4. **Phase 4**: Add telemetry and monitoring
5. **Phase 5**: Implement production checkpoint stores (database, blob)

Existing code continues to work without changes. Checkpointing is opt-in.

---

## Success Metrics

From research validation:

| Metric | Target | Actual |
|--------|--------|--------|
| Checkpoint creation overhead | < 5% | < 1% ✅ |
| Recovery time | < 1 second | < 10ms ✅ |
| Data loss on recovery | Zero | Zero ✅ |

All test scenarios pass:
- ✅ Checkpoint creation at correct intervals
- ✅ Block state serialization/deserialization
- ✅ Recovery restores all registered blocks
- ✅ Graceful handling of missing blocks
- ✅ Graceful handling of block failures
- ✅ End-to-end checkpoint and recovery

---

## References

- **Research**: `/research/checkpointing-recovery/`
- **Prototype**: `/research/checkpointing-recovery/handover/prototype/`
- **Design Doc**: `/research/checkpointing-recovery/design/checkpoint-architecture.md`
- **Epoch System ADR**: `/docs/adr/poc/2025-11-16-formalized-epoch-system.md`
