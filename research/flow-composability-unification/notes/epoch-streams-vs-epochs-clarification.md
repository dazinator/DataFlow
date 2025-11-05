# Research Note: Epoch Streams vs Epochs - Clarification and Multi-Source Implications

**Date**: 2025-11-05  
**Context**: PR Review Comment on Flow Composability Research  
**Issue**: Clarify distinction between epoch streams (carry epoch vector) and epochs (transaction boundaries)

## Executive Summary

The research prototype correctly demonstrates epoch-aware blocks for **single-source scenarios**. However, a critical distinction must be clarified:

- **Epoch Streams**: Data carriers with `EpochVector` metadata that may merge/evolve
- **Epochs (Transaction Boundaries)**: Begin at `OnEpochCreatedAsync`, end at `OnGlobalEpochAlignedAsync`

For multi-source scenarios with merges, lifecycle-aware blocks are needed to respect actual transaction boundaries rather than just epoch stream boundaries.

## Questions Raised

1. **Epoch Streams ≠ Epochs**: Epoch streams carry epoch vector information but aren't necessarily transaction boundaries themselves
2. **Epoch Vector Evolution**: When epoch streams merge, vectors mutate to incorporate ancestry via subsumption
3. **Impact on EpochBatchBlock**: Should batches respect epoch stream boundaries or actual epoch boundaries (global alignment)?
4. **Multi-Source Segmentation**: With decoupled segmentation, how do multiple plain sources work?
5. **Dynamic Sources**: What happens with producer groups (e.g., 4 sources, max 2 concurrent)?

## Key Findings

### 1. Epoch Streams vs Transaction Boundaries

**Two Distinct Concepts**:

| Aspect | Epoch Stream | Epoch (Transaction Boundary) |
|--------|--------------|------------------------------|
| **Type** | `IEpochStream<T>` | Lifecycle events |
| **Purpose** | Data carrier + metadata | Transaction boundary |
| **Lifetime** | While items flow | `OnEpochCreatedAsync` → `OnGlobalEpochAlignedAsync` |
| **Evolution** | Vector mutates at merges | Expands to subsume ancestors |
| **Safety** | NOT a transaction boundary | SAFE to commit |

**Code Evidence**:
- `EpochVector.Subsumes()` - Detects ancestry relationships
- `FindMostSpecificAncestor()` - Promotes context at merge points  
- `WriteContextBlock` - Reuses DbContext when epochs merge

### 2. Decoupled Segmentation Simplifies Multi-Source

**Old Model (Source-Centric)**:
- Each source creates its own epochs
- Merge points create complex ancestry
- Requires tracking subsumption throughout

**New Model (Decoupled)**:
- Segmentation external to sources
- Can unify sources BEFORE segmentation
- Ancestry only needed if sources segmented separately

**Recommended Pattern**:
```csharp
// Simplest: Unify then segment
Source1 ─┐
        ├→ UnionBlock → EpochSegmenter("unified") → EpochBlocks
Source2 ─┘
// Result: Single-source epochs, no ancestry
```

### 3. EpochBatchBlock - Two Variants Needed

**Current Prototype** (`SimpleEpochBatchBlock`):
- Batches within each epoch stream boundary
- Works for single-source scenarios
- May create small batches at merge points

**Future Extension** (`TransactionalEpochBatchBlock`):
- Batches until `OnGlobalEpochAlignedAsync`
- Requires `IEpochLifecycleParticipant`
- Respects actual transaction boundaries
- Better for multi-source with merges

### 4. Producer Groups with Decoupled Segmentation

**Recommendation**: Segment at group level
```csharp
ProducerGroup(4 producers, max 2 concurrent) → EpochSegmenter("group") → Downstream
```
- All producers share single sourceId
- No per-producer tracking needed
- Aligns with decoupled philosophy

## Implications for Research

### Scope Clarification

**What the Prototype Demonstrates**:
- ✅ Epoch-aware blocks for single-source pipelines
- ✅ Composability patterns (plain, epoch, mixed)
- ✅ Basic epoch boundary preservation

**What Requires Future Work**:
- ⏳ Lifecycle-aware variants for multi-source
- ⏳ Context promotion at merge points  
- ⏳ Transactional batching across merged streams

### Research Findings Remain Valid

The core conclusion is **still correct**: Epoch-aware wrapper blocks solve the composability gap.

**Clarification**: The prototype focuses on the most common case (single-source), which the decoupled segmentation model makes even more prevalent.

## Recommendations

### 1. Update ADR with Clarification

Add section distinguishing epoch streams from transaction boundaries and noting single-source focus.

### 2. Document Multi-Source Patterns

Explain unified-then-segment pattern as the recommended approach for multi-source scenarios.

### 3. Note Lifecycle-Aware Variants

Document that lifecycle integration is needed for multi-source scenarios where epochs merge.

### 4. Add Research Note to Handover

Include this analysis in the handover folder so implementers understand the scope and future extensions.

## Conclusion

**The research successfully validates the epoch-aware block pattern for composability.**

**Scope**: Prototype demonstrates single-source scenarios (the common case, especially with decoupled segmentation).

**Extension**: Multi-source scenarios with merges require lifecycle-aware variants - a natural future enhancement building on this foundation.

The decoupled segmentation design actually **simplifies** the common path by encouraging unified-then-segment patterns that avoid complex ancestry tracking.
