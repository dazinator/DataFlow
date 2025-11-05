# Implementation Issue: Consolidate Plain Blocks Around ActorBlock Pattern

## Context

This issue addresses consolidation of plain (non-epoch) stream processing blocks around the ActorBlock pattern to provide consistent DI scope safety across the codebase.

**Related Research**: `/research/flow-composability-unification/notes/actor-block-consolidation-analysis.md`

**Parent Issue**: This is a follow-up to the epoch-aware blocks implementation (#TBD). Complete that first.

## Problem Statement

Currently, the POC has specialized blocks for different operations:
- `TransformerBlock<TIn, TOut>` - transformations without DI safety
- `ProcessorBlock<T>` - processing without DI safety
- `ActorBlock<TIn, TOut, TActor>` - DI-aware with scope isolation and rotation

**Issue**: Developers can accidentally use unsafe blocks (Transformer/Processor) in concurrent scenarios, leading to dependency sharing bugs.

**Solution**: Consolidate around ActorBlock pattern as the default, providing safety by default.

## Objectives

- [ ] Establish baseline performance metrics for current blocks
- [ ] Mark TransformerBlock/ProcessorBlock as `[Obsolete]` with migration guidance
- [ ] Validate ActorBlock performance parity (<1% overhead after warmup)
- [ ] Migrate all tests to ActorBlock pattern
- [ ] Consolidate tests to eliminate redundancies
- [ ] After validation, remove obsolete blocks from codebase

## Implementation Plan

### Phase 1: Baseline Performance Capture

**Critical**: Capture current performance before any changes.

1. **Create Comprehensive Benchmarks**:
   - `TransformerBlock` scenarios:
     - Simple 1-to-1: `x => x * 2`
     - 1-to-many: `x => [x, x * 2, x * 3]`
     - Filtering: `x => x % 2 == 0 ? [x] : []`
   - `ProcessorBlock` scenarios:
     - Simple side effect: `x => counter++`
     - Async operation: `x => SaveToDbAsync(x)`

2. **Benchmark Methodology**:
   - **Warmup Phase**: Run 1000+ items to eliminate JIT/initialization costs
   - **Measurement Phase**: Measure steady-state throughput over 10,000+ items
   - **Metrics**: 
     - Throughput (items/sec)
     - Memory allocation per item (bytes)
     - Latency percentiles (P50, P95, P99)

3. **Document Results**:
   - Save to `/Benchmarks/plain-blocks-baseline-results.md`
   - Include:
     - Benchmark configuration (hardware, .NET version, etc.)
     - Raw measurements
     - Statistical summary (mean, std dev, percentiles)

**Success Criteria**: Baseline results documented and reviewed.

### Phase 2: Mark Blocks as Obsolete

**Goal**: Signal deprecation while maintaining compatibility.

1. **Add Obsolete Attributes**:
```csharp
[Obsolete("Use ActorBlock<TIn, TOut, TActor> for DI scope safety. " +
          "See migration guide: /docs/migrations/actor-block-migration.md")]
public class TransformerBlock<TIn, TOut> : BlockBase<TIn, TOut>
{
    // ... existing implementation
}

[Obsolete("Use ActorBlock<T, object, TActor> for DI scope safety. " +
          "See migration guide: /docs/migrations/actor-block-migration.md")]
public class ProcessorBlock<T> : BlockBase<T, object>
{
    // ... existing implementation
}
```

2. **Create Migration Guide**:
   - Document in `/poc/docs/migrations/actor-block-migration.md`
   - Include:
     - Why consolidation (DI safety benefits)
     - Before/after examples
     - Helper factory methods
     - Performance validation results

**Success Criteria**: Obsolete warnings appear in IDE, migration guide available.

### Phase 3: Validate ActorBlock Performance

**Goal**: Ensure ActorBlock has <1% overhead after warmup.

1. **Create ActorBlock Equivalent Benchmarks**:
   - Same scenarios as baseline
   - Use `ActorBlock<TIn, TOut, TActor>` with simple actors
   - Include warmup phase

2. **Compare Results**:
   - Calculate percentage difference: `((actor - baseline) / baseline) * 100`
   - **Target**: <1% overhead after warmup
   - If >1%, investigate and optimize:
     - Check for unnecessary allocations
     - Review DI scope creation overhead
     - Profile hot paths

3. **Document Comparison**:
   - Add to `/Benchmarks/actor-block-performance-validation.md`
   - Include side-by-side comparison table
   - Explain any differences
   - Sign off on acceptance criteria

**Success Criteria**: ActorBlock performance within <1% of baseline after warmup.

### Phase 4: Migrate Tests

**Goal**: Update all tests to use ActorBlock pattern.

1. **Identify Tests to Migrate**:
   - Find all tests using `TransformerBlock`
   - Find all tests using `ProcessorBlock`
   - List in `/docs/test-migration-checklist.md`

2. **Migration Strategy**:
   - Create helper methods for common patterns:
     ```csharp
     public static ActorBlock<TIn, TOut, SimpleTransformerActor<TIn, TOut>> 
         CreateTransformerActor<TIn, TOut>(
             string name, 
             IServiceScopeFactory scopeFactory,
             Func<TIn, TOut> transform)
     {
         // Register SimpleTransformerActor with transform function
         return new ActorBlock<TIn, TOut, SimpleTransformerActor<TIn, TOut>>(
             name, scopeFactory);
     }
     ```
   - Migrate tests one module at a time
   - Ensure all tests pass after each module migration

3. **Consolidate Redundant Tests**:
   - **Assessment Required**: Review tests for redundancies
   - Example redundancies to check:
     - Multiple tests validating same transformation logic
     - Tests that differ only in block type (Transform vs Processor vs Actor)
     - Tests with overlapping coverage
   - **Guideline**: Keep tests that validate unique scenarios or edge cases
   - **Document**: Create test consolidation report showing what was consolidated and why

**Success Criteria**: 
- All tests use ActorBlock pattern
- Redundant tests identified and consolidated
- All tests passing
- Test consolidation report reviewed

### Phase 5: Remove Obsolete Blocks

**Goal**: Clean up codebase after successful validation.

1. **Pre-Deletion Checklist**:
   - [ ] Baseline performance documented
   - [ ] ActorBlock performance validated (<1% overhead)
   - [ ] All tests migrated to ActorBlock
   - [ ] Tests consolidated to remove redundancies
   - [ ] Migration guide published
   - [ ] No remaining usages in codebase (search for `TransformerBlock<` and `ProcessorBlock<`)

2. **Deletion Process**:
   - Delete `TransformerBlock.cs`
   - Delete `ProcessorBlock.cs`
   - Remove associated test files (if fully migrated)
   - Update documentation to remove references
   - Add to CHANGELOG: "BREAKING: TransformerBlock/ProcessorBlock removed, use ActorBlock"

3. **Verification**:
   - All tests pass
   - Benchmarks still run
   - Documentation builds successfully

**Success Criteria**: Obsolete blocks removed, codebase clean, all tests passing.

## Performance Validation Methodology

### Warmup Phase

**Why**: Eliminate JIT compilation, type initialization, and cache warming effects.

**How**:
1. Run benchmark scenario for 1000+ items
2. Discard all measurements from warmup phase
3. Only then begin actual measurement

### Measurement Phase

**Metrics**:
- **Throughput**: Items processed per second (higher is better)
- **Memory**: Bytes allocated per item (lower is better)
- **Latency Percentiles**: 
  - P50 (median) - typical case
  - P95 - slower case
  - P99 - outliers

**Statistical Rigor**:
- Run each benchmark 10+ times
- Calculate mean, standard deviation
- Report confidence intervals
- Check for statistical significance of differences

### Target: <1% Regression

**Rationale**: With proper warmup and optimized implementation, the actor pattern should have near-zero overhead compared to direct function invocation.

**If Target Not Met**:
1. Profile both implementations to identify bottlenecks
2. Optimize hot paths (e.g., reduce allocations, inline methods)
3. Consider caching strategies for DI scope creation
4. Re-benchmark after optimizations
5. Document findings and optimizations applied

## Test Consolidation Guidance

**Objective**: Avoid redundant test coverage after ActorBlock migration.

**Redundancy Patterns to Look For**:

1. **Same Scenario, Different Block**:
   - Example: `TransformerBlock_ShouldTransform_Int_To_String` + `ProcessorBlock_ShouldProcess_Int`
   - **Consolidation**: Single `ActorBlock_ShouldTransform_...` test

2. **Overlapping Coverage**:
   - Example: Multiple tests validating cancellation token handling
   - **Consolidation**: One comprehensive cancellation test per block type

3. **Duplicate Edge Cases**:
   - Example: Multiple null input tests across different blocks
   - **Consolidation**: Parameterized test covering all block types

**Process**:
1. Create test coverage matrix (scenario × block type)
2. Identify cells with duplicate coverage
3. Consolidate to minimal set providing full coverage
4. Document consolidation decisions in test consolidation report

**Success Metric**: Test suite reduced by 20-40% while maintaining coverage.

## Success Criteria

- [ ] **Phase 1 Complete**: Baseline performance documented
- [ ] **Phase 2 Complete**: Blocks marked obsolete with migration guide
- [ ] **Phase 3 Complete**: ActorBlock performance validated (<1% overhead after warmup)
- [ ] **Phase 4 Complete**: All tests migrated to ActorBlock, redundancies eliminated
- [ ] **Phase 5 Complete**: Obsolete blocks removed from codebase

## Related Issues

- **Prerequisite**: Implement epoch-aware blocks (Issue #TBD)
- **Research**: `/research/flow-composability-unification/notes/actor-block-consolidation-analysis.md`

## Notes

- **Not Yet Released**: This POC code has not been released externally, so breaking changes are acceptable.
- **Safety First**: The primary goal is safety (DI scope isolation) - performance is secondary but must be validated.
- **Controlled Replacement**: This is a controlled, validated replacement with clear metrics and rollback plan if performance targets are not met.
