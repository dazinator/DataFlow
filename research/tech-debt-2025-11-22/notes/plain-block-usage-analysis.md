# Plain Block Usage Analysis

## Files Using Plain Blocks

### 1. EpochAwareBlockBenchmark.cs
**Usage Pattern:**
- `PlainSourceBlock` + `ActorBlock` used as BASELINE (Baseline = true)
- Compares plain blocks vs. epoch-aware blocks
- Measures epoch overhead (target: <10%)

**Plain Block Usage:**
- Line 39-61: `PlainActorBlock_Transform()` - baseline benchmark
- Line 367-388: `PlainActorBlock_RealisticProcessing()` - baseline with realistic processing delay
- Uses `BenchmarkPlainSource` actor
- Uses `SimpleTransformActor` and `RealisticTransformActor`

**Analysis:**
- **Purpose**: Measure epoch overhead by comparing against plain implementation
- **Value**: Provides baseline for performance comparison
- **Concern**: Plain blocks may drift from epoch blocks over time
- **Alternative**: Use first epoch benchmark as baseline instead

**Epoch Coverage:**
- `EpochActorBlock_Transform()` - equivalent with epochs
- `EpochActorBlock_OneToMany()` - 1-to-many transformation
- `EpochActorBlock_Filter()` - filtering
- `EpochBatchBlock_Batching()` - batching
- `ComposedPipeline_TransformAndBatch()` - composed pipeline

**Decision**: Can be REMOVED - epoch benchmarks are comprehensive

---

### 2. ActorBlockBenchmark.cs
**Usage Pattern:**
- Uses `ActorBlock` for rotation profiling
- Designed for external profiling with dotnet-counters
- NOT a BenchmarkDotNet benchmark

**Plain Block Usage:**
- Line 144: `SteadyStateActor` in ActorBlock (no rotation)
- Line 199: `RotatingActor` in ActorBlock (rotation every N items)
- Line 255: `MemoryIntensiveActor` in ActorBlock (memory + rotation)

**Analysis:**
- **Purpose**: Profile actor rotation behavior, memory patterns
- **Value**: Measures rotation overhead and memory release
- **Rotation**: Plain blocks support rotation via `context.RequestRotation()`
- **Concern**: Tests rotation functionality specific to `ActorBlock`
- **Alternative**: Epoch blocks don't use the same rotation mechanism

**Rotation in Epoch Architecture:**
- Epoch blocks use epoch boundaries as natural rotation points
- Different from explicit `RequestRotation()` calls
- Actor rotation is epoch-scoped

**Decision**: Can be REMOVED - rotation is now epoch-based, not actor-based

---

### 3. ComplexEtlPOC.cs
**Usage Pattern:**
- Uses `ActorBlock` extensively (18 instances)
- Complex ETL pipeline with routing, broadcasting, batching
- NOT a BenchmarkDotNet benchmark (just POC code)

**Plain Block Usage:**
- Validators, enrichers, processors, writers all use `ActorBlock`
- Used for concurrent processing via multiple instances
- Demonstrates competing consumer pattern

**Analysis:**
- **Purpose**: POC demonstration of complex ETL
- **Value**: Shows how to build complex pipelines
- **Active Use**: Unknown - appears to be reference implementation
- **Alternative**: Could be migrated to epoch blocks

**Questions:**
- Is this actively used or just example code?
- Are there tests that depend on it?
- Should it be kept as a reference implementation?

**Decision**: INVESTIGATE - Check if actively used or example code

---

### 4. SimpleEtlPOC.cs
**Usage Pattern:**
- Uses `ActorBlock` (7 instances)
- Simplified ETL: DataSource → Validators → Enrichers → Collector
- NOT a BenchmarkDotNet benchmark (just POC code)

**Plain Block Usage:**
- Validators, enrichers, collectors use `ActorBlock`
- Demonstrates fan-out/fan-in patterns with buffers

**Analysis:**
- **Purpose**: POC demonstration of simple ETL
- **Value**: Shows basic pipeline patterns
- **Active Use**: Unknown - appears to be reference implementation
- **Alternative**: Could be migrated to epoch blocks

**Decision**: INVESTIGATE - Check if actively used or example code

---

## Summary

### Definite Removals (High Confidence):
1. **EpochAwareBlockBenchmark.cs** - Plain block baselines
   - Epoch benchmarks already comprehensive
   - Can make first epoch benchmark the baseline
   
2. **ActorBlockBenchmark.cs** - Actor rotation profiling
   - Rotation is now epoch-based, not actor-based
   - No longer relevant to current architecture

### Needs Investigation:
3. **ComplexEtlPOC.cs** - Complex ETL POC
   - ✅ INVESTIGATION COMPLETE
   - **ACTIVELY USED** in benchmarks:
     - `DirectComparisonBenchmark.cs`
     - `ExtendedComparisonBenchmark.cs`
     - `ComparisonBenchmark.cs`
   - **ACTIVELY USED** in tests:
     - `ConcurrencyScalingTests.cs` - Level 5, Level 8 tests
   - **Decision**: Cannot be removed - actively used for benchmarking and testing
   
4. **SimpleEtlPOC.cs** - Simple ETL POC
   - ✅ INVESTIGATION COMPLETE
   - **ACTIVELY USED** in benchmarks:
     - `DirectComparisonBenchmark.cs`
     - `SimpleComparisonBenchmark.cs`
     - `PythonComparativeBenchmark.cs`
   - **ACTIVELY USED** for Python comparisons
   - **Decision**: Cannot be removed - actively used for benchmarking

---

## Updated Summary

### Can Be Removed Safely:
1. **EpochAwareBlockBenchmark.cs** - Plain block baseline benchmarks (2 methods)
2. **ActorBlockBenchmark.cs** - Actor rotation profiling (not a BenchmarkDotNet benchmark)
3. **ActorBlock.cs** and **PlainSourceBlock.cs** - The deprecated block implementations (if no other usage)

### Must Keep (Actively Used):
3. **ComplexEtlPOC.cs** - Used in comparison benchmarks and concurrency tests
4. **SimpleEtlPOC.cs** - Used in comparison benchmarks and Python benchmarks

### Migration Path for Kept Files:
- ComplexEtlPOC and SimpleEtlPOC should remain for now
- Can be migrated to epoch blocks in a separate effort
- Document as "legacy POC code for benchmarking"

---

## Epoch Benchmark Coverage Analysis

**Existing Epoch Benchmarks:**
1. `EpochSyntheticBaselineBenchmark` - Pure data flow vs. epoch segmentation
2. `EpochRealisticWorkloadBenchmark` - EF Core SaveChanges with varying epoch/item counts
3. `EpochProductionIOBenchmark` - Production-like I/O patterns
4. `EpochGranularityScalingBenchmark` - Epoch size scaling analysis
5. `EpochAsyncOverheadBenchmark` - Async operation overhead
6. `EpochCoordinatorContentionBenchmark` - Multi-source contention
7. `EpochTrackingBlockBenchmark` - Tracking block performance

**Coverage Assessment:**
- ✅ Synthetic baseline (no I/O)
- ✅ Realistic workload (EF Core)
- ✅ Production I/O patterns
- ✅ Scaling analysis
- ✅ Async overhead
- ✅ Multi-source coordination
- ✅ Tracking blocks

**Gaps Identified:**
- None - epoch benchmarks cover all relevant scenarios
- Plain block baselines redundant given comprehensive epoch coverage

---

## Next Steps

1. Search for references to `ComplexEtlPOC` and `SimpleEtlPOC` in tests
2. Check if these POC classes are documented as examples
3. Determine if they should be:
   - Removed (if unused)
   - Migrated to epoch blocks (if actively used)
   - Kept as deprecated examples with documentation
