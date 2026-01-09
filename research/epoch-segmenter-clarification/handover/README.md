# Implementation Handover: Refactor EpochSegmenterBlock to SegmentationBlock

**Research Issue**: [Current Issue Number]
**Research Documentation**: `/research/epoch-segmenter-clarification/`
**Target**: POC Codebase

---

## Objective

Refactor `EpochSegmenterBlock` to align with the mandatory epochs architecture while preserving all segmentation capabilities.

**Key Change**: Update block signature from `BlockBase<T, IEpochStream<T>>` to `BlockBase<IEpochStream<T>, IEpochStream<T>>` to reflect that in mandatory epochs, all streams are already epoch streams.

---

## Background

The mandatory epochs architecture (ADR 2025-11-20) established that all data flows through epochs. `EpochSegmenterBlock`'s current signature (`T → IEpochStream<T>`) suggests it "adds" epochs to plain streams, which is conceptually misaligned.

However, the block provides critical multi-epoch segmentation capabilities (count-based, key-based, clock-based, custom) that have NO REPLACEMENT in the current architecture.

**Solution**: Refactor to `SegmentationBlock` with epoch-to-epoch signature, making it clear that it re-segments existing epoch streams.

---

## Success Criteria

- [ ] New `SegmentationBlock<T>` created with epoch-to-epoch signature
- [ ] All segmentation modes preserved (Count, Key, Clock, Custom, None)
- [ ] `EpochSegmenterBlock` marked `[Obsolete]` with migration guidance
- [ ] All tests updated to use new pattern
- [ ] All benchmarks updated to use new pattern
- [ ] Migration guide documented
- [ ] Design documentation updated
- [ ] All existing tests pass

---

## Implementation Checklist

### 1. Create SegmentationBlock<T>

**File**: `poc/DataFlow/Blocks/SegmentationBlock.cs`

**Signature**:
```csharp
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
        // Implementation strategy:
        // 1. Unwrap input epoch streams to get plain items
        // 2. Apply segmentation policy to create new epoch streams
        // 3. Yield re-segmented epoch streams
    }
}
```

**Implementation Notes**:
- Start by flattening input epoch streams to plain items
- Then apply existing segmentation logic (can reuse code from `EpochSegmenterBlock`)
- Handle source ID preservation (use first epoch's source ID or policy source ID)
- Handle cancellation correctly

**Key Difference from EpochSegmenterBlock**:
- Input is `IAsyncEnumerable<IEpochStream<T>>` not `IAsyncEnumerable<T>`
- Must unwrap first, then segment

### 2. Update/Rename SegmentationPolicy

**Option A: Rename (Recommended)**
- Rename `EpochSegmentationPolicy` → `SegmentationPolicy`
- Update XML docs to clarify "re-segmentation"
- Keep all existing modes and factory methods

**Option B: Keep Name**
- Keep `EpochSegmentationPolicy` name
- Just update XML docs for clarity

**File**: `poc/DataFlow/Blocks/SegmentationPolicy.cs` (if renaming)

**Changes**:
- Update XML docs to reference `SegmentationBlock` not `EpochSegmenterBlock`
- Clarify that policies define re-segmentation strategies
- Keep all existing modes: None, Count, Key, Clock, Custom, Time

### 3. Mark EpochSegmenterBlock Obsolete

**File**: `poc/DataFlow/Blocks/EpochSegmenterBlock.cs`

**Changes**:
```csharp
[Obsolete(
    "EpochSegmenterBlock is deprecated. Use SegmentationBlock<T> for multi-epoch segmentation. " +
    "For single-epoch wrapping, use PlainSourceAdapter or WrapInSingleEpoch(). " +
    "Migration: plainStream.WrapInSingleEpoch(\"source\") → SegmentationBlock(policy). " +
    "Will be removed in v3.0.",
    error: false)]
public sealed class EpochSegmenterBlock<T> : BlockBase<T, IEpochStream<T>>
{
    // Keep existing implementation for compatibility
}
```

**Update XML docs**:
```xml
/// <summary>
/// [DEPRECATED] Block that applies epoch segmentation to continuous data streams.
/// Use <see cref="SegmentationBlock{T}"/> instead.
/// </summary>
/// <remarks>
/// <para><strong>Migration Guide:</strong></para>
/// <para>OLD: plainStream → EpochSegmenterBlock → epochProcessing</para>
/// <para>NEW: plainStream → WrapInSingleEpoch() → SegmentationBlock → epochProcessing</para>
/// <para>See /poc/MIGRATION_GUIDE.md for complete migration examples.</para>
/// </remarks>
```

### 4. Update BlockHelpers

**File**: `poc/DataFlow.Tests/TestHelpers/BlockHelpers.cs`

**Add new helper**:
```csharp
/// <summary>
/// Creates a SegmentationBlock with a segmentation policy.
/// </summary>
public static SegmentationBlock<T> CreateSegmentation<T>(
    string name,
    SegmentationPolicy policy)
{
    return new SegmentationBlock<T>(new BlockContext(name), policy);
}
```

**Mark old helper obsolete** (or keep for compatibility):
```csharp
[Obsolete("Use CreateSegmentation<T> instead. Will be removed in v3.0.")]
public static EpochSegmenterBlock<T> CreateEpochSegmenter<T>(
    string name,
    EpochSegmentationPolicy policy)
{
    return new EpochSegmenterBlock<T>(new BlockContext(name), policy);
}
```

### 5. Update Tests

**Files to Update** (~15 files):
- `poc/DataFlow.Tests/DecoupledEpochTests.cs`
- `poc/DataFlow.Tests/EpochAwareBlockTests.cs`
- `poc/DataFlow.Tests/MultiSourceSegmentationTests.cs`
- `poc/DataFlow.Tests/EpochBufferBlockTests.cs`
- `poc/DataFlow.Tests/DecoupledEpochPerformanceTests.cs`
- `poc/DataFlow.Tests/BlockContextConstructorInjectionTests.cs`
- Other files using `CreateEpochSegmenter`

**Migration Pattern**:
```csharp
// BEFORE
var plainStream = ProducePlainItems(10);
var segmenter = BlockHelpers.CreateEpochSegmenter<int>(
    "segmenter", 
    EpochSegmentationPolicy.ByCount(3, "test-source"));
var epochStreams = segmenter.ExecuteAsync(plainStream, context);

// AFTER
var plainStream = ProducePlainItems(10);
var singleEpochStream = plainStream.WrapInSingleEpoch("test-source", context.CancellationToken);
var segmenter = BlockHelpers.CreateSegmentation<int>(
    "segmenter", 
    SegmentationPolicy.ByCount(3));  // Source ID already in epoch
var epochStreams = segmenter.ExecuteAsync(singleEpochStream, context);
```

**Notes**:
- Add explicit `WrapInSingleEpoch()` call after plain stream creation
- Change `CreateEpochSegmenter` → `CreateSegmentation`
- Update policy references if renamed
- Source ID now in epoch, not policy (for most cases)

### 6. Update Benchmarks

**Files to Update** (~5 files):
- `poc/DataFlow.Benchmarks/DecoupledEpochBenchmark.cs`
- `poc/DataFlow.Benchmarks/EpochAwareBlockBenchmark.cs`
- `poc/DataFlow.Benchmarks/BatchBlockComparisonBenchmark.cs`
- `poc/DataFlow.Benchmarks/ComplexEtlPOC.cs`
- `poc/DataFlow.Benchmarks/SimpleEtlPOC.cs`

**Same migration pattern as tests**

### 7. Create Migration Guide

**File**: `poc/MIGRATION_GUIDE.md` (append new section)

**Content**:
```markdown
## Migrating from EpochSegmenterBlock to SegmentationBlock

### Overview

`EpochSegmenterBlock` has been deprecated in favor of `SegmentationBlock` to align with
the mandatory epochs architecture. The key difference: `SegmentationBlock` accepts epoch
streams as input (not plain streams), since in mandatory epochs, all streams are already
epoch streams.

### Migration Patterns

#### Pattern 1: Count-Based Segmentation

**Before (v2.0)**:
```csharp
var plainStream = GetItems();
var segmenter = new EpochSegmenterBlock<int>(
    context, 
    EpochSegmentationPolicy.ByCount(100, "source"));
var epochs = segmenter.ExecuteAsync(plainStream, ctx);
```

**After (v2.1+)**:
```csharp
var plainStream = GetItems();
var singleEpoch = plainStream.WrapInSingleEpoch("source");
var segmenter = new SegmentationBlock<int>(
    context, 
    SegmentationPolicy.ByCount(100));
var epochs = segmenter.ExecuteAsync(singleEpoch, ctx);
```

[... more patterns ...]
```

### 8. Update Documentation

**Files to Update**:

1. **Design Doc**: `poc/docs/design/blocks/epoch-segmenter-block.md`
   - Rename to `segmentation-block.md`
   - Update all content to reference new signature
   - Add migration section

2. **Usage Guide**: `poc/docs/guides/using-epochs.md`
   - Update segmentation examples
   - Show `WrapInSingleEpoch()` → `SegmentationBlock` pattern

3. **Glossary**: `poc/docs/POC_GLOSSARY.md`
   - Update `EpochSegmenterBlock` entry (mark deprecated)
   - Add `SegmentationBlock` entry

4. **Buffer Guide**: `docs/guides/epoch-buffer-blocks.md`
   - Update references and examples

### 9. Create ADR

**File**: `docs/adr/poc/2026-01-09-segmentation-block-refactoring.md`

**Content**: Document refactoring rationale, signature change, and migration strategy

**Key Sections**:
- Context: Mandatory epochs architecture
- Problem: EpochSegmenterBlock signature misalignment
- Decision: Refactor to epoch-to-epoch signature
- Consequences: Breaking change with clear migration path
- Migration: Deprecation timeline (v2.1 → v3.0)

---

## Test Scenarios

All existing tests should pass after updates. Key scenarios to validate:

### Scenario 1: Count-Based Segmentation
- Plain stream → single epoch → segment by count → validate epoch boundaries

### Scenario 2: Key-Based Segmentation
- Plain stream → single epoch → segment by key → validate grouping

### Scenario 3: Clock-Based Segmentation
- Plain stream → single epoch → segment by clock → validate time windows

### Scenario 4: None Mode (Pass-through)
- Plain stream → single epoch → segment(None) → validate single epoch output

### Scenario 5: Multi-Source Coordination
- Multiple sources → segmentation → validate epoch vectors

### Scenario 6: Backward Compatibility
- Verify obsolete `EpochSegmenterBlock` still works with warning

---

## Performance Requirements

No performance regression expected:
- Unwrapping input epoch + re-segmenting should have same cost as original segmentation
- Benchmarks should show equivalent performance
- If overhead > 5%, investigate and optimize

---

## Migration Timeline

| Version | Status | Actions |
|---------|--------|---------|
| v2.1 | Deprecation | Add SegmentationBlock, deprecate EpochSegmenterBlock |
| v2.1-v2.x | Migration | Both blocks work, encourage migration |
| v3.0 | Removal | Remove EpochSegmenterBlock |

**Timeline**: 6-12 months between v2.1 and v3.0

---

## Design References

- **Research**: `/research/epoch-segmenter-clarification/README.md`
- **Architectural Analysis**: `/research/epoch-segmenter-clarification/notes/architectural-analysis.md`
- **Mandatory Epochs ADR**: `/docs/adr/poc/2025-11-20-mandatory-epochs-unified-architecture.md`

---

## Implementation Notes

### Key Implementation Challenge

**Challenge**: Flattening input epoch streams before re-segmentation

**Consideration**: Should we preserve epoch boundaries from input, or completely re-segment?

**Recommended Approach**: Completely re-segment (flatten then segment)
- Simpler implementation
- Clear semantics: "re-segment this data according to new policy"
- Original epoch boundaries are typically not meaningful after segmentation

**Alternative**: Respect input epoch boundaries (segment within each input epoch)
- More complex
- Unclear use case
- Can be added later if needed

### Source ID Handling

**Question**: What source ID to use for output epochs?

**Options**:
1. Use policy's source ID (if provided)
2. Use first input epoch's source ID
3. Create new source ID based on segmenter name

**Recommended**: Use policy's source ID if provided, else use first input epoch's source ID

### Error Handling

- Validate policy configuration (same as current)
- Handle empty input epoch streams
- Propagate cancellation correctly
- Ensure resources cleaned up properly

---

## Risks and Mitigations

### Risk 1: Breaking Changes

**Mitigation**: 
- Deprecation period (not immediate removal)
- Keep old block working with warnings
- Comprehensive migration guide
- Clear error messages

### Risk 2: Test Update Effort

**Mitigation**:
- Straightforward mechanical updates
- Pattern is consistent across all tests
- Can be done incrementally

### Risk 3: User Confusion

**Mitigation**:
- Clear XML docs
- Obsolete attribute with migration message
- Migration guide with examples
- Update all documentation

---

## Acceptance Criteria

**Code**:
- [ ] `SegmentationBlock<T>` implemented and tested
- [ ] All segmentation modes working (Count, Key, Clock, Custom, None)
- [ ] `EpochSegmenterBlock` marked obsolete
- [ ] Test helpers updated

**Tests**:
- [ ] All existing tests updated and passing
- [ ] No test coverage lost
- [ ] Backward compatibility test (obsolete block still works)

**Documentation**:
- [ ] Migration guide created
- [ ] Design docs updated
- [ ] ADR created
- [ ] XML docs updated

**Validation**:
- [ ] Benchmarks show no performance regression
- [ ] All test scenarios pass
- [ ] Obsolete warnings appear correctly

---

## For Implementation Team

**Estimated Effort**: 3-5 days

**Breakdown**:
- Day 1: Implement `SegmentationBlock<T>` and mark old block obsolete
- Day 2: Update test helpers and 50% of tests
- Day 3: Update remaining tests and benchmarks
- Day 4: Update documentation and create migration guide
- Day 5: Review, validation, and ADR creation

**Order of Work**:
1. Implement new block first (get it working)
2. Update test helpers
3. Update tests incrementally (validate as you go)
4. Update benchmarks
5. Documentation last (once everything works)

**Tips**:
- Start with one test file to validate the migration pattern
- Use find/replace for mechanical changes (but review each)
- Run tests frequently to catch issues early
- Consider creating a script to automate test updates

---

## Questions for Implementation

None at this time. All design decisions documented above. If questions arise during implementation, consult:
- Research documentation in `/research/epoch-segmenter-clarification/`
- Mandatory epochs ADR
- Create clarifying issue if needed
