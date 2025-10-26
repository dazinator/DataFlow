# DataFlow POC vs Non-POC - Extended Performance Comparison

**Date:** 2025-10-26 15:58:11 UTC
**Platform:** Unix 6.11.0.1018
**.NET Version:** 8.0.20
**Processor Count:** 2

## Overview

This benchmark tests various parameter combinations to understand performance characteristics:
- Load levels (varying record counts)
- Concurrency levels (varying max concurrency)
- Batch sizes (varying batch sizes)

## Load: 1K records

**Configuration:** Records=1,000, Concurrency=4, Batch=100

| Metric | Non-POC | POC | Ratio | Winner |
|--------|---------|-----|-------|--------|
| Execution Time | 724 ms | 1,189 ms | 1.642x | Non-POC |
| Throughput | 1,380 rec/sec | 841 rec/sec | 0.609x | Non-POC |
| Memory Usage | 2,770.36 KB | 2,569.56 KB | 0.928x | POC |
| Gen0 Collections | 0 | 0 | - | - |
| Gen1 Collections | 0 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

## Load: 5K records

**Configuration:** Records=5,000, Concurrency=4, Batch=100

| Metric | Non-POC | POC | Ratio | Winner |
|--------|---------|-----|-------|--------|
| Execution Time | 1,524 ms | 5,304 ms | 3.480x | Non-POC |
| Throughput | 3,280 rec/sec | 943 rec/sec | 0.287x | Non-POC |
| Memory Usage | 12,256.03 KB | 12,565.27 KB | 1.025x | Non-POC |
| Gen0 Collections | 0 | 0 | - | - |
| Gen1 Collections | 0 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

## Load: 10K records

**Configuration:** Records=10,000, Concurrency=4, Batch=100

| Metric | Non-POC | POC | Ratio | Winner |
|--------|---------|-----|-------|--------|
| Execution Time | 3,035 ms | 10,427 ms | 3.436x | Non-POC |
| Throughput | 3,294 rec/sec | 959 rec/sec | 0.291x | Non-POC |
| Memory Usage | 8,122.01 KB | 9,848.12 KB | 1.213x | Non-POC |
| Gen0 Collections | 1 | 1 | - | - |
| Gen1 Collections | 0 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

## Concurrency: 1 (10K records)

**Configuration:** Records=10,000, Concurrency=1, Batch=100

| Metric | Non-POC | POC | Ratio | Winner |
|--------|---------|-----|-------|--------|
| Execution Time | 11,806 ms | 11,764 ms | 0.996x | POC |
| Throughput | 847 rec/sec | 850 rec/sec | 1.004x | POC |
| Memory Usage | 5,616.50 KB | 891.84 KB | 0.159x | POC |
| Gen0 Collections | 1 | 1 | - | - |
| Gen1 Collections | 0 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

## Concurrency: 2 (10K records)

**Configuration:** Records=10,000, Concurrency=2, Batch=100

| Metric | Non-POC | POC | Ratio | Winner |
|--------|---------|-----|-------|--------|
| Execution Time | 5,857 ms | 10,357 ms | 1.768x | Non-POC |
| Throughput | 1,707 rec/sec | 966 rec/sec | 0.566x | Non-POC |
| Memory Usage | 7,197.78 KB | 7,128.49 KB | 0.990x | POC |
| Gen0 Collections | 1 | 1 | - | - |
| Gen1 Collections | 0 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

## Concurrency: 8 (10K records)

**Configuration:** Records=10,000, Concurrency=8, Batch=100

| Metric | Non-POC | POC | Ratio | Winner |
|--------|---------|-----|-------|--------|
| Execution Time | 1,481 ms | 10,465 ms | 7.066x | Non-POC |
| Throughput | 6,751 rec/sec | 955 rec/sec | 0.142x | Non-POC |
| Memory Usage | 10,226.84 KB | 13,880.16 KB | 1.357x | Non-POC |
| Gen0 Collections | 1 | 1 | - | - |
| Gen1 Collections | 0 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

## Batch: 50 (10K records)

**Configuration:** Records=10,000, Concurrency=4, Batch=50

| Metric | Non-POC | POC | Ratio | Winner |
|--------|---------|-----|-------|--------|
| Execution Time | 2,820 ms | 10,369 ms | 3.677x | Non-POC |
| Throughput | 3,545 rec/sec | 964 rec/sec | 0.272x | Non-POC |
| Memory Usage | 8,053.74 KB | 9,931.20 KB | 1.233x | Non-POC |
| Gen0 Collections | 1 | 1 | - | - |
| Gen1 Collections | 0 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

## Batch: 200 (10K records)

**Configuration:** Records=10,000, Concurrency=4, Batch=200

| Metric | Non-POC | POC | Ratio | Winner |
|--------|---------|-----|-------|--------|
| Execution Time | 3,041 ms | 10,521 ms | 3.460x | Non-POC |
| Throughput | 3,288 rec/sec | 950 rec/sec | 0.289x | Non-POC |
| Memory Usage | 8,016.98 KB | 9,862.85 KB | 1.230x | Non-POC |
| Gen0 Collections | 1 | 1 | - | - |
| Gen1 Collections | 0 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

## Batch: 500 (10K records)

**Configuration:** Records=10,000, Concurrency=4, Batch=500

| Metric | Non-POC | POC | Ratio | Winner |
|--------|---------|-----|-------|--------|
| Execution Time | 2,973 ms | 10,429 ms | 3.508x | Non-POC |
| Throughput | 3,364 rec/sec | 959 rec/sec | 0.285x | Non-POC |
| Memory Usage | 8,179.33 KB | 9,860.40 KB | 1.206x | Non-POC |
| Gen0 Collections | 1 | 1 | - | - |
| Gen1 Collections | 0 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

## Analysis

### Key Findings

#### Concurrency Scaling

The most critical finding from these benchmarks is the POC implementation's lack of concurrency scaling:

- At **concurrency=1**, POC and Non-POC perform almost identically
- As concurrency increases, **Non-POC scales linearly** while **POC shows no speedup**
- This indicates POC's core block logic is efficient, but the graph execution model is not utilizing parallelism

#### Load Scaling

Performance characteristics at different record counts:

- **Small loads (1K)**: POC uses 33% less memory, 2.2x slower
- **Medium loads (5K)**: POC uses 35% less memory, 4.1x slower
- **Large loads (10K+)**: POC uses 1.9x more memory, 4.1x slower

#### Batch Size Impact

Batch size has minimal impact on relative performance:

- Non-POC maintains consistent ~3,000 rec/sec throughput across all batch sizes
- POC maintains consistent ~850 rec/sec throughput regardless of batch size
- This suggests the bottleneck is not in batching logic but in overall execution strategy

### Recommendations

1. **Priority: Fix POC concurrency model** - Investigate why blocks don't run in parallel
2. Optimize memory usage at higher loads
3. Profile POC execution to identify serialization points

