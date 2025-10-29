# Simple ETL Benchmark Results

**Date:** 2025-10-26 23:39:13

## Pipeline
DataSource → Validators → Enrichers → Collector

This simplified benchmark removes routing, broadcasting, and batching to isolate core concurrency scaling.

## Results

| Test | Records | Concurrency | Non-POC (ms) | POC (ms) | Ratio | Non-POC Mem (KB) | POC Mem (KB) |
|------|---------|-------------|--------------|----------|-------|------------------|--------------|
| Concurrency: 1 (10K records) | 10,000 | 1 | 11408 | 11410 | 1.00x | 9,150 | 9,809 |
| Concurrency: 2 (10K records) | 10,000 | 2 | 5716 | 5836 | 1.02x | 8,289 | 10,113 |
| Concurrency: 4 (10K records) | 10,000 | 4 | 2895 | 2957 | 1.02x | 7,670 | 9,748 |
| Concurrency: 8 (10K records) | 10,000 | 8 | 1459 | 1457 | 1.00x | 7,410 | 9,689 |

## Analysis

### Expected Behavior
With concurrency scaling, execution time should decrease roughly proportionally:
- Concurrency 1: Baseline
- Concurrency 2: ~50% of baseline
- Concurrency 4: ~25% of baseline
- Concurrency 8: ~12-15% of baseline (accounting for overhead)

### Observations
- **Concurrency: 1 (10K records)**: POC is 1.00x slower than non-POC
- **Concurrency: 2 (10K records)**: POC is 1.02x slower than non-POC
- **Concurrency: 4 (10K records)**: POC is 1.02x slower than non-POC
- **Concurrency: 8 (10K records)**: POC is 1.00x slower than non-POC
