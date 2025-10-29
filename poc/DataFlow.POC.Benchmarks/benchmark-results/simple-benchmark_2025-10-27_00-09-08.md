# Simple ETL Benchmark Results

**Date:** 2025-10-27 00:09:08

## Pipeline
DataSource → Validators → Enrichers → Collector

This simplified benchmark removes routing, broadcasting, and batching to isolate core concurrency scaling.

## Results

| Test | Records | Concurrency | Non-POC (ms) | POC (ms) | Ratio | Non-POC Mem (KB) | POC Mem (KB) |
|------|---------|-------------|--------------|----------|-------|------------------|--------------|
| Concurrency: 1 (10K records) | 10,000 | 1 | 11795 | 11062 | 0.94x | 9,154 | 9,800 |
| Concurrency: 2 (10K records) | 10,000 | 2 | 5518 | 5537 | 1.00x | 8,336 | 10,153 |
| Concurrency: 4 (10K records) | 10,000 | 4 | 2804 | 2857 | 1.02x | 7,671 | 9,692 |
| Concurrency: 8 (10K records) | 10,000 | 8 | 1437 | 1458 | 1.01x | 7,423 | 9,702 |

## Analysis

### Expected Behavior
With concurrency scaling, execution time should decrease roughly proportionally:
- Concurrency 1: Baseline
- Concurrency 2: ~50% of baseline
- Concurrency 4: ~25% of baseline
- Concurrency 8: ~12-15% of baseline (accounting for overhead)

### Observations
- **Concurrency: 1 (10K records)**: POC is 0.94x slower than non-POC
- **Concurrency: 2 (10K records)**: POC is 1.00x slower than non-POC
- **Concurrency: 4 (10K records)**: POC is 1.02x slower than non-POC
- **Concurrency: 8 (10K records)**: POC is 1.01x slower than non-POC
