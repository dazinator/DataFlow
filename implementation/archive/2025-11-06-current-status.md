# Plain Blocks Consolidation - Current Status

**Last Updated**: 2025-11-06  
**Phase**: 4 - Test Migration (IN PROGRESS)  
**Completion**: 39% (7/18 files fully migrated)

## Quick Status

- ✅ **Phases 1-3 Complete**: Baseline, Obsolete Marking, Performance Validation
- 🚧 **Phase 4 In Progress**: Test Migration (39% complete)
- ⏳ **Phase 5 Pending**: Remove Obsolete Blocks

## Current Metrics

| Metric | Value | Progress |
|--------|-------|----------|
| Obsolete Warnings | 197 → 174 | 23 eliminated (12%) |
| Files Fully Migrated | 7/18 | 39% |
| Files Partially Migrated | 1/18 | EdgeStrategyTests |
| Tests Passing | 174/174 | ✅ 100% |

## Files Status

### ✅ Fully Migrated (7 files)

1. **BasicFlowTests.cs** - 3 tests migrated
   - Patterns: CollectorActor, IntToStringActor
   - Separate DI scopes for processors

2. **BatchFlowTests.cs** - 2 tests migrated
   - Pattern: BatchCollectorActor for int[]

3. **BroadcastFlowTests.cs** - 1 test migrated
   - Pattern: Multiple collectors with separate DI scopes

4. **RoutingFlowTests.cs** - 2 tests migrated
   - Pattern: Routing with IntCollectorActor per route

5. **ComplexFlowTests.cs** - 2 tests migrated
   - Patterns: IntToFormattedStringActor, PrefixTransformActor, StringArrayCollectorActor

6. **EpochControlPlaneTests.cs** - 1 test migrated
   - Pattern: SimpleIntCollectorActor

7. **ActorBlockTests.cs** - 1 test migrated
   - Pattern: IntCollectorActor added to test file

### 🚧 Partially Migrated (1 file)

8. **EdgeStrategyTests.cs** - 1/10 tests migrated (9 remaining)
   - ✅ Actors defined: WorkSimulatingCollectorActor, CloneableItemCollectorActor
   - ✅ First test migrated: `CompetingEdge_Should_Distribute_Items_To_Competing_Consumers`
   - ⏳ Remaining tests: 9 tests, all using same actor patterns
   - **Next Step**: Apply same pattern to remaining 9 tests

### ⏳ Not Started (10 files)

9. **AsyncLocalPropagationTests.cs** - 9 usages
10. **BufferNodeTests.cs** - 11 usages
11. **BufferNodeDemonstrationTests.cs** - 7 usages
12. **BufferNodeControlSignalTests.cs** - 10 usages
13. **EnvelopeBlocksTests.cs** - 10 usages
14. **EnvelopeAdvancedTests.cs** - 8 usages
15. **EnvelopeEdgeStrategyTests.cs** - 7 usages
16. **OptimizedSideChannelTests.cs** - 8 usages
17. **SideChannelCompetingEdgeTests.cs** - 8 usages
18. **ConcurrencyScalingTests.cs** - ⚠️ **63 usages** (largest file, 1231 lines)

## Migration Patterns Reference

### Pattern 1: Simple Collector Actor

```csharp
private class IntCollectorActor : IStreamActor<int, object>
{
    private readonly List<int> _collected;
    
    public IntCollectorActor(List<int> collected)
    {
        _collected = collected;
    }

    public async IAsyncEnumerable<object> RunAsync(
        IAsyncEnumerable<int> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            _collected.Add(item);
        }
        yield break;
    }
}
```

### Pattern 2: Separate DI Scopes for Multiple Collectors

```csharp
// Create isolated scopes
var services1 = new ServiceCollection();
services1.AddScoped(_ => new IntCollectorActor(list1));
var sp1 = services1.BuildServiceProvider();

var services2 = new ServiceCollection();
services2.AddScoped(_ => new IntCollectorActor(list2));
var sp2 = services2.BuildServiceProvider();

// Common execution context
var commonServices = new ServiceCollection().BuildServiceProvider();

// Create blocks
var processor1 = new ActorBlock<int, object, IntCollectorActor>(
    "processor1",
    sp1.GetRequiredService<IServiceScopeFactory>());

var processor2 = new ActorBlock<int, object, IntCollectorActor>(
    "processor2",
    sp2.GetRequiredService<IServiceScopeFactory>());

// Execute with common context
var context = new ExecutionContext(commonServices, CancellationToken.None);
```

### Pattern 3: Thread-Safe Collector with Work Simulation

```csharp
private class WorkSimulatingCollectorActor : IStreamActor<int, object>
{
    private readonly List<int> _collected;
    private readonly int _delayMs;

    public WorkSimulatingCollectorActor(List<int> collected, int delayMs = 10)
    {
        _collected = collected;
        _delayMs = delayMs;
    }

    public async IAsyncEnumerable<object> RunAsync(
        IAsyncEnumerable<int> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            lock (_collected)
            {
                _collected.Add(item);
            }
            await Task.Delay(_delayMs, context.CancellationToken);
        }
        yield break;
    }
}
```

## How to Continue

### For Next Session

1. **Complete EdgeStrategyTests** (~1-2 hours)
   - 9 tests remaining, actors already defined
   - Apply same pattern as first test to remaining tests
   - Lines to update: 150, 156, 199, 206, 267, 273, 280, 289

2. **Migrate Remaining Small Files** (~3-4 hours)
   - Start with files having 7-11 usages
   - Use established patterns from reference above
   - Test incrementally after each file

3. **Tackle ConcurrencyScalingTests** (~2-3 hours dedicated)
   - Largest file: 1231 lines, 63 obsolete usages
   - Recommend dedicated focus session
   - May require additional actor patterns

4. **Test Consolidation** (~1-2 hours)
   - After all migrations complete
   - Identify redundant tests
   - Target: 20-40% reduction
   - Document in test-consolidation-report.md

### Commands

```bash
# Navigate to repository
cd /home/runner/work/lib-dataflow/lib-dataflow

# Build and test
dotnet build poc/DataFlow.POC.Tests/DataFlow.POC.Tests.csproj
dotnet test poc/DataFlow.POC.Tests/DataFlow.POC.Tests.csproj

# Check remaining obsolete warnings
dotnet build poc/DataFlow.POC.Tests/DataFlow.POC.Tests.csproj 2>&1 | grep -c "CS0618"

# Find files with obsolete blocks
grep -r "TransformerBlock\|ProcessorBlock" poc/DataFlow.POC.Tests --include="*.cs" -l
```

## Estimated Effort Remaining

- **EdgeStrategyTests completion**: 1-2 hours
- **Remaining small files (9 files)**: 3-4 hours  
- **ConcurrencyScalingTests**: 2-3 hours
- **Test consolidation**: 1-2 hours
- **Total**: ~7-11 hours

## Success Criteria for Phase 4

- [ ] All 18 test files migrated to ActorBlock (currently 7/18)
- [ ] Zero obsolete warnings (currently 174)
- [ ] All tests passing (currently ✅)
- [ ] Test count reduced by 20-40%
- [ ] Test consolidation report created

## Related Documents

- **Implementation Plan**: `/implementation/plain-blocks-consolidation/plan.md`
- **Handover Document**: `/research/flow-composability-unification/handover/github-issue-consolidate-plain-blocks.md`
- **Migration Guide**: `/poc/docs/migrations/actor-block-migration.md`
- **Performance Validation**: `/poc/docs/benchmarks/actor-block-performance-validation.md`
