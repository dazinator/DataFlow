# Phase 2: Real-World Scenario Analysis

## Scenario Overview

Created a realistic cashflow processing pipeline to evaluate testing challenges:

**Business Requirements:**
1. Load cashflows from source
2. Aggregate by company code and value date
3. Check if aggregates already exist in database
4. Create new records only for non-existent aggregates
5. Route to different processors based on amount threshold

**Implementation:**
- 6 actors with complex business logic
- Database dependency (mock for testing)
- Multiple service providers needed
- Complex graph wiring
- Side effects (database writes)

## Code Statistics

**Real-World Scenario Test File:**
- ~520 lines of code
- 5 business logic actors
- 3 integration tests (1 complete, 2 unit attempts)
- 8 separate service provider setups in one test
- Domain models, mock database, test helpers

## Testing Challenges Discovered

### 1. Service Provider Boilerplate (Critical Issue)

**Problem**: Every test requires extensive DI setup.

**Example from Complete_CashFlow_Pipeline test:**
```csharp
// 8 separate service provider setups!
var commonServices = new ServiceCollection();
commonServices.AddScoped<ICashFlowDatabase>(_ => database);
var commonServiceProvider = commonServices.BuildServiceProvider();

var aggregatorServices = new ServiceCollection();
aggregatorServices.AddScoped<CashFlowAggregatorActor>();
var aggregatorServiceProvider = aggregatorServices.BuildServiceProvider();

// ... 6 more similar blocks
```

**Impact:**
- ~60 lines of boilerplate in a single test
- Error-prone (easy to forget dependencies)
- Cognitive overhead for test authors
- Obscures test intent

**Observation**: This is THE biggest pain point in POC testing.

### 2. Testing Business Logic in Actors (Critical Issue)

**Problem**: Business logic embedded in actors is hard to unit test.

**Example - CashFlowAggregatorActor:**
```csharp
public class CashFlowAggregatorActor : IStreamActor<CashFlow, CashFlowAggregate>
{
    public async IAsyncEnumerable<CashFlowAggregate> RunAsync(...)
    {
        // Complex aggregation logic mixed with streaming
        var groups = new Dictionary<(string, DateTime), List<CashFlow>>();
        await foreach (var cashFlow in input) { /* ... */ }
        foreach (var (key, flows) in groups)
        {
            yield return CreateAggregate(key, flows); // <-- Business logic here
        }
    }
    
    // Want to unit test this separately
    private CashFlowAggregate CreateAggregate(...) { /* ... */ }
}
```

**Testing Requirements:**
- Must create `IAsyncEnumerable<CashFlow>` input
- Must consume `IAsyncEnumerable<CashFlowAggregate>` output
- Must provide `IActorExecutionContext` mock
- Cannot test `CreateAggregate` method directly (private)

**Attempted Unit Test:**
```csharp
[Fact]
public async Task CashFlowAggregator_Should_Group_By_Company_And_Date()
{
    // Still requires DI setup
    var services = new ServiceCollection();
    services.AddTransient<CashFlowAggregatorActor>();
    var serviceProvider = services.BuildServiceProvider();

    // Create actor and context
    var actor = serviceProvider.GetRequiredService<CashFlowAggregatorActor>();
    var context = new ActorExecutionContext(CancellationToken.None);

    // Create input stream
    var input = new List<CashFlow> { /* ... */ }.ToAsyncEnumerable();

    // Execute and collect
    var results = new List<CashFlowAggregate>();
    await foreach (var aggregate in actor.RunAsync(input, context))
    {
        results.Add(aggregate);
    }

    // Assert
    results.Count.ShouldBe(2);
}
```

**Issues:**
- Not really a "unit" test - still requires full streaming infrastructure
- Business logic (`CreateAggregate`) can't be tested in isolation
- Hard to test edge cases without creating many input scenarios
- Mocking is difficult because actor controls the stream

### 3. Testing Database Interactions

**Problem**: Actors with database dependencies are hard to test.

**Example - DatabaseCheckActor:**
```csharp
public class DatabaseCheckActor : IStreamActor<CashFlowAggregate, AggregateCheckResult>
{
    private readonly ICashFlowDatabase _database;

    public DatabaseCheckActor(ICashFlowDatabase database) { /* ... */ }

    public async IAsyncEnumerable<AggregateCheckResult> RunAsync(...)
    {
        await foreach (var aggregate in input)
        {
            var exists = await _database.AggregateExistsAsync(...);
            yield return new AggregateCheckResult(aggregate, exists);
        }
    }
}
```

**Testing Challenges:**
- Must mock database through DI
- Must create streams to test interaction
- Hard to verify database calls without integration test
- Side effects (writes) are hard to assert without full execution

### 4. Graph Wiring Complexity

**Problem**: Building test graphs is verbose and error-prone.

**From Complete_CashFlow_Pipeline test:**
```csharp
var builder = new DataFlowGraphBuilder("cashflow-pipeline");
builder.AddBlock(producer)
    .AddBlock(aggregator)
    .Connect(producer, aggregator)
    .AddBlock(checker)
    .Connect(aggregator, checker)
    .AddBlock(creator)
    .Connect(checker, creator);
    // Plus routing blocks, filters, collectors...
```

**Issues:**
- 20+ lines just for graph construction
- Easy to miss connections
- No compile-time validation of type compatibility
- Hard to visualize topology from code

### 5. Routing Complexity

**Problem**: Routing scenarios are extremely complex to wire up in tests.

**Attempted Pattern:**
```csharp
// Need actor to tag items with route
var router = new ActorBlock<CashFlowAggregate, (CashFlowAggregate, string), AmountRouter>(...);

// Need RouterBlock to actually route
var routerBlock = new RouterBlock<(CashFlowAggregate, string)>(...);

// Need filters for each route
var largeFilter = new RouteFilterBlock<...>("large");
var mediumFilter = new RouteFilterBlock<...>("medium");
var smallFilter = new RouteFilterBlock<...>("small");

// Need collectors for each route
// Need service providers for each collector
// Need to connect everything correctly
```

**Result**: I gave up and simplified the test because the routing wiring was too complex.

**Observation**: Routing in tests is a major pain point.

### 6. Integration vs Unit Testing

**Current Reality:**
- Most tests are integration tests (full graph execution)
- Unit testing individual actors is cumbersome
- No clear pattern for isolating business logic
- Testing pyramid is inverted (mostly integration, few unit tests)

**Ideal:**
- Business logic unit testable without streaming infrastructure
- Integration tests validate graph wiring and data flow
- Clear separation between logic and infrastructure

## Testability Insights

### Actor Interface Trade-offs

**Current Interface:**
```csharp
public interface IStreamActor<TIn, TOut>
{
    IAsyncEnumerable<TOut> RunAsync(
        IAsyncEnumerable<TIn> input, 
        IActorExecutionContext context);
}
```

**Pros:**
- Very flexible (filter, transform, change cardinality)
- Full streaming semantics
- Actor controls enumeration timing

**Cons:**
- Hard to mock
- Business logic coupled to streaming
- Testing requires full async enumerable infrastructure
- Can't easily unit test helper methods
- Not beginner-friendly for test authors

**Alternative Pattern (for comparison):**
```csharp
public interface ISimpleProcessor<TIn, TOut>
{
    Task<TOut> ProcessAsync(TIn input, CancellationToken ct);
}
```

**Pros:**
- Easy to mock
- Simple to unit test
- Clear input/output
- Testable without streaming infrastructure

**Cons:**
- Less flexible (fixed 1:1 cardinality)
- Would need multiple interfaces for different patterns
- Loses streaming control
- Doesn't support filtering or batching naturally

### Decoupling Business Logic

**Pattern Observed**: Business logic is often private methods in actors.

**Example:**
```csharp
public class CashFlowAggregatorActor : IStreamActor<CashFlow, CashFlowAggregate>
{
    public async IAsyncEnumerable<CashFlowAggregate> RunAsync(...)
    {
        // Streaming orchestration
        var groups = CollectGroups(input);
        foreach (var group in groups)
        {
            yield return CreateAggregate(group); // <-- Want to test this
        }
    }
    
    private CashFlowAggregate CreateAggregate(...) { /* Business logic */ }
}
```

**Better Pattern** (for testability):
```csharp
// Business logic as separate, testable service
public interface ICashFlowAggregationService
{
    CashFlowAggregate CreateAggregate(string company, DateTime date, List<CashFlow> flows);
}

public class CashFlowAggregationService : ICashFlowAggregationService
{
    public CashFlowAggregate CreateAggregate(...) { /* Business logic */ }
}

// Actor becomes thin orchestration layer
public class CashFlowAggregatorActor : IStreamActor<CashFlow, CashFlowAggregate>
{
    private readonly ICashFlowAggregationService _service;
    
    public CashFlowAggregatorActor(ICashFlowAggregationService service)
    {
        _service = service;
    }
    
    public async IAsyncEnumerable<CashFlowAggregate> RunAsync(...)
    {
        var groups = CollectGroups(input);
        foreach (var group in groups)
        {
            yield return _service.CreateAggregate(group); // Delegate to service
        }
    }
}
```

**Benefits:**
- Business logic is independently unit testable
- Actor focuses on streaming orchestration
- Clear separation of concerns
- Easy to mock business logic when testing actor
- Easy to test business logic without actor infrastructure

**Trade-offs:**
- More classes/interfaces
- Additional DI registration
- Potential performance overhead (usually negligible)

## Recommendations from Scenario

### High Priority

1. **Create Test Helpers for Service Provider Setup**
   - Builder pattern for test DI configuration
   - Reduce 60-line boilerplate to 5-10 lines
   - Example: `TestServiceBuilder.WithActor<TActor>().WithScoped<IDb>(mockDb).Build()`

2. **Provide Testing Guidance on Decoupling**
   - Document pattern: Extract business logic into services
   - Actors become thin orchestration layers
   - Services are independently unit testable
   - Show examples in documentation

3. **Create Test Helpers for Streams**
   - `TestStreams.FromArray(items)` - Create test streams
   - `await stream.CollectAllAsync()` - Collect for assertions
   - `TestContext.Create()` - Simple execution context
   - Reduce ceremony in unit tests

4. **Simplify Graph Building for Tests**
   - Fluent helpers for common patterns
   - Example: `TestFlow.Source(data).Through(actor).AssertProduces(expected)`
   - Visual graph builder?

### Medium Priority

5. **Consider Naming: ActorBlock → ScopeBlock**
   - "Actor" is misleading (not traditional actor model)
   - "Scope" better describes DI scope management purpose
   - Would need to evaluate user feedback

6. **Explore Simpler Interfaces for Some Use Cases**
   - Not replacing `IStreamActor` (still needed for flexibility)
   - Additional simple interfaces for common patterns
   - Example: `IItemProcessor<TIn, TOut>` for 1:1 transformations
   - Easier testing for simple scenarios

7. **Routing Test Helpers**
   - Simplified routing setup for tests
   - Pre-built collectors for common patterns
   - Route assertion helpers

### Low Priority

8. **Visual Test Builders**
   - DSL or fluent API for test flow construction
   - Potentially overkill for most scenarios

## User Guidance Needed

Based on this scenario, end users need clear guidance on:

1. **How to structure business logic for testability**
   - Extract complex logic into services
   - Keep actors thin
   - Show examples

2. **Testing strategies**
   - Unit test services (business logic)
   - Integration test actors (streaming orchestration)
   - Full pipeline tests (end-to-end behavior)

3. **Test helpers available**
   - How to use shared test utilities
   - Patterns for common scenarios

4. **Mocking strategies**
   - When and how to mock dependencies
   - DI scope considerations

## Metrics

**Test Complexity Score** (subjective):
- Service Provider Setup: 8/10 complexity (very high)
- Business Logic Testing: 7/10 complexity (high)
- Graph Wiring: 6/10 complexity (medium-high)
- Routing: 9/10 complexity (very high)
- Overall Test Authoring: 7.5/10 complexity (high)

**Test Code Volume:**
- Single integration test: ~150 lines
- Multiple service providers: ~60 lines
- Graph building: ~20 lines
- Actual test logic: ~20 lines
- Boilerplate percentage: ~70%

**Comparison to Ideal:**
- Ideal integration test: ~30 lines
- Potential reduction: 80%

## Next Steps

Phase 3: Prototype test helpers and alternative approaches to validate recommendations.
