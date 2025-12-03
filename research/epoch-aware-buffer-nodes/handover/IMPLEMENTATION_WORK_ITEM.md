# Implementation Work Item: Epoch-Aware Buffer Nodes

**⚠️ This is an implementation work item**

**Research Complete**: Yes  
**Research Location**: `/research/epoch-aware-buffer-nodes/`  
**Handover Document**: `/research/epoch-aware-buffer-nodes/handover/README.md`

## Summary

Implement epoch-aware buffer blocks based on completed research. The design has been validated through prototypes and is ready for production implementation.

## Background

Buffer nodes were designed before the epoch-only architecture and are not compatible with epoch streams. Research has determined the best approach is:

1. Mark existing `BufferNode<T>` as obsolete with clear migration path
2. Implement new `EpochBufferBlock<T>` for epoch streams (replacement)

This approach provides:
- ✅ Clear deprecation signal via `[Obsolete]` attribute
- ✅ Clear migration path to `EpochBufferBlock<T>`
- ✅ Optimized routing for epoch streams
- ✅ Natural epoch boundary preservation

**Note**: `BufferNode<T>` will be completely removed in Issue #55 after `EpochBufferBlock<T>` is stable.

## Scope

### In Scope
- Mark `BufferNode<T>` as obsolete with `[Obsolete]` attribute
- Add runtime validation to reject epoch streams in `BufferNode<T>`
- Implement `EpochBufferBlock<T>` and `IBufferConfiguration`
- Implement graph builder extensions (`AddEpochBuffer<T>()`)
- Comprehensive test suite
- Documentation and examples

### Out of Scope
- Removal of `BufferNode<T>` code (handled in Issue #55)
- Changes to routing system (already supports the pattern)
- Performance optimizations beyond basic implementation
- Migration of existing `BufferNode<T>` usage (handled in Issue #55)

## Implementation Phases

### Phase 1: Mark Buffer Node as Obsolete (2 hours)

**Goal**: Mark `BufferNode<T>` as obsolete and document that it will be removed.

**Tasks**:
- [ ] Add `[Obsolete]` attribute to `BufferNode<T>` class
  - Use message: "BufferNode<T> is obsolete and will be removed. Use EpochBufferBlock<T> for epoch stream buffering. See issue #55 for migration details."
  - Set `error: false` to allow compilation with warnings
- [ ] Update XML documentation in `BufferNode.cs`
  - Add obsolescence notice
  - Add pointer to `EpochBufferBlock<T>` as replacement
- [ ] Add runtime validation
  - Detect `IEpochStream<T>` in constructor
  - Throw `ArgumentException` with helpful message pointing to `EpochBufferBlock<T>`
  - Add test for validation

**Files**:
- `/poc/DataFlow.POC/Core/BufferNode.cs`
- `/poc/DataFlow.POC.Tests/BufferNodeTests.cs` (add validation test)

**Example**:
```csharp
[Obsolete("BufferNode<T> is obsolete and will be removed. Use EpochBufferBlock<T> for epoch stream buffering. See issue #55 for migration details.", error: false)]
public class BufferNode<T> : BufferNode
{
    // ...
}
```

**Acceptance Criteria**:
- `[Obsolete]` attribute added with clear migration message
- Documentation clearly states obsolescence and replacement
- Runtime validation prevents epoch stream usage
- Test verifies exception thrown for epoch streams
- Existing tests still pass (with obsolete warnings)

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
- [ ] `BufferNode<T>` marked with `[Obsolete]` attribute
- [ ] Runtime validation prevents epoch stream usage in `BufferNode<T>`
- [ ] All tests pass (existing + new)
- [ ] Code review approved
- [ ] Documentation complete
- [ ] Performance acceptable (<10% overhead)
- [ ] Self-improvement feedback submitted

## Effort Estimate

| Phase | Estimate | Risk |
|-------|----------|------|
| Phase 1: Mark BufferNode Obsolete | 2 hours | Minimal |
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
