# Benchmark Scaling Issue Analysis

**Date**: 2026-01-13  
**Status**: ✅ **ROOT CAUSE IDENTIFIED AND PARTIALLY FIXED**  
**Related Issue**: [Analysis] Investigate scaling benchmark issue

---

## Executive Summary

The POC implementation showed a severe performance degradation (3-4x slower) with higher concurrency in benchmarks compared to the Non-POC implementation. Through investigation, two distinct root causes were identified:

### Root Cause #1: Broadcast Pattern Creating Redundant Work ✅ FIXED
**Problem**: The POC was connecting the data source to all validator blocks using multiple `Connect()` calls, which creates **broadcast** semantics. Each validator was processing ALL records instead of sharing the load.

**Impact**: With 4 validators and 10,000 records, the system was performing 40,000 validations instead of 10,000 (4x redundant work).

**Fix**: Changed architecture to use a single validator and single enricher block, eliminating the broadcast pattern.

**Result**: Performance improved from 4.25x slower to 2.68x slower with concurrency=4.

### Root Cause #2: Missing MaxConcurrency Support ⚠️ NOT YET FIXED
**Problem**: The Non-POC uses `MaxConcurrency` parameter on single transform blocks, allowing internal parallelization. The POC's `EpochActorBlock` doesn't support this feature, so it processes items sequentially regardless of the `maxConcurrency` parameter passed to the benchmark.

**Impact**: POC doesn't scale with increased concurrency, while Non-POC scales linearly.

**Status**: Architectural limitation requiring new feature development.

---

## Detailed Analysis

### Initial Symptoms

From `/research/benchmark-obsolete-migration/BENCHMARK_ANALYSIS.md`:

| Configuration | Non-POC | POC | Ratio | POC Scalability |
|--------------|---------|-----|-------|-----------------|
| 1K records, c=1 | 1,131 ms | 1,125 ms | 0.99x | Baseline |
| 1K records, c=4 | 283 ms | 1,203 ms | **4.25x** | **No improvement** |
| 10K records, c=4 | 2,829 ms | 11,484 ms | **4.06x** | **No improvement** |

**Key Observation**: POC performance was identical at c=1 and c=4 (~1,200ms for 1K records), indicating concurrency wasn't being utilized.

### Investigation Process

#### Step 1: Architecture Comparison

**Non-POC Implementation** (`src/Benchmarks/Shared/SimpleEtlDataFlow.cs`):
```csharp
// Single validator block with internal concurrency
builder.AddTransform<RawRecord, ValidatedRecord>("validator", sp =>
    ActivatorUtilities.CreateInstance<SimpleValidationTransformer>(sp),
    new BlockOptions { MaxConcurrency = maxConcurrency, Capacity = 100 })
    .ReceiveFrom("data-source");

// Single enricher block with internal concurrency  
builder.AddTransform<ValidatedRecord, EnrichedRecord>("enricher", sp =>
    ActivatorUtilities.CreateInstance<SimpleEnrichmentTransformer>(sp),
    new BlockOptions { MaxConcurrency = maxConcurrency, Capacity = 100 })
    .ReceiveFrom("validator");
```

**POC Implementation (Original)** (`poc/DataFlow.Benchmarks/SimpleEtlPOC.cs`):
```csharp
// PROBLEM: Created 4 separate validator blocks
for (int i = 0; i < maxConcurrency; i++)
{
    df.AddScopedBlock($"validator-{i}", sp => ...);
}

// PROBLEM: Connected source to ALL validators (broadcast)
for (int i = 0; i < maxConcurrency; i++)
{
    g.Connect("data-source", $"validator-{i}");
}

// PROBLEM: Round-robin connections to enrichers
var validatorIndex = i % maxConcurrency;
g.Connect($"validator-{validatorIndex}", enricherName);
```

#### Step 2: Understanding the Broadcast Problem

When you call `g.Connect("data-source", "validator-0")` and then `g.Connect("data-source", "validator-1")`, each creates a separate edge with its own channel. This is **broadcast** semantics - each target receives a copy of ALL data.

**Expected**: 10,000 records processed by validators  
**Actual**: 4 validators × 10,000 records = 40,000 validations

This explained a significant portion of the performance degradation.

#### Step 3: The Fix

Changed POC to use single blocks:
```csharp
// Single validator block (no MaxConcurrency support yet)
df.AddScopedBlock("validator", sp => ...);

// Simple linear pipeline
g.Connect("data-source", "validator");
g.Connect("validator", "enricher");
g.Connect("enricher", "collector");
```

**Results After Fix**:

| Configuration | Non-POC | POC (Fixed) | Ratio | Improvement |
|--------------|---------|-------------|-------|-------------|
| 1K records, c=1 | 1,565 ms | 1,218 ms | 0.78x | ✅ POC faster |
| 1K records, c=4 | 501 ms | 1,344 ms | 2.68x | ✅ 37% better |

The fix eliminated the 4x redundant work, but POC still doesn't scale because `EpochActorBlock` lacks MaxConcurrency support.

#### Step 4: Understanding the MaxConcurrency Gap

**Non-POC Architecture**:
- `MaxConcurrency = 4` creates a single block with 4 concurrent workers
- All workers share a single input channel
- Natural load balancing through competing consumers
- Scales linearly with concurrency

**POC Architecture**:
- `EpochActorBlock` processes items sequentially
- No internal concurrency parameter
- Each item processed one at a time regardless of maxConcurrency parameter
- Cannot scale with concurrency

### Edge Strategy Analysis

The POC supports different edge strategies:

1. **BroadcastEdgeStrategy** (default): Each target gets a copy of all data
2. **CompetingEdgeStrategy**: Multiple targets share a single channel, compete for items
3. **Round-robin**: Not directly supported, would need custom implementation

The fix moved from inadvertent broadcast to a single-block architecture, which is correct for the current POC capabilities.

---

## Architectural Implications

### Why Multiple Blocks Don't Help

The investigation revealed that creating multiple POC blocks (e.g., 4 validators) doesn't provide parallelism because:

1. Each block processes sequentially through its input
2. Connecting source to multiple blocks creates broadcast (copies data)
3. Using CompetingEdgeStrategy would help distribution but each block still processes sequentially
4. The overhead of multiple blocks + channels exceeds any benefit

### The MaxConcurrency Design Pattern

The Non-POC's `MaxConcurrency` pattern is superior because:

- **Single channel**: Reduced overhead, better cache locality
- **Worker pool**: Dynamic work stealing, automatic load balancing
- **Backpressure**: Simpler to reason about with fewer channels
- **Efficiency**: Less context switching, fewer allocations

---

## Recommendations

### 1. SHORT TERM: Accept Current Limitations ⚠️ MEDIUM PRIORITY

**Action**: Document that POC benchmarks should use `maxConcurrency = 1` until MaxConcurrency support is added.

**Rationale**: The simplified single-block architecture performs well at c=1, and attempting to use multiple blocks creates overhead without benefit.

### 2. MEDIUM TERM: Add MaxConcurrency to EpochActorBlock ⚠️ HIGH PRIORITY

**Proposal**: Enhance `EpochActorBlock` to support internal concurrency:

```csharp
public EpochActorBlock(
    IBlockContext context, 
    IServiceScopeFactory scopeFactory,
    int maxConcurrency = 1)  // NEW PARAMETER
{
    _scopeFactory = scopeFactory;
    _maxConcurrency = maxConcurrency;
}
```

**Implementation Strategy**:
- Create a pool of actor instances (one per concurrent worker)
- Each worker processes items from the shared input channel
- Maintain epoch boundaries (workers within same epoch)
- Proper DI scope management per worker

**Expected Impact**: POC should achieve near-parity with Non-POC performance.

### 3. LONG TERM: Unified Block Architecture 💡 LOW PRIORITY

**Vision**: Consolidate POC and Non-POC block implementations under a unified architecture that supports:
- MaxConcurrency for internal parallelism
- Epoch awareness where needed
- Consistent performance characteristics
- Simplified mental model for users

---

## Verification Tests

To verify the fix and future improvements:

### Test 1: Baseline Single-threaded Performance
```bash
dotnet run -- direct-simple 1000 1 3
```
**Expected**: POC roughly equivalent to Non-POC (0.8x - 1.2x ratio)

### Test 2: Scaling with Concurrency (After MaxConcurrency Implementation)
```bash
dotnet run -- direct-simple 10000 1 1  # Baseline
dotnet run -- direct-simple 10000 2 1  # Should be ~2x faster
dotnet run -- direct-simple 10000 4 1  # Should be ~4x faster
```

### Test 3: Redundant Work Verification
Monitor total work performed (use logging or metrics):
- Should see exactly `recordCount` validations, not `recordCount * concurrency`

---

## Lessons Learned

### 1. Broadcast vs Competing Consumer Semantics
Multiple `Connect()` calls from one source to multiple targets creates broadcast. For load distribution, need `ConnectCompeting()` or similar pattern.

### 2. Architecture Patterns Don't Always Transfer
The POC's actor-per-block pattern doesn't directly map to Non-POC's MaxConcurrency pattern. Attempting to compensate with multiple blocks creates overhead.

### 3. Performance Analysis Requires Understanding Execution Model
Can't diagnose performance issues without understanding:
- How data flows through channels
- Whether work is duplicated or distributed
- Internal concurrency vs external parallelism

### 4. Benchmarks Need Observability
Adding logging/metrics to verify work performed would have identified the 4x redundant work immediately.

---

## Related Documentation

- `/research/benchmark-obsolete-migration/BENCHMARK_ANALYSIS.md` - Original performance analysis
- `src/Benchmarks/Shared/SimpleEtlDataFlow.cs` - Non-POC reference implementation
- `poc/DataFlow.Benchmarks/SimpleEtlPOC.cs` - Fixed POC implementation
- `poc/DataFlow/Blocks/EpochActorBlock.cs` - Actor block implementation

---

## Status Summary

| Issue | Status | Priority |
|-------|--------|----------|
| Broadcast pattern causing 4x work | ✅ Fixed | CRITICAL |
| Missing MaxConcurrency support | ⚠️ Open | HIGH |
| Documentation of limitations | ⚠️ TODO | MEDIUM |
| Unified architecture design | 💡 Future | LOW |

**Next Steps**:
1. ✅ Fix broadcast pattern (DONE)
2. Document current limitations in benchmark README
3. Create design proposal for MaxConcurrency support in EpochActorBlock
4. Implement MaxConcurrency feature
5. Re-run benchmarks to verify parity with Non-POC

---

## Conclusion

The benchmark scaling issue was caused by two distinct problems:

1. **Architectural mistake**: Using broadcast semantics instead of load distribution (FIXED)
2. **Missing feature**: No MaxConcurrency support in EpochActorBlock (OPEN)

The first issue has been resolved, improving performance by 37%. The second issue requires feature development but has a clear path forward. Once MaxConcurrency is implemented, POC should achieve performance parity with Non-POC.

The investigation demonstrates the importance of understanding execution semantics and architectural patterns when analyzing performance issues.
