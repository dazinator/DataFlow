# Benchmark Results - POC vs Non-POC Comparison

This directory contains benchmark comparison results between the POC (Proof of Concept) and Non-POC DataFlow implementations.

## How to Run Benchmarks

### Standard Comparison
Tests three load levels (1K, 10K, 50K records):
```bash
cd poc/DataFlow.Benchmarks
dotnet run -c Release -- comparison
```

### Extended Comparison
Tests various parameter combinations (load levels, concurrency, batch sizes):
```bash
cd poc/DataFlow.Benchmarks
dotnet run -c Release -- extended
```

Results are saved in this directory with timestamps.

## Understanding the Results

### Metrics Tracked

- **Execution Time**: Total time to process all records through the pipeline
- **Throughput**: Records processed per second
- **Memory Usage**: Net memory allocated during execution (measured via GC)
- **GC Collections**: Number of Gen0, Gen1, and Gen2 garbage collections

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
