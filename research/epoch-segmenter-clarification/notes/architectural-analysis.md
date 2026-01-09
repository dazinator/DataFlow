# Architectural Analysis: EpochSegmenterBlock in Mandatory Epochs Context

## Executive Summary

**Status**: `EpochSegmenterBlock` is **PARTIALLY OBSOLETE** but serves a legitimate purpose

**Key Finding**: The mandatory epochs architecture provides single-epoch wrapping via `PlainSourceAdapter` and `SingleEpochExtensions.WrapInSingleEpoch()`, but there's **NO DIRECT REPLACEMENT** for multi-epoch segmentation (count-based, key-based, clock-based).

**Recommendation**: REFACTOR, don't remove entirely

## Architectural Evolution

### Phase 1: Optional Epochs (Original Design)
```
Plain Stream → [Optional: EpochSegmenterBlock] → Plain/Epoch Processing
```
- Epochs were optional
- Added via segmenter blocks when needed
- Parallel plain and epoch block implementations

### Phase 2: Mandatory Epochs (Current Design - ADR 2025-11-20)
```
Plain Source → PlainSourceAdapter → Single Epoch Stream → Epoch Processing
```
- ALL streams are epoch streams
- Plain sources automatically wrapped in single epoch
- Unified epoch-aware block implementations only

### The Gap: Multi-Epoch Segmentation

**Question**: How does a user segment a plain source into MULTIPLE epochs?

**Current answer**: Use `EpochSegmenterBlock`
**Problem**: This doesn't align with the "mandatory epochs" architecture principle

## Analysis of EpochSegmenterBlock Capabilities

### Capability 1: Single-Epoch Wrapping (SUPERSEDED)
```csharp
EpochSegmentationPolicy.None  // Wraps entire stream in one epoch
```
**Status**: ✅ FULLY REPLACED by `PlainSourceAdapter` and `WrapInSingleEpoch()`

### Capability 2: Count-Based Segmentation (NOT REPLACED)
```csharp
EpochSegmentationPolicy.ByCount(100, "source")  // 100 items per epoch
```
**Status**: ❌ NO REPLACEMENT in current architecture

**Use Case**: Split large batches into smaller epochs for:
- Memory management (process 100 items at a time)
- Transaction boundaries (commit every 1000 items)
- Checkpoint granularity (save state every 500 items)

### Capability 3: Key-Based Segmentation (NOT REPLACED)
```csharp
EpochSegmentationPolicy.ByKey<Item, string>(x => x.CustomerId, "source")
```
**Status**: ❌ NO REPLACEMENT in current architecture

**Use Case**: Group items by business key:
- Process all orders for each customer in one epoch
- Batch database operations by entity type
- Transaction boundaries per customer/account

### Capability 4: Clock-Based Segmentation (NOT REPLACED)
```csharp
EpochSegmentationPolicy.ByClock(epochClock, "source")
```
**Status**: ❌ NO REPLACEMENT in current architecture

**Use Case**: Time-based windowing:
- Process data in hourly epochs
- Batch operations by time window
- Coordinate with external systems by time

### Capability 5: Custom Segmentation (NOT REPLACED)
```csharp
EpochSegmentationPolicy.Custom<T>(customSegmenter, "source")
```
**Status**: ❌ NO REPLACEMENT in current architecture

**Use Case**: Domain-specific segmentation logic

## Current Architecture Gaps

### Gap 1: No Multi-Epoch Segmentation Pattern

The mandatory epochs architecture provides:
- ✅ Single-epoch wrapping (via `PlainSourceAdapter`, `WrapInSingleEpoch`)
- ✅ Native epoch production (via `EpochSourceBlock` with `ISourceActor<T>`)
- ❌ Multi-epoch segmentation for plain sources
- ❌ Re-segmentation of existing epoch streams

### Gap 2: No Graph-Level Segmentation Configuration

`ConfigureEpochs()` currently configures:
- Checkpoint strategy
- Epoch hooks
- Epoch coordinator

It does NOT configure:
- ❌ Segmentation policies for sources
- ❌ How epochs should be created from plain streams
- ❌ Re-segmentation strategies

### Gap 3: Source-Level Segmentation is Not Always Appropriate

**Problem**: Forcing segmentation logic into sources has issues:

1. **Separation of Concerns**: Source knows how to produce data, not how to segment it
2. **Reusability**: Same source might need different segmentation in different pipelines
3. **Composability**: Can't easily change segmentation strategy without changing source

**Example**:
```csharp
// Want to process same source with different segmentation
Pipeline A: CustomerSource → Segment by 100 items → Process
Pipeline B: CustomerSource → Segment by customer ID → Process
Pipeline C: CustomerSource → Segment by time window → Process

// Current approach requires three different source implementations
// Better: One source + configurable segmentation
```

## Architectural Options

### Option 1: Keep EpochSegmenterBlock As-Is (Status Quo)

**Pros**:
- No changes needed
- All capabilities preserved
- Tests and benchmarks work

**Cons**:
- Conceptually misaligned with mandatory epochs
- Doesn't integrate with `ConfigureEpochs()` API
- Documentation suggests epochs are "optional add-ons"

**Verdict**: ❌ Not recommended - perpetuates architectural inconsistency

### Option 2: Remove EpochSegmenterBlock Entirely

**Pros**:
- Clean mandatory epochs architecture
- Forces segmentation into sources (where it "belongs"?)

**Cons**:
- ❌ **LOSES CRITICAL CAPABILITIES** (count, key, clock, custom segmentation)
- ❌ Reduces composability (sources become less reusable)
- ❌ No replacement for re-segmentation
- ❌ Forces users to write custom sources for simple segmentation needs

**Verdict**: ❌ Not recommended - removes valuable functionality without replacement

### Option 3: Refactor as SegmentationBlock (RECOMMENDED)

**Proposal**: Rename and reframe as a first-class segmentation concept

```csharp
// Old name (implies "adding" epochs)
EpochSegmenterBlock<T>

// New name (clear: re-segments existing epoch streams)
SegmentationBlock<T>  // or EpochSegmentationBlock<T>
```

**Signature Change**:
```csharp
// OLD: Plain → Epoch
public class EpochSegmenterBlock<T> : BlockBase<T, IEpochStream<T>>

// NEW: Epoch → Segmented Epochs
public class SegmentationBlock<T> : BlockBase<IEpochStream<T>, IEpochStream<T>>
```

**Pros**:
- ✅ Aligns with mandatory epochs (input is already epoch streams)
- ✅ Preserves all segmentation capabilities
- ✅ Clear purpose: re-segment epochs, not create them
- ✅ Works with both native epoch sources and wrapped plain sources
- ✅ Integrates better with `ConfigureEpochs()` conceptually

**Cons**:
- Requires migration of existing usage
- Breaking change to signature

**Verdict**: ✅ **RECOMMENDED** - best balance of architecture alignment and capability preservation

### Option 4: Integrate with ConfigureEpochs() API

**Proposal**: Add segmentation configuration to graph builder

```csharp
builder
    .AddPlainSource<int, MySource>("source")
    .ConfigureEpochs(config => {
        config.SegmentBy("source", SegmentationPolicy.ByCount(100));
    });
```

**Pros**:
- Clean API integration
- Graph-level configuration
- Declarative segmentation

**Cons**:
- Complex implementation (graph builder needs to insert segmentation blocks)
- Less explicit in pipeline topology
- May be over-engineering

**Verdict**: 🤔 Possible future enhancement, but not immediate priority

## Recommended Solution

### Phase 1: Refactor (v2.1)

1. **Create new `SegmentationBlock<T>`**:
   - Signature: `BlockBase<IEpochStream<T>, IEpochStream<T>>`
   - Accepts epoch streams as input
   - Re-segments them based on policy
   - Preserves all current segmentation modes

2. **Update `EpochSegmentationPolicy`**:
   - Rename if needed for clarity
   - Keep all existing modes
   - Document as "re-segmentation policies"

3. **Mark `EpochSegmenterBlock` as `[Obsolete]`**:
   ```csharp
   [Obsolete("Use SegmentationBlock<T> instead. " +
             "For single-epoch wrapping, use PlainSourceAdapter or WrapInSingleEpoch(). " +
             "For multi-epoch segmentation, wrap plain source first, then use SegmentationBlock.")]
   ```

4. **Migration Pattern**:
   ```csharp
   // OLD
   plainStream 
       → EpochSegmenterBlock(ByCount(100)) 
       → epoch processing
   
   // NEW
   plainStream 
       → WrapInSingleEpoch("source")  // Now epoch stream
       → SegmentationBlock(ByCount(100))  // Re-segment
       → epoch processing
   ```

### Phase 2: Remove Obsolete (v3.0)

1. Remove `EpochSegmenterBlock`
2. Keep `SegmentationBlock<T>` as the unified solution

## Impact Assessment

### Code Changes Required

**Production Code**:
- New file: `SegmentationBlock.cs` (copy and refactor from `EpochSegmenterBlock.cs`)
- Mark obsolete: `EpochSegmenterBlock.cs`
- Keep: `EpochSegmentationPolicy.cs` (rename to `SegmentationPolicy.cs`?)

**Tests**:
- ~15 test files using `CreateEpochSegmenter`
- Update to use `CreateSegmentation` helper
- Add explicit single-epoch wrapping where needed

**Benchmarks**:
- ~5 benchmark files
- Similar updates

**Documentation**:
- Update design docs
- Add migration guide
- Update examples

### Breaking Changes

**v2.1 (Deprecation)**:
- Obsolete warnings on `EpochSegmenterBlock` usage
- No breaking changes (both work)

**v3.0 (Removal)**:
- `EpochSegmenterBlock` removed
- Users must migrate to `SegmentationBlock`

## Test and Benchmark Preservation

### Tests to Keep (Refactor)

All segmentation tests provide value and should be preserved:

1. **Count-based segmentation tests**: Validate batch size boundaries
2. **Key-based segmentation tests**: Validate grouping logic
3. **Clock-based segmentation tests**: Validate time windowing
4. **Multi-source coordination tests**: Validate epoch vector handling
5. **Performance tests**: Validate segmentation overhead

### Tests to Remove

- None - all tests provide unique value

### Benchmarks to Keep

All benchmarks measuring segmentation performance:
- `DecoupledEpochBenchmark.cs` - Segmentation overhead
- `EpochAwareBlockBenchmark.cs` - Integrated pipeline performance
- `BatchBlockComparisonBenchmark.cs` - Comparison with batching

## Documentation Updates Needed

### ADRs

**New ADR**: "Segmentation as First-Class Concept in Mandatory Epochs"
- Document refactoring rationale
- Explain input signature change (epoch → epoch)
- Migration guidance

**Update ADR**: "Mandatory Epochs Unified Architecture" (2025-11-20)
- Add section on segmentation
- Clarify that single-epoch wrapping ≠ multi-epoch segmentation

### Design Docs

- Update: `poc/docs/design/blocks/epoch-segmenter-block.md` → rename to `segmentation-block.md`
- Update: `poc/docs/guides/using-epochs.md` - add segmentation guidance
- Update: `poc/docs/POC_GLOSSARY.md` - update terminology

### API Docs

- Update XML docs for all segmentation-related classes
- Add migration examples

## Conclusion

**EpochSegmenterBlock is NOT fully obsolete**. While single-epoch wrapping is superseded by `PlainSourceAdapter`, the multi-epoch segmentation capabilities (count, key, clock, custom) are NOT replaced in the current architecture.

**Recommended Action**: REFACTOR, not remove
1. Create `SegmentationBlock<T>` with epoch-to-epoch signature
2. Deprecate `EpochSegmenterBlock` with migration guidance
3. Preserve all tests and benchmarks (just update them)
4. Document as first-class segmentation concept in mandatory epoch architecture

This approach:
- ✅ Aligns with mandatory epochs architecture
- ✅ Preserves all critical capabilities
- ✅ Improves conceptual clarity
- ✅ Maintains backward compatibility during migration
- ✅ Provides clear migration path
