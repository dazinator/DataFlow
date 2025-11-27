# Research Plan: Unified Epoch Model with Edge-Level Item Routing

**Research Objective**: Design and validate a unified epoch model that works correctly with all edge-level routing strategies while maintaining efficient epoch boundary detection.

**Date**: 2025-11-27  
**Researcher**: GitHub Copilot (Research Duty)  
**Related Issues**: #15, #26, #28, PR#29, PR#30

---

## Executive Summary

### The Problem

The current epoch coordination design has a fundamental architectural mismatch:

1. **Epoch-Aware Blocks** (`IAsyncEnumerable<IEpochStream<T>>`) - Broken for edge-level routing
   - Edges route epoch stream **containers**, not data items
   - Broadcast routing fails with channel-based streams (only one consumer gets data)
   - Cannot reliably use with any multi-consumer topology

2. **Plain Blocks** (`IAsyncEnumerable<T>`) - Lose epoch boundary efficiency
   - Must check `context.CurrentEpoch` per item to detect epoch changes
   - Negates the entire purpose of `IEpochStream` as epoch boundary marker
   - Forces inefficient per-item epoch awareness

**Neither approach provides a unified, efficient model for epoch processing with edge-level routing.**

### Research Objective

Design and validate a **unified epoch model** that:
- Allows blocks to process per-epoch data as demarcated by stream boundaries
- Works correctly with all edge-level routing strategies (broadcast, selective, competing)
- Provides efficient epoch boundary detection without per-item checks
- Maintains epoch vector propagation through graph topology
- Supports all documented topology patterns

---

## Background

### Discovery Path

1. **Original Issue (#15)**: Attempted to fix epoch blocks
2. **Research (#26)**: Discovered architectural mismatch - edges route containers, not items
3. **Phase 1 Implementation (PR#29)**: Added `IExecutionContext.CurrentEpoch` property
4. **Phase 2-4 Implementation (PR#30)**: Documented limitations, added tests/docs for interim solution
5. **Critical Realization**: Current approach provides workarounds, not a unified solution

### Evidence of Container Routing Problem

From `/research/architectural-mismatch-epoch-routing/`:

**Edges route containers:**
```csharp
private static async Task EnumerateAndRouteTypedStreamGenericAsync<T>(
    object typedStream,
    List<ITypedEdgeRouter> routers,
    CancellationToken cancellationToken)
{
    var stream = (IAsyncEnumerable<T>)typedStream;
    await foreach (var item in stream.WithCancellation(cancellationToken))
    {
        // For IAsyncEnumerable<IEpochStream<int>>, T = IEpochStream<int>
        // Routes CONTAINERS, not data items
        await router.RouteTypedItemAsync(item, cancellationToken);
    }
}
```

**Broadcast fails with channels:**
```csharp
// Test Result:
// Consumer A: 5 items [1, 2, 3, 4, 5]  
// Consumer B: 0 items []
// ⚠️ Only one consumer gets data - channels can't be re-enumerated
```

---

## Research Questions

### Primary Questions

1. **Option 1: Can we make graph re-instantiation efficient enough?**
   - What is the overhead of creating new graph instances per epoch?
   - How do we manage multiple concurrent epoch executions?
   - Can we simplify EpochActorBlock with epoch-scoped lifetime?
   - What is the API design for separating configuration from execution?

2. **Option 2: Can edge unwrap/wrap work with all topologies?**
   - How do edge strategies implement unwrap/wrap semantics?
   - What backs each downstream epoch stream (channel management)?
   - How does epoch vector information propagate through wrapping?
   - Does backpressure work correctly through unwrap/wrap?
   - What is the performance overhead?

3. **Which option provides the best unified model?**
   - Performance: <10% overhead acceptable
   - Simplicity: Easy to understand and reason about
   - Completeness: Works with all topologies
   - Migration: Clear path from existing code

---

## Research Scope

### Option 1: Epochs as Separate Graph Executions

**Concept**: Treat each epoch as its own sub-execution with dedicated graph instance.

**Architecture**:
- Graph topology is re-instantiated per epoch
- Blocks have simple signatures: `IAsyncEnumerable<T>` → `IAsyncEnumerable<T>`
- Each epoch execution is isolated and independent
- Multiple epochs can run concurrently as parallel sub-graphs

**Research Tasks**:
1. Design epoch orchestrator architecture
2. Prototype graph re-instantiation mechanism
3. Implement concurrent epoch execution management
4. Measure performance overhead
5. Simplify EpochActorBlock for single-epoch lifetime
6. Design configuration vs execution API separation

**Validation**:
- Performance benchmarks vs current approach
- Concurrency stress tests (multiple parallel epochs)
- Memory profiling (graph instance overhead)
- API usability testing

### Option 2: Intelligent Edge Unwrap/Wrap

**Concept**: Preserve `IEpochStream<T>` but make edges intelligent about unwrapping and routing items.

**Architecture**:
- Blocks keep signature: `IAsyncEnumerable<IEpochStream<T>>` → `IAsyncEnumerable<IEpochStream<T>>`
- Edges unwrap incoming epoch streams to route individual items
- Edges re-wrap items into new epoch streams per downstream block
- Channel management is strategic (shared for competing, separate for broadcast)
- Epoch vector information propagates through wrapping/unwrapping

**Research Tasks**:
1. Design edge unwrap/wrap semantics
2. Implement edge strategy extensions for epoch awareness
3. Design epoch stream creation and channel backing strategy
4. Implement epoch vector propagation logic
5. Test against all topology patterns
6. Measure backpressure propagation
7. Measure performance overhead

**Validation**:
- Topology compatibility tests (broadcast, selective, competing, buffer, fan-in)
- Backpressure propagation tests
- Epoch vector correlation tests
- Performance benchmarks vs current approach

---

## Success Metrics

### Quantitative Metrics

1. **Performance**:
   - Chosen option has <10% overhead vs current approach
   - Throughput comparable to plain block processing
   - Memory overhead acceptable for realistic workloads

2. **Topology Coverage**:
   - All documented topologies work correctly
   - Broadcast: Each consumer gets all items
   - Selective: Routing based on item properties
   - Competing: Load balancing across consumers
   - Buffer: Batching and windowing work
   - Fan-in: Multiple sources merge correctly

3. **Correctness**:
   - Epoch boundaries detected efficiently
   - Epoch vector propagation maintains causality
   - No per-item epoch checks needed
   - Backpressure works correctly

### Qualitative Metrics

1. **Simplicity**:
   - Block contracts are easy to understand
   - Data flow is easy to reason about
   - Edge logic is not overly complex

2. **Migration**:
   - Clear path from current approach
   - Minimal breaking changes
   - Good backward compatibility story

3. **Documentation**:
   - Comprehensive examples for all topologies
   - Clear architectural rationale
   - Easy to understand design decisions

---

## Validation Approach

### Phase 1: Setup and Context Analysis (Day 1)

**Tasks**:
- [x] Create research folder structure
- [ ] Review `/research/architectural-mismatch-epoch-routing/` findings
- [ ] Document current epoch coordination approaches
- [ ] Review POC code structure (`/poc/DataFlow.POC/`)
- [ ] Review all topology patterns (`/poc/docs/guides/topology-*.md`)
- [ ] Understand current edge routing implementation
- [ ] Document baseline performance characteristics

**Deliverables**:
- Context analysis document
- Baseline performance measurements
- Topology requirements matrix

---

### Phase 2: Option 1 Analysis - Epochs as Separate Graph Executions (Days 2-3)

**Tasks**:
- [ ] Design epoch orchestrator architecture
- [ ] Design graph configuration vs execution separation
- [ ] Prototype graph re-instantiation mechanism
- [ ] Implement concurrent epoch execution manager
- [ ] Test with multiple parallel epochs
- [ ] Measure performance overhead
- [ ] Analyze block lifetime management
- [ ] Design EpochActorBlock simplification
- [ ] Test DI scope management

**Deliverables**:
- Architecture design document
- Proof-of-concept implementation
- Performance benchmark results
- API design specification

**Test Scenarios**:
1. Single epoch execution
2. Multiple concurrent epochs
3. Epoch completion and cleanup
4. DI scope isolation per epoch
5. Error handling and cancellation
6. Memory profiling

---

### Phase 3: Option 2 Analysis - Intelligent Edge Unwrap/Wrap (Days 3-4)

**Tasks**:
- [ ] Design edge unwrap/wrap semantics
- [ ] Design epoch stream creation strategy
- [ ] Prototype edge strategy extensions
- [ ] Implement channel management logic
- [ ] Implement epoch vector propagation
- [ ] Test against all topology patterns
- [ ] Measure backpressure behavior
- [ ] Measure performance overhead

**Deliverables**:
- Architecture design document
- Proof-of-concept implementation
- Topology validation matrix
- Performance benchmark results

**Test Scenarios**:
1. Broadcast topology with epoch streams
2. Selective routing with epoch streams
3. Competing consumers with epoch streams
4. Buffer blocks with epoch streams
5. Fan-in topology with epoch streams
6. Epoch vector propagation
7. Backpressure propagation
8. Error handling and cancellation

---

### Phase 4: Comparison and Evaluation (Day 5)

**Tasks**:
- [ ] Run comprehensive performance benchmarks
- [ ] Compare complexity (code, reasoning, maintenance)
- [ ] Validate topology compatibility
- [ ] Assess migration paths
- [ ] Compare API designs
- [ ] Evaluate user experience

**Deliverables**:
- Performance comparison report
- Complexity analysis
- Topology compatibility matrix
- Migration assessment
- Recommendation with justification

**Comparison Criteria**:
| Criterion | Option 1 | Option 2 | Weight |
|-----------|----------|----------|--------|
| Performance | TBD | TBD | High |
| Simplicity | TBD | TBD | High |
| Topology Support | TBD | TBD | Critical |
| Migration Path | TBD | TBD | Medium |
| API Design | TBD | TBD | Medium |
| Maintenance | TBD | TBD | Medium |

---

### Phase 5: Recommendation and Documentation (Days 6-7)

**Tasks**:
- [ ] Write Architecture Decision Record (ADR)
- [ ] Create complete design specification
- [ ] Document implementation phases
- [ ] Write migration guide
- [ ] Create test strategy
- [ ] Document all edge cases
- [ ] Create examples for all topologies

**Deliverables**:
- ADR document
- Complete design specification
- Phased implementation plan
- Migration guide
- Test strategy document
- Comprehensive examples

---

### Phase 6: Handover (Day 8)

**Tasks**:
- [ ] Save prototype code to `/research/unified-epoch-model/prototypes/`
- [ ] Revert all exploratory code from `/poc/` and `/src/`
- [ ] Create implementation handover work item
- [ ] Submit self-improvement feedback
- [ ] Hand over to implementation duty

**Deliverables**:
- Research README.md with complete findings
- Saved prototype code
- Implementation work item
- Self-improvement feedback

---

## Expected Outcomes

### Option 1: Epochs as Separate Graph Executions

**Advantages**:
- ✅ Clean epoch isolation
- ✅ Simple block contracts
- ✅ No epoch stream containers needed
- ✅ Natural per-epoch DI scoping

**Disadvantages**:
- ❌ Potential overhead from re-instantiation
- ❌ Complex coordination between orchestrator and sub-executions
- ❌ Need to manage concurrent graph instances

**Success Criteria**:
- Graph re-instantiation overhead <5% of total execution time
- Clean API for configuration vs execution
- Concurrent epoch management is robust
- EpochActorBlock is significantly simplified

---

### Option 2: Intelligent Edge Unwrap/Wrap

**Advantages**:
- ✅ Single long-running execution model
- ✅ Efficient epoch boundary detection (stream boundaries)
- ✅ Natural backpressure through channels

**Disadvantages**:
- ❌ Complex edge logic
- ❌ More complicated reasoning about data flow
- ❌ Potential performance overhead

**Success Criteria**:
- All topology patterns work correctly
- Backpressure propagates without deadlocks
- Epoch vector propagation maintains causality
- Performance overhead <10%

---

## Timeline

**Total Estimated Duration**: 7-11 days (approximately 2 weeks)

- **Day 1**: Setup and context analysis
- **Days 2-3**: Option 1 research and prototyping
- **Days 3-4**: Option 2 research and prototyping (overlap)
- **Day 5**: Comparison and evaluation
- **Days 6-7**: Recommendation and documentation
- **Day 8**: Handover

---

## Resources

### Existing Research
- `/research/architectural-mismatch-epoch-routing/` - Container routing problem
- `/research/epoch-source-coordination/` - Epoch coordination patterns

### Codebase References
- `/poc/DataFlow.POC/Core/DataFlowGraph.cs` - Edge routing implementation
- `/poc/DataFlow.POC/Core/EdgeStrategy.cs` - Edge strategies
- `/poc/DataFlow.POC/Core/EpochStream.cs` - Epoch stream types
- `/poc/DataFlow.POC/Blocks/EpochActorBlock.cs` - Current epoch actor implementation
- `/poc/docs/guides/topology-*.md` - Topology patterns

### Related Work
- **PR#29**: Phase 1 (CurrentEpoch property)
- **PR#30**: Phase 2-4 (interim solution - to be archived)
- **Issue #15**: Original epoch blocks issue
- **Issue #26**: Architectural mismatch research

---

## Notes

- This research supersedes the graph-level coordination approach in PR#30
- PR#30 will be merged to feature branch for reference but archived
- Fresh design starting from validated problem statement
- Focus on unified model, not workarounds
- Both options will be prototyped before making recommendation

---

## Risk Assessment

### Option 1 Risks
- Graph re-instantiation may be too slow
- Concurrent epoch management may be complex
- API design may be unintuitive

### Option 2 Risks
- Edge complexity may be unmanageable
- Some topologies may not work correctly
- Performance overhead may be too high

### Mitigation
- Prototype both options fully before deciding
- Performance benchmarks are mandatory
- All topologies must be tested
- Clear success criteria defined upfront
