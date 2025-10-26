# DataFlow POC vs Non-POC - Extended Performance Comparison

**Date:** 2025-10-26 11:20:50 UTC
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
| Execution Time | 600 ms | 1,295 ms | 2.158x | Non-POC |
| Throughput | 1,666 rec/sec | 772 rec/sec | 0.463x | Non-POC |
| Memory Usage | 2,743.31 KB | 1,845.85 KB | 0.673x | POC |
| Gen0 Collections | 0 | 0 | - | - |
| Gen1 Collections | 0 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

## Load: 5K records

**Configuration:** Records=5,000, Concurrency=4, Batch=100

| Metric | Non-POC | POC | Ratio | Winner |
|--------|---------|-----|-------|--------|
| Execution Time | 1,424 ms | 5,859 ms | 4.114x | Non-POC |
| Throughput | 3,511 rec/sec | 853 rec/sec | 0.243x | Non-POC |
| Memory Usage | 12,178.34 KB | 7,932.19 KB | 0.651x | POC |
| Gen0 Collections | 0 | 0 | - | - |
| Gen1 Collections | 0 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

## Load: 10K records

**Configuration:** Records=10,000, Concurrency=4, Batch=100

| Metric | Non-POC | POC | Ratio | Winner |
|--------|---------|-----|-------|--------|
| Execution Time | 2,883 ms | 11,739 ms | 4.072x | Non-POC |
| Throughput | 3,468 rec/sec | 852 rec/sec | 0.246x | Non-POC |
| Memory Usage | 8,081.86 KB | 15,702.45 KB | 1.943x | Non-POC |
| Gen0 Collections | 1 | 0 | - | - |
| Gen1 Collections | 0 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

## Concurrency: 1 (10K records)

**Configuration:** Records=10,000, Concurrency=1, Batch=100

| Metric | Non-POC | POC | Ratio | Winner |
|--------|---------|-----|-------|--------|
| Execution Time | 11,825 ms | 11,779 ms | 0.996x | POC |
| Throughput | 846 rec/sec | 849 rec/sec | 1.004x | POC |
| Memory Usage | 5,632.52 KB | 15,697.48 KB | 2.787x | Non-POC |
| Gen0 Collections | 1 | 0 | - | - |
| Gen1 Collections | 0 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

## Concurrency: 2 (10K records)

**Configuration:** Records=10,000, Concurrency=2, Batch=100

| Metric | Non-POC | POC | Ratio | Winner |
|--------|---------|-----|-------|--------|
| Execution Time | 5,979 ms | 11,823 ms | 1.977x | Non-POC |
| Throughput | 1,672 rec/sec | 846 rec/sec | 0.506x | Non-POC |
| Memory Usage | 7,255.80 KB | 15,699.25 KB | 2.164x | Non-POC |
| Gen0 Collections | 1 | 0 | - | - |
| Gen1 Collections | 0 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

## Concurrency: 8 (10K records)

**Configuration:** Records=10,000, Concurrency=8, Batch=100

| Metric | Non-POC | POC | Ratio | Winner |
|--------|---------|-----|-------|--------|
| Execution Time | 1,501 ms | 11,731 ms | 7.815x | Non-POC |
| Throughput | 6,660 rec/sec | 852 rec/sec | 0.128x | Non-POC |
| Memory Usage | 10,315.05 KB | 15,696.68 KB | 1.522x | Non-POC |
| Gen0 Collections | 1 | 0 | - | - |
| Gen1 Collections | 0 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

## Batch: 50 (10K records)

**Configuration:** Records=10,000, Concurrency=4, Batch=50

| Metric | Non-POC | POC | Ratio | Winner |
|--------|---------|-----|-------|--------|
| Execution Time | 3,007 ms | 11,795 ms | 3.923x | Non-POC |
| Throughput | 3,325 rec/sec | 848 rec/sec | 0.255x | Non-POC |
| Memory Usage | 8,184.03 KB | 15,703.77 KB | 1.919x | Non-POC |
| Gen0 Collections | 1 | 0 | - | - |
| Gen1 Collections | 0 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

## Batch: 200 (10K records)

**Configuration:** Records=10,000, Concurrency=4, Batch=200

| Metric | Non-POC | POC | Ratio | Winner |
|--------|---------|-----|-------|--------|
| Execution Time | 3,004 ms | 11,815 ms | 3.933x | Non-POC |
| Throughput | 3,328 rec/sec | 846 rec/sec | 0.254x | Non-POC |
| Memory Usage | 8,036.63 KB | 15,688.00 KB | 1.952x | Non-POC |
| Gen0 Collections | 1 | 0 | - | - |
| Gen1 Collections | 0 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

## Batch: 500 (10K records)

**Configuration:** Records=10,000, Concurrency=4, Batch=500

| Metric | Non-POC | POC | Ratio | Winner |
|--------|---------|-----|-------|--------|
| Execution Time | 3,021 ms | 11,867 ms | 3.928x | Non-POC |
| Throughput | 3,310 rec/sec | 843 rec/sec | 0.255x | Non-POC |
| Memory Usage | 8,151.76 KB | 15,713.37 KB | 1.928x | Non-POC |
| Gen0 Collections | 1 | 0 | - | - |
| Gen1 Collections | 0 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

## Analysis

### Load Scaling
How performance changes with increasing record counts.

### Concurrency Scaling
How performance changes with different concurrency levels.

### Batch Size Impact
How batch size affects throughput and memory usage.

