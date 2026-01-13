# Research Plan: Benchmark Migration from Obsolete Code

## Research Objective

Migrate benchmarks from obsolete `SimpleEtlPOC.BuildDataFlow` method to modern DI-based registration pattern. The obsolete method used the removed `EpochSegmenterBlock` and needs to be updated to use graph-level `ConfigureEpochs()` API and modern DI registration patterns.

## Research Questions

### Q1: What is the current obsolete pattern?

**Current State:**
- `SimpleEtlPOC.BuildDataFlow()` is marked `[Obsolete]` and throws `NotSupportedException`
- Uses removed `EpochSegmenterBlock` 
- Direct block instantiation without DI
- Three benchmarks depend on this method:
  - `PythonComparativeBenchmark.cs` (line 148)
  - `SimpleComparisonBenchmark.cs` (line 157)
  - `DirectComparisonBenchmark.cs` (line 183)

### Q2: What is the modern pattern?

**Reference Examples:**
- `RevisedDiRegistrationTests.cs` - Shows DI block registration patterns
- `BlockLifetimeAndGraphReuseTests.cs` - Shows graph building and reuse patterns

**Key Modern Patterns:**
1. Use `services.AddDataFlows("namespace", df => {...})` for registration
2. Register blocks with `df.AddScopedBlock()` or `df.AddActorBlock<TIn, TOut, TActor>()`
3. Build graphs with `df.AddGraph("graph-name", g => {...})`
4. Use `GraphHelpers.CreateGraphBuilder()` with ServiceProvider
5. Retrieve graphs via `serviceProvider.GetRequiredKeyedService<DataFlowGraph>("namespace:graph-name")`

### Q3: How should SimpleEtlPOC dataflow be modernized?

**Approach Options:**
1. **Convert to DI registration** - Register all blocks and graph globally
2. **Hybrid approach** - Use DI for some blocks, direct instantiation for others
3. **Builder-based** - Use GraphHelpers.CreateGraphBuilder with ServiceProvider

**Preferred**: Hybrid approach using `GraphHelpers.CreateGraphBuilder()` with minimal DI registration for actors.

### Q4: What about epoch configuration?

The obsolete code used `EpochSegmenterBlock`. Modern approach:
- Remove `EpochSegmenterBlock` usage
- Use `graph.ConfigureEpochs()` at graph level (if needed)
- Or simplify to plain streams if epochs not required for benchmarks

## Success Metrics

### Quantitative
- All 3 benchmarks compile without errors
- All 3 benchmarks execute successfully
- Benchmark execution time similar to previous (±10%)

### Qualitative
- Code follows modern DI patterns from reference examples
- Code is clear and maintainable
- Pattern can be reused for other POC benchmarks

### Baseline
- Current: Benchmarks throw `NotSupportedException`
- Target: Benchmarks execute and produce valid results

### Validation
- Run each benchmark and verify output
- Compare with non-POC benchmarks for sanity check

## Validation Approach

1. **Build Validation**
   - Ensure all benchmarks compile
   - Check for obsolete warnings

2. **Execution Validation**
   - Run `PythonComparativeBenchmark`
   - Run `SimpleComparisonBenchmark`
   - Run `DirectComparisonBenchmark`
   - Verify no exceptions

3. **Pattern Validation**
   - Compare with `RevisedDiRegistrationTests` patterns
   - Ensure DI usage is consistent
   - Verify actor registration follows best practices

## Expected Outcomes

1. **Research Documentation** in `/research/benchmark-obsolete-migration/`
   - Analysis of obsolete vs modern patterns
   - Design decisions documented
   - Migration guide

2. **Implementation-ready work item**
   - Complete specification for migration
   - Test scenarios defined
   - Acceptance criteria clear

3. **Prototype Code** (saved in handover/prototype/)
   - Working SimpleEtlPOC using modern pattern
   - Updated benchmark implementations
   - Test validations

4. **Formal Documentation**
   - ADR documenting pattern choice (if needed)
   - Updated guides for POC benchmarks

## Timeline

**Estimated Duration**: 2-3 days

**Phases:**
1. Phase 1 (0.5 day): Deep analysis of current vs target patterns
2. Phase 2 (1 day): Prototype implementation and testing
3. Phase 3 (0.5 day): Documentation and handover
4. Phase 4 (0.5 day): Review and finalization
