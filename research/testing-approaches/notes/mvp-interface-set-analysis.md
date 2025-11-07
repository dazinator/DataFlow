# MVP Interface Set Analysis: Specialized Actor Interfaces

## Executive Summary

**Goal**: Define the Minimum Viable Product (MVP) set of specialized actor interfaces with clear, consistent naming conventions.

**Recommendation**: Start with 3 core interfaces covering 90%+ of use cases, using consistent naming based on cardinality patterns.

## Naming Convention Principles

### Cardinality-Based Naming

We use cardinality to categorize operations:
- **1:1** - Transform one item into one item
- **1:0 or 1** - Filter (conditionally keep item)
- **1:N** - Project/expand one item into multiple items
- **N:1** - Aggregate multiple items into one result
- **N:N** - Batch operations (batch of items → batch of items)

### Naming Pattern: `I[Action]<TypeParams>`

Where `[Action]` describes what the interface does:
- Use verbs or nouns that are standard in stream processing
- Be specific but not overly verbose
- Align with common terminology (LINQ, Reactive Extensions, Stream APIs)

## MVP Interface Set (3 Interfaces)

### 1. ITransform<TIn, TOut> - One-to-One (P0 - ESSENTIAL)

```csharp
/// <summary>
/// Transforms individual items with 1:1 cardinality.
/// Each input item produces exactly one output item.
/// </summary>
/// <typeparam name="TIn">Input item type</typeparam>
/// <typeparam name="TOut">Output item type</typeparam>
public interface ITransform<TIn, TOut>
{
    /// <summary>
    /// Transforms a single input item into a single output item.
    /// </summary>
    /// <param name="item">The input item to transform</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The transformed output item</returns>
    Task<TOut> TransformAsync(TIn item, CancellationToken ct);
}
```

**Supporting Block**: `TransformBlock<TIn, TOut, TTransform>`

**Why "Transform"**:
- Standard terminology in stream processing (LINQ `Select`, Reactive `Select`)
- Clear, simple, widely understood
- Matches existing DataFlow terminology (TransformBlock already exists)
- Verb form makes action explicit

**Use Cases** (80% of all operations):
- Data mapping/conversion
- Enrichment
- Validation with result
- Simple calculations
- Format transformations

**Examples**:
```csharp
public class TemperatureConverter : ITransform<Temperature, Temperature>
public class OrderValidator : ITransform<Order, ValidationResult>
public class JsonSerializer<T> : ITransform<T, string>
```

**Why P0**: Covers vast majority of use cases, highest ROI

---

### 2. IPredicate<T> - One-to-Zero-or-One (P0 - ESSENTIAL)

```csharp
/// <summary>
/// Evaluates a condition for each item to determine if it should be included.
/// Items that pass the predicate continue through the pipeline.
/// </summary>
/// <typeparam name="T">Item type</typeparam>
public interface IPredicate<T>
{
    /// <summary>
    /// Evaluates whether an item should be included in the output stream.
    /// </summary>
    /// <param name="item">The item to evaluate</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if the item should be included, false otherwise</returns>
    Task<bool> EvaluateAsync(T item, CancellationToken ct);
}
```

**Supporting Block**: `FilterBlock<T, TPredicate>`

**Why "Predicate"**:
- Standard functional programming term
- LINQ uses predicates (Where, All, Any)
- Clear semantic meaning - evaluates to true/false
- More precise than "Filter" (predicate is the logic, filter is the operation)
- Avoids confusion with noun "filter"

**Alternative Names Considered**:
- ❌ `IItemFilter<T>` - "Filter" is a noun, not clear it's the logic
- ❌ `ICondition<T>` - Too generic
- ❌ `ISelector<T>` - Conflicts with "select" meaning transform
- ✅ `IPredicate<T>` - Standard, clear, widely understood

**Use Cases** (10-15% of operations):
- Filtering by condition
- Validation gates
- Conditional routing preparation
- Quality checks

**Examples**:
```csharp
public class ValidOrderPredicate : IPredicate<Order>
public class IsPositivePredicate : IPredicate<decimal>
public class HasCompletedPredicate<T> : IPredicate<T> where T : ITask
```

**Why P0**: Very common pattern, clear use case

---

### 3. IProjection<TIn, TOut> - One-to-Many (P1 - HIGH VALUE)

```csharp
/// <summary>
/// Projects a single input item into a stream of output items.
/// Each input item can produce zero or more output items.
/// </summary>
/// <typeparam name="TIn">Input item type</typeparam>
/// <typeparam name="TOut">Output item type</typeparam>
public interface IProjection<TIn, TOut>
{
    /// <summary>
    /// Projects a single input item into multiple output items.
    /// </summary>
    /// <param name="item">The input item to project</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>A stream of output items produced from the input item</returns>
    IAsyncEnumerable<TOut> ProjectAsync(TIn item, CancellationToken ct);
}
```

**Supporting Block**: `ProjectionBlock<TIn, TOut, TProjection>`

**Why "Projection"**:
- Standard database/LINQ terminology (SelectMany is a projection)
- Clearly conveys one-to-many transformation
- Used in Reactive Extensions
- "Project" means to map/transform into a different form

**Alternative Names Considered**:
- ❌ `IItemExpander<TIn, TOut>` - "Expander" is not standard terminology
- ❌ `IFlatMap<TIn, TOut>` - Too specific to functional programming
- ❌ `IUnfold<TIn, TOut>` - Less common, unclear
- ❌ `IExplode<TIn, TOut>` - Too informal/imprecise
- ✅ `IProjection<TIn, TOut>` - Standard, clear, professional

**Use Cases** (5-8% of operations):
- Split aggregates into constituents
- Expand hierarchies (parent → children)
- Denormalization
- Decomposition
- Unwrapping collections

**Examples**:
```csharp
public class OrderToLineItems : IProjection<Order, LineItem>
public class BatchSplitter : IProjection<Batch<T>, T>
public class HierarchyFlattener : IProjection<Node, Node>
```

**Why P1**: Common enough to justify inclusion in MVP, distinct pattern

---

## Extended Interface Set (Future - P2)

These are deferred until proven need emerges:

### 4. IAggregator<TIn, TOut> - Many-to-One

```csharp
/// <summary>
/// Aggregates multiple input items into a single output item.
/// </summary>
public interface IAggregator<TIn, TOut>
{
    Task<TOut> AggregateAsync(IReadOnlyList<TIn> items, CancellationToken ct);
}
```

**Why Deferred**: 
- Less common pattern
- More complex (requires batching/windowing strategy)
- Can be handled by `IStreamActor` for now
- Batching semantics need more design work

### 5. IBatchTransform<TIn, TOut> - Many-to-Many

```csharp
/// <summary>
/// Transforms a batch of items into a batch of items.
/// Useful for bulk operations.
/// </summary>
public interface IBatchTransform<TIn, TOut>
{
    Task<IReadOnlyList<TOut>> TransformBatchAsync(IReadOnlyList<TIn> items, CancellationToken ct);
}
```

**Why Deferred**:
- Specialized use case (database bulk operations)
- `IStreamActor` handles this well enough
- Batching strategy is application-specific

## Terminology Consistency

### Comparison with Other Frameworks

| Framework | 1:1 Transform | Filter | 1:N Project |
|-----------|---------------|--------|-------------|
| **LINQ** | Select | Where(predicate) | SelectMany |
| **Reactive Extensions** | Select | Where(predicate) | SelectMany |
| **Java Streams** | map | filter(predicate) | flatMap |
| **Our MVP** | ITransform | IPredicate | IProjection |

### Verb vs Noun Choice

**Interfaces use nouns** (they describe what the implementer IS):
- `ITransform` - IS a transformer
- `IPredicate` - IS a predicate (evaluator)
- `IProjection` - IS a projection

**Methods use verbs** (they describe what to DO):
- `TransformAsync` - DO transform
- `EvaluateAsync` - DO evaluate
- `ProjectAsync` - DO project

This is consistent with .NET interface naming conventions (IDisposable, IEnumerable, IComparable).

## MVP Rationale

### Coverage Analysis

Based on analysis of existing POC tests (174 tests):

| Pattern | Estimated % | MVP Interface | Priority |
|---------|-------------|---------------|----------|
| One-to-one transform | 70-80% | ITransform<TIn, TOut> | P0 |
| Filtering | 10-15% | IPredicate<T> | P0 |
| One-to-many | 5-8% | IProjection<TIn, TOut> | P1 |
| Many-to-one | 3-5% | IStreamActor (for now) | P2 |
| Complex stateful | 2-5% | IStreamActor | N/A |

**Total MVP Coverage**: ~90-95% of use cases with 3 interfaces

### Benefits of This MVP

1. **High Coverage**: 90%+ of use cases with just 3 interfaces
2. **Clear Names**: Standard terminology, widely understood
3. **Manageable API**: Only 3 new interfaces + 3 new blocks
4. **Consistent**: Naming pattern is predictable
5. **Extensible**: Can add more later without breaking changes
6. **Professional**: Uses industry-standard terminology

### Trade-offs Addressed

**Concern**: API inflation
**Mitigation**: Only 3 interfaces in MVP (not 5+), covers 90%+ of cases

**Concern**: Naming clarity
**Mitigation**: Used standard terminology from LINQ/Reactive/databases

**Concern**: Learning curve
**Mitigation**: Names are self-documenting, match existing knowledge

## Implementation Strategy

### Phase 1: Core MVP (P0)

**Week 1-2**: Implement core interfaces
1. `ITransform<TIn, TOut>` + `TransformBlock<>`
2. `IPredicate<T>` + `FilterBlock<>`

**Deliverables**:
- Interface definitions
- Block implementations
- Unit tests
- Documentation
- Migration guide

### Phase 2: Complete MVP (P1)

**Week 3**: Add projection support
3. `IProjection<TIn, TOut>` + `ProjectionBlock<>`

**Deliverables**:
- Interface definition
- Block implementation
- Unit tests
- Documentation updates

### Phase 3: Adoption (P1)

**Week 4+**: 
- Update testing guide
- Provide examples
- Create migration path from `IStreamActor`

### Future: Extended Set (P2+)

**When needed**: Add `IAggregator`, `IBatchTransform` based on user feedback

## Decision Matrix

| Interface | Include in MVP? | Rationale |
|-----------|----------------|-----------|
| ITransform<TIn, TOut> | ✅ YES (P0) | 70-80% of use cases, essential |
| IPredicate<T> | ✅ YES (P0) | 10-15% of use cases, very clear pattern |
| IProjection<TIn, TOut> | ✅ YES (P1) | 5-8% of use cases, distinct value |
| IAggregator<TIn, TOut> | ❌ NO (P2) | 3-5% of use cases, complex, defer |
| IBatchTransform<TIn, TOut> | ❌ NO (P2) | Specialized, defer until needed |

## Naming Alternatives Rejected

### Why Not "IItemProcessor"?

While considered:
```csharp
public interface IItemProcessor<TIn, TOut> // Original proposal
{
    Task<TOut> ProcessAsync(TIn item, CancellationToken ct);
}
```

**Rejected because**:
- "Process" is too generic - doesn't convey 1:1 transformation
- "ItemProcessor" is verbose
- "Transform" is more precise and standard

### Why Not "IItemFilter"?

```csharp
public interface IItemFilter<T> // Original proposal
{
    Task<bool> ShouldIncludeAsync(T item, CancellationToken ct);
}
```

**Rejected because**:
- "Filter" is a noun (the operation), not the logic
- "Predicate" is standard term for evaluation logic
- "EvaluateAsync" is clearer than "ShouldIncludeAsync"

### Why Not "IItemExpander"?

```csharp
public interface IItemExpander<TIn, TOut> // Original proposal
{
    IAsyncEnumerable<TOut> ExpandAsync(TIn item, CancellationToken ct);
}
```

**Rejected because**:
- "Expander" is not standard terminology in stream processing
- "Projection" is standard (LINQ SelectMany, SQL projections)
- "ProjectAsync" is more professional

## Recommendation Summary

**Adopt this MVP**:
1. `ITransform<TIn, TOut>` - One-to-one transformations
2. `IPredicate<T>` - Filtering/conditional evaluation
3. `IProjection<TIn, TOut>` - One-to-many projections

**Benefits**:
- Clear, standard naming
- 90%+ coverage with just 3 interfaces
- Manageable API growth
- Professional terminology
- Extensible for future needs

**Implementation**: Phased approach over 3-4 weeks

**Result**: Dramatic testability improvement while maintaining API clarity
