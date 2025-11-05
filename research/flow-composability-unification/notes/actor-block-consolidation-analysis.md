# ActorBlock Consolidation Analysis

## Context

During the composability research, a consolidation opportunity emerged: Could `ActorBlock` (with DI scope management and rotation) serve as the unified foundation for both transformation and processing operations, replacing specialized `TransformerBlock` and `ProcessorBlock`?

## Current Block Landscape

### ActorBlock<TIn, TOut, TActor>
**Purpose**: Host scoped actors with DI scope rotation capability

**Key Features**:
1. Each actor runs in its own async DI scope
2. Optional scope rotation via `context.RequestRotation()`
3. Generic transformation/processing via `IStreamActor<TIn, TOut>`

**Safety Benefits**:
- Ensures DI scope isolation (prevents concurrent sharing of scoped dependencies)
- Enables lifecycle management (rotate scopes to clean up accumulated state)
- Prevents memory leaks from long-running streams with stateful dependencies

### TransformerBlock<TIn, TOut>
**Purpose**: Transform items (1-to-1, 1-to-many, filtering)

**Characteristics**:
- Lightweight: `Func<TIn, IExecutionContext, IAsyncEnumerable<TOut>>`
- No built-in DI scope management
- Direct functional composition

### ProcessorBlock<T>
**Purpose**: Terminal block with side effects

**Characteristics**:
- Lightweight: `Func<T, IExecutionContext, Task>`
- No built-in DI scope management
- No output stream

## Consolidation Proposal

### Recommendation: **YES - Consolidate around ActorBlock pattern**

**Rationale**:

1. **Safety First**: DI scope isolation should be the default, not optional
   - Concurrent processing without scope isolation risks dependency sharing bugs
   - State accumulation (caches, DbContext, etc.) needs rotation capability
   - ActorBlock enforces these patterns

2. **Unified Mental Model**: Single "business logic participant" pattern
   - All stream processing follows same DI-aware actor pattern
   - Reduces cognitive load (fewer block types to learn)
   - Consistent approach to concurrency and lifecycle

3. **Optional Features**: Rotation is opt-in via `context.RequestRotation()`
   - Actors that don't need rotation simply never call it
   - No performance penalty for simple scenarios
   - Safety guardrails always present

4. **Type Flexibility**: ActorBlock already supports all transformation patterns
   - 1-to-1: `IStreamActor<T, T>` with single yield
   - 1-to-many: `IStreamActor<T, TOut>` with multiple yields
   - Filtering: Don't yield for filtered items
   - Processing: `IStreamActor<T, object>` (or unit type) with no yield

## Migration Strategy

### Phase 1: Deprecate TransformerBlock and ProcessorBlock

**Plain Stream Variants**:
- Mark `TransformerBlock<TIn, TOut>` as `[Obsolete]` → Use `ActorBlock<TIn, TOut, TActor>`
- Mark `SimpleTransformerBlock<TIn, TOut>` as `[Obsolete]` → Use `ActorBlock` with simple actor
- Mark `ProcessorBlock<T>` as `[Obsolete]` → Use `ActorBlock<T, object, TActor>`

**Epoch Stream Variants** (from this research):
- Don't implement `EpochTransformerBlock` → Use `EpochActorBlock` instead
- Don't implement `EpochProcessorBlock` → Use `EpochActorBlock` instead

### Phase 2: Provide Helper Factories

Ease migration with factory methods:

```csharp
// Factory for simple 1-to-1 transformations
public static ActorBlock<TIn, TOut, SimpleTransformActor<TIn, TOut>> CreateTransformer<TIn, TOut>(
    string name,
    Func<TIn, TOut> transform,
    IServiceScopeFactory scopeFactory)
{
    // Register SimpleTransformActor<TIn, TOut> in DI with transform function
    return new ActorBlock<TIn, TOut, SimpleTransformActor<TIn, TOut>>(name, scopeFactory);
}

// Factory for processors
public static ActorBlock<T, object, SimpleProcessorActor<T>> CreateProcessor<T>(
    string name,
    Func<T, Task> processor,
    IServiceScopeFactory scopeFactory)
{
    // Register SimpleProcessorActor<T> in DI with processor function
    return new ActorBlock<T, object, SimpleProcessorActor<T>>(name, scopeFactory);
}
```

### Phase 3: Test Migration and Consolidation

1. Update all tests currently using `TransformerBlock` → `ActorBlock` equivalents
2. Update all tests currently using `ProcessorBlock` → `ActorBlock` equivalents
3. **Assess and consolidate redundant tests**:
   - Identify tests with duplicate coverage (same scenario, different block type)
   - Consolidate overlapping edge case tests
   - Create test coverage matrix to identify redundancies
   - Document consolidation decisions
   - Target: 20-40% reduction in test count while maintaining coverage
4. Ensure performance parity (see benchmarks below)

## Performance Validation

### Baseline Benchmarks Required

To ensure ActorBlock consolidation doesn't introduce regressions:

1. **TransformerBlock Baseline**
   - Simple 1-to-1 transformation: `x => x * 2`
   - 1-to-many transformation: `x => [x, x * 2, x * 3]`
   - Filtering transformation: `x => x % 2 == 0`
   - Measure: Throughput (items/sec), Memory allocation per item

2. **ProcessorBlock Baseline**
   - Simple side effect: `x => counter++`
   - Async side effect: `x => SaveToDbAsync(x)`
   - Measure: Throughput (items/sec), Memory allocation per item

3. **ActorBlock Equivalent Performance** (with warmup)
   - **Warmup Phase**: Run 1000+ items to eliminate JIT/initialization overhead
   - **Measurement Phase**: Measure steady-state performance over 10,000+ items
   - Same operations implemented as actors
   - Measure: Throughput (items/sec), Memory allocation per item, Latency percentiles (P50, P95, P99)
   - **Ambitious Target**: < 1% overhead after warmup compared to direct function blocks
   - **Rationale**: With proper warmup and optimization, near-zero overhead is achievable

### Benchmark Results (To Be Collected)

**Status**: Baseline benchmarks to be run as part of implementation

**Handover Requirement**: 
- **Phase 1**: Run benchmarks for current `TransformerBlock` and `ProcessorBlock` baseline performance
- **Phase 2**: Warm up ActorBlock (1000+ items), then measure steady-state performance
- **Phase 3**: Compare and analyze any differences > 1%
- Document results in `/Benchmarks/plain-blocks-baseline-results.md`
- Implementation team must validate < 1% regression after warmup
- If > 1%, profile and optimize before proceeding with migration

## Alternative Considered: Keep Both

**Keep specialized blocks alongside ActorBlock**

**Pros**:
- Maximum flexibility (choose lightweight vs DI-aware)
- No migration needed
- Potentially better performance for simple lambdas

**Cons**:
- **Safety risk**: Easy to accidentally use lightweight blocks in concurrent scenarios
- **Confusion**: When to use which block type?
- **Maintenance burden**: Multiple code paths for similar functionality
- **Split ecosystem**: Some blocks DI-aware, others not

**Decision**: Rejected - Safety and simplicity outweigh flexibility

## Epoch-Aware Variants

### EpochActorBlock<TIn, TOut, TActor>

Instead of implementing separate `EpochTransformerBlock` and `EpochProcessorBlock`, implement:

```csharp
public sealed class EpochActorBlock<TIn, TOut, TActor> : BlockBase<IEpochStream<TIn>, IEpochStream<TOut>>
    where TActor : IStreamActor<TIn, TOut>
{
    private readonly IServiceScopeFactory _scopeFactory;
    
    public override async IAsyncEnumerable<IEpochStream<TOut>> ExecuteAsync(
        IAsyncEnumerable<IEpochStream<TIn>> input,
        IExecutionContext context)
    {
        await foreach (var epochStream in input.WithCancellation(context.CancellationToken))
        {
            yield return new EpochStream<TOut>(
                epochStream.EpochVector,
                ProcessEpochItems(epochStream, context));
        }
    }
    
    private async IAsyncEnumerable<TOut> ProcessEpochItems(
        IEpochStream<TIn> epochStream,
        IExecutionContext context)
    {
        // Create actor in DI scope, process items within epoch
        await using var scope = _scopeFactory.CreateAsyncScope();
        var actor = scope.ServiceProvider.GetRequiredService<TActor>();
        
        var actorContext = new ActorExecutionContext(context.CancellationToken, context.InvocationId);
        
        await foreach (var outputItem in actor.RunAsync(epochStream.Items, actorContext))
        {
            yield return outputItem;
        }
        
        // Note: Rotation not supported in epoch-aware variant (epoch boundaries serve as natural rotation points)
    }
}
```

**Benefits**:
- Unified actor pattern for both plain and epoch streams
- DI scope isolation within each epoch
- Epoch boundaries serve as natural rotation points (no manual rotation needed)

## Implementation Guidance

### For Implementation Team

**Note**: This code is POC and has not been released externally. Breaking changes are acceptable.

**Two-Phase Approach**:

#### Issue 1: Implement Epoch-Aware Blocks
- Implement `EpochActorBlock<TIn, TOut, TActor>` (see prototype in handover)
- No migration needed (new functionality)
- Follow existing ActorBlock pattern for consistency

#### Issue 2: Consolidate Plain Blocks (Separate Issue)
See `/research/flow-composability-unification/handover/github-issue-consolidate-plain-blocks.md` for complete implementation plan.

**Summary**:
1. **Create Baseline Benchmarks with Warmup**:
   - Run benchmarks for `TransformerBlock` (1-to-1, 1-to-many, filtering)
   - Run benchmarks for `ProcessorBlock` (simple, async)
   - Include warmup phase (1000+ items) before measurement
   - Document baseline results in `/Benchmarks/plain-blocks-baseline-results.md`

2. **Mark Blocks as Obsolete**:
   - Add `[Obsolete]` attributes to `TransformerBlock`, `ProcessorBlock`
   - No migration required yet (just warnings)

3. **Validate Performance**:
   - Implement ActorBlock equivalents
   - Warm up (1000+ items), then measure steady-state performance
   - **Target**: < 1% regression after warmup
   - If > 1%, profile and optimize before proceeding

4. **Migrate and Consolidate Tests**:
   - Migrate all tests to use ActorBlock pattern
   - **Assess for redundancies**: Same scenario tested with different blocks
   - Consolidate duplicate coverage to reduce test count by 20-40%
   - Document consolidation decisions

5. **Remove Obsolete Blocks**:
   - After validation, delete `TransformerBlock` and `ProcessorBlock` from codebase
   - Update documentation

5. **Documentation**:
   - Update POC guides to promote ActorBlock pattern
   - Provide migration examples for common scenarios
   - Document when to use rotation vs rely on epoch boundaries

## Decision

**Consolidate around ActorBlock pattern for both plain and epoch streams.**

**Rationale**: Safety, simplicity, and unified mental model outweigh flexibility of specialized blocks. DI scope isolation should be the default for stream processing.

**Status**: Pending implementation and performance validation
