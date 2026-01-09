# Research: Clarify Status of EpochSegmenterBlock

**Status**: Complete  
**Created**: 2026-01-09  
**Research Duty**: Following `.team/duties/RESEARCH_DUTY.md`

---

## Executive Summary

This research investigates whether `EpochSegmenterBlock` and `EpochSegmentationPolicy` are superseded by the mandatory epochs architecture introduced in ADR 2025-11-20.

**Finding**: ⚠️ **PARTIALLY OBSOLETE** - Requires refactoring, not removal

**Key Insight**: While single-epoch wrapping is superseded by `PlainSourceAdapter` and `SingleEpochExtensions.WrapInSingleEpoch()`, the multi-epoch segmentation capabilities (count-based, key-based, clock-based, custom) have **NO REPLACEMENT** in the current mandatory epochs architecture.

**Recommendation**: ✅ **REFACTOR** to align with mandatory epochs while preserving capabilities

---

## Research Objective

Validate the hypothesis that `EpochSegmenterBlock` relates to an earlier implementation where epochs were optional components added at block level, and that it's superseded by:
- `ConfigureEpochs()` API for graph-level epoch configuration
- `EpochSourceBlock` for native epoch stream production
- `PlainSourceAdapter` for single-epoch wrapping

Then determine:
1. How to remove it from the codebase (if obsolete)
2. Which tests/benchmarks provide unique value and should be preserved

---

## Research Questions & Answers

### 1. Is EpochSegmenterBlock superseded by newer architecture?

**Answer**: ⚠️ **PARTIALLY**

**Superseded Capabilities**:
- ✅ `SegmentationMode.None` - Single-epoch wrapping
  - Replaced by: `PlainSourceAdapter`, `SingleEpochExtensions.WrapInSingleEpoch()`

**NOT Superseded Capabilities**:
- ❌ `SegmentationMode.Count` - Count-based segmentation (100 items per epoch)
- ❌ `SegmentationMode.Key` - Key-based segmentation (group by business key)
- ❌ `SegmentationMode.Clock` - Clock-based segmentation (time windows)
- ❌ `SegmentationMode.Custom` - Custom segmentation logic

**Critical Finding**: There is NO REPLACEMENT for multi-epoch segmentation in the mandatory epochs architecture.

### 2. What is the architectural relationship?

**Phase 1: Optional Epochs (Original)**
```
Plain Stream → [Optional: EpochSegmenterBlock] → Plain/Epoch Processing
```

**Phase 2: Mandatory Epochs (Current - ADR 2025-11-20)**
```
Plain Source → PlainSourceAdapter → Single Epoch Stream → Epoch Processing
```

**The Gap**: How to segment into MULTIPLE epochs?
```
Plain Source → ??? → Multiple Epoch Streams → Epoch Processing
```

**Current Answer**: Use `EpochSegmenterBlock` (but this doesn't align with mandatory epochs principle)

### 3. What legitimate use cases exist for multi-epoch segmentation?

**Use Case 1: Memory/Resource Management**
```csharp
// Process large dataset in chunks of 1000 items per epoch
SegmentBy.Count(1000, "source")
```

**Use Case 2: Transaction Boundaries**
```csharp
// Commit database transaction after every 500 items
SegmentBy.Count(500, "db-source")
```

**Use Case 3: Business Key Grouping**
```csharp
// Process all orders for each customer together
SegmentBy.Key<Order, string>(o => o.CustomerId, "orders")
```

**Use Case 4: Time Windowing**
```csharp
// Process data in hourly epochs
SegmentBy.Clock(hourlyEpochClock, "events")
```

### 4. Why not push segmentation into sources?

**Problems with source-level segmentation**:

1. **Separation of Concerns**: Sources know data production, not segmentation strategy
2. **Reusability**: Same source needs different segmentation in different pipelines
3. **Composability**: Can't easily change segmentation without changing source

**Example**:
```csharp
// Same source, different segmentation needs
Pipeline A: CustomerSource → Segment(Count:100) → ProcessA
Pipeline B: CustomerSource → Segment(Key:CustomerId) → ProcessB
Pipeline C: CustomerSource → Segment(Clock:Hourly) → ProcessC

// Pushing into source = 3 source implementations
// Keeping segmentation separate = 1 source + 3 configurations
```

### 5. How should this be refactored?

**Recommended Solution**: Refactor signature to align with mandatory epochs

**Current (Misaligned)**:
```csharp
// Implies "adding" epochs to plain streams
EpochSegmenterBlock<T> : BlockBase<T, IEpochStream<T>>
```

**Proposed (Aligned)**:
```csharp
// Clear: re-segments existing epoch streams
SegmentationBlock<T> : BlockBase<IEpochStream<T>, IEpochStream<T>>
```

**Migration Pattern**:
```csharp
// OLD
plainStream → EpochSegmenterBlock(ByCount(100)) → processing

// NEW
plainStream 
    → WrapInSingleEpoch("source")      // Now epoch stream
    → SegmentationBlock(ByCount(100))   // Re-segment
    → processing
```

### 6. Which tests/benchmarks should be preserved?

**Answer**: ✅ **ALL TESTS AND BENCHMARKS** provide unique value

**Tests to Keep (Refactor)**:
- Count-based segmentation tests - Validate batch boundaries
- Key-based segmentation tests - Validate grouping logic
- Clock-based segmentation tests - Validate time windowing
- Multi-source coordination tests - Validate epoch vectors
- Performance tests - Validate segmentation overhead

**Tests to Remove**: None

**Benchmarks to Keep**:
- `DecoupledEpochBenchmark.cs` - Segmentation overhead measurement
- `EpochAwareBlockBenchmark.cs` - Integrated pipeline performance
- `BatchBlockComparisonBenchmark.cs` - Comparison with batching

**Benchmarks to Remove**: None

---

## Approaches Explored

### Approach 1: Remove Entirely (Rejected)

**Rationale**: EpochSegmenterBlock is obsolete, remove it

**Pros**:
- Clean mandatory epochs architecture
- Simplifies codebase

**Cons**:
- ❌ **LOSES CRITICAL CAPABILITIES** without replacement
- ❌ No way to do multi-epoch segmentation
- ❌ Reduces composability
- ❌ Forces complex source implementations

**Verdict**: ❌ Not recommended - removes valuable functionality

### Approach 2: Keep As-Is (Rejected)

**Rationale**: It still works, don't change it

**Pros**:
- No migration needed
- All capabilities preserved
- Tests work as-is

**Cons**:
- Conceptually misaligned with mandatory epochs
- Documentation suggests epochs are "optional add-ons"
- Doesn't integrate with `ConfigureEpochs()` API

**Verdict**: ❌ Not recommended - perpetuates architectural inconsistency

### Approach 3: Refactor to SegmentationBlock (Recommended)

**Rationale**: Align with mandatory epochs while preserving capabilities

**Changes**:
1. Rename: `EpochSegmenterBlock` → `SegmentationBlock`
2. Signature change: `BlockBase<T, IEpochStream<T>>` → `BlockBase<IEpochStream<T>, IEpochStream<T>>`
3. Semantic: "re-segments epoch streams" not "adds epochs to plain streams"
4. Deprecation: Mark old block obsolete with migration guidance

**Pros**:
- ✅ Aligns with mandatory epochs (input is already epochs)
- ✅ Preserves all segmentation capabilities
- ✅ Clear conceptual model (re-segmentation)
- ✅ Works with both native epoch sources and wrapped plain sources
- ✅ Smooth migration path

**Cons**:
- Breaking change (mitigated with deprecation period)
- Requires test updates

**Verdict**: ✅ **RECOMMENDED** - best balance

---

## Recommended Approach

**Make segmentation a first-class concept in mandatory epochs architecture through refactoring**

### Implementation Strategy

**Phase 1: Add SegmentationBlock (v2.1)**

1. **Create `SegmentationBlock<T>`**:
   - Signature: `BlockBase<IEpochStream<T>, IEpochStream<T>>`
   - Accepts epoch streams as input (mandatory epochs principle)
   - Re-segments based on policy
   - Preserves all current segmentation modes

2. **Keep `EpochSegmentationPolicy`** (maybe rename to `SegmentationPolicy`):
   - All existing modes preserved
   - Document as "re-segmentation policies"

3. **Mark `EpochSegmenterBlock` as `[Obsolete]`**:
   ```csharp
   [Obsolete("Use SegmentationBlock<T> instead. " +
             "For single-epoch wrapping, use PlainSourceAdapter or WrapInSingleEpoch(). " +
             "For multi-epoch segmentation: plainStream.WrapInSingleEpoch() → SegmentationBlock")]
   ```

4. **Add `BlockHelpers.CreateSegmentation<T>()` method**

5. **Update all tests** to use new pattern

**Phase 2: Migration Period (v2.1-v2.x, 6-12 months)**

1. Support both APIs with deprecation warnings
2. Provide migration documentation
3. Update examples
4. Encourage migration

**Phase 3: Remove EpochSegmenterBlock (v3.0)**

1. Remove obsolete block type
2. Keep `SegmentationBlock<T>` as unified solution

### Migration Pattern

```csharp
// BEFORE (v2.0)
var plainStream = ProducePlainItems();
var segmenter = BlockHelpers.CreateEpochSegmenter<int>(
    "segmenter", 
    EpochSegmentationPolicy.ByCount(100, "source"));
var epochStreams = segmenter.ExecuteAsync(plainStream, context);

// AFTER (v2.1+)
var plainStream = ProducePlainItems();
var wrappedStream = plainStream.WrapInSingleEpoch("source", context.CancellationToken);
var segmenter = BlockHelpers.CreateSegmentation<int>(
    "segmenter", 
    SegmentationPolicy.ByCount(100));  // Source name in epoch already
var epochStreams = segmenter.ExecuteAsync(wrappedStream, context);
```

---

## Success Metrics Results

### Quantitative ✅

- ✅ **Usage sites identified**: 15+ test files, 5 benchmark files, 2 production files
- ✅ **Code to refactor**: ~330 lines (EpochSegmenterBlock.cs + EpochSegmentationPolicy.cs)
- ✅ **Tests affected**: ~15 test files (all preserved, just updated)
- ✅ **Benchmarks affected**: ~5 files (all preserved)
- ✅ **Breaking changes**: Signature change (mitigated with deprecation)

### Qualitative ✅

- ✅ **Architectural clarity**: Refactoring aligns segmentation with mandatory epochs
- ✅ **Capability preservation**: All segmentation modes preserved
- ✅ **Migration path**: Clear two-phase approach (deprecate → remove)
- ✅ **Composability**: Improved (sources stay simple, segmentation is separate concern)
- ✅ **Test value**: All tests provide unique coverage, none redundant

---

## Implementation Guidance

### For Implementation Duty

See `handover/README.md` for complete implementation specifications.

**Key Tasks**:

1. **Create `SegmentationBlock<T>`**:
   - Copy from `EpochSegmenterBlock.cs`
   - Change signature to `BlockBase<IEpochStream<T>, IEpochStream<T>>`
   - Update implementation to accept epoch streams
   - Flatten/unwrap input epoch stream, then re-segment

2. **Update (or rename) `EpochSegmentationPolicy`**:
   - Consider renaming to `SegmentationPolicy`
   - Update XML docs to clarify "re-segmentation"
   - Keep all existing modes

3. **Mark `EpochSegmenterBlock` obsolete**:
   - Add `[Obsolete]` attribute with migration message
   - Update XML docs with migration guidance

4. **Add test helper**:
   - `BlockHelpers.CreateSegmentation<T>(name, policy)`

5. **Update all tests**:
   - Add explicit `WrapInSingleEpoch()` calls before segmentation
   - Update `CreateEpochSegmenter` calls to `CreateSegmentation`

6. **Update benchmarks**:
   - Same pattern as tests

7. **Create migration guide**:
   - Document in `/poc/MIGRATION_GUIDE.md`
   - Add examples for each segmentation mode

8. **Update documentation**:
   - Rename design doc: `epoch-segmenter-block.md` → `segmentation-block.md`
   - Update guides and glossary
   - Create ADR for refactoring

### Testing Requirements

All existing tests should pass with updates:
- Update segmentation tests to use new signature
- Validate migration pattern works
- No new test categories needed (all cases already covered)

---

## References

### Research Artifacts

- **Research Plan**: `research-plan.md`
- **Usage Analysis**: `notes/usage-analysis.md`
- **Architectural Analysis**: `notes/architectural-analysis.md`
- **Implementation Handover**: `handover/README.md`

### Related ADRs

- **Mandatory Epochs**: `/docs/adr/poc/2025-11-20-mandatory-epochs-unified-architecture.md`
- **Epoch Aware Blocks**: `/docs/adr/poc/2025-11-05-epoch-aware-block-pattern.md`

### Related Research

- **Mandatory Epochs**: `/research/mandatory-epochs/`
- **Epoch Stream Separation**: `/research/epoch-stream-separation/`

---

## Conclusions

### Key Findings

1. **Not Fully Obsolete**: `EpochSegmenterBlock` is partially superseded, but multi-epoch segmentation capabilities have no replacement

2. **Architectural Misalignment**: Block's signature (`T → IEpochStream<T>`) doesn't align with mandatory epochs principle (all streams are already epochs)

3. **Legitimate Use Cases**: Count-based, key-based, clock-based, and custom segmentation are all valuable capabilities

4. **Gap in Architecture**: Mandatory epochs architecture lacks a pattern for multi-epoch segmentation

5. **Refactoring Solution**: Change signature to `IEpochStream<T> → IEpochStream<T>` to align with mandatory epochs while preserving all capabilities

### Recommendation

✅ **REFACTOR** `EpochSegmenterBlock` to `SegmentationBlock` with epoch-to-epoch signature

**Rationale**:
1. Preserves all critical segmentation capabilities
2. Aligns with mandatory epochs architecture
3. Improves conceptual clarity (re-segmentation vs adding epochs)
4. Maintains composability (sources stay simple)
5. Clear migration path with deprecation period

### Next Steps

1. ✅ Create implementation handover work item
2. ✅ Document complete specifications
3. ✅ Submit self-improvement feedback
4. ✅ Hand over to implementation duty

---

## Deliverables

### Documentation Created

1. ✅ Research plan (`research-plan.md`)
2. ✅ Usage analysis (`notes/usage-analysis.md`)
3. ✅ Architectural analysis (`notes/architectural-analysis.md`)
4. ✅ This README (research findings)
5. ✅ Implementation handover (`handover/README.md`)

### Prototype Code

**Note**: No prototype code needed for this research. The refactoring is straightforward:
- Copy existing `EpochSegmenterBlock`
- Change signature
- Update implementation

All can be done directly in implementation phase.

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-01-09 | Initial research completed |
