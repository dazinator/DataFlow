# Implementation Handover: Remove Obsolete Block-Level Epoch Segmentation System

**Research Issue**: #88
**Implementation Issue**: #90
**Research Documentation**: `/research/epoch-segmenter-clarification/`
**Target**: POC Codebase

---

## Objective

Remove the obsolete block-level epoch segmentation system entirely. All segmentation is now done at graph level via `ConfigureEpochs()` API.

**Key Change**: Remove `EpochSegmenterBlock`, `EpochSegmentationPolicy`, `PlainSourceAdapter`, and `IPlainSourceActor` - these are superseded by graph-level configuration.

---

## Background

The mandatory epochs architecture (ADR 2025-11-20) established that all data flows through epochs. The block-level segmentation system (`EpochSegmenterBlock`, `EpochSegmentationPolicy`, `PlainSourceAdapter`) represents an earlier architectural approach that has been fully superseded.

**Replacement**: 
- Graph-level segmentation via `ConfigureEpochs(config => config.SetPolicy(EpochPolicy.ByCount(n)))`
- Canonical sources via `EpochSourceBlock` with `ISourceActor<T>`

**Solution**: Remove the entire obsolete block-level system after verifying equivalent test coverage exists.

---

## Success Criteria

- [ ] Verify test coverage exists for `ConfigureEpochs()` API
- [ ] Remove obsolete production code (4 main files)
- [ ] Remove obsolete tests using block-level segmentation
- [ ] Remove obsolete benchmarks
- [ ] Update test helpers (BlockHelpers.cs)
- [ ] Remove obsolete documentation
- [ ] All remaining tests pass
- [ ] No references to obsolete classes remain

---

## Implementation Checklist

### Step 1: Verify Test Coverage

Before removing obsolete code, ensure equivalent test coverage exists for graph-level API.

**Verify these tests exist**:
- [ ] Count-based segmentation: `EpochPolicy.ByCount(n)` 
- [ ] Time-based segmentation: `EpochPolicy.ByTime(period)`
- [ ] Combined segmentation: `EpochPolicy.ByCountOrTime(n, period)`
- [ ] Epoch lifecycle hooks: `OnBeginEpoch`, `OnCommitEpoch`, `OnEpochError`
- [ ] `EpochSourceBlock` with `ISourceActor<T>` usage

**Test Files to Check**:
- `EpochGraphIntegrationTests.cs` - Should have graph-level epoch coverage
- `EpochConfigurationApiDemoTests.cs` - Should have ConfigureEpochs API coverage
- Documentation tests - Should show current patterns

### Step 2: Remove Obsolete Production Code

**Files to Delete**:
```bash
rm poc/DataFlow/Blocks/EpochSegmenterBlock.cs
rm poc/DataFlow/Blocks/EpochSegmentationPolicy.cs
rm poc/DataFlow/Blocks/PlainSourceAdapter.cs
rm poc/DataFlow/Core/IPlainSourceActor.cs
```

### Step 3: Remove Obsolete Tests

**Test Files to Delete**:
```bash
rm poc/DataFlow.Tests/DecoupledEpochTests.cs
rm poc/DataFlow.Tests/EpochAwareBlockTests.cs
rm poc/DataFlow.Tests/MultiSourceSegmentationTests.cs
rm poc/DataFlow.Tests/DecoupledEpochPerformanceTests.cs
```

**Test Files to Update** (remove references):
- `EpochBufferBlockTests.cs` - Remove EpochSegmenterBlock references
- `BlockContextConstructorInjectionTests.cs` - Remove references

### Step 4: Remove Obsolete Benchmarks

**Benchmark Files to Delete**:
```bash
rm poc/DataFlow.Benchmarks/DecoupledEpochBenchmark.cs
rm poc/DataFlow.Benchmarks/EpochAwareBlockBenchmark.cs
```

**Benchmark Files to Update** (remove references):
- `SimpleEtlPOC.cs`
- `ComplexEtlPOC.cs`
- `BatchBlockComparisonBenchmark.cs`

### Step 5: Update Test Helpers

**File**: `poc/DataFlow.Tests/TestHelpers/BlockHelpers.cs`

**Remove these methods**:
- `CreateEpochSegmenter<T>()` - Obsolete segmenter helper
- `CreatePlainSource<T>()` methods - Obsolete source helpers
- `PlainProducerWrapper<T>` class - Obsolete wrapper
- `ConcurrentProducerWrapper<T>` class - Obsolete wrapper
- `PlainToEpochActorWrapper<T>` class - Obsolete wrapper
- `PlainToEpochBatchWrapper<T>` class - Obsolete wrapper

### Step 6: Remove Obsolete Documentation

**Documentation Files to Delete**:
```bash
rm poc/docs/design/blocks/epoch-segmenter-block.md
rm poc/docs/design/blocks/plain-source-block.md
```

**Documentation Files to Update**:
- `poc/docs/POC_GLOSSARY.md` - Remove obsolete entries
- `poc/docs/guides/using-epochs.md` - Remove obsolete pattern references
- `docs/guides/epoch-buffer-blocks.md` - Remove references

### Step 7: Final Validation

- [ ] Run all tests: `dotnet test`
- [ ] Verify no compilation errors
- [ ] Search for any remaining references:
  ```bash
  grep -r "EpochSegmenterBlock" poc/
  grep -r "EpochSegmentationPolicy" poc/
  grep -r "PlainSourceAdapter" poc/
