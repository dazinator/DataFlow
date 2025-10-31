# Python Streaming DAG Library Benchmarks

This directory contains benchmark implementations comparing Python streaming DAG libraries with the .NET DataFlow POC.

## 📖 Documentation

- **[ARCHITECTURE.md](ARCHITECTURE.md)** - Detailed infrastructure overview with diagrams and component responsibilities
- **[QUICKSTART.md](QUICKSTART.md)** - Quick setup and running guide
- **[BENCHMARK_ANALYSIS.md](BENCHMARK_ANALYSIS.md)** - Detailed methodology and findings
- **[DOTNET_VS_PYTHON_RESULTS.md](DOTNET_VS_PYTHON_RESULTS.md)** - Executive summary of comparison results

## Quick Overview

The benchmarking infrastructure consists of:
1. **Independent benchmark runners** (.NET and Python) that output to common CSV/JSON format
2. **Comparison script** that loads all results and generates visualizations
3. **4 chart types** comparing throughput, memory, execution time, and speedup factors

See [ARCHITECTURE.md](ARCHITECTURE.md) for detailed workflow diagrams and component explanations.

## Libraries Tested

1. **Ray** - Actor-based DAG with distributed async streaming
2. **Pydantic-Dataflow** - Typed function DAG (async-first)
3. **Simple Async Streaming** - Lightweight async streaming implementation (StreamsConcept alternative)

## Setup

```bash
# Create virtual environment
python3 -m venv venv
source venv/bin/activate  # On Windows: venv\Scripts\activate

# Install dependencies
pip install -r requirements.txt
```

## Running Benchmarks

```bash
# Run all benchmarks
python run_benchmarks.py

# Run specific library benchmark
python run_benchmarks.py --library ray
python run_benchmarks.py --library pydantic
python run_benchmarks.py --library simple

# Run with specific workload sizes
python run_benchmarks.py --sizes 1000,5000,10000,50000

# Run with specific concurrency levels
python run_benchmarks.py --concurrency 1,2,8
```

## Benchmark Topology

All implementations use the same dataflow topology:
- **Producer** → generates records
- **Transformer** → validates and enriches records
- **Router** → routes records by category
- **Writer** → consumes final records

## Metrics Collected

- **Execution Time**: Total time to process all records
- **Throughput**: Records per second
- **Memory Usage**: Peak memory consumption (MB)
- **GC Statistics**: Collections and pause times
- **CPU Load**: Average CPU utilization

## Results

Results are saved to the `results/` directory with timestamps and include:
- Raw benchmark data (CSV)
- Summary reports (Markdown)
- Comparison charts (PNG)
