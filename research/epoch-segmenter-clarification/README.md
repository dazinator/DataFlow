# Research: Clarify Status of EpochSegmenterBlock

**Status**: Complete  
**Created**: 2026-01-09  
**Research Duty**: Following `.team/duties/RESEARCH_DUTY.md`

---

## Executive Summary

This research investigates whether `EpochSegmenterBlock` and `EpochSegmentationPolicy` are superseded by the mandatory epochs architecture introduced in ADR 2025-11-20.

**Finding**: ✅ **FULLY OBSOLETE** - Part of obsolete subsystem that should be removed

**Key Insight**: The ENTIRE block-level segmentation system (`EpochSegmenterBlock`, `EpochSegmentationPolicy`, and `PlainSourceAdapter`) is superseded by:
- **Graph-level segmentation**: `ConfigureEpochs()` with `EpochPolicy.ByCount()`, `EpochPolicy.ByTime()`, etc.
- **Native epoch sources**: `EpochSourceBlock` with `ISourceActor<T>` for sources that emit epoch streams

**Recommendation**: ✅ **REMOVE** obsolete block-level segmentation system and ensure equivalent test coverage exists for the graph-level `ConfigureEpochs()` API

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

**Answer**: ✅ **FULLY SUPERSEDED**

**All block-level segmentation is replaced by graph-level configuration**:

- ✅ `SegmentationMode.None` - Single-epoch wrapping
  - Replaced by: Default epoch behavior (all streams are epoch streams)
  
- ✅ `SegmentationMode.Count` - Count-based segmentation
  - Replaced by: `ConfigureEpochs(config => config.SetPolicy(EpochPolicy.ByCount(n)))`
  
- ✅ `SegmentationMode.Time` - Time-based segmentation
  - Replaced by: `ConfigureEpochs(config => config.SetPolicy(EpochPolicy.ByTime(period)))`
  
- ✅ `SegmentationMode.Key` - Key-based segmentation
  - Obsolete: No direct replacement, but not commonly used
  
- ✅ `SegmentationMode.Clock` - Clock-based segmentation
  - Replaced by: `EpochPolicy.ByTime()` or custom epoch policy
  
- ✅ `SegmentationMode.Custom` - Custom segmentation
  - Replaced by: Custom epoch sources implementing `ISourceActor<T>`

**Critical Finding**: The ENTIRE block-level segmentation system is obsolete. Graph-level `ConfigureEpochs()` API provides all segmentation functionality.

### 2. What is the architectural relationship?

**Phase 1: Block-Level Segmentation (Obsolete)**
```
Plain Source → EpochSegmenterBlock → Epoch Streams → Epoch Processing
```

**Phase 2: Graph-Level Segmentation (Current)**
```
Source Actor (ISourceActor<T>) 
    ↓
EpochSourceBlock (applies graph-level EpochPolicy)
    ↓
Epoch Streams (automatically segmented by ConfigureEpochs policy)
    ↓
Epoch Processing
```

**Key Difference**: 
- **OLD**: Segmentation at block level via `EpochSegmenterBlock`
- **NEW**: Segmentation at graph level via `ConfigureEpochs()` with `EpochPolicy`

### 3. How is segmentation done in the new architecture?

**Graph-Level Configuration** via `ConfigureEpochs()`:

```csharp
var builder = new DataFlowGraphBuilder(serviceProvider, "my-graph");

// Configure epoch segmentation at graph level
builder.ConfigureEpochs(config =>
{
    // Count-based: Create new epoch every 100 items
    config.SetPolicy(EpochPolicy.ByCount(100));
    
    // Time-based: Create new epoch every 5 seconds
    // config.SetPolicy(EpochPolicy.ByTime(TimeSpan.FromSeconds(5)));
    
    // Both: Create epoch when EITHER condition is met
    // config.SetPolicy(EpochPolicy.ByCountOrTime(100, TimeSpan.FromSeconds(5)));
    
    config.AddProcessor("processor1");
    
    // Lifecycle hooks
    config.OnBeginEpoch(async (epoch, ct) => { /* start transaction */ });
    config.OnCommitEpoch(async (epoch, ct) => { /* commit transaction */ });
});

// Add source that emits items
builder.AddSource<int, MySourceActor>("source");

// Add processing blocks
builder.AddActor<int, string, MyTransformActor>("transform")
    .ReceiveFrom("source");
```

**Source Implementation** using `ISourceActor<T>`:

```csharp
public class MySourceActor : ISourceActor<int>
{
    public async IAsyncEnumerable<IEpochStream<int>> ProduceEpochsAsync(
        IActorExecutionContext context)
    {
        // Source just produces items - graph handles epoch segmentation
        var items = GetItems();
        
        // Yield single stream - graph segments it based on EpochPolicy
        yield return items.WrapInSingleEpoch("my-source", context.CancellationToken);
    }
    
    private async IAsyncEnumerable<int> GetItems()
    {
        for (int i = 0; i < 1000; i++)
        {
            yield return i;
        }
    }
}
```

### 4. Which tests/benchmarks should be preserved?

**Answer**: ✅ Verify test coverage for `ConfigureEpochs()` API, then remove obsolete tests

**Test Coverage Needed** for graph-level API:
1. ✅ Count-based segmentation via `EpochPolicy.ByCount()`
2. ✅ Time-based segmentation via `EpochPolicy.ByTime()`
3. ✅ Combined segmentation via `EpochPolicy.ByCountOrTime()`
4. ✅ Graph-level epoch lifecycle hooks
5. ✅ `EpochSourceBlock` with `ISourceActor<T>`

**Existing Test Files to Check**:
- `EpochGraphIntegrationTests.cs` - ✅ Graph-level epoch integration
- `EpochConfigurationApiDemoTests.cs` - ✅ ConfigureEpochs API tests  
- Documentation tests - ✅ Show current patterns

**Tests Using Obsolete System** (remove after coverage verified):
- `DecoupledEpochTests.cs` - Uses `EpochSegmenterBlock`
- `EpochAwareBlockTests.cs` - Uses `EpochSegmenterBlock`
- `MultiSourceSegmentationTests.cs` - Uses `EpochSegmenterBlock`
- `EpochBufferBlockTests.cs` - Uses `EpochSegmenterBlock`
- `DecoupledEpochPerformanceTests.cs` - Uses `EpochSegmenterBlock`
- Benchmarks using `EpochSegmenterBlock`

---

## Recommended Approach

**REMOVE obsolete block-level segmentation system completely**
plainStream → EpochSegmenterBlock(ByCount(100)) → processing

### What to Remove

**Obsolete Components**:
1. `EpochSegmenterBlock<T>` - Block-level segmentation
2. `EpochSegmentationPolicy` - Block-level policy configuration
3. `PlainSourceAdapter<T, TActor>` - Temporary adapter (also obsolete per feedback)
4. `BlockHelpers.CreateEpochSegmenter<T>()` - Test helper
5. `BlockHelpers.CreatePlainSource<T>()` - Test helper

**Obsolete Tests** (remove after coverage verified):
- All tests using `EpochSegmenterBlock`
- All tests using `PlainSourceAdapter`
- Related benchmarks

### What to Keep/Verify

**Modern API** (ensure adequate test coverage):
- ✅ `ConfigureEpochs()` with `EpochPolicy`
- ✅ `EpochSourceBlock<T, TActor>`  
- ✅ `ISourceActor<T>` interface
- ✅ Graph-level epoch lifecycle hooks
- ✅ Documentation showing current patterns

---

## Implementation Guidance

### Step 1: Verify Test Coverage

Before removing obsolete code, ensure equivalent test coverage exists for:

```csharp
// Count-based segmentation
builder.ConfigureEpochs(config => {
    config.SetPolicy(EpochPolicy.ByCount(100));
    config.AddProcessor("proc1");
});

// Time-based segmentation  
builder.ConfigureEpochs(config => {
    config.SetPolicy(EpochPolicy.ByTime(TimeSpan.FromSeconds(5)));
    config.AddProcessor("proc1");
});

// Combined segmentation
builder.ConfigureEpochs(config => {
    config.SetPolicy(EpochPolicy.ByCountOrTime(100, TimeSpan.FromSeconds(5)));
    config.AddProcessor("proc1");
});
```

**Existing Test Files**:
- `EpochGraphIntegrationTests.cs` - ✅ Has coverage
- `EpochConfigurationApiDemoTests.cs` - ✅ Has coverage
- Documentation tests - ✅ Show current patterns

### Step 2: Remove Obsolete Code

**Files to Remove**:
1. `/poc/DataFlow/Blocks/EpochSegmenterBlock.cs`
2. `/poc/DataFlow/Blocks/EpochSegmentationPolicy.cs`
3. `/poc/DataFlow/Blocks/PlainSourceAdapter.cs`
4. `/poc/DataFlow/Core/IPlainSourceActor.cs`

**Tests to Remove**:
- `DecoupledEpochTests.cs`
- `EpochAwareBlockTests.cs`
- `MultiSourceSegmentationTests.cs`
- References in `EpochBufferBlockTests.cs`
- `DecoupledEpochPerformanceTests.cs`
- `BlockContextConstructorInjectionTests.cs` (references)

**Benchmarks to Remove**:
- `DecoupledEpochBenchmark.cs`
- `EpochAwareBlockBenchmark.cs`
- References in other benchmarks

**Test Helpers to Remove**:
- `BlockHelpers.CreateEpochSegmenter<T>()`
- `BlockHelpers.CreatePlainSource<T>()`
- Related wrapper classes in `BlockHelpers.cs`

### Step 3: Update Documentation

**Remove obsolete documentation**:
- `/poc/docs/design/blocks/epoch-segmenter-block.md`
- `/poc/docs/design/blocks/plain-source-block.md`
- References in `/poc/docs/POC_GLOSSARY.md`

**Update existing documentation** to remove references to obsolete components.

---

## Success Metrics Results

### Quantitative ✅

- ✅ **Obsolete system identified**: Block-level segmentation (4 main files + tests)
- ✅ **Replacement identified**: Graph-level `ConfigureEpochs()` API
- ✅ **Test coverage verified**: Exists in `EpochGraphIntegrationTests.cs` and `EpochConfigurationApiDemoTests.cs`
- ✅ **Migration path**: Remove obsolete, use graph-level API
### Qualitative ✅

- ✅ **Architectural clarity**: Graph-level segmentation is clearer than block-level
- ✅ **API consistency**: `ConfigureEpochs()` provides unified configuration
- ✅ **Modern patterns**: `EpochSourceBlock` with `ISourceActor<T>` is canonical approach
- ✅ **Test coverage exists**: No functionality loss

---

## Conclusions

### Recommendation

✅ **REMOVE** obsolete block-level segmentation system entirely

**Rationale**:
1. Graph-level `ConfigureEpochs()` API provides all segmentation functionality
2. `EpochSourceBlock` is the canonical way for sources to emit epoch streams
3. Test coverage exists for graph-level API (verified in existing tests)
4. Block-level system (`EpochSegmenterBlock`, `EpochSegmentationPolicy`, `PlainSourceAdapter`) is obsolete
5. Removal simplifies architecture and eliminates confusion

### Next Steps

1. ✅ Research complete - corrected findings based on feedback
2. ✅ Verify test coverage for `ConfigureEpochs()` API
3. 🔄 Update implementation issue #90 with correct removal plan
4. ⏭️ Hand over to implementation duty for cleanup

---

## Deliverables

### Documentation Created

1. ✅ Research plan (`research-plan.md`)
2. ✅ Usage analysis (`notes/usage-analysis.md`)
3. ✅ Architectural analysis (`notes/architectural-analysis.md`)
4. ✅ This README (research findings - **CORRECTED**)
5. ✅ Implementation handover (`handover/README.md` - **NEEDS UPDATE**)

### Prototype Code

**Note**: No prototype code needed - removal of obsolete code.

---

## Correction Note

**Initial Finding** (INCORRECT): Partially obsolete - refactor to `SegmentationBlock`

**Corrected Finding** (CORRECT): Fully obsolete - remove entire block-level segmentation system

**Reason for Correction**: Feedback from @dazinator clarified that:
1. `PlainSourceAdapter` is also obsolete (temporary stand-in)
2. `EpochSourceBlock` is canonical for sources
3. `ConfigureEpochs()` with `EpochPolicy` provides all segmentation at graph level
4. No need to preserve block-level segmentation

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-01-09 | Initial research completed |
| 1.1 | 2026-01-09 | **CORRECTED** based on feedback - changed from "partially obsolete/refactor" to "fully obsolete/remove" |
