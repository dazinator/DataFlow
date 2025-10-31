# Comprehensive Benchmark: .NET DataFlow POC vs Python Libraries

*Generated: 2025-10-31 01:00:25*

## Libraries Compared

- **DotNet-POC**
- **Pydantic**
- **SimpleAsync**

## Overall Performance Summary

| Library | Avg Throughput | Max Throughput | Avg Memory | Min Memory |
|---------|----------------|----------------|------------|------------|
| DotNet-POC | 1,955 rec/s | 3,452 rec/s | 0.03 MB | 0.01 MB |
| Pydantic | 1,608 rec/s | 2,569 rec/s | 0.16 MB | 0.04 MB |
| SimpleAsync | 853 rec/s | 855 rec/s | 0.04 MB | 0.04 MB |

## Detailed Results by Configuration

### 1,000 records, 1 workers

| Library | Time (s) | Throughput (rec/s) | Memory (MB) | GC (0/1/2) |
|---------|----------|--------------------|--------------|-----------|
| SimpleAsync | 1.174 | 852 | 0.04 | 27/0/0 |
| Pydantic | 1.209 | 827 | 0.04 | 38/0/0 |
| DotNet-POC | 1.779 | 562 | 0.09 | 0/0/0 |

### 1,000 records, 2 workers

| Library | Time (s) | Throughput (rec/s) | Memory (MB) | GC (0/1/2) |
|---------|----------|--------------------|--------------|-----------|
| DotNet-POC | 0.592 | 1,688 | 0.03 | 0/0/0 |
| Pydantic | 0.676 | 1,480 | 0.22 | 0/1/0 |
| SimpleAsync | 1.169 | 855 | 0.04 | 23/0/0 |

### 1,000 records, 4 workers

| Library | Time (s) | Throughput (rec/s) | Memory (MB) | GC (0/1/2) |
|---------|----------|--------------------|--------------|-----------|
| DotNet-POC | 0.290 | 3,452 | 0.02 | 0/0/0 |
| Pydantic | 0.389 | 2,569 | 0.23 | 0/1/0 |
| SimpleAsync | 1.173 | 853 | 0.04 | 23/0/0 |

### 5,000 records, 1 workers

| Library | Time (s) | Throughput (rec/s) | Memory (MB) | GC (0/1/2) |
|---------|----------|--------------------|--------------|-----------|
| DotNet-POC | 5.805 | 861 | 0.01 | 0/0/0 |
| SimpleAsync | 5.865 | 853 | 0.04 | 23/0/0 |
| Pydantic | 6.035 | 829 | 0.04 | 37/0/0 |

### 5,000 records, 2 workers

| Library | Time (s) | Throughput (rec/s) | Memory (MB) | GC (0/1/2) |
|---------|----------|--------------------|--------------|-----------|
| DotNet-POC | 2.958 | 1,690 | 0.02 | 0/0/0 |
| Pydantic | 3.432 | 1,457 | 0.22 | 0/1/0 |
| SimpleAsync | 5.852 | 854 | 0.04 | 23/0/0 |

### 5,000 records, 4 workers

| Library | Time (s) | Throughput (rec/s) | Memory (MB) | GC (0/1/2) |
|---------|----------|--------------------|--------------|-----------|
| DotNet-POC | 1.488 | 3,361 | 0.02 | 0/0/0 |
| Pydantic | 1.979 | 2,527 | 0.23 | 0/1/0 |
| SimpleAsync | 5.876 | 851 | 0.04 | 23/0/0 |

### 10,000 records, 1 workers

| Library | Time (s) | Throughput (rec/s) | Memory (MB) | GC (0/1/2) |
|---------|----------|--------------------|--------------|-----------|
| DotNet-POC | 11.642 | 859 | 0.01 | 0/0/0 |
| SimpleAsync | 11.700 | 855 | 0.04 | 23/0/0 |
| Pydantic | 12.063 | 829 | 0.04 | 37/0/0 |

### 10,000 records, 2 workers

| Library | Time (s) | Throughput (rec/s) | Memory (MB) | GC (0/1/2) |
|---------|----------|--------------------|--------------|-----------|
| DotNet-POC | 5.852 | 1,709 | 0.02 | 0/0/0 |
| Pydantic | 6.928 | 1,443 | 0.22 | 0/1/0 |
| SimpleAsync | 11.716 | 854 | 0.04 | 23/0/0 |

### 10,000 records, 4 workers

| Library | Time (s) | Throughput (rec/s) | Memory (MB) | GC (0/1/2) |
|---------|----------|--------------------|--------------|-----------|
| DotNet-POC | 2.927 | 3,417 | 0.02 | 0/0/0 |
| Pydantic | 3.983 | 2,510 | 0.23 | 0/1/0 |
| SimpleAsync | 11.740 | 852 | 0.04 | 23/0/0 |

## Key Insights

### Performance Winners

- **Highest Throughput**: DotNet-POC - 3,452 rec/s (1,000 records, 4 workers)
- **Most Memory Efficient**: DotNet-POC - 0.01 MB (5,000 records, 1 workers)

### Concurrency Scaling Analysis

Comparing throughput improvement from 1 to 4 workers:

- **DotNet-POC**: 3.98x improvement (859 → 3417 rec/s)
- **Pydantic**: 3.03x improvement (829 → 2510 rec/s)
- **SimpleAsync**: 1.00x improvement (855 → 852 rec/s)

## Recommendations

Based on the comprehensive benchmark results:

1. **.NET DataFlow POC** shows **1.22x average speedup** over Pydantic
2. **.NET excels** in throughput and memory efficiency with excellent GC characteristics
3. **Pydantic** offers good Python performance with type safety
4. **SimpleAsync** is most memory-efficient but doesn't scale with concurrency

**Choose .NET DataFlow POC when:**
- Maximum performance is critical
- You need efficient memory usage
- Strong type safety and compile-time guarantees are important
- You're in a .NET ecosystem

**Choose Python (Pydantic) when:**
- Rapid prototyping is a priority
- Python ecosystem integration is needed
- Performance requirements are moderate
- Team has Python expertise
