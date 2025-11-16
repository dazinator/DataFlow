# Implementation Handover: Checkpointing for Point-in-Time Recovery

**Issue Type**: Implementation  
**Priority**: Medium  
**Estimated Effort**: 3-5 days  
**Research Reference**: `/research/checkpointing-recovery/`

---

## Overview

Implement a checkpointing mechanism that enables point-in-time recovery in the DataFlow runtime. The design has been validated through research and prototyping.

**Research Outcome**: ✅ Design validated, ready for implementation  
**Prototype Location**: `/research/checkpointing-recovery/handover/prototype/`

---

## Acceptance Criteria

- [ ] Core checkpoint interfaces implemented in `/poc/DataFlow.POC/Checkpointing/`
- [ ] Checkpoint coordinator integrated with epoch processor
- [ ] DataFlow builder supports checkpoint configuration
- [ ] File system checkpoint store implemented
- [ ] Comprehensive unit tests for all components
- [ ] Integration tests validating end-to-end recovery
- [ ] Documentation updated with checkpoint usage examples
- [ ] Performance benchmarks show < 5% overhead

---

## Implementation Tasks

### Phase 1: Core Infrastructure (Day 1-2)

#### 1.1 Create Checkpoint Types

**Location**: `/poc/DataFlow.POC/Checkpointing/`

**Files to Create**:
- `ICheckpoint.cs` - Checkpoint interface and related abstractions
- `Checkpoint.cs` - Concrete checkpoint implementation
- `ICheckpointAware.cs` - Interface for checkpoint-aware blocks
- `ICheckpointStrategy.cs` - Strategy interface and implementations
- `ICheckpointStore.cs` - Storage abstraction

**Reference**: `/research/checkpointing-recovery/handover/prototype/ICheckpoint.cs`

#### 1.2 Implement Checkpoint Coordinator

**Location**: `/poc/DataFlow.POC/Checkpointing/CheckpointCoordinator.cs`

**Responsibilities**:
- Register checkpoint-aware blocks
- Coordinate checkpoint creation during epoch lifecycle
- Handle checkpoint failures gracefully (best-effort)
- Orchestrate recovery from checkpoint

**Reference**: `/research/checkpointing-recovery/handover/prototype/CheckpointCoordinator.cs`

#### 1.3 Implement Checkpoint Strategies

**Location**: `/poc/DataFlow.POC/Checkpointing/Strategies/`

**Implementations**:
- `EveryNEpochsStrategy` - Checkpoint every N epochs
- `TimeBasedStrategy` - Checkpoint at time intervals
- Allow custom strategies via interface

**Reference**: `/research/checkpointing-recovery/handover/prototype/CheckpointImplementations.cs`

### Phase 2: Storage Implementations (Day 2-3)

#### 2.1 In-Memory Checkpoint Store

**Location**: `/poc/DataFlow.POC/Checkpointing/Stores/InMemoryCheckpointStore.cs`

**Purpose**: Testing and development

**Reference**: `/research/checkpointing-recovery/handover/prototype/CheckpointImplementations.cs`

#### 2.2 File System Checkpoint Store

**Location**: `/poc/DataFlow.POC/Checkpointing/Stores/FileSystemCheckpointStore.cs`

**Requirements**:
- Serialize checkpoints to JSON files
- One file per checkpoint: `/checkpoints/checkpoint-{id}.json`
- Atomic writes (write to temp file, then rename)
- List checkpoints ordered by timestamp
- Clean up old checkpoints based on retention policy

**Format**:
```json
{
  "checkpointId": "checkpoint-{vector}-{ticks}",
  "epochVector": { "source1": 10, "source2": 5 },
  "timestamp": "2025-11-16T12:00:00Z",
  "blockStates": {
    "source": "base64-encoded-state",
    "processor": "base64-encoded-state"
  }
}
```

### Phase 3: Integration (Day 3-4)

#### 3.1 Integrate with Epoch Processor

**Location**: `/poc/DataFlow.POC/Core/EpochProcessorNode.cs`

**Changes**:
- Add optional checkpoint coordinator parameter
- Call `OnEpochCompletedAsync` in `OnCommitEpoch` hook (after transaction commits)
- Ensure checkpoint failures don't propagate to epoch processing

**Example**:
```csharp
public EpochProcessorNode(
    EpochSourceNode source,
    EpochHooks? hooks = null,
    CheckpointCoordinator? checkpointCoordinator = null)
{
    // ...
}

// In ProcessEpochAsync, after OnCommitEpoch:
if (_checkpointCoordinator != null)
{
    try
    {
        await _checkpointCoordinator.OnEpochCompletedAsync(epoch, cancellationToken);
    }
    catch (Exception ex)
    {
        // Log but don't propagate
        _logger?.LogWarning(ex, "Checkpoint creation failed");
    }
}
```

#### 3.2 Add DataFlow Builder Configuration

**Location**: `/poc/DataFlow.POC/Builder/` (new file)

**Add Extension Methods**:
```csharp
public static class CheckpointingExtensions
{
    public static DataFlowBuilder ConfigureCheckpointing(
        this DataFlowBuilder builder,
        Action<CheckpointConfiguration> configure)
    {
        // Configure checkpoint coordinator
        // Register checkpoint-aware blocks
        return builder;
    }
    
    public static DataFlowBuilder ConfigureRecovery(
        this DataFlowBuilder builder,
        Action<RecoveryConfiguration> configure)
    {
        // Configure recovery options
        return builder;
    }
}
```

#### 3.3 Add Recovery Initialization

**Location**: `/poc/DataFlow.POC/Builder/DataFlowBuilder.cs` (or similar)

**Changes**:
- Before execution, check if recovery is configured
- If so, load checkpoint and restore blocks
- Document recovery flow

### Phase 4: Testing (Day 4-5)

#### 4.1 Unit Tests

**Location**: `/poc/DataFlow.POC.Tests/Checkpointing/`

**Test Files**:
- `CheckpointTests.cs` - Core checkpoint functionality
- `CheckpointCoordinatorTests.cs` - Coordinator behavior
- `CheckpointStrategyTests.cs` - Strategy implementations
- `CheckpointStoreTests.cs` - Storage implementations

**Reference**: `/research/checkpointing-recovery/handover/prototype/CheckpointTests.cs`

**Coverage**:
- Checkpoint creation and retrieval
- Strategy trigger logic
- Block state serialization/deserialization
- Coordinator registration and coordination
- Graceful error handling
- Recovery from checkpoint

#### 4.2 Integration Tests

**Location**: `/poc/DataFlow.POC.Tests/Checkpointing/CheckpointIntegrationTests.cs`

**Reference**: `/research/checkpointing-recovery/handover/prototype/CheckpointIntegrationTests.cs`

**Scenarios**:
- End-to-end checkpoint and recovery
- Multiple checkpoints with latest restoration
- Recovery with missing blocks
- Checkpoint with no blocks
- Specific checkpoint restoration

#### 4.3 Performance Benchmarks

**Location**: `/poc/DataFlow.POC.Benchmarks/CheckpointBenchmarks.cs`

**Measure**:
- Checkpoint creation overhead vs. epoch completion time
- Recovery initialization time
- Different checkpoint sizes (1, 10, 100 blocks)
- Different storage backends (memory, file system)

**Target**: < 5% overhead on epoch completion time

### Phase 5: Documentation (Day 5)

#### 5.1 API Documentation

Add XML documentation comments to all public types and methods.

#### 5.2 Usage Examples

**Location**: `/poc/DataFlow.POC/Examples/` or `/docs/examples/`

**Example**: Checkpoint-aware message queue source

**Reference**: `/research/checkpointing-recovery/handover/prototype/CheckpointAwareBlocks.cs`

#### 5.3 Update README

**Location**: `/poc/README.md`

Add section on checkpointing:
- What is checkpointing
- When to use it
- How to configure it
- Example usage
- Performance considerations

---

## Technical Specifications

### Checkpoint Data Model

```csharp
public interface ICheckpoint
{
    string CheckpointId { get; }
    EpochVector EpochVector { get; }
    DateTimeOffset Timestamp { get; }
    IReadOnlyDictionary<string, byte[]> BlockStates { get; }
}
```

### Checkpoint-Aware Block Interface

```csharp
public interface ICheckpointAware
{
    Task<byte[]?> CreateCheckpointAsync(
        string checkpointId,
        EpochVector epochVector,
        CancellationToken cancellationToken = default);
    
    Task RestoreFromCheckpointAsync(
        string checkpointId,
        byte[] state,
        CancellationToken cancellationToken = default);
}
```

### Integration with Epoch Hooks

```csharp
var checkpointCoordinator = new CheckpointCoordinator(store, strategy);
checkpointCoordinator.RegisterBlock("source", sourceBlock);

var hooks = new EpochHooks
{
    OnCommitEpoch = async (epoch, ct) =>
    {
        // 1. Commit transaction
        await epoch.QueueSerializedOperationAsync<DbContext>(async (db, context) =>
        {
            await db.SaveChangesAsync(ct);
            await db.Database.CommitTransactionAsync(ct);
        }, ct);
        
        // 2. Create checkpoint
        await checkpointCoordinator.OnEpochCompletedAsync(epoch, ct);
    }
};
```

---

## Design Decisions (from Research)

1. **Checkpoint Timing**: Create checkpoints AFTER epoch commit (not during)
2. **Data Model**: Container-based with `byte[]` values (not strongly-typed)
3. **Block Participation**: Optional via `ICheckpointAware` interface
4. **Persistence**: Abstract via `ICheckpointStore` for multiple backends
5. **Best-Effort**: Checkpoint failures don't propagate to execution
6. **Recovery**: Restore before dataflow execution begins

**Rationale**: See `/docs/adr/poc/2025-11-16-epoch-based-checkpointing.md`

---

## Performance Requirements

From research validation:

| Metric | Target | Research Result |
|--------|--------|-----------------|
| Checkpoint creation overhead | < 5% | < 1% ✅ |
| Recovery time | < 1 second | < 10ms ✅ |
| Memory per checkpoint | < 10KB | < 5KB ✅ |

Implementation should maintain these performance characteristics.

---

## Testing Requirements

### Unit Test Coverage

- [ ] All public methods have tests
- [ ] Error scenarios are covered
- [ ] Edge cases (empty checkpoints, missing blocks, etc.)
- [ ] Concurrent access scenarios

### Integration Test Coverage

- [ ] End-to-end checkpoint and recovery
- [ ] Multiple checkpoint retention
- [ ] Partial block restoration
- [ ] Checkpoint failure handling

### Performance Benchmarks

- [ ] Checkpoint creation overhead < 5%
- [ ] Recovery time < 1 second
- [ ] Scale to 100+ blocks

---

## References

### Research Documentation

- **Research README**: `/research/checkpointing-recovery/README.md`
- **Design Doc**: `/research/checkpointing-recovery/design/checkpoint-architecture.md`
- **Implementation Notes**: `/research/checkpointing-recovery/notes/implementation-notes.md`
- **ADR**: `/docs/adr/poc/2025-11-16-epoch-based-checkpointing.md`

### Prototype Code (for reference only, will be reverted)

- `/research/checkpointing-recovery/handover/prototype/ICheckpoint.cs`
- `/research/checkpointing-recovery/handover/prototype/CheckpointCoordinator.cs`
- `/research/checkpointing-recovery/handover/prototype/CheckpointImplementations.cs`
- `/research/checkpointing-recovery/handover/prototype/CheckpointAwareBlocks.cs`
- `/research/checkpointing-recovery/handover/prototype/CheckpointTests.cs`
- `/research/checkpointing-recovery/handover/prototype/CheckpointIntegrationTests.cs`

### Related Systems

- **Epoch System**: `/poc/DataFlow.POC/Core/`
  - `IEpoch.cs`
  - `EpochCoordinator.cs`
  - `EpochProcessorNode.cs`
  - `EpochHooks.cs`

---

## Open Questions for Implementation

1. **Block ID Assignment**: How should block IDs be managed in builder?
   - Option A: Manual string IDs
   - Option B: Builder auto-assigns based on registration order
   - **Recommendation**: Start with manual, consider auto-assignment in future

2. **Retention Policy**: Default checkpoint retention?
   - **Recommendation**: Keep last 10 checkpoints by default

3. **Telemetry**: What metrics should be emitted?
   - Checkpoint creation success/failure
   - Checkpoint size (bytes)
   - Recovery success/failure
   - **Recommendation**: Integrate with existing telemetry system

4. **Namespace**: Where should checkpoint types live?
   - Option A: `DataFlow.POC.Checkpointing`
   - Option B: `DataFlow.POC.Core.Checkpointing`
   - **Recommendation**: `DataFlow.POC.Checkpointing` (separate namespace)

---

## Success Criteria

Implementation is complete when:

- [ ] All code is in `/poc/DataFlow.POC/Checkpointing/`
- [ ] Integration with epoch processor works correctly
- [ ] Builder configuration API is intuitive and well-documented
- [ ] All unit tests pass (100% coverage of public API)
- [ ] All integration tests pass
- [ ] Performance benchmarks meet targets
- [ ] Documentation is complete and clear
- [ ] Example code demonstrates usage
- [ ] Code review approved
- [ ] CI/CD pipeline passes

---

## Notes

- Prototype code in `/research/checkpointing-recovery/handover/prototype/` is for reference only
- Research branch will be reverted after implementation handover
- Implementation should be in POC codebase (`/poc/DataFlow.POC/`)
- Follow existing code style and patterns
- Add comprehensive tests alongside implementation
