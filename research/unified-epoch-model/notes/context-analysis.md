# Context Analysis: Unified Epoch Model Research

**Date**: 2025-11-27  
**Purpose**: Document current state and requirements for unified epoch model research

---

## Current State Summary

### Confirmed Architectural Mismatch

From `/research/architectural-mismatch-epoch-routing/`:

**Problem**: Edges route epoch stream **containers** (`IEpochStream<T>`), not data **items** (`T`)

**Impact**:
- ✅ Async iterator-based epoch streams work by accident (re-enumerable)
- ❌ Channel-based epoch streams broken (only one consumer gets data)
- ❌ Broadcast routing shares containers, not data
- ❌ Realistic use cases fail

**Test Evidence**:
```csharp
// Channel-based broadcast test result:
// Consumer A: 5 items [1, 2, 3, 4, 5]  
// Consumer B: 0 items []
// Only first consumer exhausts the channel
```

---

## Current Approaches

### 1. Epoch-Aware Blocks

**Signature**: `IAsyncEnumerable<IEpochStream<T>>` → `IAsyncEnumerable<IEpochStream<T>>`

**Example**: `EpochActorBlock`, `EpochSourceNode`, `EpochProcessorNode`

**Advantages**:
- Clear epoch boundaries via stream boundaries
- No per-item epoch checks needed
- Efficient epoch processing

**Critical Flaw**:
- Edges route containers, breaking broadcast/selective routing
- Only works for 1:1 connections
- Not suitable for multi-consumer topologies

---

### 2. Plain Blocks with Graph-Level Coordination

**Signature**: `IAsyncEnumerable<T>` → `IAsyncEnumerable<T>`

**Approach**: Blocks output plain items, access epoch via `IExecutionContext.CurrentEpoch`

**Implemented in**: PR#29, PR#30 (interim solution)

**Advantages**:
- Edges naturally route data items
- Works with all topologies

**Critical Flaw**:
- Must check `CurrentEpoch` per item to detect changes
- Loses epoch boundary efficiency
- Negates purpose of epoch streams
- Inefficient for epoch-aware processing

---

## Topology Requirements

From `/poc/docs/guides/topology-*.md`:

### 1. Broadcast Topology
- **Requirement**: Each downstream block receives ALL items
- **Current State**: Broken with epoch-aware blocks (container sharing)
- **Expected**: Source → Edge → [Block A: all items, Block B: all items]

### 2. Selective Routing
- **Requirement**: Route items based on item properties/predicates
- **Current State**: Routes containers, not items (can't inspect item properties)
- **Expected**: Route individual items to appropriate downstream blocks

### 3. Competing Consumers
- **Requirement**: Load balance items across consumers
- **Current State**: Routes containers (all items go to one consumer)
- **Expected**: Round-robin or strategy-based distribution of items

### 4. Buffer/Batch Blocks
- **Requirement**: Buffer or batch individual items
- **Current State**: Unclear semantics (buffer containers vs items?)
- **Expected**: Buffer/batch individual items within epoch context

### 5. Fan-In
- **Requirement**: Merge multiple sources into single stream
- **Current State**: Works with source-level coordination
- **Expected**: Maintain epoch boundaries and vector correlation

---

## Performance Baseline

From existing POC implementation:

### Single-Source Epoch Performance
- Source coordination: ~10-20ns overhead (fast path)
- Multi-source coordination: ~50-100ns overhead
- Per-item processing: Varies by block type

### Current Overhead
- `ConfigureEpochs`: Adds epoch wrapping overhead
- Graph-level coordination: Per-item epoch context lookup
- DI scope creation: Per-epoch overhead

**Target**: <10% overhead for unified model

---

## Key Code Locations

### POC Implementation
- **Edge Routing**: `/poc/DataFlow.POC/Core/DataFlowGraph.cs`
  - `EnumerateAndRouteTypedStreamGenericAsync<T>()` - Generic routing logic
- **Edge Strategies**: `/poc/DataFlow.POC/Core/EdgeStrategy.cs`
  - `BroadcastStrategy`, `SelectiveStrategy`, `CompetingStrategy`
- **Epoch Types**: `/poc/DataFlow.POC/Core/EpochStream.cs`
  - `IEpochStream<T>`, `EpochStream<T>`
- **Epoch Coordination**: `/poc/DataFlow.POC/Core/EpochCoordinator.cs`
  - `IEpochCoordinator`, `EpochCoordinator`
- **Epoch Blocks**: `/poc/DataFlow.POC/Blocks/`
  - `EpochActorBlock.cs`, `EpochSourceNode.cs`, `EpochProcessorNode.cs`

### Research Artifacts
- **Architectural Mismatch**: `/research/architectural-mismatch-epoch-routing/`
- **Source Coordination**: `/research/epoch-source-coordination/`
- **Prior Attempts**: PR#29, PR#30 (not merged, interim solutions)

---

## Research Options Overview

### Option 1: Epochs as Separate Graph Executions

**Core Concept**: Each epoch runs as independent sub-graph instance

**Key Questions**:
1. Can we make graph re-instantiation efficient enough?
2. How to manage concurrent epoch executions?
3. How to simplify EpochActorBlock with single-epoch lifetime?
4. What's the API design for config vs execution?

**Expected Advantages**:
- Clean epoch isolation
- Simple block signatures (`IAsyncEnumerable<T>`)
- Natural DI scoping per epoch
- No epoch stream containers needed

**Expected Challenges**:
- Graph re-instantiation overhead
- Concurrent execution management
- API design complexity

---

### Option 2: Intelligent Edge Unwrap/Wrap

**Core Concept**: Edges unwrap epoch streams, route items, re-wrap per consumer

**Key Questions**:
1. How do edges implement unwrap/wrap?
2. What backs each downstream epoch stream?
3. How to propagate epoch vectors through wrapping?
4. How does backpressure work through unwrap/wrap?
5. Can all topologies work with this model?

**Expected Advantages**:
- Single long-running execution
- Epoch boundary efficiency preserved
- Natural backpressure via channels

**Expected Challenges**:
- Complex edge logic
- Harder to reason about data flow
- Potential performance overhead
- Need strategic channel management

---

## Testing Strategy

### Functional Requirements

For each option, validate:

1. **Broadcast**: Each consumer gets all items ✓
2. **Selective**: Items routed by properties ✓
3. **Competing**: Load balancing works ✓
4. **Buffer**: Batching within epochs ✓
5. **Fan-in**: Multiple sources merge correctly ✓

### Performance Requirements

1. **Throughput**: <10% degradation vs plain blocks
2. **Memory**: Reasonable overhead for epoch management
3. **Latency**: No significant increase in item processing time

### Stress Tests

1. **Concurrent Epochs**: Multiple epochs in flight simultaneously
2. **High Throughput**: Millions of items per second
3. **Large Batches**: Epoch streams with many items
4. **Error Handling**: Exceptions, cancellation

---

## Success Criteria

### Must Have
- ✅ All topology patterns work correctly
- ✅ Epoch boundaries detected efficiently (no per-item checks)
- ✅ Performance overhead <10%
- ✅ Epoch vector propagation maintains causality
- ✅ Backpressure works correctly
- ✅ Clear, simple block contracts

### Should Have
- ✅ Clean migration path from current approach
- ✅ Simple API design
- ✅ Easy to reason about data flow
- ✅ Good documentation and examples

### Nice to Have
- ✅ Backward compatibility where possible
- ✅ Minimal breaking changes
- ✅ Performance improvements over current

---

## Next Steps

### Phase 1 Completion Checklist
- [x] Created research folder structure
- [x] Reviewed architectural mismatch research
- [ ] Reviewed current POC implementation in detail
- [ ] Documented topology requirements
- [ ] Established baseline performance
- [ ] Ready to start Option 1 prototyping

### Phase 2: Option 1 Prototyping
- [ ] Design epoch orchestrator
- [ ] Prototype graph per epoch
- [ ] Test concurrent execution
- [ ] Measure performance
- [ ] Document findings

---

## Open Questions

1. **Graph Re-instantiation**: Can we reuse block instances or must we recreate?
2. **State Management**: How do stateful blocks work with graph-per-epoch?
3. **Backward Compatibility**: Can we support existing epoch-aware blocks?
4. **API Design**: How to express "execute this graph for each epoch"?
5. **Resource Management**: How to clean up completed epoch graphs?

---

## References

- [Architectural Mismatch Research](../../architectural-mismatch-epoch-routing/README.md)
- [Source Coordination Research](../../epoch-source-coordination/README.md)
- [Topology Guides](/poc/docs/guides/)
- [POC Implementation](/poc/DataFlow.POC/)
- [Issue #15](https://github.com/uniun-technology/dataflow/issues/15)
- [PR #29](https://github.com/uniun-technology/dataflow/pull/29)
- [PR #30](https://github.com/uniun-technology/dataflow/pull/30)
