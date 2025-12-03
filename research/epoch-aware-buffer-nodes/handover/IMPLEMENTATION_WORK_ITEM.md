# Implementation Work Item: Epoch-Aware Buffer Nodes

**⚠️ This is an implementation work item**

**Research Complete**: Yes  
**Research Location**: `/research/epoch-aware-buffer-nodes/`  
**Handover Document**: `/research/epoch-aware-buffer-nodes/handover/README.md`

## Summary

Implement epoch-aware buffer blocks based on completed research. The design has been validated through prototypes and is ready for production implementation.

## Background

Buffer nodes were designed before the epoch-only architecture and are not compatible with epoch streams. Research has determined the best approach is:

1. Keep existing `BufferNode<T>` for plain types only (document limitation)
2. Implement new `EpochBufferBlock<T>` for epoch streams (new capability)

This approach provides:
- ✅ No breaking changes
- ✅ Clear separation of concerns
- ✅ Optimized routing for epoch streams
- ✅ Natural epoch boundary preservation

## Scope

### In Scope
- Update `BufferNode<T>` documentation (plain types only)
- Optional: Add runtime validation to reject epoch streams
- Implement `EpochBufferBlock<T>` and `IBufferConfiguration`
- Implement graph builder extensions (`AddEpochBuffer<T>()`)
- Comprehensive test suite
- Documentation and examples

### Out of Scope
- Changes to existing `BufferNode<T>` implementation
- Changes to routing system (already supports the pattern)
- Performance optimizations beyond basic implementation
- Migration tools (not needed - additive change)

## Implementation Phases

### Phase 1: Update Buffer Node Documentation (2 hours)

**Goal**: Document that `BufferNode<T>` is for plain types only.

**Tasks**:
- [ ] Update XML documentation in `BufferNode.cs`
  - Add "Supported Use Cases" section
  - Add "Not Supported" section with pointer to `EpochBufferBlock`
- [ ] Optional: Add runtime validation
  - Detect `IEpochStream<T>` in constructor
  - Throw `ArgumentException` with helpful message
  - Add test for validation

**Files**:
- `/poc/DataFlow.POC/Core/BufferNode.cs`
- `/poc/DataFlow.POC.Tests/BufferNodeTests.cs` (if validation added)

**Acceptance Criteria**:
- Documentation clearly states supported types
- If validation added, test verifies exception thrown
- Existing tests still pass

### Phase 2: Implement EpochBufferBlock (6 hours)

**Goal**: Create production-ready epoch buffer block.

**Tasks**:
- [ ] Create `EpochBufferBlock<T>` class
  - Extend `BlockBase<IEpochStream<T>, IEpochStream<T>>`
  - Implement per-epoch buffering
  - Handle epoch metadata preservation
  - Add comprehensive XML documentation
- [ ] Create `IBufferConfiguration` interface
  - Define capacity property
  - Create default implementation
- [ ] Create graph builder extensions
  - `AddEpochBuffer<T>(name, capacity)` method
  - Optional overload with custom configuration
  - Follow existing extension pattern

**Files**:
- `/poc/DataFlow.POC/Core/EpochBufferBlock.cs` (new)
- `/poc/DataFlow.POC/Builders/EpochBufferBlockExtensions.cs` (new)

**Reference**: See prototype in `/research/epoch-aware-buffer-nodes/prototypes/`

**Acceptance Criteria**:
- Code compiles without warnings
- Follows existing code style and patterns
- XML documentation complete
- No changes to existing files (except new extensions)

### Phase 3: Comprehensive Testing (8 hours)

**Goal**: Ensure EpochBufferBlock works correctly in all scenarios.

**Tasks**:
- [ ] Create test file `EpochBufferBlockTests.cs`
- [ ] Implement basic functionality tests
  - Single epoch buffering
  - Multiple epochs in sequence
  - Epoch boundary preservation
- [ ] Implement metadata tests
  - Epoch vector preservation
  - Epoch scope preservation
- [ ] Implement backpressure tests
  - Buffer capacity enforcement
  - Blocking behavior when full
- [ ] Implement topology tests
  - Multiple producers (fan-in)
  - Multiple consumers (fan-out)
  - Different edge strategies
- [ ] Implement error handling tests
  - Cancellation during buffering
  - Exception propagation
- [ ] Optional: Performance tests
  - Overhead measurement
  - Comparison with direct connection

**Files**:
- `/poc/DataFlow.POC.Tests/EpochBufferBlockTests.cs` (new)

**Reference**: See test prototype in `/research/epoch-aware-buffer-nodes/prototypes/EpochBufferBlockTests.cs`

**Acceptance Criteria**:
- Test coverage >90% for `EpochBufferBlock`
- All tests pass
- Tests follow xUnit patterns
- Tests have clear names and documentation

### Phase 4: Documentation (6 hours)

**Goal**: Provide clear user guidance.

**Tasks**:
- [ ] Create architecture documentation
  - Explain two buffer types
  - When to use each
  - Design rationale
- [ ] Update user guides
  - Add epoch buffering examples
  - Update plain buffering docs (if needed)
- [ ] Add code examples
  - Plain flow with BufferNode
  - Epoch flow with EpochBufferBlock
  - Multiple producers/consumers
- [ ] Review all XML documentation
  - Ensure completeness
  - Ensure clarity
  - Add usage examples where helpful

**Files**:
- `/docs/architecture/buffer-nodes.md` (new)
- Relevant user guide files (update)
- Code XML documentation (review)

**Acceptance Criteria**:
- Documentation clearly explains when to use each buffer type
- Examples compile and run
- All public APIs documented
- Documentation reviewed

## Dependencies

### Completed Dependencies
- ✅ Issue #39: Epoch stream routing optimization
- ✅ Unified epoch model implementation
- ✅ Research phase complete

### Current Dependencies
- `IEpochStream<T>` interface exists
- `ChannelBackedEpochStream<T>` exists
- `BlockBase<TIn, TOut>` exists
- `IBlockContext` exists
- Graph builder extension pattern exists

## Implementation Notes

### Key Design Decisions (From Research)

1. **Per-Epoch Buffering**: Each epoch gets its own channel
   - Preserves boundaries naturally
   - Simple implementation
   - Predictable behavior

2. **Strict Boundary Preservation**: Process one epoch at a time
   - Clear semantics
   - May optimize in future if needed

3. **Standard Block Pattern**: Extends `BlockBase`
   - Uses optimized routing
   - Integrates naturally
   - Supports DI

4. **Capacity Semantics**: Per-epoch capacity
   - Simple and predictable
   - Matches user expectations

### Code Style Guidelines

- Follow existing DataFlow.POC patterns
- Use nullable reference types appropriately
- Add `ArgumentNullException.ThrowIfNull()` for validation
- Use `ConfigureAwait(false)` for async calls
- Comprehensive XML documentation
- Clear variable names

### Testing Guidelines

- Use xUnit framework
- Follow Arrange-Act-Assert pattern
- Clear test names: `Method_Should_Behavior_When_Condition`
- Use `ITestOutputHelper` for diagnostic output
- Async tests for async methods
- Test both success and failure paths

## Acceptance Criteria

- [ ] All implementation phases complete
- [ ] All tests pass (existing + new)
- [ ] Code review approved
- [ ] Documentation complete
- [ ] No breaking changes
- [ ] Performance acceptable (<10% overhead)
- [ ] Self-improvement feedback submitted

## Effort Estimate

| Phase | Estimate | Risk |
|-------|----------|------|
| Phase 1: Buffer Node Docs | 2 hours | Minimal |
| Phase 2: EpochBufferBlock | 6 hours | Low |
| Phase 3: Testing | 8 hours | Low |
| Phase 4: Documentation | 6 hours | Minimal |
| **Total** | **22 hours (~3 days)** | **Low** |

## Success Metrics

- All existing tests pass (no regressions)
- New tests provide >90% coverage
- Performance overhead <10% vs direct connection
- Documentation reviewed and approved
- No compiler warnings
- Code review approved

## References

- **Research**: `/research/epoch-aware-buffer-nodes/`
- **Handover**: `/research/epoch-aware-buffer-nodes/handover/README.md`
- **Recommendation**: `/research/epoch-aware-buffer-nodes/RECOMMENDATION.md`
- **Prototypes**: `/research/epoch-aware-buffer-nodes/prototypes/`
- **Original Issue**: Tech Debt: Design epoch-aware buffer nodes
- **Issue #39**: Epoch stream routing optimization

## Questions?

Refer to handover document or research artifacts. All design decisions have been validated through research and prototyping.
