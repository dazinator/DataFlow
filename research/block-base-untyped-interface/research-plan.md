# Research Plan: Block Base Untyped Interface

## Research Objective

Investigate the untyped `IBlock.ExecuteAsync(IAsyncEnumerable<object>)` interface in BlockBase.cs to determine:
1. Is it used or can it be removed?
2. If it's being used - is it boxing per item?
3. If it is boxing per item, was this something we tried to eliminate in the past?
4. If it's still needed, is there a path to replace it with type safety using reflection at graph build time?

## Research Questions

### Question 1: Is the untyped interface used?
- **Target Codebase**: POC (`/poc/DataFlow.POC/Core/BlockBase.cs`)
- **Analysis Required**:
  - Search for direct calls to `IBlock.ExecuteAsync`
  - Check if `ExecutableBlockAdapter` has replaced it
  - Verify the execution path in `DataFlowGraph.ExecuteAsync`
  
### Question 2: If used, is it boxing per item?
- **Analysis Required**:
  - Trace the execution flow from typed output to untyped interface
  - Identify where `yield return item!` boxes value types
  - Measure boxing overhead via benchmarks

### Question 3: Historical context - was boxing elimination attempted?
- **Sources to Review**:
  - `/research/typed-channel-performance.md`
  - `/poc/docs/adr/` for relevant ADRs
  - `/poc/refactoring-logs/` for past work
  - Git history for BlockBase.cs changes

### Question 4: Type-safe alternative using reflection at build time?
- **Investigation**:
  - Review existing `ExecutableBlockAdapter` pattern
  - Check if untyped interface can be fully replaced
  - Document migration path if replacement is viable

## Success Metrics

### Quantitative
- Identify exact number of callsites using untyped interface
- If boxing occurs, measure allocation overhead (bytes/item, GC pressure)
- Benchmark performance difference between typed and untyped paths

### Qualitative
- Clear understanding of whether interface is vestigial or essential
- Documented rationale for keeping or removing the interface
- Migration strategy if removal is recommended

### Baseline
- Current POC architecture using ExecutableBlockAdapter (zero-boxing)
- Production code has different IBlock interface (no untyped async enumerable)

### Validation
- Code search and static analysis
- Benchmarks if hot path usage confirmed
- All tests pass after any exploratory changes

## Validation Approach

1. **Static Code Analysis**:
   - Search for all references to `IBlock.ExecuteAsync(IAsyncEnumerable<object>)`
   - Trace execution paths in DataFlowGraph
   - Review ExecutableBlockAdapter usage

2. **Benchmark Comparison** (if needed):
   - Create benchmark comparing:
     - Untyped interface path (if used)
     - ExecutableBlockAdapter path (current)
   - Measure allocations, throughput, GC pressure
   - Test with value types (int, struct) and reference types

3. **Test Coverage Verification**:
   - Run all POC tests
   - Ensure no tests directly rely on untyped interface
   - Verify ExecutableBlockAdapter covers all scenarios

## Expected Outcomes

### Research Documentation
- `/research/block-base-untyped-interface/README.md` - Complete findings
- `/research/block-base-untyped-interface/notes/` - Exploration notes
- Benchmark results (if applicable) in `/research/block-base-untyped-interface/benchmarks/`

### Product Backlog Item
- `/product/backlog/research-YYYY-MM-DD-block-base-interface-cleanup.md`
- Implementation-ready specification for removal or refactoring
- Migration guide if changes needed

### Supporting Documentation
- Design doc if architectural changes recommended
- ADR if significant decision needed (in `/poc/docs/adr/`)
- Update to `/poc/docs/POC_GLOSSARY.md` if new terminology introduced

## Timeline

**Estimated Duration**: 1-2 days

**Phases**:
1. **Phase 1** (2-4 hours): Code analysis and usage identification
2. **Phase 2** (2-4 hours): Historical context review and benchmark creation (if needed)
3. **Phase 3** (2-3 hours): Documentation and handover materials
4. **Phase 4** (1 hour): Self-improvement evaluation and PR preparation

## Notes

- This research focuses on POC codebase - production code has different architecture
- Initial exploration suggests ExecutableBlockAdapter has already replaced the untyped path
- If untyped interface is unused, this becomes a simple cleanup task
- If it's used, this confirms a boxing issue that needs addressing
