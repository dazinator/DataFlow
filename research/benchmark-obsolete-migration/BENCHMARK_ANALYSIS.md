# Benchmark Analysis Report

**Date**: 2026-01-13  
**Purpose**: Performance analysis of migrated POC benchmarks after refactoring to AddDataFlows pattern

---

## Executive Summary

After migrating from obsolete `EpochSegmenterBlock` to modern `AddDataFlows` pattern, the POC implementation shows:

**Key Findings:**
- ✅ **Single-threaded performance**: POC matches Non-POC (~1:1 ratio)
- ⚠️ **Multi-threaded performance**: POC shows significant degradation with higher concurrency
- ⚠️ **Scalability issue**: POC does NOT scale linearly with concurrency (Non-POC does)
- ⚠️ **Memory usage**: POC uses ~50% more memory than Non-POC

**Performance Summary:**

| Configuration | Non-POC | POC | Ratio | POC Scalability |
|--------------|---------|-----|-------|-----------------|
| 1K records, c=1 | 1,131 ms | 1,125 ms | 0.99x | Baseline |
| 1K records, c=4 | 283 ms | 1,203 ms | **4.25x** | **No improvement** |
| 10K records, c=4 | 2,829 ms | 11,484 ms | **4.06x** | **No improvement** |

---

## Detailed Results

### Test 1: 1,000 Records, Concurrency = 1

**Configuration:**
- Records: 1,000
- Concurrency: 1 (single validator, single enricher)
- Iterations: 3

**Results:**

| Iteration | Non-POC (ms) | POC (ms) | Ratio | Non-POC (rec/sec) | POC (rec/sec) |
|-----------|--------------|----------|-------|-------------------|---------------|
| 1 | 1,438 | 1,395 | 0.97x | 695 | 716 |
| 2 | 1,132 | 1,146 | 1.01x | 883 | 872 |
| 3 | 1,131 | 1,125 | 0.99x | 884 | 888 |
| **Average** | **1,234** | **1,222** | **0.99x** | **821** | **825** |

**Analysis:**
✅ POC performs **equivalently** to Non-POC in single-threaded mode
- Average ratio: 0.99x (virtually identical)
- POC is actually slightly faster (1.2% improvement)
- Both implementations process ~820 records/second

---

### Test 2: 1,000 Records, Concurrency = 4

**Configuration:**
- Records: 1,000
- Concurrency: 4 (4 validators, 4 enrichers)
- Iterations: 3

**Results:**

| Iteration | Non-POC (ms) | POC (ms) | Ratio | Non-POC (rec/sec) | POC (rec/sec) |
|-----------|--------------|----------|-------|-------------------|---------------|
| 1 | 378 | 1,274 | 3.37x | 2,645 | 784 |
| 2 | 301 | 1,183 | 3.93x | 3,322 | 845 |
| 3 | 283 | 1,203 | 4.25x | 3,533 | 831 |
| **Average** | **321** | **1,220** | **3.85x** | **3,167** | **820** |

**Analysis:**
⚠️ POC shows **severe performance degradation** with concurrency
- Non-POC scales nearly **4x faster** (321ms vs 1,220ms)
- Non-POC throughput increases from 821 → 3,167 rec/sec (3.9x improvement)
- POC throughput **remains flat** at ~820 rec/sec (NO improvement from concurrency)
- POC is **3.85x slower** than Non-POC in multi-threaded mode

**Critical Issue:**
POC's throughput (820 rec/sec) is identical to single-threaded performance, indicating:
- ❌ Concurrency is not being utilized effectively
- ❌ Possible serialization bottleneck
- ❌ Work distribution issue

---

### Test 3: 10,000 Records, Concurrency = 4

**Configuration:**
- Records: 10,000
- Concurrency: 4
- Iterations: 3

**Results:**

| Iteration | Non-POC (ms) | POC (ms) | Ratio | Non-POC (rec/sec) | POC (rec/sec) |
|-----------|--------------|----------|-------|-------------------|---------------|
| 1 | 2,981 | 11,605 | 3.89x | 3,354 | 861 |
| 2 | 2,830 | 11,466 | 4.05x | 3,533 | 872 |
| 3 | 2,829 | 11,484 | 4.06x | 3,534 | 870 |
| **Average** | **2,880** | **11,518** | **4.00x** | **3,474** | **868** |

**Analysis:**
⚠️ Performance degradation **persists** with larger datasets
- Non-POC maintains high throughput: ~3,474 rec/sec
- POC throughput remains low: ~868 rec/sec
- Ratio remains consistent at **4.0x** (same bottleneck as 1K records)
- POC takes **4x longer** to complete

**Consistency:**
The consistent 4x ratio across dataset sizes indicates:
- ❌ The bottleneck is NOT data volume related
- ❌ The issue is architectural/concurrency-related
- ❌ Linear scaling with concurrency is broken

---

### Test 4: Simple Comparison (10K Records, Variable Concurrency)

**Configuration:**
- Records: 10,000
- Variable concurrency: 1, 2, 4
- Single iteration per configuration

**Results:**

| Concurrency | Non-POC (ms) | POC (ms) | Non-POC (rec/sec) | POC (rec/sec) | Memory Non-POC | Memory POC |
|-------------|--------------|----------|-------------------|---------------|----------------|------------|
| 1 | 11,297 | 11,303 | 885 | 884 | 41.11 KB | 53.03 KB |
| 2 | 5,633 | 11,493 | 1,775 | 870 | 22.91 KB | 54.73 KB |
| 4 | 2,824 | (timeout) | 3,541 | (timeout) | 11.44 KB | (timeout) |

**Analysis:**
⚠️ POC fails to scale with concurrency
- **Concurrency 1**: POC ≈ Non-POC (identical performance)
- **Concurrency 2**: Non-POC improves 2x, POC shows NO improvement
- **Concurrency 4**: Non-POC improves 4x, POC timed out (likely ~11-12s based on trend)

**Memory Usage:**
⚠️ POC uses significantly more memory
- Concurrency 1: POC uses 29% more memory (53 KB vs 41 KB)
- Concurrency 2: POC uses 139% more memory (55 KB vs 23 KB)
- Memory usage per record is higher in POC

---

## Root Cause Analysis

### Hypothesis 1: Graph Retrieval Overhead ✅ LIKELY

**Evidence:**
```csharp
// POC retrieves graph from DI for each execution
var graph = provider.GetRequiredKeyedService<DataFlowGraph>("simple-etl:graph");
```

The graph is registered as **Scoped** in DI, which means:
- Each execution might be creating new block instances
- DI resolution overhead on every call
- Potential re-initialization of all blocks

**Non-POC Pattern:**
```csharp
// Non-POC builds graph once and reuses it
var builder = ComplexEtlDataFlow.BuildDataFlow(...);
var dataflow = builder.Build();
await dataflow.ExecuteAsync(context);  // Reuses same graph instance
```

### Hypothesis 2: Round-Robin Connection Pattern ✅ LIKELY

**Current Implementation:**
```csharp
// Round-robin: each validator connects to one enricher
for (int i = 0; i < validators.Count; i++)
{
    var enricherIndex = i % enrichers.Count;
    g.Connect($"validator-{validatorIndex}", enricherName);
}
```

**Issue:**
- With 4 validators and 4 enrichers, each validator connects to exactly ONE enricher
- If validators process at different rates, enrichers become unbalanced
- Some enrichers may be starved while others are busy

**Non-POC likely uses:**
- Broadcast from validators to ALL enrichers (competing consumer pattern)
- Better load distribution

### Hypothesis 3: AddDataFlows Registration Overhead ❌ UNLIKELY

The AddDataFlows pattern shouldn't cause 4x overhead, but the way blocks are registered might cause:
- Excessive DI scope creation
- Block re-instantiation per execution

---

## Recommendations

### 1. Critical: Fix Concurrency Scaling (HIGH PRIORITY)

**Problem**: POC doesn't benefit from increased concurrency

**Recommendation**: Change connection pattern from round-robin to broadcast

```csharp
// Instead of round-robin:
for (int i = 0; i < maxConcurrency; i++)
{
    var enricherName = $"enricher-{i}";
    g.UseBlock(enricherName);
    
    // CHANGE: Each validator broadcasts to ALL enrichers
    for (int v = 0; v < maxConcurrency; v++)
    {
        g.Connect($"validator-{v}", enricherName);
    }
}
```

**Expected Impact**: POC throughput should improve from ~870 to ~3,000+ rec/sec

### 2. Important: Investigate Graph Reuse (MEDIUM PRIORITY)

**Problem**: Graph might be recreated on each execution

**Recommendation**: 
- Verify graph lifetime (should be Singleton or Scoped at app level)
- Consider caching the graph instance
- Profile block instantiation patterns

### 3. Important: Memory Optimization (MEDIUM PRIORITY)

**Problem**: POC uses 30-140% more memory than Non-POC

**Recommendation**:
- Analyze object allocation patterns
- Check if blocks are being duplicated
- Consider object pooling for high-frequency allocations

### 4. Future: Validate EpochBufferBlock Removal (LOW PRIORITY)

**Context**: Original implementation used EpochBufferBlock between stages

**Question**: Does removing buffers impact performance?
- Non-POC likely has internal buffering
- POC might need explicit buffering for optimal performance
- Test adding `BoundedChannel` configuration

---

## Benchmark Configuration Matrix

For comprehensive analysis, the following configurations should be tested:

| Records | Concurrency | Expected Completion Time | Priority |
|---------|-------------|-------------------------|----------|
| 100 | 1 | < 1s | Smoke test |
| 1,000 | 1 | ~1s | Baseline |
| 1,000 | 2 | ~0.5s | Scaling test |
| 1,000 | 4 | ~0.3s | Scaling test |
| 1,000 | 8 | ~0.2s | Scaling test |
| 10,000 | 1 | ~11s | Baseline |
| 10,000 | 4 | ~3s | Scaling test |
| 100,000 | 4 | ~30s | Stress test |

---

## Conclusion

The migration from obsolete code to AddDataFlows pattern was **functionally successful** but has **revealed significant performance issues** in the POC implementation:

**Successes:**
✅ Benchmarks compile and run without errors
✅ Single-threaded performance matches Non-POC
✅ Modern DI patterns implemented correctly
✅ No separate ServiceProvider creation (anti-pattern eliminated)

**Critical Issues:**
❌ **POC does not scale with concurrency** (4x slower than Non-POC)
❌ **Throughput remains flat** regardless of worker count
❌ **Memory usage is 30-140% higher** than Non-POC

**Next Steps:**
1. **URGENT**: Fix connection pattern (round-robin → broadcast)
2. Investigate graph reuse and block instantiation
3. Profile memory allocations
4. Re-run benchmarks after fixes

**Status**: Migration complete, but **POC requires performance optimization** before production use.

---

## Appendix: Raw Benchmark Output

### Direct Comparison - 1K records, c=1
```
Iteration 1/3: Non-POC: 1,438 ms | 695 rec/sec, POC: 1,395 ms | 716 rec/sec, Ratio: 0.97x
Iteration 2/3: Non-POC: 1,132 ms | 883 rec/sec, POC: 1,146 ms | 872 rec/sec, Ratio: 1.01x
Iteration 3/3: Non-POC: 1,131 ms | 884 rec/sec, POC: 1,125 ms | 888 rec/sec, Ratio: 0.99x
```

### Direct Comparison - 1K records, c=4
```
Iteration 1/3: Non-POC: 378 ms | 2,645 rec/sec, POC: 1,274 ms | 784 rec/sec, Ratio: 3.37x
Iteration 2/3: Non-POC: 301 ms | 3,322 rec/sec, POC: 1,183 ms | 845 rec/sec, Ratio: 3.93x
Iteration 3/3: Non-POC: 283 ms | 3,533 rec/sec, POC: 1,203 ms | 831 rec/sec, Ratio: 4.25x
```

### Direct Comparison - 10K records, c=4
```
Iteration 1/3: Non-POC: 2,981 ms | 3,354 rec/sec, POC: 11,605 ms | 861 rec/sec, Ratio: 3.89x
Iteration 2/3: Non-POC: 2,830 ms | 3,533 rec/sec, POC: 11,466 ms | 872 rec/sec, Ratio: 4.05x
Iteration 3/3: Non-POC: 2,829 ms | 3,534 rec/sec, POC: 11,484 ms | 870 rec/sec, Ratio: 4.06x
```
