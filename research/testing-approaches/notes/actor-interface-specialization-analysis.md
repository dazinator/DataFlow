# Actor Interface Design: Multiple Types vs Single Flexible Interface

## Context

The current `IStreamActor<TIn, TOut>` interface is highly flexible - it takes a stream and returns a stream, giving the actor complete control over enumeration. This enables actors to:
- Filter (one-to-none)
- Transform (one-to-one)
- Expand (one-to-many)
- Aggregate (many-to-one)
- Batch (many-to-many)

**Question**: Should we introduce multiple specialized actor interfaces for different cardinality patterns, or keep the single flexible interface?

## Current Design

### IStreamActor<TIn, TOut>
```csharp
public interface IStreamActor<TIn, TOut>
{
    IAsyncEnumerable<TOut> RunAsync(
        IAsyncEnumerable<TIn> input,
        IActorExecutionContext context);
}
```

**Pros**:
- Single interface to learn
- Maximum flexibility
- Actor controls all streaming behavior
- Can combine operations (filter + transform)

**Cons**:
- Harder to mock/test
- Business logic coupled to streaming
- Unclear intent from interface
- All complexity in actor implementation

## Alternative: Multiple Specialized Interfaces

### Proposed Interfaces

#### 1. IItemProcessor<TIn, TOut> - One-to-One
```csharp
/// <summary>
/// Processes individual items with 1:1 cardinality.
/// Simplest interface - easiest to test and mock.
/// </summary>
public interface IItemProcessor<TIn, TOut>
{
    Task<TOut> ProcessAsync(TIn item, CancellationToken ct);
}

// Supporting block
public class ItemProcessorBlock<TIn, TOut, TProcessor> : IBlock<TIn, TOut>
    where TProcessor : IItemProcessor<TIn, TOut>
{
    // Manages stream, DI scopes, calls ProcessAsync for each item
}
```

**Use Cases**: Simple transformations, enrichment, validation
**Example**: Convert temperature units, format strings, validate data

#### 2. IItemFilter<T> - One-to-None (Filtering)
```csharp
/// <summary>
/// Filters items based on predicate.
/// </summary>
public interface IItemFilter<T>
{
    Task<bool> ShouldIncludeAsync(T item, CancellationToken ct);
}

// Supporting block
public class FilterBlock<T, TFilter> : IBlock<T, T>
    where TFilter : IItemFilter<T>
{
    // Manages stream, only yields items where ShouldIncludeAsync returns true
}
```

**Use Cases**: Filtering, validation gates
**Example**: Filter valid transactions, exclude duplicates

#### 3. IItemExpander<TIn, TOut> - One-to-Many
```csharp
/// <summary>
/// Expands a single item into multiple items.
/// </summary>
public interface IItemExpander<TIn, TOut>
{
    IAsyncEnumerable<TOut> ExpandAsync(TIn item, CancellationToken ct);
}

// Supporting block
public class ExpanderBlock<TIn, TOut, TExpander> : IBlock<TIn, TOut>
    where TExpander : IItemExpander<TIn, TOut>
{
    // Manages stream, expands each item, flattens results
}
```

**Use Cases**: Splitting, exploding aggregates
**Example**: Split batch into individual items, expand hierarchies

#### 4. IBatchProcessor<TIn, TOut> - Many-to-One or Many-to-Many
```csharp
/// <summary>
/// Processes chunks/batches of items.
/// Simpler than IStreamActor but still handles collections.
/// </summary>
public interface IBatchProcessor<TIn, TOut>
{
    Task<IEnumerable<TOut>> ProcessBatchAsync(
        IReadOnlyList<TIn> batch, 
        CancellationToken ct);
}

// Supporting block
public class BatchProcessorBlock<TIn, TOut, TProcessor> : IBlock<TIn, TOut>
    where TProcessor : IBatchProcessor<TIn, TOut>
{
    // Manages stream, batching, DI scopes
}
```

**Use Cases**: Aggregation, bulk operations
**Example**: Database bulk insert, aggregate statistics

#### 5. IStreamActor<TIn, TOut> - Full Flexibility
Keep current interface for complex scenarios that need full stream control.

**Use Cases**: Complex state machines, custom windowing, streaming algorithms
**Example**: Moving averages, pattern matching, stateful processing

## Comparison Analysis

### Testability

| Interface | Mocking Difficulty | Test Setup Lines | Business Logic Isolation |
|-----------|-------------------|------------------|-------------------------|
| IItemProcessor | ⭐ Very Easy | 2-3 | ✅ Excellent |
| IItemFilter | ⭐ Very Easy | 2-3 | ✅ Excellent |
| IItemExpander | ⭐ Easy | 3-5 | ✅ Good |
| IBatchProcessor | ⭐⭐ Medium | 5-8 | ✅ Good |
| IStreamActor | ⭐⭐⭐⭐ Hard | 10-15 | ❌ Poor |

**Example - Testing IItemProcessor**:
```csharp
[Fact]
public async Task Processor_Should_Transform_Item()
{
    var processor = new TemperatureConverter();
    
    var result = await processor.ProcessAsync(
        new Temperature(100, "F"), 
        CancellationToken.None);
    
    result.Value.ShouldBe(37.78m);
    result.Unit.ShouldBe("C");
}
```

vs **Testing IStreamActor** (Current):
```csharp
[Fact]
public async Task Actor_Should_Transform_Items()
{
    var actor = new TemperatureConverterActor();
    var input = TestStreams.FromArray(new Temperature(100, "F"));
    var context = TestContext.CreateActor();
    
    var results = await TestStreams.CollectAsync(
        actor.RunAsync(input, context));
    
    results[0].Value.ShouldBe(37.78m);
}
```

### API Complexity

**Single Interface Approach**:
- 1 actor interface
- 1 block type (ActorBlock)
- Simple API surface

**Multiple Interface Approach**:
- 5+ actor interfaces
- 5+ block types
- Larger API surface
- Need to choose correct interface

### Flexibility vs Simplicity Trade-off

```
Flexibility ←────────────────────→ Simplicity
IStreamActor                        IItemProcessor
    ↓                                     ↓
Complex, powerful              Simple, testable
Hard to test                   Easy to mock
Any cardinality               Fixed cardinality
```

## Recommendation: Hybrid Approach

### Keep IStreamActor as Foundation

**Reasoning**:
1. **No breaking changes** - existing code continues to work
2. **Flexibility preserved** - complex scenarios still supported
3. **Proven pattern** - already working in production

### Add Specialized Interfaces: MVP Set

**⚠️ UPDATED**: See comprehensive MVP analysis in `mvp-interface-set-analysis.md`

**Phase 1: Core MVP (P0)** - Essential, 80-85% coverage
- `ITransform<TIn, TOut>` - One-to-one transformations
- `IPredicate<T>` - Filtering/conditional evaluation

**Phase 2: Complete MVP (P1)** - High value, 90%+ coverage
- `IProjection<TIn, TOut>` - One-to-many projections

**Phase 3: Extended Set (P2)** - Defer until proven need
- `IAggregator<TIn, TOut>` - Many-to-one aggregations
- `IBatchTransform<TIn, TOut>` - Batch operations

**Naming Rationale**:
- **ITransform** vs ~~IItemProcessor~~ - "Transform" is standard (LINQ Select), more precise
- **IPredicate** vs ~~IItemFilter~~ - "Predicate" is the evaluation logic (LINQ Where), clearer
- **IProjection** vs ~~IItemExpander~~ - "Projection" is standard (LINQ SelectMany), professional

**Coverage**: 90%+ of use cases with just 3 MVP interfaces

See detailed analysis: `/research/testing-approaches/notes/mvp-interface-set-analysis.md`

### Implementation Strategy

1. **Create interfaces and blocks**
2. **Provide migration path** from IStreamActor
3. **Document when to use each**
4. **Update testing guide** with examples for each type

### Adoption Guidelines

**Use ITransform<TIn, TOut> when**:
- ✅ One-to-one transformation
- ✅ No state needed across items
- ✅ Simple, testable logic
- ✅ No filtering or cardinality changes

**Use IPredicate<T> when**:
- ✅ Filtering only
- ✅ Binary decision per item
- ✅ No transformation

**Use IProjection<TIn, TOut> when**:
- ✅ One-to-many expansion
- ✅ Splitting/decomposing items
- ✅ Flattening hierarchies

**Use IStreamActor when**:
- ✅ Need full stream control
- ✅ Stateful processing
- ✅ Custom cardinality logic
- ✅ Complex streaming algorithms
- ✅ Multiple operations combined

## Benefits of Hybrid Approach

### For Simple Cases (ITransform)
```csharp
// Implementation
public class TemperatureConverter : ITransform<Temperature, Temperature>
{
    public Task<Temperature> TransformAsync(Temperature input, CancellationToken ct)
    {
        var celsius = (input.Value - 32) * 5 / 9;
        return Task.FromResult(new Temperature(celsius, "C"));
    }
}

// Testing - So simple!
[Fact]
public async Task Should_Convert_Temperature()
{
    var converter = new TemperatureConverter();
    var result = await converter.TransformAsync(new Temperature(100, "F"), default);
    result.Value.ShouldBe(37.78m);
}

// Mocking with NSubstitute
var mockTransform = Substitute.For<ITransform<int, string>>();
mockTransform.TransformAsync(5, Arg.Any<CancellationToken>()).Returns("five");
```

### For Complex Cases (IStreamActor)
```csharp
// Still available for complex scenarios
public class StatefulAggregator : IStreamActor<Transaction, Summary>
{
    public async IAsyncEnumerable<Summary> RunAsync(
        IAsyncEnumerable<Transaction> input,
        IActorExecutionContext context)
    {
        // Complex stateful logic, windowing, etc.
    }
}
```

## NSubstitute Integration

**ITransform** is perfect for NSubstitute:
```csharp
var mockTransform = Substitute.For<ITransform<int, string>>();
mockTransform.TransformAsync(1, Arg.Any<CancellationToken>()).Returns("one");
mockTransform.TransformAsync(2, Arg.Any<CancellationToken>()).Returns("two");

// Test actor that uses transform...

mockProcessor.Received(2).ProcessAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
```

**IStreamActor** is harder:
```csharp
// Can't easily mock stream-returning methods
var mockActor = Substitute.For<IStreamActor<int, string>>();
mockActor.RunAsync(Arg.Any<IAsyncEnumerable<int>>(), Arg.Any<IActorExecutionContext>())
    .Returns(???); // Complex to set up
```

## Addressing Concerns

### API Inflation

**Concern**: Multiple interfaces/blocks increase API surface.

**Mitigation**:
1. Start with just IItemProcessor (80/20 rule)
2. Add others only when proven need
3. Good documentation reduces confusion
4. Clear naming makes intent obvious
5. Keep IStreamActor as escape hatch

### Learning Curve

**Concern**: Users must choose correct interface.

**Mitigation**:
1. Decision tree in documentation
2. Examples for each type
3. Start simple (IItemProcessor) by default
4. Escalate to IStreamActor when needed
5. Compiler helps (type constraints)

### Migration

**Concern**: Existing code uses IStreamActor.

**Mitigation**:
1. No breaking changes - IStreamActor stays
2. Gradual migration is optional
3. Both patterns coexist
4. New code can use simpler interfaces

## Conclusion

**Recommendation: Implement hybrid approach**

1. **Keep `IStreamActor`** - no breaking changes, flexibility preserved
2. **Add `IItemProcessor`** - immediate testability improvement for 70% of cases
3. **Add `IItemFilter`** - common pattern, clear value
4. **Defer others** - wait for proven need
5. **Document clearly** - when to use each

**Priorities**:
- **P0**: Add `IItemProcessor<TIn, TOut>` and `ItemProcessorBlock<>`
- **P1**: Add `IItemFilter<T>` and `FilterBlock<>`
- **P2**: Add others based on user feedback

**Expected Impact**:
- Testability improvement: 80% of actors become trivial to test
- NSubstitute integration: Perfect fit for simple interfaces
- API complexity: Moderate increase (2-3 interfaces/blocks)
- Learning curve: Slightly steeper but manageable
- Business logic decoupling: Natural fit with simple interfaces

**This addresses the testability concerns while maintaining flexibility for complex scenarios.**
