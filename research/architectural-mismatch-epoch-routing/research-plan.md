# Research Plan: Architectural Mismatch in Epoch Stream Routing

## Research Objective

Investigate fundamental architectural mismatch in epoch stream handling discovered during issue #15 resolution attempt. Determine whether edges should route epoch stream **containers** (`IEpochStream<T>`) or **data items** (`T`), and validate the correct architecture.

## Background

**Source**: https://github.com/uniun-technology/dataflow/pull/24#issuecomment-3583042628

While attempting to fix epoch blocks (#15), a deeper architectural issue was uncovered:
- Edges may be routing epoch stream **containers** instead of **data items**
- This breaks edge-level routing semantics (broadcast, selective routing, buffering)

## Research Questions

### Primary Questions

1. **What do edges currently route?**
   - When a block outputs `IAsyncEnumerable<IEpochStream<T>>`, do edges route:
     - Option A: The `IEpochStream<T>` containers (one per epoch)?
     - Option B: The individual `T` items within each epoch stream?

2. **What SHOULD edges route?**
   - Which option provides correct routing semantics?
   - How does this affect broadcast, selective routing, and buffering?

3. **How does `ConfigureEpochs` infrastructure work?**
   - Does it manage epochs at a layer that allows edges to route data items?
   - Why do we have both `ConfigureEpochs` infrastructure AND epoch-aware blocks?

### Secondary Questions

4. **What are the architectural options?**
   - Option A: Edges route containers, blocks handle epoch awareness
   - Option B: Edges route items, infrastructure manages epoch boundaries
   - Option C: Dual-mode support for both approaches

5. **What is the recommended path forward?**
   - Which architecture best supports the dataflow model?
   - What changes are needed to implement the correct architecture?

## Success Metrics

### Quantitative
- All test scenarios pass with expected routing behavior
- No edge-level routing semantic violations

### Qualitative
- Clear understanding of current vs intended architecture
- Documented architectural decision with rationale
- Implementation-ready specification for correct architecture

### Baseline
- Current behavior: Unknown (to be determined through testing)
- Expected behavior: Edge routing semantics work correctly (broadcast duplicates items, not containers)

### Validation
- Test cases demonstrating:
  1. Broadcast routing with epochs
  2. Selective routing with epochs
  3. Buffer blocks with epochs
  4. Comparison with `ConfigureEpochs` approach

## Validation Approach

### Phase 1: Code Analysis
- [x] Review edge routing implementation (`EdgeStrategy`, `DataFlowGraph`)
- [x] Review epoch block types (`EpochSourceNode`, `EpochProcessorNode`)
- [x] Review `ConfigureEpochs` infrastructure (`SingleEpochExtensions`)
- [x] Understand how `EnumerateAndRouteTypedStreamAsync` works

### Phase 2: Test Scenarios
- [ ] Create test: Broadcast routing with epoch streams
- [ ] Create test: Selective routing with epoch streams
- [ ] Create test: Buffer blocks with epoch streams
- [ ] Compare behavior with existing `ConfigureEpochs` tests

### Phase 3: Architecture Analysis
- [ ] Document current routing behavior
- [ ] Identify routing semantic violations (if any)
- [ ] Analyze `ConfigureEpochs` vs epoch-aware blocks approach
- [ ] Compare with intended architecture from documentation

### Phase 4: Recommendation
- [ ] Document architectural decision
- [ ] Create implementation plan if changes needed
- [ ] Identify affected areas and migration path

## Expected Outcomes

### Research Documentation
- `/research/architectural-mismatch-epoch-routing/README.md` - Research findings
- `/research/architectural-mismatch-epoch-routing/design/architecture-analysis.md` - Detailed architecture comparison

### Test Artifacts
- Test cases validating edge routing behavior with epochs
- Comparison tests showing `ConfigureEpochs` vs epoch blocks behavior

### Implementation Handover
- Implementation-ready work item with specifications
- Clear architectural decision documented
- Test scenarios for validation

## Timeline

**Estimated Duration**: 3-5 days

- **Day 1**: Code analysis and test scenario creation (✅ Complete)
- **Day 2**: Test execution and behavior analysis
- **Day 3**: Architecture analysis and comparison
- **Day 4**: Documentation and recommendations
- **Day 5**: Implementation handover creation

## Initial Findings (from Code Analysis)

### Current Architecture Understanding

1. **Edge Routing Mechanism**:
   - Edges use `EnumerateAndRouteTypedStreamAsync<T>` to enumerate block outputs
   - The generic type `T` is the block's output item type
   - For blocks outputting `IAsyncEnumerable<IEpochStream<int>>`, T = `IEpochStream<int>`
   - Therefore, edges enumerate and route **epoch stream containers**, not data items

2. **Implication**:
   - When a broadcast edge receives an `IEpochStream<int>`, it broadcasts the **container**
   - Multiple downstream blocks would receive references to the SAME epoch stream object
   - Enumerating the epoch stream's items would consume them for ALL consumers
   - This violates broadcast semantics (each consumer should get independent copy of data)

3. **`ConfigureEpochs` Alternative**:
   - `SingleEpochExtensions.WrapInSingleEpoch` wraps plain streams in epoch containers
   - Returns `IAsyncEnumerable<IEpochStream<T>>` where one epoch stream contains all items
   - Still suffers from same routing issue when broadcast/selective routing applied

### Key Question to Validate

**Does the current implementation have epoch-specific edge handling that unwraps epoch streams?**
- Need to check if there's any special handling for `IEpochStream<T>` types
- Review `EnvelopeEdgeStrategy` and related envelope handling
- Check if epoch coordinator provides special merging logic

This will be investigated in Phase 2 testing.

## Resources

- **PR #24**: Implementation attempt revealing the mismatch
- **Issue #15**: Epoch blocks issue that led to this discovery
- **Topology Guide**: `/poc/docs/guides/topology-*.md` (edge routing documentation)
- **Existing Research**: `/research/developer-epoch-issue/` (related epoch findings)
- **ConfigureEpochs Examples**: Existing tests in `SingleEpochExtensionsTests.cs`
