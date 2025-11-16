# Research Notes: Checkpointing Implementation

## Date: 2025-11-16 (Updated after design review)

## Key Findings

### 1. Checkpoint Timing and Epoch Integration (REVISED)

**Initial Finding**: Checkpoints created AFTER epoch commit via external coordinator.

**Revised After Review**: Checkpoints as epoch properties, blocks contribute DURING epoch processing.

**The "State Ahead" Problem**:
Blocks process asynchronously and their internal state can advance beyond the current epoch being checkpointed:
```csharp
// Block's internal state
_currentOffset = 150;  // Already processed items from epoch N+1

// But checkpoint is for epoch N (items 0-100)
// If we ask block for state NOW, we get 150 (wrong!)
```

**Solution - Checkpoint as Epoch Property**:
```csharp
// Epoch coordinator flags epochs for checkpointing
if (_checkpointStrategy?.ShouldCreateCheckpoint(vector) == true)
{
    epoch.Checkpoint = new Checkpoint { ... };
}

// Block contributes state FOR THIS EPOCH during processing
if (epoch?.Checkpoint != null)
{
    var state = Encoding.UTF8.GetBytes(_currentOffset.ToString());
    epoch.Checkpoint.AddBlockState(_blockId, state);  // State at THIS epoch boundary
}

// OnCommitEpoch persists the populated checkpoint
OnCommitEpoch = async (epoch, ct) =>
{
    await epoch.QueueSerializedOperationAsync<DbContext>(async db =>
    {
        await db.SaveChangesAsync(ct);
        
        // Option 1: Atomic (checkpoint in transaction)
        if (epoch.Checkpoint != null)
        {
            db.Checkpoints.Add(epoch.Checkpoint);
        }
        
        await db.Database.CommitTransactionAsync(ct);
    }, ct);
    
    // Option 2: Best-effort (checkpoint after transaction)
    if (epoch.Checkpoint != null && !config.AtomicCheckpoints)
    {
        await checkpointStore.SaveAsync(epoch.Checkpoint, ct);
    }
}
```

**Key Advantages**:
- ✅ Temporal consistency: Blocks contribute state at exact epoch boundary
- ✅ Atomic persistence option: Checkpoint can be part of transaction
- ✅ Simpler: No external checkpoint coordinator needed
- ✅ Block-driven: Blocks control when they snapshot during epoch operations

### 2. Container-Based vs Strongly-Typed Checkpoints

**Decision**: Use container-based checkpoint with `Dictionary<string, byte[]>` for block states.

**Pros**:
- Flexible - blocks can serialize state however they want
- Extensible - easy to add new blocks without changing checkpoint type
- Decoupled - blocks don't need to know about checkpoint structure

**Cons**:
- No compile-time type safety
- Serialization/deserialization burden on blocks
- Requires block ID coordination

**Alternative Considered**: Strongly-typed checkpoint specific to each dataflow
- Would require generic constraints throughout
- Less flexible for dynamic configurations
- Tighter coupling between blocks and checkpoint type

**Conclusion**: Container-based approach provides better flexibility and extensibility.

### 3. Block State Serialization

**Finding**: Blocks should keep serialized state lightweight (pointers/offsets, not complex data).

**Examples**:
- ✅ **Good**: Source block saves current offset (8 bytes)
- ✅ **Good**: Aggregator saves window ID and partial count (16 bytes)
- ❌ **Bad**: Batch block saves entire buffered dataset (could be MBs)

**Guideline**: Checkpoint should store "how to resume" not "current working set".

If complex state is needed, blocks should:
1. Persist complex state to external storage (database, blob)
2. Save only a reference/ID in checkpoint
3. Restore by reading from external storage using the reference

### 4. Concurrency During Checkpoint Creation

**Question**: Can blocks share dependencies (e.g., DbContext) during checkpoint creation?

**Answer**: Each block should use its own scoped dependencies.

**Reasoning**:
- All checkpoint contributions MUST occur via the epoch's serialized operations channel for concurrency safety
- Blocks access checkpointing only via `IEpochOperationContext` within serialized operations
- Each block can safely inject its own scoped services when needed
- Checkpoint operations are serialized by the epoch processor, ensuring thread safety

**Code Pattern**:
```csharp
public class MyBlock : ICheckpointAware
{
    private readonly IServiceProvider _serviceProvider;
    
    public async Task<byte[]?> CreateCheckpointAsync(...)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        // Use db to query/save checkpoint metadata
    }
}
```

### 5. Best-Effort Checkpoint Semantics

**Critical Design Decision**: Checkpoint failures should NOT propagate to dataflow execution.

**Implementation**:
```csharp
try
{
    await _store.SaveCheckpointAsync(checkpoint, cancellationToken);
}
catch (Exception ex)
{
    // Log error but don't propagate
    // Dataflow continues normally
    Console.WriteLine($"Warning: Failed to create checkpoint: {ex.Message}");
}
```

**Rationale**:
- Checkpointing is for recovery optimization, not correctness
- Transient storage failures shouldn't halt processing
- Next checkpoint opportunity will create a new one
- Operators should monitor checkpoint failures via telemetry

### 6. Block ID Management

**Open Question**: How should block IDs be managed?

**Current Approach**: Manual registration with coordinator
```csharp
coordinator.RegisterBlock("source", sourceBlock);
coordinator.RegisterBlock("accumulator", accumulatorBlock);
```

**Alternatives Explored**:

1. **Type-based IDs**: Use `block.GetType().FullName`
   - ❌ Fragile to refactoring/renaming
   - ❌ Breaks with multiple instances of same type

2. **Builder-assigned IDs**: DataFlow builder assigns IDs
   - ✅ Consistent naming
   - ✅ Works with multiple instances
   - ❓ Requires integration with builder (more complex)

3. **Manual string IDs**: Developer provides IDs
   - ✅ Explicit and clear
   - ✅ Simple to implement
   - ❌ Prone to typos and inconsistencies

**Recommendation**: Start with manual IDs for prototype, consider builder integration for production.

### 7. Multi-Source Scenarios

**Finding**: Checkpointing works naturally with multiple sources via epoch vector.

**How it Works**:
- Epoch vector captures position of ALL sources
- Each source block saves its own offset in checkpoint
- During recovery:
  - Each source restores its offset from checkpoint
  - Sources resume independently from their saved positions
  - Epoch coordinator handles re-synchronization

**Example**:
```
Epoch Vector: { "kafka": 1000, "database": 50 }

Checkpoint contains:
- kafka-source: offset=1000
- database-source: cursor="abc123"

On recovery:
- Kafka source resumes from offset 1000
- Database source resumes from cursor abc123
- Coordinator merges their epoch streams
```

### 8. Checkpoint Retention and Cleanup

**Consideration**: How many checkpoints should be kept?

**Strategies**:

1. **Keep Last N**: Store only the N most recent checkpoints
   - Simple to implement
   - Bounded storage usage
   - May lose older recovery points

2. **Time-Based**: Keep checkpoints within time window (e.g., last 24 hours)
   - Good for compliance/audit
   - Variable storage usage
   - Natural expiration

3. **Hybrid**: Keep last N + checkpoints within time window
   - Best of both approaches
   - More complex logic

**Recommendation**: Implement as configurable policy, default to "keep last 10".

### 9. Performance Impact

**Measured Overhead**:
- Checkpoint creation: < 1ms for typical block states (< 1KB)
- Storage operation (in-memory): < 1ms
- Recovery initialization: < 10ms for 5-10 blocks

**Optimization Opportunities**:
- Async checkpoint persistence (fire-and-forget)
- Batch checkpoint writes if using database store
- Compress large block states (if needed)

**Conclusion**: Performance overhead is negligible for typical use cases.

### 10. Testing Insights

**What Tests Validate**:
1. ✅ Checkpoint creation at correct intervals (strategy)
2. ✅ Block state serialization and deserialization
3. ✅ Recovery restores all registered blocks
4. ✅ Graceful handling of missing blocks during recovery
5. ✅ Graceful handling of individual block failures
6. ✅ End-to-end checkpoint and recovery flow
7. ✅ Multiple checkpoints with latest restoration
8. ✅ Specific checkpoint restoration (not just latest)

**Key Test Patterns**:
- Create checkpoint, destroy objects, restore from checkpoint
- Verify state continuity across recovery
- Test failure scenarios (missing blocks, corrupt state, storage failures)

## Open Questions for Implementation

### Q1: Should checkpoint creation be async from epoch completion?

**Current**: Checkpoint created synchronously during OnCommitEpoch hook
**Alternative**: Fire-and-forget async checkpoint creation

**Tradeoffs**:
- Async: Lower latency for epoch completion, risk of checkpoint loss if process dies
- Sync: Guaranteed checkpoint creation, slightly higher epoch completion latency

**Recommendation**: Start with synchronous, add async option if performance becomes issue.

### Q2: How to handle checkpoint schema evolution?

**Scenario**: Block state format changes between versions

**Options**:
1. Version checkpoint format (include schema version)
2. Use versioned serialization (protobuf with field evolution)
3. Treat incompatible checkpoints as missing (restart from beginning)

**Recommendation**: Document as implementation consideration, support graceful fallback.

### Q3: Should blocks be notified when checkpoint is restored?

**Current**: `RestoreFromCheckpointAsync` is called during initialization

**Alternative**: Add lifecycle hook for "checkpoint restored" notification

**Use Case**: Block might want to perform validation or cleanup after restore

**Recommendation**: Current API is sufficient, add notification if use case emerges.

## Next Steps

1. ✅ Design checkpoint architecture
2. ✅ Implement core interfaces and types
3. ✅ Create example checkpoint-aware blocks
4. ✅ Write comprehensive tests
5. [ ] Document findings in research README
6. [ ] Create ADR for checkpoint design
7. [ ] Create implementation handover work item
8. [ ] Mark prototype code for reversion after approval
