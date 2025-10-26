# DataFlow POC vs Non-POC - Extended Performance Comparison

**Date:** 2025-10-26 11:26:51 UTC
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
| Execution Time | 632 ms | 1,220 ms | 1.930x | Non-POC |
| Throughput | 1,581 rec/sec | 819 rec/sec | 0.518x | Non-POC |
| Memory Usage | 2,750.70 KB | 1,826.88 KB | 0.664x | POC |
| Gen0 Collections | 0 | 0 | - | - |
| Gen1 Collections | 0 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

## Load: 5K records

**Configuration:** Records=5,000, Concurrency=4, Batch=100

| Metric | Non-POC | POC | Ratio | Winner |
|--------|---------|-----|-------|--------|
| Execution Time | 1,447 ms | 5,814 ms | 4.018x | Non-POC |
| Throughput | 3,455 rec/sec | 860 rec/sec | 0.249x | Non-POC |
| Memory Usage | 12,169.75 KB | 7,923.02 KB | 0.651x | POC |
| Gen0 Collections | 0 | 0 | - | - |
| Gen1 Collections | 0 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

## Load: 10K records

**Configuration:** Records=10,000, Concurrency=4, Batch=100

| Metric | Non-POC | POC | Ratio | Winner |
|--------|---------|-----|-------|--------|
| Execution Time | 2,904 ms | 11,687 ms | 4.024x | Non-POC |
| Throughput | 3,443 rec/sec | 856 rec/sec | 0.248x | Non-POC |
| Memory Usage | 8,139.09 KB | 15,704.90 KB | 1.930x | Non-POC |
| Gen0 Collections | 1 | 0 | - | - |
| Gen1 Collections | 0 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

## Concurrency: 1 (10K records)

**Configuration:** Records=10,000, Concurrency=1, Batch=100

| Metric | Non-POC | POC | Ratio | Winner |
|--------|---------|-----|-------|--------|
| Execution Time | 11,645 ms | 11,681 ms | 1.003x | Non-POC |
| Throughput | 859 rec/sec | 856 rec/sec | 0.997x | Non-POC |
| Memory Usage | 5,622.69 KB | 15,702.30 KB | 2.793x | Non-POC |
| Gen0 Collections | 1 | 0 | - | - |
| Gen1 Collections | 0 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

## Concurrency: 2 (10K records)

**Configuration:** Records=10,000, Concurrency=2, Batch=100

| Metric | Non-POC | POC | Ratio | Winner |
|--------|---------|-----|-------|--------|
| Execution Time | 5,941 ms | 11,765 ms | 1.980x | Non-POC |
| Throughput | 1,683 rec/sec | 850 rec/sec | 0.505x | Non-POC |
| Memory Usage | 7,195.52 KB | 15,699.34 KB | 2.182x | Non-POC |
| Gen0 Collections | 1 | 0 | - | - |
| Gen1 Collections | 0 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

## Concurrency: 8 (10K records)

**Configuration:** Records=10,000, Concurrency=8, Batch=100

| Metric | Non-POC | POC | Ratio | Winner |
|--------|---------|-----|-------|--------|
| Execution Time | 1,495 ms | 11,724 ms | 7.842x | Non-POC |
| Throughput | 6,689 rec/sec | 853 rec/sec | 0.128x | Non-POC |
| Memory Usage | 10,294.88 KB | 15,698.68 KB | 1.525x | Non-POC |
| Gen0 Collections | 1 | 0 | - | - |
| Gen1 Collections | 0 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

## Batch: 50 (10K records)

**Configuration:** Records=10,000, Concurrency=4, Batch=50

| Metric | Non-POC | POC | Ratio | Winner |
|--------|---------|-----|-------|--------|
| Execution Time | 2,980 ms | 11,776 ms | 3.952x | Non-POC |
| Throughput | 3,355 rec/sec | 849 rec/sec | 0.253x | Non-POC |
| Memory Usage | 7,956.39 KB | 15,699.27 KB | 1.973x | Non-POC |
| Gen0 Collections | 1 | 0 | - | - |
| Gen1 Collections | 0 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

## Batch: 200 (10K records)

**Configuration:** Records=10,000, Concurrency=4, Batch=200

| Metric | Non-POC | POC | Ratio | Winner |
|--------|---------|-----|-------|--------|
| Execution Time | 3,012 ms | 11,734 ms | 3.896x | Non-POC |
| Throughput | 3,320 rec/sec | 852 rec/sec | 0.257x | Non-POC |
| Memory Usage | 8,127.39 KB | 15,703.31 KB | 1.932x | Non-POC |
| Gen0 Collections | 1 | 0 | - | - |
| Gen1 Collections | 0 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

## Batch: 500 (10K records)

**Configuration:** Records=10,000, Concurrency=4, Batch=500

| Metric | Non-POC | POC | Ratio | Winner |
|--------|---------|-----|-------|--------|
| Execution Time | 3,037 ms | 11,896 ms | 3.917x | Non-POC |
| Throughput | 3,292 rec/sec | 841 rec/sec | 0.255x | Non-POC |
| Memory Usage | 8,122.35 KB | 15,698.20 KB | 1.933x | Non-POC |
| Gen0 Collections | 1 | 0 | - | - |
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

