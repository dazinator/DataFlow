# Research Plan: Source-Level Epoch Coordination

**Created**: 2025-11-14  
**Research Duty**: Explore alternative epoch DI scope management with source-level coordination  
**Parent Research**: `/research/epoch-scope-propagation/` (rejected stream-coupled approach)

---

## Research Question

**Can epoch DI scope management be simplified by coordinating at the source level with bounded epoch growth and readiness-based signaling?**

**Key Innovation**: Sources coordinate via global `IEpochCoordinator` to:
1. Get same epoch object immediately (single DI scope from start)
2. Signal **readiness for next epoch** (not wait for completion)
3. Bound epoch growth (one sequence per source per epoch)
4. Propagate unified epoch on streams (no downstream scope merging)

---

## Background

### Why This Approach?

Previous research (`/research/epoch-scope-propagation/`) rejected stream-coupled DI scopes because:
- **Problem**: Fan-in scope merging unsolvable
- **Issue**: Two streams with different scopes merge → which scope to use?

**This approach solves it differently**:
- Coordinate at **source creation** not **merge point**
- Sources get **same epoch object** from coordinator
- Both streams carry **same scope** → no merge problem!

### Key Refinements from Discussion

1. **Readiness-Based Coordination** (not completion-based)
   - Sources signal when ready for next epoch
   - Don't wait for current epoch to fully complete
   - Maintains current pipelining behavior

2. **Bounded Epoch Growth**
   - Epoch incorporates one sequence from each source
   - Then sources must align on readiness before advancing
   - Prevents unbounded epoch accumulation

3. **Single-Source Optimization**
   - 80%+ of dataflows are single source
   - No coordination overhead for single source
   - Fast path with no blocking

---

## Research Objectives

### Primary Objectives

1. **Prototype `IEpochCoordinator`**
   - Readiness-based signaling
   - Bounded subsumption logic
   - Single-source fast path

2. **Validate Key Scenarios**
   - Single source (optimized, no coordination)
   - Multi-source alignment (bounded growth)
   - Dynamic sources (late-joining sources)
   - Fan-in (verify same scope across merged streams)

3. **Create Design Documentation**
   - Architecture with coordinator
   - Lifecycle flows
   - Comparison with EpochManager approach

4. **Comparative Analysis**
   - Performance (hot path)
   - Predictability (bounded epochs)
   - Complexity (lifetime clarity)
   - Safety (race conditions, disposal)

### Secondary Objectives

1. Test edge cases (source failure, varying rates)
2. Identify potential issues early
3. Quantify trade-offs

---

## Research Phases

### Phase 1: Core Prototype (Current)

**Deliverables**:
- `IEpochCoordinator` interface
- Coordinator implementation with:
  - Readiness tracking
  - Bounded subsumption
  - Single-source fast path
- Basic tests

**Key Scenarios to Prototype**:
1. Single source advancing epochs (no blocking)
2. Two sources coordinating on readiness
3. Dynamic source joining mid-flow
4. Fast source waiting for slow source

### Phase 2: Fan-In Validation

**Deliverables**:
- BufferNode-like fan-in test
- Verify same scope propagated
- Confirm no scope merging needed

**Test**:
```csharp
Source A → gets epoch from coordinator
Source B → gets SAME epoch from coordinator
Both propagate same epoch on streams
Fan-in: streams already have same scope ✓
```

### Phase 3: Comparative Analysis

**Deliverables**:
- Performance comparison (single vs multi-source)
- Complexity comparison (code, concepts)
- Safety analysis (locking, disposal, races)
- Design recommendation

**Comparison Dimensions**:
1. **Performance**: Hot path for typical case (single source)
2. **Predictability**: Control over epoch lifetime/boundaries
3. **Complexity**: Clarity of epoch lifetime rules
4. **Safety**: Race conditions, disposal guarantees, locking complexity

### Phase 4: Design Documentation

**Deliverables**:
- Architecture diagrams
- Lifecycle flows
- API specifications
- Trade-off analysis
- Recommendation with evidence

---

## Success Criteria

**Research Complete When**:
- [ ] Prototype validates core coordination mechanics
- [ ] Tests demonstrate single-source optimization
- [ ] Tests show multi-source bounded growth
- [ ] Fan-in verified (same scope, no merging)
- [ ] Comparative analysis complete across 4 dimensions
- [ ] Design documentation created
- [ ] Clear recommendation provided

**High-Quality Output Indicators**:
- Prototype runs and passes tests
- Edge cases identified and documented
- Performance characteristics measured
- Trade-offs clearly articulated
- Decision can be made with confidence

---

## Known Constraints

1. **Must maintain existing semantics**:
   - Epoch vector operations (increment, subsume)
   - Concurrent access patterns
   - Service sharing guarantee

2. **Must optimize for common case**:
   - Single source (>80% of dataflows)
   - No coordination overhead when unnecessary

3. **Must handle dynamic scenarios**:
   - Sources appearing at different times
   - Varying throughput rates
   - Source failures

---

## Key Innovations to Test

### 1. Readiness Signaling (Not Completion)

**Current Behavior**:
```
Source emits epoch 1 items
  → Pipeline processes
  → Source emits epoch 2 items (doesn't wait for epoch 1 completion)
```

**Proposed Behavior (Multi-Source)**:
```
Source A ready for epoch 2
Source B ready for epoch 2
  → Coordinator: "All sources ready, epoch 2 can start"
  → Both get epoch 2 object
  → No waiting for epoch 1 to complete!
```

### 2. Bounded Subsumption

**Rule**: Epoch incorporates at most one sequence from each source

```
Active Epoch: {A=1, B=1}

Source A ready for {A=2}:
  → Coordinator: "Source B still at {B=1}, you must wait"
  
Source B ready for {B=2}:
  → Coordinator: "All sources ready for next epoch"
  → Create new epoch {A=2, B=2}
  → Both sources get same epoch object
```

### 3. Single-Source Fast Path

**No Coordination When Single Source**:
```csharp
if (_sources.Count == 1)
{
    // Fast path - no readiness check
    return CreateNewEpoch(vector);
}
else
{
    // Multi-source - check readiness
    return CoordinateEpochCreation(vector);
}
```

---

## Potential Issues to Explore

1. **Deadlock**: Could sources deadlock waiting for each other?
2. **Fairness**: How to handle vastly different source rates?
3. **Dynamic Sources**: What if new source appears mid-epoch?
4. **Source Removal**: What if source stops producing?
5. **Memory**: Does bounded growth actually bound memory?

---

## Next Steps

1. ✅ Research plan created
2. ⏭️ Create `IEpochCoordinator` interface
3. ⏭️ Implement coordinator with readiness logic
4. ⏭️ Create tests for key scenarios
5. ⏭️ Validate fan-in (same scope)
6. ⏭️ Comparative analysis
7. ⏭️ Design documentation
