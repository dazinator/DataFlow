# [Implementation] Refactor EpochSegmenterBlock to SegmentationBlock

## Context and Objectives

### Problem Statement

`EpochSegmenterBlock` provides critical multi-epoch segmentation capabilities (count-based, key-based, clock-based, custom), but its signature (`BlockBase<T, IEpochStream<T>>`) is misaligned with the mandatory epochs architecture, where ALL streams are already epoch streams.

The block's name and signature suggest it "adds" epochs to plain streams, which contradicts the architectural principle that epochs are mandatory and native to the system.

### Research Background

Research was conducted to determine if `EpochSegmenterBlock` was superseded by the mandatory epochs architecture.

**Research Issue**: [Link to research issue]
**Research Documentation**: `/research/epoch-segmenter-clarification/`

**Key Research Artifacts**:
- Main findings: `/research/epoch-segmenter-clarification/README.md`
- Architectural analysis: `/research/epoch-segmenter-clarification/notes/architectural-analysis.md`
- Usage analysis: `/research/epoch-segmenter-clarification/notes/usage-analysis.md`
- Implementation specs: `/research/epoch-segmenter-clarification/handover/README.md`

**Key Findings from Research**:
1. **Partially Obsolete**: Single-epoch wrapping (SegmentationMode.None) is superseded by `PlainSourceAdapter`/`WrapInSingleEpoch()`
2. **Critical Capabilities NOT Superseded**: Multi-epoch segmentation (count, key, clock, custom) has NO REPLACEMENT in current architecture
3. **Architectural Misalignment**: Signature suggests "adding" epochs, but all streams are already epochs in mandatory epochs architecture
4. **Solution**: Refactor signature to `BlockBase<IEpochStream<T>, IEpochStream<T>>` to align with architecture while preserving capabilities

### Objectives

- [ ] Create new `SegmentationBlock<T>` with epoch-to-epoch signature
- [ ] Preserve all segmentation capabilities (Count, Key, Clock, Custom, None modes)
- [ ] Mark `EpochSegmenterBlock` as `[Obsolete]` with migration guidance
- [ ] Update all tests to use new pattern
- [ ] Update all benchmarks to use new pattern
- [ ] Create comprehensive migration guide
- [ ] Update design documentation
- [ ] Create ADR documenting refactoring decision

## Implementation Guidance

### Recommended Approach

**Refactor, don't remove** - `EpochSegmenterBlock` provides valuable functionality but needs architectural alignment.

**Key Change**: Update signature from plain-to-epoch (`T → IEpochStream<T>`) to epoch-to-epoch (`IEpochStream<T> → IEpochStream<T>`)

**Key Principles**:
1. **Preserve All Capabilities**: All segmentation modes must continue to work
2. **Align with Mandatory Epochs**: Accept epoch streams as input (since all streams are epochs)
3. **Clear Semantics**: Re-segment existing epoch streams, not "add" epochs
4. **Smooth Migration**: Deprecation period with clear migration path
5. **No Performance Regression**: Refactored block should perform equivalently

### Design References

Supporting documentation:
- **Research Findings**: `/research/epoch-segmenter-clarification/README.md`
- **Implementation Specs**: `/research/epoch-segmenter-clarification/handover/README.md`
- **Architectural Analysis**: `/research/epoch-segmenter-clarification/notes/architectural-analysis.md`
- **Mandatory Epochs ADR**: `/docs/adr/poc/2025-11-20-mandatory-epochs-unified-architecture.md`

### API Design

#### New SegmentationBlock (Aligned with Mandatory Epochs)

```csharp
/// <summary>
/// Re-segments epoch streams based on segmentation policy.
/// In mandatory epochs architecture, all streams are already epochs.
/// This block re-segments existing epoch streams into new epoch boundaries.
/// </summary>
public sealed class SegmentationBlock<T> : BlockBase<IEpochStream<T>, IEpochStream<T>>
{
    private readonly SegmentationPolicy _policy;
    
    public SegmentationBlock(IBlockContext context, SegmentationPolicy policy)
        : base(context)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }
    
    public override async IAsyncEnumerable<IEpochStream<T>> ExecuteAsync(
        IAsyncEnumerable<IEpochStream<T>> input,
        IExecutionContext context)
    {
        // 1. Flatten input epoch streams to plain items
        // 2. Apply segmentation policy
        // 3. Yield re-segmented epoch streams
    }
}
```

#### Obsolete EpochSegmenterBlock (Compatibility)

```csharp
[Obsolete(
    "EpochSegmenterBlock is deprecated. Use SegmentationBlock<T> for multi-epoch segmentation. " +
    "For single-epoch wrapping, use PlainSourceAdapter or WrapInSingleEpoch(). " +
    "Migration: plainStream.WrapInSingleEpoch(\"source\") → SegmentationBlock(policy). " +
    "Will be removed in v3.0.",
    error: false)]
public sealed class EpochSegmenterBlock<T> : BlockBase<T, IEpochStream<T>>
{
    // Keep existing implementation for compatibility period
}
```

### Migration Pattern

**Before (v2.0)**:
```csharp
var plainStream = ProducePlainItems();
var segmenter = BlockHelpers.CreateEpochSegmenter<int>(
    "segmenter", 
    EpochSegmentationPolicy.ByCount(100, "source"));
var epochStreams = segmenter.ExecuteAsync(plainStream, context);
```

**After (v2.1+)**:
```csharp
var plainStream = ProducePlainItems();
var singleEpochStream = plainStream.WrapInSingleEpoch("source", context.CancellationToken);
var segmenter = BlockHelpers.CreateSegmentation<int>(
    "segmenter", 
    SegmentationPolicy.ByCount(100));
var epochStreams = segmenter.ExecuteAsync(singleEpochStream, context);
```

### Component Architecture

```
┌─────────────────┐
│  Plain Stream   │
└────────┬────────┘
         │
         ▼
┌────────────────────────┐
│ WrapInSingleEpoch()    │ (Mandatory epochs - always epoch streams)
└────────┬───────────────┘
         │
         ▼
┌────────────────────────┐
│  Single Epoch Stream   │
└────────┬───────────────┘
         │
         ▼
┌────────────────────────┐
│  SegmentationBlock     │ (Re-segments existing epoch streams)
└────────┬───────────────┘
         │
         ▼
┌────────────────────────┐
│ Multiple Epoch Streams │
└────────────────────────┘
```

### Key Implementation Considerations

1. **Input Flattening**: Must unwrap input epoch streams before applying segmentation
2. **Source ID Handling**: Preserve source ID from input epochs or use policy source ID
3. **Cancellation**: Properly propagate cancellation through flattening and segmentation
4. **Resource Cleanup**: Ensure epoch streams are properly disposed
5. **Backward Compatibility**: Keep obsolete block working during deprecation period
6. **Test Updates**: ~15 test files need mechanical updates (add WrapInSingleEpoch calls)
7. **Benchmark Updates**: ~5 benchmark files need same updates
8. **Documentation**: Update design docs, create migration guide, write ADR

## Test Scenarios

All existing test scenarios must pass after migration:

### Critical Test Scenarios

1. **Count-Based Segmentation**
   - Input: 10 items
   - Policy: ByCount(3)
   - Expected: 4 epochs (3+3+3+1 items)

2. **Key-Based Segmentation**
   - Input: Items with keys [A, A, B, B, C]
   - Policy: ByKey(x => x.key)
   - Expected: 3 epochs (A, B, C groups)

3. **Clock-Based Segmentation**
   - Input: Stream with epoch clock
   - Policy: ByClock(epochClock)
   - Expected: Epochs aligned with clock boundaries

4. **None Mode (Pass-through)**
   - Input: Single epoch stream
   - Policy: None
   - Expected: Single epoch output (no re-segmentation)

5. **Multi-Source Coordination**
   - Input: Multiple sources with different epoch vectors
   - Expected: Correct epoch vector handling in output

6. **Empty Input**
   - Input: Empty epoch stream
   - Expected: No output epochs, no errors

7. **Cancellation**
   - Input: Long-running stream
   - Action: Cancel midway
   - Expected: Clean cancellation, resources cleaned up

### Performance Validation

- Benchmark segmentation overhead (should be equivalent to current)
- Validate no regression in integrated pipeline benchmarks
- Target: <5% overhead compared to current implementation

## Performance Requirements

- **Throughput**: Equivalent to current EpochSegmenterBlock
- **Memory**: No significant increase in memory usage
- **Latency**: Flattening + re-segmentation overhead should be minimal
- **Validation**: Run existing benchmarks, compare results

## Implementation Checklist

See complete checklist in `/research/epoch-segmenter-clarification/handover/README.md`

**Summary**:
1. [ ] Create `SegmentationBlock<T>` with new signature
2. [ ] Update/rename `SegmentationPolicy` (optional)
3. [ ] Mark `EpochSegmenterBlock` as `[Obsolete]`
4. [ ] Add `BlockHelpers.CreateSegmentation<T>()` method
5. [ ] Update ~15 test files (add WrapInSingleEpoch, change helper call)
6. [ ] Update ~5 benchmark files (same pattern)
7. [ ] Create migration guide in `/poc/MIGRATION_GUIDE.md`
8. [ ] Update design documentation
9. [ ] Create ADR: `2026-01-09-segmentation-block-refactoring.md`
10. [ ] Validate all tests pass
11. [ ] Validate benchmarks show no regression
12. [ ] Code review and merge

## Timeline and Phases

### Phase 1: Deprecation (v2.1)
- Add `SegmentationBlock`
- Mark `EpochSegmenterBlock` obsolete
- Both blocks work

### Phase 2: Migration Period (v2.1-v2.x, 6-12 months)
- Support both APIs
- Encourage migration via warnings
- Update documentation

### Phase 3: Removal (v3.0)
- Remove `EpochSegmenterBlock`
- `SegmentationBlock` is the unified solution

## Design Decisions

### Why Not Remove Entirely?

Multi-epoch segmentation capabilities (count, key, clock, custom) have NO REPLACEMENT in the mandatory epochs architecture. Removing would lose critical functionality.

### Why Change Signature?

Current signature (`T → IEpochStream<T>`) suggests "adding" epochs to plain streams, which contradicts mandatory epochs principle. New signature (`IEpochStream<T> → IEpochStream<T>`) makes it clear: re-segment existing epoch streams.

### Why Not Push Segmentation into Sources?

Violates separation of concerns:
- Sources know data production, not segmentation
- Same source may need different segmentation in different pipelines
- Reduces reusability and composability

### Alternatives Considered

1. **Remove Entirely**: ❌ Loses critical capabilities
2. **Keep As-Is**: ❌ Perpetuates architectural misalignment
3. **Integrate with ConfigureEpochs()**: 🤔 Possible future enhancement, but complex
4. **Refactor to SegmentationBlock**: ✅ **CHOSEN** - best balance

## Related Issues

- Research Issue: [Link when created]
- Mandatory Epochs Implementation: [Link to previous implementation issue]

## Labels

- `workflow:implementation`
- `type:refactoring`
- `area:poc`
- `priority:medium`
- `breaking-change` (v3.0)

## Estimated Effort

**3-5 days** (based on research analysis)

- Day 1: Implement `SegmentationBlock<T>` and mark old block obsolete
- Day 2: Update test helpers and 50% of tests
- Day 3: Update remaining tests and benchmarks  
- Day 4: Update documentation and create migration guide
- Day 5: Review, validation, and ADR creation
