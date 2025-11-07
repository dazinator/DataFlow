# Initiative: Test Helper Adoption

**Status**: Active  
**Started**: 2025-11-07  
**Owner**: Implementation Team

## Objective

Incrementally refactor POC tests to use the new test helper utilities (`TestServiceBuilder`, `CollectorActor`, `TestStreams`, etc.) to reduce boilerplate and improve maintainability.

## Background

Test helper utilities were introduced in PR #[165] to reduce test boilerplate by 40-60%. The helpers are production-ready and documented, but only a small portion of existing tests have been refactored to use them.

**References:**
- ADR: `/poc/docs/adr/2025-11-07-test-helper-utilities.md`
- Testing Guide: `/poc/docs/guides/testing-guide.md`
- Test Helpers: `/poc/DataFlow.POC.Tests/TestHelpers/`

## Pattern to Follow

When writing new tests or modifying existing tests, replace custom implementations with test helpers:

### Replace Custom Collectors

**Before:**
```csharp
private class IntCollectorActor : IStreamActor<int, object>
{
    private readonly List<int> _collected;
    public IntCollectorActor(List<int> collected) => _collected = collected;
    
    public async IAsyncEnumerable<object> RunAsync(
        IAsyncEnumerable<int> input, IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            _collected.Add(item);
        }
        yield break;
    }
}
```

**After:**
```csharp
// Use TestHelpers.CollectorActor<T> - no custom class needed
using DataFlow.POC.Tests.TestHelpers;
// ...
var collected = new List<int>();
var collector = new CollectorActor<int>(collected);
```

### Replace Custom Producers

**Before:**
```csharp
private static async IAsyncEnumerable<int> ProduceIntegers(IExecutionContext ctx, int count)
{
    for (int i = 1; i <= count; i++)
    {
        yield return i;
    }
}
```

**After:**
```csharp
// Use TestStreams.Integers()
var producer = new ProducerBlock<int>("producer", _ => TestStreams.Integers(10));
```

### Replace Manual ServiceCollection Setup

**Before:**
```csharp
var services = new ServiceCollection();
services.AddScoped(_ => new MyCollector(list));
var serviceProvider = services.BuildServiceProvider();
var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
```

**After:**
```csharp
// Use TestServiceBuilder
var scopeFactory = TestServiceBuilder.Create()
    .WithScoped(new MyCollector(list))
    .BuildScopeFactory();
```

### Replace Custom Transform Actors

**Before:**
```csharp
private class IntToStringActor : IStreamActor<int, string>
{
    public async IAsyncEnumerable<string> RunAsync(
        IAsyncEnumerable<int> input, IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            yield return $"Item-{item}";
        }
    }
}
```

**After:**
```csharp
// Use TransformActor<TIn, TOut>
var transformer = new TransformActor<int, string>(i => $"Item-{i}");
```

## When to Apply

Apply this initiative when:
- ✅ Writing new tests in `/poc/DataFlow.POC.Tests/`
- ✅ Modifying existing tests that have custom collectors/producers
- ✅ Refactoring tests for readability
- ✅ Fixing test bugs (refactor while fixing)
- ✅ After completing primary implementation work (if time permits)

## When NOT to Apply

Don't apply when:
- ❌ Test has specialized logic (tracking, delays, complex state)
- ❌ Custom actor behavior is essential to what's being tested
- ❌ Refactoring would complicate an already complex PR
- ❌ Unfamiliar with test helper APIs (read guide first)

## Success Metrics

**Target**: Refactor 25-50% of applicable test files over time  
**Current Status**: 5/~30 applicable files refactored (17%)

**Refactored Files:**
- BasicFlowTests.cs (-31 lines, 19% reduction)
- BroadcastFlowTests.cs (-27 lines, 28% reduction)
- BatchFlowTests.cs (-9 lines, 8% reduction)
- ComplexFlowTests.cs (-89 lines, 36% reduction)
- RoutingFlowTests.cs (-54 lines, 30% reduction)

**Total Impact So Far:**
- 210 lines removed
- 24% average reduction
- 10 tests refactored

## How to Identify Candidates

Look for test files with:
```bash
# Files with custom collectors
grep -l "class.*CollectorActor" poc/DataFlow.POC.Tests/*.cs

# Files with custom producers  
grep -l "IAsyncEnumerable.*Produce" poc/DataFlow.POC.Tests/*.cs

# Files with manual ServiceCollection setup
grep -l "new ServiceCollection()" poc/DataFlow.POC.Tests/*.cs
```

## References

- **Testing Guide**: `/poc/docs/guides/testing-guide.md`
- **ADR**: `/poc/docs/adr/2025-11-07-test-helper-utilities.md`
- **Demo Tests**: `/poc/DataFlow.POC.Tests/TestHelpersDemoTests.cs`
- **Test Helpers**: `/poc/DataFlow.POC.Tests/TestHelpers/`
- **Original PR**: #165

## Progress Log

### 2025-11-07 - PR #165
- Implemented test helper utilities
- Created comprehensive documentation
- Refactored 5 test files as proof of concept
- Files: BasicFlowTests, BroadcastFlowTests, BatchFlowTests, ComplexFlowTests, RoutingFlowTests
- Total reduction: 210 lines (24% average)
