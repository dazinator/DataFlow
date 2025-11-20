# Research Plan: Mandatory Epochs

**Created**: 2025-11-20  
**Research Duty**: Following `.team/duties/RESEARCH_DUTY.md`

---

## Research Objective

Investigate whether we can consolidate the architecture by making epochs mandatory for all blocks, treating non-epoch sources as single-epoch sequences, and removing duplicate block implementations.

### Motivation

Currently, we maintain parallel implementations:
- **Plain blocks**: `ActorBlock`, `BatchBlock`, `ProducerBlock`, `PlainSourceBlock`
- **Epoch blocks**: `EpochActorBlock`, `EpochBatchBlock`, `EpochSourceBlock`

This creates:
- Maintenance burden (two code paths for similar logic)
- Conceptual overhead (users must choose between plain vs epoch)
- Testing complexity (must test both variants)

**Key Insight**: A stream with no breaks or epochs can be considered the same as one long epoch (a single sequence from the source).

---

## Research Questions

1. **Feasibility**: Can we treat plain sources as single-epoch sequences?
2. **Architecture**: What does unified epoch-only block architecture look like?
3. **Performance**: What is the overhead of mandatory epochs vs plain streams?
4. **Migration**: How do existing plain-stream usages migrate?
5. **Developer Experience**: Does this simplification improve or complicate the API?
6. **Graph Configuration**: How should epoch node configuration be automated?

---

## Success Metrics

### Quantitative
- **Performance**: Epoch overhead should be <5% vs plain streams for single-epoch scenarios
- **Code Reduction**: Eliminate at least 4 duplicate block implementations
- **API Surface**: Reduce block types by ~50%

### Qualitative
- **Conceptual Clarity**: Single mental model (everything is epoch-based)
- **Maintainability**: One code path to maintain and test
- **Flexibility**: Same capability to segment or not segment

### Baseline
- **Current**: 12 block types (6 plain + 6 epoch variants)
- **Target**: ~6 epoch-only block types with plain sources wrapped automatically

### Validation
- Prototype must demonstrate:
  - Plain source → single epoch conversion
  - Performance acceptable for single-epoch case
  - Existing tests pass with unified blocks
  - Migration path is clear

---

## Validation Approach

### Phase 1: Analysis (Days 1-2)
1. Document current block pairs and their differences
2. Identify conversion points (plain → epoch)
3. Map migration paths for existing code

### Phase 2: Design (Days 2-3)
1. Design unified epoch-based block API
2. Design automatic epoch wrapper for plain sources
3. Design graph builder convenience methods

### Phase 3: Prototyping (Days 3-5)
1. Implement single-epoch wrapper for plain sources
2. Convert one block pair to epoch-only (ActorBlock)
3. Validate performance with benchmarks
4. Test with existing test suite

### Phase 4: Documentation (Days 5-6)
1. Document recommended approach
2. Create migration guide
3. Document performance characteristics
4. Create implementation specifications

---

## Expected Outcomes

### Primary Outcome: Implementation Handover
- Research documentation in `/research/mandatory-epochs/`
- Implementation-ready work item with specifications
- Formal design documentation
- Prototype code demonstrating approach
- Benchmark results showing performance impact

### Deliverables
1. **Research documentation**: Complete analysis and findings
2. **Design documents**: Unified architecture specification
3. **Prototype code**: Working demonstration (to be reverted)
4. **Benchmarks**: Performance comparison data
5. **Implementation work item**: Ready for implementation duty
6. **Migration guide**: For transitioning existing code

---

## Timeline

**Estimated Duration**: 5-6 days

- **Day 1**: Analysis and current state documentation
- **Day 2**: Design unified approach
- **Day 3-4**: Prototype implementation
- **Day 5**: Benchmarking and validation
- **Day 6**: Documentation and handover

---

## Risks and Mitigation

### Risk 1: Performance Overhead
- **Mitigation**: Benchmark single-epoch case vs plain streams
- **Threshold**: Must be <5% overhead or approach is not viable

### Risk 2: API Complexity
- **Mitigation**: Design automatic wrapping for plain sources
- **Validation**: User code should not become more complex

### Risk 3: Breaking Changes
- **Mitigation**: Design migration path and deprecation strategy
- **Validation**: Gradual migration with backward compatibility period

---

## References

- Current epoch research: `/research/epoch-stream-separation/`
- Epoch coordination: `/research/epoch-source-coordination/`
- POC blocks: `/poc/DataFlow.POC/Blocks/`
- Research duty: `.team/duties/RESEARCH_DUTY.md`
