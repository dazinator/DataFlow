# Usage Analysis: EpochSegmenterBlock and EpochSegmentationPolicy

## Overview

This document analyzes where and how `EpochSegmenterBlock` and `EpochSegmentationPolicy` are used in the codebase to understand their role and determine if they're superseded by newer architecture.

## File Count Summary

### Production Code (poc/DataFlow/)
- `poc/DataFlow/Blocks/EpochSegmenterBlock.cs` - Implementation (160 lines)
- `poc/DataFlow/Blocks/EpochSegmentationPolicy.cs` - Policy configuration (169 lines)
- `poc/DataFlow/Core/IPlainSourceActor.cs` - Related interface

### Tests (poc/DataFlow.Tests/)
- `poc/DataFlow.Tests/DecoupledEpochTests.cs` - Main test suite
- `poc/DataFlow.Tests/EpochAwareBlockTests.cs` - Epoch-aware block tests
- `poc/DataFlow.Tests/MultiSourceSegmentationTests.cs` - Multi-source tests
- `poc/DataFlow.Tests/EpochBufferBlockTests.cs` - Buffer block tests
- `poc/DataFlow.Tests/DecoupledEpochPerformanceTests.cs` - Performance tests
- `poc/DataFlow.Tests/BlockContextConstructorInjectionTests.cs` - Constructor tests
- `poc/DataFlow.Tests/TestHelpers/BlockHelpers.cs` - Test helper (CreateEpochSegmenter method)

### Benchmarks (poc/DataFlow.Benchmarks/)
- `poc/DataFlow.Benchmarks/SimpleEtlPOC.cs`
- `poc/DataFlow.Benchmarks/BatchBlockComparisonBenchmark.cs`
- `poc/DataFlow.Benchmarks/DecoupledEpochBenchmark.cs`
- `poc/DataFlow.Benchmarks/ComplexEtlPOC.cs`
- `poc/DataFlow.Benchmarks/EpochAwareBlockBenchmark.cs`

### Documentation
- `poc/docs/design/blocks/epoch-segmenter-block.md` - Design documentation
- `poc/docs/design/blocks/plain-source-block.md` - Related docs
- `poc/docs/guides/using-epochs.md` - Usage guide
- `poc/docs/POC_GLOSSARY.md` - Glossary entry
- `docs/guides/epoch-buffer-blocks.md` - Guide reference

### Research Artifacts
- Multiple references in `/research/epoch-stream-separation/`
- Multiple references in `/research/flow-composability-unification/`
- Multiple references in other research folders

## Usage Pattern Analysis

### Pattern 1: Converting Plain Streams to Epoch Streams

The most common usage is converting plain `IAsyncEnumerable<T>` to epoch streams:

```csharp
var producer = BlockHelpers.CreateProducer("source", TestStreams.Integers(10));
var segmenter = BlockHelpers.CreateEpochSegmenter<int>(
    "segmenter", 
    EpochSegmentationPolicy.ByCount(3, "test-source"));
```

**Purpose**: Take a plain stream and segment it into epochs based on various policies.

### Pattern 2: Segmentation Policies

Multiple segmentation strategies supported:
- `SegmentationMode.None` - Single epoch wrapper
- `SegmentationMode.Count` - Segment by item count
- `SegmentationMode.Key` - Segment by key selector
- `SegmentationMode.Clock` - Segment by epoch clock
- `SegmentationMode.Custom` - Custom segmentation logic

### Pattern 3: Test Helper Usage

The `BlockHelpers.CreateEpochSegmenter<T>()` method is used extensively in tests to create segmenter instances with consistent configuration.

## Architectural Context

### Current State: EpochSegmenterBlock

**Role**: Converts plain streams to epoch streams at the block level
**Approach**: Added as a block in the pipeline DAG
**Design**: Each segmenter is a block with its own configuration

### New State: Mandatory Epochs Architecture

**Role**: All streams are epoch streams by default
**Approach**: 
- Plain sources are automatically wrapped in single epoch (via `PlainSourceAdapter`)
- Epoch configuration done at graph level via `ConfigureEpochs()`
- Sources produce epoch streams natively via `EpochSourceBlock`

**Key Difference**: 
- OLD: Epochs are optional, added via segmenter blocks
- NEW: Epochs are mandatory, built into the architecture

## Analysis: Is EpochSegmenterBlock Superseded?

### Evidence FOR Supersession

1. **Mandatory Epochs ADR (2025-11-20)**: Establishes that all data flows through epochs
2. **PlainSourceAdapter**: Provides automatic single-epoch wrapping for plain sources
3. **ConfigureEpochs API**: Graph-level epoch configuration
4. **EpochSourceBlock**: Native epoch stream production

### Evidence AGAINST Supersession

1. **Multi-Epoch Segmentation**: `EpochSegmenterBlock` provides segmentation strategies (count, key, clock) that aren't directly replaced
2. **Active Usage**: Still used in tests and benchmarks
3. **Documented Pattern**: Has design documentation and examples

### Key Question: How to Achieve Multi-Epoch Segmentation?

With mandatory epochs, how does a user segment a plain source into multiple epochs?

**Current approach**:
```csharp
plainSource → EpochSegmenterBlock (by count) → epoch-aware processing
```

**New approach options**:
1. Make sources epoch-aware (implement ISourceActor and segment in ProduceEpochsAsync)
2. Use a transformation block that re-segments epoch streams
3. Keep EpochSegmenterBlock as a utility for this specific use case

## Initial Findings

### Finding 1: Partial Supersession
`EpochSegmenterBlock` is **partially superseded** but NOT fully obsolete.

**Superseded aspect**: Single-epoch wrapping (SegmentationMode.None)
- This is now handled by `PlainSourceAdapter` and `SingleEpochExtensions.WrapInSingleEpoch()`

**NOT superseded**: Multi-epoch segmentation strategies
- Count-based segmentation
- Key-based segmentation  
- Clock-based segmentation
- Custom segmentation

### Finding 2: Architectural Misalignment
The block-level approach of `EpochSegmenterBlock` doesn't fully align with the mandatory epochs architecture where:
- Sources are expected to produce epoch streams natively
- Graph-level configuration via `ConfigureEpochs()`

However, there's still a legitimate need for:
- Converting plain sources to multi-epoch streams
- Re-segmenting existing epoch streams

### Finding 3: Missing Replacement Pattern
There's no clear replacement in the current architecture for:
- Plain source → multi-epoch segmentation
- Epoch stream → re-segmented epoch stream

## Questions for Further Investigation

1. Should multi-epoch segmentation be done:
   a) At the source level (sources produce segmented epochs)?
   b) Via a dedicated segmentation utility block?
   c) Both approaches supported?

2. Is there value in graph-level segmentation policies (via ConfigureEpochs)?

3. Should segmentation be a first-class concept in the builder API?

4. What's the migration path for existing EpochSegmenterBlock usage?

## Next Steps

1. Review ConfigureEpochs implementation to understand its capabilities
2. Examine EpochSourceBlock to understand how sources produce epochs
3. Identify if there's a gap in multi-epoch segmentation support
4. Propose architectural solution for segmentation in mandatory epoch world
5. Create migration strategy based on findings
