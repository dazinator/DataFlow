# Benchmark Results - POC vs Non-POC Comparison

This directory contains benchmark comparison results between the POC (Proof of Concept) and Non-POC DataFlow implementations.

## How to Run Benchmarks

### Standard Comparison
Tests three load levels (1K, 10K, 50K records):
```bash
cd poc/DataFlow.POC.Benchmarks
dotnet run -c Release -- comparison
```

### Extended Comparison
Tests various parameter combinations (load levels, concurrency, batch sizes):
```bash
cd poc/DataFlow.POC.Benchmarks
dotnet run -c Release -- extended
```

Results are saved in this directory with timestamps.

## Understanding the Results

### Metrics Tracked

- **Execution Time**: Total time to process all records through the pipeline
- **Throughput**: Records processed per second
- **Memory Usage**: Net memory allocated during execution (measured via GC)
- **GC Collections**: Number of Gen0, Gen1, and Gen2 garbage collections

### Key Findings

#### Non-POC Implementation (Current Production)
- **Strengths**:
  - Significantly faster execution time (2-4x faster at moderate concurrency)
  - Higher throughput (3-4x higher)
  - Excellent concurrency scaling (7.8x speedup from concurrency 1→8)
  - Better performance at scale
- **Weaknesses**:
  - Higher memory usage in some scenarios

#### POC Implementation (New Approach)
- **Strengths**:
  - Lower memory usage in small-to-medium loads (30-35% less)
  - Fewer GC collections
  - Cleaner, more modular architecture
  - **Competitive at low concurrency** (nearly identical to Non-POC at concurrency=1)
- **Weaknesses**:
  - Slower execution time (currently)
  - Lower throughput
  - **Poor concurrency scaling** (minimal speedup from increased concurrency)
  - Performance gap increases with load

### Critical Insight: Concurrency Scaling

The extended benchmarks reveal a critical finding:

**At concurrency=1, POC and Non-POC perform almost identically (11,825ms vs 11,779ms)**

This strongly suggests the POC implementation's performance issue is **not** in the core block execution logic, but rather in:

1. **Parallel execution strategy**: The POC may not be properly utilizing multiple threads/tasks
2. **Channel coordination**: Blocks may be waiting unnecessarily on channel operations
3. **Graph execution model**: The way the POC executes the graph topology may be inherently sequential

#### Concurrency Scaling Data (10K records):
- **Concurrency 1**: POC=11,779ms (0.996x vs Non-POC) - **Nearly identical**
- **Concurrency 2**: POC=11,823ms (1.98x slower) - **No speedup from adding thread**
- **Concurrency 4**: POC=11,739ms (4.07x slower) - **No speedup from 4 threads**
- **Concurrency 8**: POC=11,731ms (7.82x slower) - **Still no speedup**

Meanwhile, Non-POC shows excellent scaling:
- **Concurrency 1**: 11,825ms (baseline)
- **Concurrency 2**: 5,979ms (1.98x speedup)
- **Concurrency 4**: 2,883ms (4.10x speedup)
- **Concurrency 8**: 1,501ms (7.88x speedup)

### Analysis

The POC implementation shows promise in terms of memory efficiency and architectural cleanliness, but has a critical performance issue related to concurrency utilization. The fact that POC execution time stays constant (~11.7 seconds) regardless of concurrency level indicates blocks are not running in parallel.

**Priority optimization areas for POC:**
1. ✅ Core block logic is efficient (proven by concurrency=1 performance)
2. ❌ **Parallel execution model** - blocks are not running concurrently
3. ❌ **Graph coordination** - may be forcing sequential execution
4. ? **Channel strategies** - may be introducing serialization points

## Test Scenarios

### Complex ETL Pipeline

Both implementations use the same complex ETL scenario that includes:
- Data ingestion and validation
- Enrichment with external data
- Broadcasting to multiple consumers (metrics, audit)
- Routing based on record category (TypeA, TypeB, TypeC)
- Different processing paths:
  - TypeA: Individual record processing
  - TypeB: Batch aggregation
  - TypeC: Direct category storage

This mirrors real-world data processing scenarios with multiple stages and branching logic.

## Latest Results

See the timestamped markdown files in this directory for detailed results:
- `benchmark-results_YYYY-MM-DD_HH-mm-ss.md` - Standard comparison results
- `extended-benchmark_YYYY-MM-DD_HH-mm-ss.md` - Extended parameter testing results
