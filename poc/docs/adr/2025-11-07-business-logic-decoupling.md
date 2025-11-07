# ADR: Business Logic Decoupling Pattern for Testability

**Date**: 2025-11-07  
**Status**: Recommended  
**Context**: Research Issue - Better Testing Approaches

## Context

Complex business logic embedded directly in actors (`IStreamActor<TIn, TOut>`) is difficult to unit test:
- Requires full async enumerable infrastructure
- Can't test helper methods in isolation
- Mocking is challenging
- Testing edge cases requires creating many stream scenarios

Example problem:
```csharp
public class AggregatorActor : IStreamActor<CashFlow, CashFlowAggregate>
{
    public async IAsyncEnumerable<CashFlowAggregate> RunAsync(...)
    {
        var groups = CollectGroups(input);
        foreach (var group in groups)
        {
            yield return CreateAggregate(group); // <-- Business logic, hard to test
        }
    }
    
    private CashFlowAggregate CreateAggregate(...) { /* Complex logic */ }
}
```

## Decision

Recommend pattern: Extract business logic into separate services, keep actors as thin orchestration layers.

**Recommended Pattern**:
```csharp
// Business logic as service (easily unit testable)
public interface IAggregationService
{
    CashFlowAggregate CreateAggregate(string company, DateTime date, List<CashFlow> flows);
}

public class AggregationService : IAggregationService
{
    public CashFlowAggregate CreateAggregate(string company, DateTime date, List<CashFlow> flows)
    {
        // Business logic here - pure function, easy to test
        return new CashFlowAggregate(...);
    }
}

// Actor as thin orchestration layer
public class AggregatorActor : IStreamActor<CashFlow, CashFlowAggregate>
{
    private readonly IAggregationService _service;
    
    public AggregatorActor(IAggregationService service)
    {
        _service = service;
    }
    
    public async IAsyncEnumerable<CashFlowAggregate> RunAsync(...)
    {
        var groups = await CollectGroupsAsync(input);
        foreach (var (key, flows) in groups)
        {
            yield return _service.CreateAggregate(key.Company, key.Date, flows);
        }
    }
}
```

## Alternatives Considered

### Alternative 1: Keep Logic in Actors
- **Pros**: Simpler architecture, fewer classes
- **Cons**: Poor testability, coupling, hard to maintain
- **Rejected**: Testability too important

### Alternative 2: Make Actor Methods Public
- **Pros**: Can test methods directly
- **Cons**: Breaks encapsulation, exposes implementation
- **Rejected**: Poor OOP practice

### Alternative 3: Simpler Actor Interface
- **Pros**: Could be more testable
- **Cons**: Less flexible, wouldn't support current use cases
- **Rejected**: Would break existing patterns

### Alternative 4: Test Through Integration Tests Only
- **Pros**: No architecture changes needed
- **Cons**: Slow tests, hard to isolate failures, poor coverage
- **Rejected**: Not sustainable for complex logic

## Consequences

### Positive

1. **True Unit Testing**: Business logic testable without streaming infrastructure
2. **Clear Separation**: Logic vs orchestration clearly delineated
3. **Reusability**: Services can be used outside actors
4. **Mockability**: Easy to mock services when testing actors
5. **Maintainability**: Changes to logic don't affect orchestration
6. **Testability**: Can test edge cases without complex stream setups

### Negative

1. **More Classes**: Additional interfaces and implementations
2. **DI Configuration**: More registrations needed
3. **Learning Curve**: Team needs to understand pattern
4. **Potential Over-Engineering**: Simple actors don't need this

### When to Apply

**DO decouple when**:
- Business logic is complex (> 10-15 lines)
- Logic has many edge cases
- Logic needs unit testing
- Logic might be reused
- Logic changes frequently

**DON'T decouple when**:
- Actor is simple transformation (single line)
- Logic is trivial
- No edge cases
- Integration test sufficient

## Implementation Guidance

### Testing Strategy

**Service Tests** (Unit):
```csharp
[Fact]
public void CreateAggregate_Should_Sum_Amounts()
{
    var service = new AggregationService();
    var flows = new List<CashFlow> { /* test data */ };
    
    var result = service.CreateAggregate("COMP-A", date, flows);
    
    result.TotalAmount.ShouldBe(expectedSum);
}
```

**Actor Tests** (Integration):
```csharp
[Fact]
public async Task AggregatorActor_Should_Group_And_Aggregate()
{
    var mockService = new Mock<IAggregationService>();
    var actor = new AggregatorActor(mockService.Object);
    var input = TestStreams.FromArray(/* test flows */);
    
    var results = await TestStreams.CollectAsync(actor.RunAsync(input, ctx));
    
    mockService.Verify(s => s.CreateAggregate(...), Times.Exactly(2));
}
```

### Documentation Needs

- Pattern guide in testing documentation
- Before/after examples
- Decision criteria for when to decouple
- Common patterns and anti-patterns

## Validation

- ✅ Pattern validated in research scenario
- ✅ Demonstrates clear testability improvement
- ✅ Used successfully in other frameworks
- ✅ Aligns with SOLID principles

## References

- Research: `/research/testing-approaches/`
- Scenario: `/research/testing-approaches/notes/phase2-real-world-scenario-analysis.md`
- Example: `/poc/DataFlow.POC.Tests/RealWorldScenarioTests.cs`

## Related ADRs

- [2025-11-07-test-helper-utilities.md](2025-11-07-test-helper-utilities.md) - Complements this pattern
