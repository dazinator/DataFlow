# Research Plan: Checkpointing for Point-in-Time Recovery

## Research Objective

Design and validate a lightweight checkpointing mechanism that enables point-in-time recovery in the DataFlow runtime, building on the existing epoch system.

## Research Questions

1. **Checkpoint Data Model**: What data should a checkpoint contain?
   - Block state (offsets, positions, etc.)
   - Epoch vector to identify checkpoint position
   - Metadata (timestamp, checkpoint ID)
   - Should checkpoint be a container object or strongly-typed?

2. **Checkpoint Timing**: When should checkpoints be created?
   - Strategy pattern for checkpoint triggers (every N epochs, time-based, etc.)
   - Integration with epoch completion lifecycle
   - Alignment guarantees across all blocks

3. **Block Participation**: How do blocks contribute to and restore from checkpoints?
   - Interface for checkpoint-aware blocks
   - Pattern for blocks to save state (e.g., current offset)
   - Pattern for blocks to restore state during recovery
   - Concurrency safety during checkpoint creation

4. **Persistence Strategy**: How are checkpoints persisted?
   - Abstract persistence interface (ICheckpointStore)
   - Support for different backends (database, file, memory)
   - Best-effort persistence semantics
   - Checkpoint ID generation and tracking

5. **Recovery Mechanism**: How does recovery work?
   - API for resuming from checkpoint
   - Block initialization from checkpoint data
   - Source block restart from saved offset
   - Handling missing or corrupted checkpoints

6. **Concurrency & Safety**: How to handle concurrent operations?
   - Can blocks share dependencies (e.g., DbContext) during checkpoint creation?
   - Is checkpoint creation transactional like epochs?
   - Thread-safety guarantees

## Success Metrics

- **Quantitative**: 
  - Checkpoint creation overhead < 5% of epoch completion time
  - Recovery successfully resumes from checkpoint in < 1 second
  - Zero data loss when recovering from valid checkpoint

- **Qualitative**: 
  - Clean, intuitive API for checkpoint-aware blocks
  - Minimal coupling between blocks and checkpoint types
  - Natural integration with existing epoch lifecycle

- **Baseline**: 
  - Current system: no checkpoint capability, failures require full restart
  - Epoch system provides natural alignment boundaries

- **Validation**: 
  - Prototype demonstrates successful checkpoint creation
  - Prototype demonstrates successful recovery from checkpoint
  - Unit tests validate checkpoint creation and restoration

## Validation Approach

### Phase 1: Design & Architecture (2-3 days)
- Study existing epoch lifecycle and coordination
- Design checkpoint abstractions and interfaces
- Analyze checkpoint timing strategies
- Document design decisions in ADR

### Phase 2: Prototype Implementation (3-5 days)
- Implement core checkpoint types (`ICheckpoint`, `CheckpointMetadata`)
- Implement checkpoint coordinator integrated with epoch lifecycle
- Create example checkpoint-aware blocks
- Implement in-memory checkpoint store for testing
- Add recovery initialization logic

### Phase 3: Validation & Documentation (2 days)
- Write unit tests for checkpoint creation and recovery
- Validate performance impact
- Document findings in research README
- Create implementation handover work item

## Expected Outcomes

- Research documentation in `/research/checkpointing-recovery/`
- Implementation-ready work item with full specifications
- ADR documenting checkpoint design decisions
- Prototype code demonstrating feasibility (to be reverted after approval)
- Test scenarios validating checkpoint and recovery functionality

## Timeline

Estimated research duration: 5-10 days

## Key Design Constraints

1. **Build on Epochs**: Checkpoints must leverage the existing epoch system for alignment
2. **Lightweight**: Minimize overhead on hot path (epoch completion)
3. **Flexible Persistence**: Support different storage backends via abstraction
4. **Optional Participation**: Blocks that don't need checkpointing shouldn't be forced to participate
5. **Recovery-Friendly**: Clear API for resuming execution from checkpoint
