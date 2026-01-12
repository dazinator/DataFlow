# [Implementation] Remove Obsolete Block-Level Epoch Segmentation System

## Context and Objectives

### Problem Statement

The block-level epoch segmentation system (`EpochSegmenterBlock`, `EpochSegmentationPolicy`, `PlainSourceAdapter`, `IPlainSourceActor`) is fully obsolete and superseded by the graph-level `ConfigureEpochs()` API with `EpochPolicy`.

### Research Background

Research determined that the ENTIRE block-level segmentation system should be removed.

**Research Issue**: #88
**Research Documentation**: `/research/epoch-segmenter-clarification/`

**Key Research Artifacts**:
- Main findings: `/research/epoch-segmenter-clarification/README.md`
- Usage analysis: `/research/epoch-segmenter-clarification/notes/usage-analysis.md`
- Architectural analysis: `/research/epoch-segmenter-clarification/notes/architectural-analysis.md`
- Implementation specs: `/research/epoch-segmenter-clarification/handover/README.md`

**Key Findings**:
1. **Fully Obsolete**: All block-level segmentation is superseded by graph-level `ConfigureEpochs()` API
2. **Replacement Exists**: `ConfigureEpochs()` with `EpochPolicy` provides all segmentation functionality (count, time, combined)
3. **PlainSourceAdapter Also Obsolete**: Was temporary stand-in; `EpochSourceBlock` is canonical
4. **Test Coverage Verified**: Equivalent functionality tested in `EpochGraphIntegrationTests.cs` and `EpochConfigurationApiDemoTests.cs`

### Objectives

- [ ] Remove obsolete block-level segmentation system
- [ ] Verify test coverage for `ConfigureEpochs()` API
- [ ] Remove obsolete tests using block-level system
- [ ] Remove obsolete benchmarks
- [ ] Update test helpers (BlockHelpers.cs)
- [ ] Remove obsolete documentation
- [ ] Ensure no functionality loss

## Modern Architecture

### Graph-Level Segmentation (Current)

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
```

### Canonical Source Pattern

```csharp
public class MySourceActor : ISourceActor<int>
{
    public async IAsyncEnumerable<IEpochStream<int>> ProduceEpochsAsync(
        IActorExecutionContext context)
    {
        var items = GetItems();
        yield return items.WrapInSingleEpoch("my-source", context.CancellationToken);
    }
}

// Usage
builder.AddSource<int, MySourceActor>("source");
```

Graph applies `EpochPolicy` automatically - no need for segmenter blocks.

## Implementation Checklist

### Step 1: Verify Test Coverage

Ensure equivalent test coverage exists for graph-level API:

- [ ] `EpochGraphIntegrationTests.cs` covers count-based segmentation
- [ ] `EpochGraphIntegrationTests.cs` covers time-based segmentation  
- [ ] `EpochConfigurationApiDemoTests.cs` covers ConfigureEpochs API
- [ ] Documentation tests show current patterns
- [ ] Epoch lifecycle hooks are tested

### Step 2: Remove Obsolete Production Code

**Files to Remove**:
- [ ] `/poc/DataFlow/Blocks/EpochSegmenterBlock.cs`
- [ ] `/poc/DataFlow/Blocks/EpochSegmentationPolicy.cs`
- [ ] `/poc/DataFlow/Blocks/PlainSourceAdapter.cs`
- [ ] `/poc/DataFlow/Core/IPlainSourceActor.cs`

### Step 3: Remove Obsolete Tests

**Test Files to Remove**:
- [ ] `DecoupledEpochTests.cs`
- [ ] `EpochAwareBlockTests.cs`
- [ ] `MultiSourceSegmentationTests.cs`
- [ ] `DecoupledEpochPerformanceTests.cs`
- [ ] References in `EpochBufferBlockTests.cs`
- [ ] References in `BlockContextConstructorInjectionTests.cs`

### Step 4: Remove Obsolete Benchmarks

**Benchmark Files to Remove**:
- [ ] `DecoupledEpochBenchmark.cs`
- [ ] `EpochAwareBlockBenchmark.cs`
- [ ] References in `SimpleEtlPOC.cs`
- [ ] References in `ComplexEtlPOC.cs`
- [ ] References in `BatchBlockComparisonBenchmark.cs`

### Step 5: Update Test Helpers

**BlockHelpers.cs Changes**:
- [ ] Remove `CreateEpochSegmenter<T>()` method
- [ ] Remove `CreatePlainSource<T>()` methods
- [ ] Remove `PlainProducerWrapper<T>` class
- [ ] Remove `PlainToEpochActorWrapper<T>` class
- [ ] Remove `PlainToEpochBatchWrapper<T>` class
- [ ] Remove `ConcurrentProducerWrapper<T>` class

### Step 6: Update Documentation

**Remove obsolete documentation**:
- [ ] `/poc/docs/design/blocks/epoch-segmenter-block.md`
- [ ] `/poc/docs/design/blocks/plain-source-block.md`
- [ ] Update `/poc/docs/POC_GLOSSARY.md` to remove obsolete entries
- [ ] Update `/poc/docs/guides/using-epochs.md` to remove obsolete patterns

### Step 7: Validation

- [ ] All tests pass after removal
- [ ] No references to obsolete classes remain
- [ ] Documentation is consistent with current architecture

## Test Scenarios

**Verify equivalent coverage exists**:

1. **Count-Based Segmentation**
   - Via: `ConfigureEpochs(config => config.SetPolicy(EpochPolicy.ByCount(100)))`
   - Test: `EpochGraphIntegrationTests.cs`

2. **Time-Based Segmentation**
   - Via: `ConfigureEpochs(config => config.SetPolicy(EpochPolicy.ByTime(period)))`
   - Test: Verify exists or add if missing

3. **Combined Segmentation**
   - Via: `EpochPolicy.ByCountOrTime()`
   - Test: Verify exists or add if missing

4. **Epoch Lifecycle Hooks**
   - Via: `config.OnBeginEpoch()`, `config.OnCommitEpoch()`
   - Test: Verify coverage exists

5. **EpochSourceBlock**
   - Via: `builder.AddSource<T, TActor>()`
   - Test: Verify exists

## Design References

- **Research**: `/research/epoch-segmenter-clarification/README.md`
- **Mandatory Epochs ADR**: `/docs/adr/poc/2025-11-20-mandatory-epochs-unified-architecture.md`

## Estimated Effort

**2-3 days**

- Day 1: Verify test coverage, remove production code
- Day 2: Remove obsolete tests and benchmarks
- Day 3: Update documentation, validation

## Related Issues

- Research Issue: #88
- PR: #89

## Labels

- `workflow:implementation`
- `type:cleanup`
- `area:poc`
- `priority:medium`

## Correction Note

This implementation issue was updated from an initial "refactor" approach to a "remove" approach based on corrected research findings. The entire block-level segmentation system is obsolete and should be removed, not refactored.
