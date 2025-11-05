# ADR: Decoupled Epoch Stream Segmentation

**Date**: 2025-11-05  
**Status**: Proposed (Pending Benchmark Validation)  
**Context**: Research Issue - Epoch Stream Separation from Source

## Context

The current POC design has sources emit `IAsyncEnumerable<IEpochStream<T>>` directly, meaning sources control epoch boundaries. This couples data production with epoch segmentation strategy.

Recent design discussions questioned whether this coupling creates issues:
- Hard to reuse sources with different segmentation strategies
- Sources cannot be used in non-epoch pipelines without duplication
- Testing source logic requires dealing with epoch complexity
- Changing segmentation strategy requires modifying source code

This ADR documents the decision to adopt a decoupled design where sources emit plain `IAsyncEnumerable<T>` and epoch segmentation is applied externally via an `EpochSegmenterBlock`.

## Decision

**We recommend adopting the decoupled epoch segmentation design**, where:

1. **Sources emit plain data streams**: `IAsyncEnumerable<T>`
2. **Segmentation is external**: Applied by `EpochSegmenterBlock<T>`
3. **Policies are configurable**: Count, Key, Clock, Custom, or None
4. **Epochs are optional**: Pipelines can opt out entirely

### New Interfaces

```csharp
// Plain source without epoch knowledge
public interface IPlainSourceActor<T>
{
    IAsyncEnumerable<T> ProduceAsync(IActorExecutionContext context);
}

// External segmentation block
public class EpochSegmenterBlock<T> : BlockBase<T, IEpochStream<T>>
{
    public EpochSegmenterBlock(string name, EpochSegmentationPolicy policy);
    // Converts IAsyncEnumerable<T> → IAsyncEnumerable<IEpochStream<T>>
}

// Segmentation policies
public sealed class EpochSegmentationPolicy
{
    static EpochSegmentationPolicy None { get; }
    static EpochSegmentationPolicy ByCount(int itemsPerEpoch, string sourceId);
    static EpochSegmentationPolicy ByKey<T, TKey>(Func<T, TKey> keySelector, string sourceId);
    static EpochSegmentationPolicy ByClock(IEpochClock clock, string sourceId);
    static EpochSegmentationPolicy Custom<T>(Func<...> customSegmenter, string sourceId);
}
```

### Pipeline Pattern

```csharp
// Define plain source
public class OrderSource : PlainSourceActorBase<Order>
{
    public override IAsyncEnumerable<Order> ProduceAsync(IActorExecutionContext context)
    {
        return FetchOrders(); // No epoch knowledge
    }
}

// Configure pipeline
var source = new PlainSourceBlock<Order, OrderSource>("source", scopeFactory);
var segmenter = new EpochSegmenterBlock<Order>("segmenter",
    EpochSegmentationPolicy.ByKey<Order, DateTime>(
        order => order.Date.Date,
        "order-source"));

// Flow: source → segmenter → transform → sink
```

## Alternatives Considered

### Alternative 1: Keep Source-Centric (Status Quo)

Sources emit `IAsyncEnumerable<IEpochStream<T>>`.

**Pros**:
- ✅ Explicit type contract (epochs always present)
- ✅ No migration needed
- ✅ Fewer pipeline stages
- ✅ Can align epochs with natural domain boundaries

**Cons**:
- ❌ Tight coupling of production and segmentation
- ❌ Poor source reusability (need different sources for different strategies)
- ❌ Cannot opt out of epochs without duplicate sources
- ❌ Testing complexity (source tests must deal with epochs)
- ❌ Limited flexibility (hard to change strategy)

**Why Not Chosen**: The coupling creates long-term maintainability issues and limits flexibility.

### Alternative 2: Hybrid Approach

Support both source-centric and decoupled patterns.

**Pros**:
- ✅ Backward compatible
- ✅ Gradual migration possible
- ✅ Choose pattern per use case

**Cons**:
- ❌ Two patterns to maintain
- ❌ Documentation complexity
- ❌ Developer confusion about which to use

**Why Not Chosen**: While this could be a migration strategy, long-term we want one clear pattern. May be used temporarily during migration.

### Alternative 3: Unified Interface

Single source interface that optionally produces epochs.

**Pros**:
- ✅ Single source interface
- ✅ Flexible epoch usage

**Cons**:
- ❌ Complex type signature (Optional<IEpochStream<T>>?)
- ❌ Runtime checking required
- ❌ Less type-safe

**Why Not Chosen**: Type safety is valuable, and explicit separation is clearer.

## Consequences

### Positive Consequences

1. **Improved Separation of Concerns**
   - Sources focus on data production
   - Segmentation is a separate, configurable concern
   - Easier to reason about and maintain

2. **Enhanced Reusability**
   - Same source works with multiple segmentation strategies
   - Same source works in epoch and non-epoch pipelines
   - Reduces code duplication

3. **Greater Flexibility**
   - Change segmentation strategy via configuration
   - A/B test different strategies easily
   - Runtime policy changes possible

4. **Simpler Testing**
   - Test source logic without epoch complexity
   - Test segmentation logic independently
   - Clearer test isolation

5. **Clearer Source Interface**
   - `IAsyncEnumerable<T>` is simpler than `IAsyncEnumerable<IEpochStream<T>>`
   - Lower barrier to entry for new developers

6. **Optional Epoch Usage**
   - Pipelines that don't need epochs can omit segmenter
   - Pass-through mode (single epoch) available
   - More flexible architecture

### Negative Consequences

1. **Additional Pipeline Stage**
   - Segmenter adds one more block
   - Slightly more complex pipeline topology
   - **Mitigation**: The added clarity is worth the extra stage

2. **Performance Overhead**
   - Extra async enumeration layer
   - **Expected Impact**: Minimal (1-5% in micro-benchmarks, <1% in realistic scenarios)
   - **Mitigation**: Benchmarks will validate; if significant, can optimize segmenter

3. **Less Explicit Type Contract**
   - Source type doesn't indicate if epochs will be present
   - Downstream must know configuration
   - **Mitigation**: Documentation and naming conventions

4. **Migration Effort**
   - Existing source-centric sources need updating
   - Breaking change to source interfaces
   - **Mitigation**: Gradual migration, adapter pattern, clear migration guide

5. **Additional Configuration**
   - Segmentation policy must be configured separately
   - More configuration to manage
   - **Mitigation**: Sensible defaults, clear documentation

### Impact on Subsystems

#### EpochVector
**Impact**: ✅ None - EpochVector is independent of how epochs are created.

#### Lifecycle Events
**Impact**: ⚖️ Minimal - Segmenter should be transparent to lifecycle events. Downstream blocks trigger events, not the segmenter itself.

**Recommendation**: Document that segmenter doesn't participate in lifecycle events (it's infrastructure, not a processing block).

#### EfCore Tracking Block
**Impact**: ✅ None (expected) - Tracking block reacts to epoch streams regardless of origin.

**Action Required**: Test to validate assumption.

#### CompletionBasedEpochProgress
**Impact**: ✅ None - Progress tracker works with epoch streams from any source.

#### GlobalEpochAlignment
**Impact**: ✅ None - Alignment calculated from block completions, not source type.

### Migration Strategy

#### Phase 1: Validation (Current)
- ✅ Prototype created
- ✅ Tests passing
- 🔄 Benchmarks created (need to run)
- 🔄 Subsystem integration testing

#### Phase 2: Dual Support
- Keep existing `ISourceActor<T>` and `EpochSourceBlock`
- Add new `IPlainSourceActor<T>` and `PlainSourceBlock`
- Add `EpochSegmenterBlock`
- Document both patterns
- Recommend decoupled for new code

#### Phase 3: Migration
- Create migration guide
- Provide adapter/compatibility layer if needed
- Update examples to decoupled pattern
- Update documentation

#### Phase 4: Deprecation (Future)
- Mark source-centric interfaces as obsolete
- Remove in major version bump
- Consolidate on decoupled design

### Adapter Pattern (Migration Aid)

For existing sources, provide adapter:

```csharp
// Adapter: Wraps epoch-producing source as plain source
public class EpochSourceAdapter<T> : PlainSourceActorBase<T>
{
    private readonly ISourceActor<T> _epochSource;
    
    public override async IAsyncEnumerable<T> ProduceAsync(IActorExecutionContext context)
    {
        await foreach (var epochStream in _epochSource.ProduceEpochsAsync(context))
        {
            await foreach (var item in epochStream.Items)
            {
                yield return item;
            }
        }
    }
}
```

This allows gradual migration without breaking existing code.

## Performance Validation Required

**Status**: Benchmarks created but not yet executed.

**Required Before Final Decision**:
1. Run micro-benchmarks (pure throughput)
2. Run realistic benchmarks (with 30ms simulated processing)
3. Measure memory allocation differences
4. Compare across different epoch sizes (100, 1000, 10000 items)
5. Validate that overhead is acceptable

**Acceptance Criteria**:
- Throughput reduction < 10% in micro-benchmarks
- Throughput reduction < 2% in realistic workloads
- Memory overhead reasonable (< 10% increase)

**If Performance Unacceptable**:
- Investigate optimizations (e.g., specialized segmenter implementations)
- Re-evaluate decision if overhead is prohibitive

## Implementation Guidance

### For New Sources

**DO**:
```csharp
public class MySource : PlainSourceActorBase<T>
{
    public override IAsyncEnumerable<T> ProduceAsync(IActorExecutionContext context)
    {
        // Just produce data
    }
}
```

**DON'T**:
```csharp
public class MySource : SourceActorBase<T>
{
    public override IAsyncEnumerable<IEpochStream<T>> ProduceEpochsAsync(...)
    {
        // Don't couple epoch logic to source
    }
}
```

### For Pipelines

**With Epochs**:
```csharp
var source = new PlainSourceBlock<T, MySource>("source", factory);
var segmenter = new EpochSegmenterBlock<T>("segmenter", policy);
// Connect: source → segmenter → downstream
```

**Without Epochs**:
```csharp
var source = new PlainSourceBlock<T, MySource>("source", factory);
// Connect: source → downstream (no segmenter)
```

### Policy Selection

- **ByCount**: Fixed-size epochs (e.g., every 1000 items)
- **ByKey**: Natural domain boundaries (e.g., by date, by customer)
- **ByClock**: Time-based windows (e.g., every minute)
- **Custom**: Special logic (e.g., transaction boundaries)
- **None**: Pass-through, single epoch (migration aid or opt-out)

## Open Questions

1. **Should segmenter be a first-class block or a helper?**
   - Current: First-class block in pipeline
   - Alternative: Helper function that wraps source
   - **Recommendation**: First-class block for visibility and flexibility

2. **Should we support runtime policy changes?**
   - Current: Policy set at construction
   - Enhancement: Allow policy swapping
   - **Recommendation**: Defer to implementation phase

3. **How to handle segmenter in metrics and observability?**
   - Should it report metrics?
   - Should it appear in pipeline visualizations?
   - **Recommendation**: Document as infrastructure, minimal metrics

## References

- **Research Document**: `/research/epoch-stream-separation/README.md`
- **Prototype Tests**: `/poc/DataFlow.POC.Tests/DecoupledEpochTests.cs`
- **Prototype Implementation**: 
  - `/poc/DataFlow.POC/Core/IPlainSourceActor.cs`
  - `/poc/DataFlow.POC/Blocks/PlainSourceBlock.cs`
  - `/poc/DataFlow.POC/Blocks/EpochSegmenterBlock.cs`
- **Benchmarks**: `/poc/DataFlow.POC.Benchmarks/DecoupledEpochBenchmark.cs`

## Decision Review

This decision should be reviewed after:
1. ✅ Benchmarks confirm acceptable performance
2. ✅ Subsystem integration tests pass
3. ✅ Implementation issue is created with clear acceptance criteria

**Final Status**: Pending benchmark validation and subsystem integration testing.

---

**Recommendation**: **ADOPT** decoupled design after validation, with gradual migration strategy.
