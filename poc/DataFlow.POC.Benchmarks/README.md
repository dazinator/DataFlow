# DataFlow POC Benchmarks

Performance benchmarks for comparing POC and Non-POC dataflow implementations.

## Overview

This project provides comprehensive benchmarking tools for the DataFlow library's POC implementation:

| Tool | Purpose | When to Use |
|------|---------|-------------|
| **BenchmarkDotNet** | Statistical throughput analysis | Comparing multiple implementations, measuring aggregate performance |
| **dotnet-counters** | Time-series profiling | Understanding memory behavior, GC patterns, concurrency |
| **Direct execution** | External profiling | Running with dotnet-trace, dotnet-counters for detailed analysis |

## Quick Start

### 1. Run Comparison Benchmarks

**Simple pipeline comparison:**
```bash
dotnet run -c Release -- direct-simple 10000 4 3
```

**Extended pipeline comparison:**
```bash
dotnet run -c Release -- direct-extended 10000 4 100 3
```

### 2. Profile with Time-Series Metrics

**Linux/macOS:**
```bash
./profile-comparison.sh 10000 4 3 simple
```

**Windows:**
```powershell
.\profile-comparison.ps1 -RecordCount 10000 -MaxConcurrency 4 -Iterations 3 -Mode simple
```

### 3. Via GitHub Actions

Go to **Actions** → **Benchmarks** → Select:
- `poc-comparison-simple` - Simple pipeline with time-series metrics
- `poc-comparison-extended` - Extended pipeline with time-series metrics
- `poc-simple` - Legacy BenchmarkDotNet comparison (simple)
- `poc-extended` - Legacy BenchmarkDotNet comparison (extended)

Results uploaded as artifacts with CSV data and visualizations.

## Available Benchmarks

### Comparison Benchmarks (Recommended)

| Benchmark | Pipeline | Measurement | Command |
|-----------|----------|-------------|---------|
| **direct-simple** | DataSource → Validators → Enrichers | Time-series via dotnet-counters | `dotnet run -c Release -- direct-simple 10000 4 3` |
| **direct-extended** | Full ETL with routing, batching | Time-series via dotnet-counters | `dotnet run -c Release -- direct-extended 10000 4 100 3` |

**Why recommended?** Captures continuous metrics (GC heap, allocation rate, ThreadPool) during execution, providing accurate memory and concurrency insights.

### Legacy Benchmarks

| Benchmark | Pipeline | Measurement | Command |
|-----------|----------|-------------|---------|
| simple | DataSource → Validators → Enrichers | Static GC snapshots | `dotnet run -c Release -- simple` |
| extended | Full ETL with routing, batching | Static GC snapshots | `dotnet run -c Release -- extended` |
| comparison | Legacy full comparison | Static GC snapshots | `dotnet run -c Release -- comparison` |

**Note:** Legacy benchmarks use `GC.GetTotalMemory()` which can produce artifacts. Use for backward compatibility only.

### Performance Tests

| Benchmark | Description | Command |
|-----------|-------------|---------|
| Performance Tests | Basic POC performance validation | `dotnet run -c Release` (default) |

## Benchmark Types Explained

### BenchmarkDotNet Approach

**What it does:**
- Runs multiple warmup and measurement iterations
- Calculates statistical metrics (mean, median, std dev)
- Provides aggregate throughput comparisons

**When to use:**
- Comparing throughput of different implementations
- Need statistically significant results
- Measuring execution time performance

**Limitations:**
- Measures memory after completion (misses peaks)
- Manual GC collection can create artifacts
- Process reuse affects later runs

### dotnet-counters Approach (Recommended)

**What it does:**
- Collects time-series metrics during execution
- Samples every 100-500ms continuously
- Captures runtime behavior and peaks

**When to use:**
- Understanding memory behavior over time
- Analyzing GC patterns and frequency
- Verifying concurrency (ThreadPool activity)
- Identifying peak memory usage

**Metrics collected:**
- GC Heap Size (MB)
- Working Set Memory (MB)
- Allocation Rate (MB/s)
- GC Collections (Gen 0, 1, 2)
- ThreadPool Thread Count
- CPU Usage (when available)

## Running via GitHub Actions

The benchmarks GitHub Actions workflow supports:

**Non-POC benchmarks:**
- `minimal`, `simple`, `batch`, `transform-model`, `transform-memory`, `memory-rate`

**POC benchmarks (legacy):**
- `poc-simple` - Simple comparison with static GC measurement
- `poc-extended` - Extended comparison with static GC measurement
- `poc-all` - Both simple and extended

**POC benchmarks (time-series):**
- `poc-comparison-simple` - Simple with dotnet-counters
- `poc-comparison-extended` - Extended with dotnet-counters

**Workflow features:**
- Installs Python, matplotlib, pandas for visualization
- Installs dotnet-counters for metric collection
- Uploads CSV time-series data and PNG charts
- Commits results to repository for historical tracking

## Understanding Results

### Time-Series Metrics

**Expected patterns:**
- Memory **increases** with workload (more records = more memory)
- GC frequency **increases** with load (more allocations)
- ThreadPool **scales** with concurrency setting
- Allocation rate **correlates** with throughput

**Warning signs:**
- Memory **decreasing** with load → measurement artifact
- **No ThreadPool growth** → concurrency not working
- **Excessive Gen 2 collections** → memory pressure/leaks
- **Sawtooth memory** → normal for batch processing

### Visualization Output

Time-series benchmarks generate:
- **CSV files** - Raw time-series data from dotnet-counters
- **PNG charts** - 6-panel comparison (GC heap, allocation, collections, working set, threadpool, CPU)
- **Summary markdown** - Statistical analysis and configuration details

Location: `poc/DataFlow.POC.Benchmarks/benchmark-results/`

## Tools and Scripts

### Profiling Scripts

| Script | Platform | Purpose |
|--------|----------|---------|
| `profile-comparison.sh` | Linux/macOS | Automated dotnet-counters profiling |
| `profile-comparison.ps1` | Windows | Automated dotnet-counters profiling |

Located in: `poc/DataFlow.POC.Benchmarks/`

### Visualization

**visualize-counters.py**
- Parses dotnet-counters CSV output
- Generates 6-panel comparison charts
- Creates statistical summary
- Location: `.github/scripts/`
- Requires: `pip install matplotlib pandas`

### Automation

**run-poc-comparison.sh**
- Runs multiple configurations (1K, 5K, 10K records)
- Collects metrics for each
- Generates visualizations
- Creates summary report
- Location: `.github/scripts/`

## Documentation

| Document | Description |
|----------|-------------|
| **README-COMPARISON.md** | Quick start guide for comparison benchmarks |
| **POC_COMPARISON_IMPLEMENTATION.md** | Detailed implementation notes for time-series approach |
| **BENCHMARK_MEMORY_ANALYSIS.md** | Analysis of memory measurement issues with legacy approach |
| **BENCHMARK_SUMMARY.md** | Historical benchmark results summary |

## Requirements

**For basic benchmarks:**
- .NET 8.0 SDK
- Restored NuGet packages

**For time-series profiling:**
- dotnet-counters: `dotnet tool install -g dotnet-counters`

**For visualization:**
- Python 3.x
- matplotlib: `pip install matplotlib`
- pandas: `pip install pandas`

## Building and Running

```bash
# Restore dependencies
dotnet restore

# Build in release mode
dotnet build -c Release

# Run default benchmarks
dotnet run -c Release

# Run specific benchmark
dotnet run -c Release -- direct-simple 10000 4 3
```

## Architecture

The benchmarks compare:

**POC Implementation** (`DataFlow.POC`)
- Pull-based architecture using typed channels
- Actor-based execution model
- Located in: `poc/DataFlow.POC/`

**Non-POC Implementation** (`Uniun.DataFlow`)
- Production dataflow library
- Channel-based pipeline execution
- Located in: `src/DataFlow/`

Both implementations use equivalent ETL pipelines for fair comparison:
- **Simple**: DataSource → Validators → Enrichers → Collector
- **Extended**: Full pipeline with routing, broadcasting, batching

## Best Practices

### For Accurate Results

1. **Use time-series benchmarks** (`direct-simple`, `direct-extended`) for memory analysis
2. **Run multiple iterations** (3-5) to account for variability
3. **Close other applications** to reduce interference
4. **Use consistent hardware** for comparing across runs
5. **Document environment** (OS, CPU, memory) for reproducibility

### For CI/CD

1. **Run on dedicated runners** if possible
2. **Track trends over time** rather than absolute values
3. **Set performance budgets** (e.g., POC should be within 2x of Non-POC)
4. **Archive artifacts** for historical comparison

## Troubleshooting

**Issue:** Benchmark completes too quickly for dotnet-counters
- **Solution:** Increase record count (e.g., 50K+) or iterations

**Issue:** Python visualization fails
- **Solution:** Install requirements: `pip install matplotlib pandas`

**Issue:** Charts not uploaded in GitHub Actions
- **Solution:** Check workflow logs, ensure Python dependencies installed

**Issue:** Memory results seem incorrect
- **Solution:** Use time-series benchmarks, avoid legacy GC-based measurement

## References

- **Non-POC Benchmarks**: [../../src/Benchmarks/readme.md](../../src/Benchmarks/readme.md)
- **Non-POC Profiling**: [../../src/Benchmarks/Profiling/README.md](../../src/Benchmarks/Profiling/README.md)
- **dotnet-counters**: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters
- **BenchmarkDotNet**: https://benchmarkdotnet.org/

## Contributing

When adding new benchmarks:
1. Follow existing patterns (direct execution for profiling, BenchmarkDotNet for statistics)
2. Document parameters and expected results
3. Add to appropriate command-line argument handling
4. Update this README with usage examples
5. Test via GitHub Actions workflow

---

**Status:** Active development - POC comparison benchmarks with time-series measurement recommended for all memory and concurrency analysis.
