# Epoch-Aware Block Benchmarks

This document describes the benchmarks for epoch-aware blocks (`EpochActorBlock` and `EpochBatchBlock`).

## Overview

The benchmarks validate that epoch-aware blocks meet the performance requirements specified in the handover document:
- **Target**: < 10% overhead vs plain blocks in micro-benchmarks
- **Realistic**: < 2% overhead with 30ms processing per item

## Benchmark Classes

### EpochAwareBlockBenchmark

Micro-benchmarks comparing epoch-aware blocks against plain blocks (ActorBlock baseline).

**Scenarios:**
1. **PlainActorBlock_Transform** (Baseline) - Plain ActorBlock with simple transformation
2. **EpochActorBlock_Transform** - EpochActorBlock with 1-to-1 transformation
3. **EpochActorBlock_OneToMany** - EpochActorBlock with 1-to-many transformation
4. **EpochActorBlock_Filter** - EpochActorBlock with filtering
5. **EpochBatchBlock_Batching** - EpochBatchBlock batching performance
6. **ComposedPipeline_TransformAndBatch** - Composed pipeline (transform → batch)

**Configuration:**
- 10,000 items total
- 100 items per epoch
- Batch size: 10 items

### EpochAwareBlockRealisticBenchmark

Realistic benchmarks with processing overhead (30ms per item) to simulate real-world scenarios.

**Scenarios:**
1. **PlainActorBlock_RealisticProcessing** (Baseline) - Plain ActorBlock with 30ms processing
2. **EpochActorBlock_RealisticProcessing** - EpochActorBlock with 30ms processing

**Configuration:**
- 100 items total
- 10 items per epoch
- 30ms processing delay per item

## Running the Benchmarks

For detailed instructions on running benchmarks, filtering, and exporting results, see [README.md](./README.md#running-benchmarkdotnet-benchmarks).

### Quick Commands

```bash
cd poc/DataFlow.POC.Benchmarks

# Run all epoch-aware benchmarks
dotnet run -c Release --filter "*EpochAwareBlock*"

# Run only micro-benchmarks
dotnet run -c Release --filter "EpochAwareBlockBenchmark"

# Run only realistic workload
dotnet run -c Release --filter "EpochAwareBlockRealisticBenchmark"

# Export to CSV for analysis
dotnet run -c Release --filter "*EpochAwareBlock*" --exporters csv
```

## Expected Results

### Micro-Benchmark Targets

Based on handover document requirements:

| Scenario | Expected Overhead | Acceptance Criteria |
|----------|------------------|---------------------|
| EpochActorBlock (1-to-1) | < 10% | vs PlainActorBlock baseline |
| EpochActorBlock (1-to-many) | < 15% | Additional overhead for multiple yields |
| EpochActorBlock (filtering) | < 10% | vs PlainActorBlock baseline |
| EpochBatchBlock | < 5% | Batching overhead minimal |
| Composed Pipeline | < 12% | Combined overhead acceptable |

### Realistic Workload Targets

With 30ms processing per item:

| Scenario | Expected Overhead | Acceptance Criteria |
|----------|------------------|---------------------|
| EpochActorBlock (realistic) | < 2% | Overhead negligible in real-world |

**Rationale**: In realistic workloads with significant processing time, epoch overhead becomes negligible compared to actual work.

## Interpreting Results

See [README.md](./README.md#understanding-results) for general guidance on interpreting benchmark results.

### BenchmarkDotNet Output

```
|                          Method |     Mean |   Error |  StdDev | Ratio | RatioSD |
|-------------------------------- |---------:|--------:|--------:|------:|--------:|
|   PlainActorBlock_Transform     | 1.234 ms | 0.02 ms | 0.01 ms |  1.00 |    0.00 | <- Baseline
| EpochActorBlock_Transform       | 1.345 ms | 0.03 ms | 0.02 ms |  1.09 |    0.02 | <- 9% overhead (PASS)
```

**Key Metrics:**
- **Mean**: Average execution time
- **Ratio**: Performance relative to baseline (1.09 = 9% overhead)
- **Memory**: Allocated bytes (check Gen0/Gen1/Gen2 collections)

### Acceptance Criteria

✅ **PASS**: Ratio < 1.10 (< 10% overhead)  
⚠️ **WARNING**: Ratio 1.10-1.15 (10-15% overhead)  
❌ **FAIL**: Ratio > 1.15 (> 15% overhead)

**Expected Memory**: Epoch-aware blocks allocate slightly more memory for epoch metadata and wrapper objects. Target: < 20% increase.

## See Also

- [Main Benchmarks README](./README.md) - Running benchmarks, filtering, troubleshooting
- [Handover Document](/research/flow-composability-unification/handover/github-issue-implement-epoch-aware-blocks.md) - Performance requirements
- [Block Documentation](/poc/docs/design/blocks/) - Block design documentation
