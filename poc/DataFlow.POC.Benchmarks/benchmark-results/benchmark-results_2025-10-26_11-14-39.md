# DataFlow POC vs Non-POC Performance Comparison

**Date:** 2025-10-26 11:14:39 UTC
**Platform:** Unix 6.11.0.1018
**.NET Version:** 8.0.20
**Processor Count:** 2

## Small Load

**Configuration:** 1,000 records, 4 concurrency, 100 batch size

### Results

| Metric | Non-POC | POC | Ratio | Winner |
|--------|---------|-----|-------|--------|
| Execution Time | 585 ms | 1,331 ms | 2.275x | Non-POC |
| Throughput | 1,707 rec/sec | 751 rec/sec | 0.440x | Non-POC |
| Memory Usage | 2,747.16 KB | 1,846.66 KB | 0.672x | POC |
| Gen0 Collections | 0 | 0 | - | - |
| Gen1 Collections | 0 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

## Medium Load

**Configuration:** 10,000 records, 4 concurrency, 100 batch size

### Results

| Metric | Non-POC | POC | Ratio | Winner |
|--------|---------|-----|-------|--------|
| Execution Time | 2,955 ms | 11,958 ms | 4.047x | Non-POC |
| Throughput | 3,383 rec/sec | 836 rec/sec | 0.247x | Non-POC |
| Memory Usage | 8,079.90 KB | 15,730.15 KB | 1.947x | Non-POC |
| Gen0 Collections | 1 | 0 | - | - |
| Gen1 Collections | 0 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

## Large Load

**Configuration:** 50,000 records, 4 concurrency, 100 batch size

### Results

| Metric | Non-POC | POC | Ratio | Winner |
|--------|---------|-----|-------|--------|
| Execution Time | 15,149 ms | 57,678 ms | 3.807x | Non-POC |
| Throughput | 3,300 rec/sec | 867 rec/sec | 0.263x | Non-POC |
| Memory Usage | 6,638.09 KB | 13,481.28 KB | 2.031x | Non-POC |
| Gen0 Collections | 7 | 4 | - | - |
| Gen1 Collections | 1 | 0 | - | - |
| Gen2 Collections | 0 | 0 | - | - |

