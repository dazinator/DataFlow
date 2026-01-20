# POC vs Non-POC Comparison Benchmark Results

**Mode:** extended  
**Date:** 2026-01-20 17:25:49  
**Platform:** Linux x86_64  

## Configuration

- **Pipeline Type:** extended
- **Concurrency:** 4
- **Iterations per test:** 3
- **Batch Size:** 100

## Test Scenarios

The benchmark ran with the following record counts:
- 1000 records
- 5000 records
- 10000 records

## Results

### Time-Series Metrics

For each test scenario, time-series metrics were collected using `dotnet-counters`:

- **GC Heap Size** - Memory allocated by the GC over time
- **Working Set** - Total physical memory used by the process
- **Allocation Rate** - Rate of memory allocation (MB/sec)
- **GC Collections** - Number of Gen 0, 1, 2 collections
- **ThreadPool Threads** - Number of active ThreadPool threads


## Analysis

This benchmark addresses the memory measurement anomalies observed in previous benchmarks 
by using time-series data collection with `dotnet-counters` instead of static GC snapshots.

### Key Improvements

1. **Continuous Monitoring** - Metrics are sampled continuously during execution
2. **No Manual GC** - Avoids artifacts from manual GC collection
3. **Peak Detection** - Captures peak memory usage, not just final state
4. **GC Insights** - Shows GC collection frequency and pressure
5. **ThreadPool Activity** - Reveals concurrency patterns

### Interpreting Results

- **Memory trends should be monotonic** - Higher loads should use more memory
- **GC frequency indicates pressure** - More collections suggest memory pressure
- **Allocation rate shows throughput** - Higher rates indicate more work
- **ThreadPool growth shows concurrency** - Should scale with workload

## Raw Data

CSV files with detailed time-series data:
- `extended_1000rec_2026-01-20_17-25-10.csv`
- `extended_5000rec_2026-01-20_17-25-10.csv`
- `extended_10000rec_2026-01-20_17-25-10.csv`
