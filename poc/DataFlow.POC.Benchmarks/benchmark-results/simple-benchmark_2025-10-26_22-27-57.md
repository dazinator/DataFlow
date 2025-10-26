# Simple ETL Benchmark Results

**Date:** 2025-10-26 22:27:57

## Pipeline
DataSource → Validators → Enrichers → Collector

This simplified benchmark removes routing, broadcasting, and batching to isolate core concurrency scaling.

## Results

| Test | Records | Concurrency | Non-POC (ms) | POC (ms) | Ratio | Non-POC Mem (KB) | POC Mem (KB) |
|------|---------|-------------|--------------|----------|-------|------------------|--------------|
| Concurrency: 1 (10K records) | 10,000 | 1 | 11903 | 11629 | 0.98x | 9,152 | 9,814 |
| Concurrency: 2 (10K records) | 10,000 | 2 | 5854 | 5838 | 1.00x | 8,216 | 9,823 |
| Concurrency: 4 (10K records) | 10,000 | 4 | 2928 | 2992 | 1.02x | 7,630 | 9,724 |
| Concurrency: 8 (10K records) | 10,000 | 8 | 1544 | 1540 | 1.00x | 7,482 | 9,720 |

## Analysis

### Expected Behavior
With concurrency scaling, execution time should decrease roughly proportionally:
- Concurrency 1: Baseline
- Concurrency 2: ~50% of baseline
- Concurrency 4: ~25% of baseline
- Concurrency 8: ~12-15% of baseline (accounting for overhead)

### Observations
- **Concurrency: 1 (10K records)**: POC is 0.98x slower than non-POC
- **Concurrency: 2 (10K records)**: POC is 1.00x slower than non-POC
- **Concurrency: 4 (10K records)**: POC is 1.02x slower than non-POC
- **Concurrency: 8 (10K records)**: POC is 1.00x slower than non-POC
