# .NET DataFlow POC vs Python: Benchmark Comparison

## Executive Summary

We've completed a comprehensive benchmark comparison between the .NET DataFlow POC and Python streaming DAG libraries (Pydantic and SimpleAsync). The results show that **the .NET POC significantly outperforms Python implementations** in both throughput and memory efficiency.

## Key Findings

### Overall Performance

| Library | Avg Throughput | Max Throughput | Avg Memory | Concurrency Scaling (4x) |
|---------|----------------|----------------|------------|--------------------------|
| **.NET POC** | **1,955 rec/s** | **3,452 rec/s** | **0.03 MB** | **3.98x** ✅ |
| **Pydantic** | 1,608 rec/s | 2,569 rec/s | 0.16 MB | 3.03x |
| **SimpleAsync** | 853 rec/s | 855 rec/s | 0.04 MB | 1.00x ❌ |

### Performance Advantages of .NET POC

1. **22% faster** than Pydantic on average (1.22x speedup)
2. **34% better throughput** at peak (4 workers, 1K records)
3. **5.3x more memory efficient** than Pydantic
4. **Near-perfect concurrency scaling** (3.98x with 4 workers vs 1 worker)
5. **Zero GC pressure** (no collections during benchmarks)

## Detailed Analysis

### Throughput Comparison

At **4 workers** (optimal concurrency):
- .NET POC: **3,417 rec/s** (10K records)
- Pydantic: **2,510 rec/s** (10K records)
- **Advantage: .NET is 36% faster**

At **1 worker** (single-threaded):
- .NET POC: **859 rec/s** (10K records)
- Pydantic: **829 rec/s** (10K records)
- SimpleAsync: **855 rec/s** (10K records)
- **Advantage: Competitive performance even single-threaded**

### Memory Efficiency

Average memory usage:
- .NET POC: **0.03 MB** ✅
- Pydantic: **0.16 MB**
- SimpleAsync: **0.04 MB**

**Result**: .NET uses **81% less memory** than Pydantic while being faster.

### Concurrency Scaling

Improvement from 1 to 4 workers (10K records):
- .NET POC: **3.98x** (859 → 3,417 rec/s) ✅ Near-linear
- Pydantic: **3.03x** (829 → 2,510 rec/s) ✅ Good
- SimpleAsync: **1.00x** (855 → 852 rec/s) ❌ No scaling

**Result**: .NET shows the best concurrency scaling, indicating efficient use of System.Threading.Channels.

### GC Behavior

GC Collections (Gen0/Gen1/Gen2):
- .NET POC: **0/0/0** ✅ Zero collections
- Pydantic: **0-38/0-1/0** (varies by workload)
- SimpleAsync: **23-27/0/0** (consistent)

**Result**: .NET's GC is remarkably efficient, causing zero pressure even under load.

## Visualizations

Four comprehensive charts have been generated:

1. **Throughput Comparison** (`dotnet_vs_python_throughput.png`)
   - Shows .NET's superior scaling across all configurations
   - Demonstrates near-linear concurrency scaling

2. **Memory Usage** (`dotnet_vs_python_memory.png`)
   - Highlights .NET's minimal memory footprint
   - Shows consistent memory usage regardless of workload

3. **Execution Time** (`dotnet_vs_python_execution_time.png`)
   - Visualizes faster execution times for .NET
   - Demonstrates efficiency gains with increased concurrency

4. **Speedup Factor** (`dotnet_vs_python_speedup.png`)
   - Compares all libraries against Pydantic baseline
   - Shows .NET's 1.3-1.4x advantage at 4 workers

## Architecture Insights

### Why .NET Excels

1. **System.Threading.Channels**
   - Highly optimized, lock-free data structures
   - Better than Python's asyncio.Queue
   - Native OS-level async I/O integration

2. **Compiled Code**
   - JIT-compiled to native machine code
   - Python is interpreted (even with CPython)
   - Lower overhead per operation

3. **Memory Management**
   - Generational GC with excellent tuning
   - Stack allocation for value types
   - Python's reference counting adds overhead

4. **Async/Await Model**
   - Native OS integration via SynchronizationContext
   - More efficient task scheduling
   - Lower context-switching cost

### Why Pydantic is Competitive

Despite being slower, Pydantic shows:
- Good concurrency scaling (3x with 4 workers)
- Reasonable memory usage for Python
- Type safety via runtime validation
- Excellent developer ergonomics

## Recommendations

### Choose .NET DataFlow POC when:
✅ **Performance is critical** (real-time processing, high throughput)
✅ **Memory efficiency matters** (cloud costs, resource constraints)
✅ **Strong type safety** is required (compile-time guarantees)
✅ **You're in a .NET ecosystem** (C#, Azure, Windows)
✅ **Maximum concurrency** scaling is needed

### Choose Python (Pydantic) when:
✅ **Rapid prototyping** is a priority
✅ **Python integration** is required (ML, data science)
✅ **Performance is "good enough"** (moderate workloads)
✅ **Team has Python expertise**
✅ **Cross-platform flexibility** is important

### Avoid SimpleAsync when:
❌ Concurrency is needed (it doesn't scale)
✅ Use it only for ultra-low-memory scenarios

## Benchmark Methodology

### Configuration
- **Workloads**: 1K, 5K, 10K records
- **Concurrency**: 1, 2, 4 workers
- **Topology**: Producer → Validator → Enricher → Consumer
- **Simulation**: 1ms external lookup per record

### Metrics
- **Execution Time**: Total processing time (seconds)
- **Throughput**: Records per second
- **Memory**: Peak memory usage (MB) via tracemalloc/.NET diagnostics
- **GC**: Collection counts by generation

### Environment
- .NET 8.0 on Linux x64
- Python 3.12.3
- Consistent hardware and configuration
- No external dependencies or I/O

## Conclusion

The .NET DataFlow POC demonstrates **clear performance advantages** over Python alternatives:

- **22% average speedup** over the best Python implementation (Pydantic)
- **36% peak advantage** at optimal concurrency (4 workers)
- **81% better memory efficiency** than Pydantic
- **Zero GC pressure** vs Python's frequent collections
- **Near-linear concurrency scaling** (3.98x vs 3.03x for Pydantic)

These results validate the .NET POC's architecture and implementation, showing that it's a strong choice for performance-critical streaming data pipelines.

For teams already in the .NET ecosystem, **the POC offers significant advantages** without sacrificing developer ergonomics. For Python teams, **Pydantic remains a solid choice** when raw performance isn't the primary concern.

---

**Generated**: October 31, 2025
**Full Results**: See `dotnet_vs_python_comparison.md`
**Charts**: See `comparison_charts/` directory
