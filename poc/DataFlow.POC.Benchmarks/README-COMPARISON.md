# POC Comparison Benchmarks

This directory contains POC vs Non-POC comparison benchmarks using **time-series measurement** via `dotnet-counters` for accurate performance profiling.

## Overview

Compare POC and Non-POC dataflow implementations with continuous metrics collection:

| Benchmark Mode | Pipeline | Use Case |
|---------------|----------|----------|
| `direct-simple` | DataSource → Validators → Enrichers | Basic pipeline performance |
| `direct-extended` | Full pipeline with routing, broadcasting, batching | Complex pipeline performance |

**Metrics Collected:**
- GC Heap Size, Working Set Memory
- Allocation Rate (MB/s)
- GC Collections (Gen 0, 1, 2)
- ThreadPool Thread Count
- CPU Usage (when available)

## Quick Start

### Run Locally

```bash
# Simple pipeline comparison
dotnet run --project poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj \
    -c Release -- direct-simple 10000 4 3

# Extended pipeline comparison
dotnet run --project poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj \
    -c Release -- direct-extended 10000 4 100 3
```

### Profile with dotnet-counters

**Linux/macOS:**
```bash
cd poc/DataFlow.POC.Benchmarks
./profile-comparison.sh 10000 4 3 simple
```

**Windows:**
```powershell
cd poc\DataFlow.POC.Benchmarks
.\profile-comparison.ps1 -RecordCount 10000 -MaxConcurrency 4 -Iterations 3 -Mode simple
```

### Via GitHub Actions

1. Go to **Actions** → **Benchmarks**
2. Click **Run workflow**
3. Select `poc-comparison-simple` or `poc-comparison-extended`
4. Results uploaded as artifacts (CSV files + PNG charts)

## Benchmark Modes

### direct-simple
**Pipeline:** DataSource → Validators → Enrichers → Collector

Tests core concurrency scaling without routing, broadcasting, or batching complexity.

**Parameters:**
- `recordCount` - Number of records to process (default: 10000)
- `maxConcurrency` - Concurrency level (default: 4)
- `iterations` - Number of test iterations (default: 1)

### direct-extended
**Pipeline:** Full ETL with routing, broadcasting, batching

Tests complete pipeline including category-based routing, fan-out, and batch aggregation.

**Parameters:**
- `recordCount` - Number of records to process (default: 10000)
- `maxConcurrency` - Concurrency level (default: 4)
- `batchSize` - Batch aggregation size (default: 100)
- `iterations` - Number of test iterations (default: 1)

## Available Benchmarks

| Benchmark | Type | Description | Run Command |
|-----------|------|-------------|-------------|
| Simple Comparison (old) | Static GC | Legacy comparison with GC.GetTotalMemory | `dotnet run -c Release -- simple` |
| Extended Comparison (old) | Static GC | Legacy extended comparison | `dotnet run -c Release -- extended` |
| Direct Simple | Time-series | Simple pipeline with dotnet-counters | `dotnet run -c Release -- direct-simple 10000 4 3` |
| Direct Extended | Time-series | Extended pipeline with dotnet-counters | `dotnet run -c Release -- direct-extended 10000 4 100 3` |

## Tools and Scripts

### Local Profiling Scripts

**profile-comparison.sh / .ps1**
- Automates dotnet-counters collection
- Builds project, runs benchmark, collects CSV metrics
- Location: `poc/DataFlow.POC.Benchmarks/`

**visualize-counters.py**
- Generates 6-panel comparison charts from CSV
- Creates summary statistics markdown
- Location: `.github/scripts/`
- Requires: `pip install matplotlib pandas`

### Automated Runner

**run-poc-comparison.sh**
- Runs multiple configurations (1K, 5K, 10K records)
- Generates visualizations for each run
- Creates summary report with embedded charts
- Location: `.github/scripts/`

## Interpreting Results

### Expected Behaviors

✅ **Memory increases with load** - More records = more memory  
✅ **GC frequency increases** - More allocations trigger more collections  
✅ **ThreadPool scales with concurrency** - Higher concurrency = more threads  
✅ **Allocation rate reflects throughput** - Faster processing = higher rate  

### Warning Signs

🚩 **Memory decreasing with load** - Indicates measurement artifact  
🚩 **No ThreadPool growth** - Concurrency may not be working  
🚩 **Excessive Gen 2 collections** - Possible memory pressure or leaks  

## Visualization Output

Running with visualization generates:
```
poc/DataFlow.POC.Benchmarks/benchmark-results/
├── simple_10000rec_2025-10-31_12-00-00.csv     # Time-series data
├── charts_10000rec/
│   ├── comparison_metrics.png                   # 6-panel chart
│   └── summary_stats.md                         # Statistical summary
└── simple_comparison_summary_2025-10-31.md      # Overall report
```

## Why This Approach?

**Previous approach** used `GC.GetTotalMemory()` with manual GC collection, which showed:
- Memory appearing to **decrease** as workload increased
- Inconsistent results between runs
- Missing peak memory usage during execution

**Root causes:**
- GC timing artifacts from manual collection
- Post-execution measurement missed runtime peaks
- Process reuse effects (JIT warming, thread pool caching)

**New approach** uses continuous time-series measurement:
- Captures actual runtime behavior
- Identifies peak memory usage
- Shows GC frequency and pressure over time
- Reveals concurrency patterns via ThreadPool monitoring

See [POC_COMPARISON_IMPLEMENTATION.md](./POC_COMPARISON_IMPLEMENTATION.md) for detailed implementation notes.

## Requirements

**For basic benchmarks:**
- .NET 8.0 SDK
- dotnet-counters: `dotnet tool install -g dotnet-counters`

**For visualization:**
- Python 3.x
- matplotlib: `pip install matplotlib`
- pandas: `pip install pandas`

## References

- **Non-POC Profiling**: [../../src/Benchmarks/Profiling/README.md](../../src/Benchmarks/Profiling/README.md)
- **dotnet-counters**: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters
- **Issue**: Better POC comparison benchmarks (#103)
