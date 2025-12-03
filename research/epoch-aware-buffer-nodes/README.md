# Epoch-Aware Buffer Nodes Research - Index

**Research Date**: 2025-12-03  
**Status**: ✅ Complete  
**Issue**: Tech Debt: Design epoch-aware buffer nodes

---

## Quick Navigation

### 🎯 Start Here

1. **[SUMMARY.md](SUMMARY.md)** - Executive summary of research and recommendation
2. **[RECOMMENDATION.md](RECOMMENDATION.md)** - Detailed final recommendation and implementation plan

### 📋 For Implementation

1. **[handover/README.md](handover/README.md)** - Complete implementation specification
2. **[handover/IMPLEMENTATION_WORK_ITEM.md](handover/IMPLEMENTATION_WORK_ITEM.md)** - Ready-to-use work item template
3. **[prototypes/](prototypes/)** - Working prototype code

### 📊 Research Analysis

1. **[analysis.md](analysis.md)** - Initial analysis of all three options
2. **[prototypes/option-2-analysis.md](prototypes/option-2-analysis.md)** - Deep dive on Option 2 (rejected)
3. **[prototypes/option-3a-design.md](prototypes/option-3a-design.md)** - Detailed design for Option 3A (recommended)
4. **[prototypes/option-3c-design.md](prototypes/option-3c-design.md)** - Detailed design for Option 3C (recommended)

---

## Research Artifacts by Purpose

### Understanding the Problem

**File**: [analysis.md](analysis.md)  
**Contents**:
- Background on buffer nodes and epoch streams
- Problem statement
- Initial evaluation of three options
- Evaluation criteria

### Option Evaluation

| Option | File | Verdict |
|--------|------|---------|
| Option 1: Route containers | [analysis.md](analysis.md#option-1) | ❌ Rejected |
| Option 2: Unwrap-buffer-rewrap | [prototypes/option-2-analysis.md](prototypes/option-2-analysis.md) | ❌ Not recommended |
| Option 3A: Epoch buffer block | [prototypes/option-3a-design.md](prototypes/option-3a-design.md) | ✅ Recommended |
| Option 3C: Plain types only | [prototypes/option-3c-design.md](prototypes/option-3c-design.md) | ✅ Recommended |

### Prototype Code

**Location**: [prototypes/](prototypes/)

| File | Purpose |
|------|---------|
| `EpochBufferBlock.cs` | Core implementation (~120 LOC) |
| `EpochBufferBlockExtensions.cs` | Graph builder API (~70 LOC) |
| `EpochBufferBlockTests.cs` | Test scenarios (~300 LOC) |

**Status**: Working prototype, validates feasibility

### Implementation Handover

**Location**: [handover/](handover/)

| File | Purpose |
|------|---------|
| `README.md` | Complete implementation specification |
| `IMPLEMENTATION_WORK_ITEM.md` | Work item template for implementation |

---

## Key Decisions

### Final Recommendation

**Option 3C + Option 3A**: Combined approach
- Keep existing `BufferNode<T>` for plain types only
- Implement new `EpochBufferBlock<T>` for epoch streams

### Rationale

1. **Low risk**: No breaking changes to existing code
2. **Clear semantics**: Each buffer type has clear purpose  
3. **Good performance**: Epoch buffers use optimized routing
4. **Future flexibility**: Independent evolution

### Design Decisions

| Decision | Choice | Location |
|----------|--------|----------|
| Buffering approach | Per-epoch channels | [option-3a-design.md](prototypes/option-3a-design.md#key-implementation-details) |
| Epoch boundaries | Strict preservation | [option-3a-design.md](prototypes/option-3a-design.md#behavior) |
| Integration | Standard block pattern | [option-3a-design.md](prototypes/option-3a-design.md#block-type-signature) |
| Capacity semantics | Per-epoch | [option-3a-design.md](prototypes/option-3a-design.md#per-epoch-buffering) |

---

## Implementation Details

### Effort Estimate

**Total**: 22 hours (~3 days)

| Phase | Effort | File Reference |
|-------|--------|----------------|
| Phase 1: Buffer Node Docs | 2 hours | [handover/README.md#phase-1](handover/README.md#phase-1-update-buffer-node-documentation) |
| Phase 2: EpochBufferBlock | 6 hours | [handover/README.md#phase-2](handover/README.md#phase-2-implement-epochbufferblock) |
| Phase 3: Testing | 8 hours | [handover/README.md#phase-3](handover/README.md#phase-3-add-comprehensive-tests) |
| Phase 4: Documentation | 6 hours | [handover/README.md#phase-4](handover/README.md#phase-4-documentation) |

### Risk Assessment

**Overall Risk**: Low

- No breaking changes
- New code only
- Proven pattern (BlockBase)
- Working prototype validates approach

---

## Testing Strategy

### Test Categories

From [handover/README.md](handover/README.md#phase-3-add-comprehensive-tests):

1. Basic functionality (epoch boundaries, multiple epochs)
2. Metadata preservation (epoch vectors, scopes)
3. Backpressure (capacity enforcement)
4. Edge strategies (fan-in, fan-out)
5. Error handling (cancellation, exceptions)
6. Performance (optional benchmarks)

### Prototype Tests

See [prototypes/EpochBufferBlockTests.cs](prototypes/EpochBufferBlockTests.cs) for test examples.

---

## Performance Implications

### Plain Types
No change - `BufferNode<T>` unchanged

### Epoch Streams
New capability with optimized routing:
- ✅ Uses `SingleTargetRouter<T>` (no dictionary lookups)
- ✅ Per-epoch bounded channels
- ⚠️ Per-epoch channel creation overhead

**Expected impact**: <10% vs direct connection

See [RECOMMENDATION.md#performance-implications](RECOMMENDATION.md#performance-implications)

---

## References

### Related Issues
- Original issue: Tech Debt: Design epoch-aware buffer nodes
- Issue #39: Epoch stream routing optimization
- Issue #48: Buffer node documentation

### Related Research
- `/research/tech-debt-2025-11-29/` - Original tech debt analysis
- `/research/unified-epoch-model/` - Epoch architecture

### Current Implementation
- `/poc/DataFlow.POC/Core/BufferNode.cs` - Current buffer nodes
- `/poc/DataFlow.POC/Core/EpochStream.cs` - Epoch stream interfaces
- `/poc/DataFlow.POC/Core/SingleTargetRouter.cs` - Routing optimization

---

## Document History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-12-03 | Initial research complete |

---

## Research Status

- ✅ Problem understood
- ✅ Options evaluated
- ✅ Prototype created
- ✅ Recommendation finalized
- ✅ Implementation spec complete
- ✅ Handover documentation ready
- ✅ Self-improvement feedback submitted
- ✅ Code review completed

**Ready for**: Implementation handover
