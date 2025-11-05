# Implementation Issue: Decoupled Epoch Stream Segmentation

## Context and Objectives

### Problem Statement

The current POC design couples epoch segmentation to source blocks, requiring sources to emit `IAsyncEnumerable<IEpochStream<T>>`. This coupling creates several issues:

- **Limited Reusability**: Sources cannot be reused with different segmentation strategies without modification
- **No Opt-Out**: Pipelines that don't need epochs require duplicate non-epoch source implementations
- **Testing Complexity**: Testing source logic requires dealing with epoch complexity
- **Hard to Change**: Modifying segmentation strategy requires changing source code
- **Tight Coupling**: Data production and epoch segmentation concerns are intertwined

### Research Background

Research was conducted to validate a decoupled approach where sources emit plain data streams and epoch segmentation is applied externally.

**Research Documentation**:
- **Main findings**: `/research/epoch-stream-separation/README.md`
- **Design docs**: `/research/epoch-stream-separation/design/decoupled-epoch-architecture.md`
- **ADR**: `/research/epoch-stream-separation/adr/2025-11-05-decoupled-epoch-segmentation.md`

**Key Research Findings**:
- ✅ Decoupled design is technically feasible
- ✅ Provides significant benefits in reusability and composability
- ✅ Simplifies testing and source implementation
- ✅ Enables optional epoch usage
- ✅ Functional equivalence demonstrated in prototype
- ⚠️ Performance impact needs validation (expected minimal)

### Objectives

Implement the decoupled epoch segmentation architecture:

- [ ] Sources emit plain `IAsyncEnumerable<T>`
- [ ] Segmentation applied externally via `EpochSegmenterBlock`
- [ ] Multiple segmentation policies supported (None, Count, Key, Clock, Custom)
- [ ] Backward compatibility during migration
- [ ] Performance validated (< 10% overhead in micro-benchmarks, < 2% in realistic scenarios)
- [ ] Subsystem integration confirmed (EfCore tracking, lifecycle events)
- [ ] Documentation and migration guide complete

## Implementation Guidance

### Recommended Approach

Implement in phases to allow gradual migration:

1. **Phase 1**: Add decoupled components alongside existing source-centric design
2. **Phase 2**: Validate performance and subsystem integration
3. **Phase 3**: Migrate examples and documentation
4. **Phase 4**: Deprecate source-centric interfaces (future major version)

**Key Principles**:
1. Maintain backward compatibility during migration
2. Provide clear migration path
3. Document both patterns until deprecation
4. Validate performance before full migration

### Design References

- **Design Document**: `/research/epoch-stream-separation/design/decoupled-epoch-architecture.md`
- **Architecture Decision Record**: `/research/epoch-stream-separation/adr/2025-11-05-decoupled-epoch-segmentation.md`
- **Research Findings**: `/research/epoch-stream-separation/README.md`

### API/Interface Design

#### New Interfaces

```csharp
namespace DataFlow.POC.Core;

/// <summary>
/// Source actor that produces plain data streams without epoch knowledge.
/// </summary>
public interface IPlainSourceActor<T>
{
    IAsyncEnumerable<T> ProduceAsync(IActorExecutionContext context);
}

/// <summary>
/// Base class for plain source actors.
/// </summary>
public abstract class PlainSourceActorBase<T> : IPlainSourceActor<T>
{
    public abstract IAsyncEnumerable<T> ProduceAsync(IActorExecutionContext context);
}
```

```csharp
namespace DataFlow.POC.Blocks;

/// <summary>
/// Block that hosts a plain source actor.
/// </summary>
public sealed class PlainSourceBlock<T, TActor> : BlockBase<object, T>
    where TActor : IPlainSourceActor<T>
{
    public PlainSourceBlock(string name, IServiceScopeFactory scopeFactory);
    
    public override IAsyncEnumerable<T> ExecuteAsync(
        IAsyncEnumerable<object> input,
        IExecutionContext context);
}
```

```csharp
namespace DataFlow.POC.Blocks;

/// <summary>
/// Configuration for epoch segmentation policies.
/// </summary>
public sealed class EpochSegmentationPolicy
{
    // Pre-defined policies
    public static readonly EpochSegmentationPolicy None;
    
    // Factory methods
    public static EpochSegmentationPolicy ByCount(int itemsPerEpoch, string sourceId);
    public static EpochSegmentationPolicy ByKey<T, TKey>(Func<T, TKey> keySelector, string sourceId);
    public static EpochSegmentationPolicy ByClock(IEpochClock clock, string sourceId);
    public static EpochSegmentationPolicy Custom<T>(Func<IAsyncEnumerable<T>, IAsyncEnumerable<IEpochStream<T>>> customSegmenter, string sourceId);
    
    // Properties
    public SegmentationMode Mode { get; init; }
    public string SourceId { get; init; }
    public EpochExecutionPolicy ExecutionPolicy { get; init; }
    public int MaxConcurrentEpochs { get; init; }
    // ... mode-specific properties
}

public enum SegmentationMode
{
    None,      // Pass-through
    Count,     // By item count
    Key,       // By key change
    Clock,     // By epoch clock
    Custom     // Custom function
}
```

```csharp
namespace DataFlow.POC.Blocks;

/// <summary>
/// Block that applies epoch segmentation to plain data streams.
/// </summary>
public sealed class EpochSegmenterBlock<T> : BlockBase<T, IEpochStream<T>>
{
    public EpochSegmenterBlock(string name, EpochSegmentationPolicy policy);
    
    public override IAsyncEnumerable<IEpochStream<T>> ExecuteAsync(
        IAsyncEnumerable<T> input,
        IExecutionContext context);
}
```

### Component Architecture

```
┌─────────────────┐
│ IPlainSourceActor│
│   ProduceAsync() │
└────────┬─────────┘
         │ IAsyncEnumerable<T>
         ▼
┌────────────────────┐
│ PlainSourceBlock   │
│ (hosts actor)      │
└────────┬───────────┘
         │ IAsyncEnumerable<T>
         ▼
┌────────────────────────────┐
│ EpochSegmenterBlock        │
│ (applies policy)           │
└────────┬───────────────────┘
         │ IAsyncEnumerable<IEpochStream<T>>
         ▼
┌────────────────────┐
│ Downstream Blocks  │
└────────────────────┘
```

### Key Implementation Considerations

#### 1. **Streaming Architecture**
   - Items must be streamed through, not buffered
   - Use deferred execution patterns
   - Maintain lazy evaluation
   - **Why**: Preserve backpressure and memory efficiency

#### 2. **Segmenter as Infrastructure**
   - Segmenter should not participate in lifecycle events
   - It's transparent infrastructure, not a processing block
   - Downstream blocks trigger epoch lifecycle events
   - **Why**: Avoids complicating alignment calculations

#### 3. **Performance**
   - Minimize per-item overhead
   - Use efficient state tracking (O(1) for most policies)
   - Avoid unnecessary allocations
   - **Why**: Keep overhead < 10% in micro-benchmarks

#### 4. **Type Safety**
   - Use strongly typed policies where possible
   - Validate configuration at construction time
   - Fail fast on misconfiguration
   - **Why**: Catch errors early, clear diagnostics

#### 5. **Migration Support**
   - Keep existing source-centric interfaces during migration
   - Provide adapter for backward compatibility
   - Document both patterns clearly
   - **Why**: Allow gradual migration

### Reusable Patterns/Code

#### Pattern: Plain Source Implementation
```csharp
public class OrderSource : PlainSourceActorBase<Order>
{
    private readonly IOrderRepository _repository;
    
    public OrderSource(IOrderRepository repository)
    {
        _repository = repository;
    }
    
    public override async IAsyncEnumerable<Order> ProduceAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var order in _repository.GetOrdersAsync(cancellationToken))
        {
            yield return order;
        }
    }
}
```

#### Pattern: Pipeline Configuration
```csharp
// Configure pipeline with epochs
var source = new PlainSourceBlock<Order, OrderSource>("source", scopeFactory);
var segmenter = new EpochSegmenterBlock<Order>("segmenter",
    EpochSegmentationPolicy.ByKey<Order, DateTime>(
        order => order.Date.Date,
        "order-source"));

graph.AddBlock(source);
graph.AddBlock(segmenter);
graph.Connect(source, segmenter);

// Configure pipeline without epochs
var source2 = new PlainSourceBlock<Order, OrderSource>("source2", scopeFactory);
var transform = new TransformBlock<Order, OrderDto>("transform", mapper);
graph.AddBlock(source2);
graph.AddBlock(transform);
graph.Connect(source2, transform); // No segmenter
```

#### Pattern: Count Segmentation
```csharp
private async IAsyncEnumerable<IEpochStream<T>> SegmentByCount(
    IAsyncEnumerable<T> input,
    IExecutionContext context)
{
    long sequence = 1;
    await using var enumerator = input.GetAsyncEnumerator(context.CancellationToken);
    
    var hasMore = await enumerator.MoveNextAsync();
    
    while (hasMore)
    {
        var epochSequence = sequence++;
        var itemsInEpoch = 0;
        
        yield return new EpochStream<T>(
            EpochVector.FromSingleSource(_policy.SourceId, epochSequence),
            StreamEpochItems());

        async IAsyncEnumerable<T> StreamEpochItems()
        {
            yield return enumerator.Current;
            itemsInEpoch++;

            while (itemsInEpoch < _policy.ItemsPerEpoch && await enumerator.MoveNextAsync())
            {
                yield return enumerator.Current;
                itemsInEpoch++;
            }

            hasMore = itemsInEpoch >= _policy.ItemsPerEpoch && await enumerator.MoveNextAsync();
        }
    }
}
```

### Integration Points

#### With EpochVector
**Integration**: Segmenter creates `EpochVector.FromSingleSource(sourceId, sequence)` for each epoch.  
**No Changes Required**: EpochVector operations remain unchanged.

#### With Lifecycle Events
**Integration**: Downstream blocks trigger lifecycle events when processing epoch streams.  
**Segmenter Role**: Transparent - does not participate in lifecycle events.  
**Testing Required**: Validate event timing with decoupled sources.

#### With EfCore Tracking Block
**Integration**: Tracking block reacts to epoch streams regardless of origin.  
**Expected**: Works identically with decoupled design.  
**Testing Required**: Validate DbContext creation, promotion, and commit timing.

#### With Progress Tracking
**Integration**: Progress tracker receives completion notifications from processing blocks.  
**No Changes Required**: Works identically with epoch streams from any source.

#### With Global Alignment
**Integration**: Watermark computed from block completions.  
**No Changes Required**: If segmenter doesn't participate in lifecycle events.

## Testing and Validation

### Test Coverage Required

#### Unit Tests

1. **Plain Source Tests**
   ```csharp
   [Fact]
   public async Task PlainSource_Should_ProduceItems()
   {
       // Test source produces data without epoch complexity
       var source = new TestPlainSource(data);
       var items = await source.ProduceAsync(context).ToListAsync();
       items.ShouldBe(expectedData);
   }
   ```

2. **Segmenter Tests - Count Policy**
   ```csharp
   [Fact]
   public async Task Segmenter_ByCount_Should_CreateCorrectEpochs()
   {
       var policy = EpochSegmentationPolicy.ByCount(3, "test");
       var segmenter = new EpochSegmenterBlock<int>("seg", policy);
       
       var input = Enumerable.Range(0, 10).ToAsyncEnumerable();
       var epochs = await segmenter.ExecuteAsync(input, context).ToListAsync();
       
       epochs.Count.ShouldBe(4); // 3+3+3+1
       var firstEpochItems = await epochs[0].Items.ToListAsync();
       firstEpochItems.ShouldBe(new[] { 0, 1, 2 });
   }
   ```

3. **Segmenter Tests - Key Policy**
   ```csharp
   [Fact]
   public async Task Segmenter_ByKey_Should_SegmentOnKeyChanges()
   {
       var policy = EpochSegmentationPolicy.ByKey<(string, int), string>(
           item => item.Item1, "test");
       var segmenter = new EpochSegmenterBlock<(string, int)>("seg", policy);
       
       var input = new[] { ("A", 1), ("A", 2), ("B", 3), ("B", 4) }.ToAsyncEnumerable();
       var epochs = await segmenter.ExecuteAsync(input, context).ToListAsync();
       
       epochs.Count.ShouldBe(2);
       // Each epoch should have items with same key
   }
   ```

4. **Segmenter Tests - None Policy**
   ```csharp
   [Fact]
   public async Task Segmenter_None_Should_CreateSingleEpoch()
   {
       var policy = EpochSegmentationPolicy.None;
       var segmenter = new EpochSegmenterBlock<int>("seg", policy);
       
       var input = Enumerable.Range(0, 100).ToAsyncEnumerable();
       var epochs = await segmenter.ExecuteAsync(input, context).ToListAsync();
       
       epochs.Count.ShouldBe(1);
       var items = await epochs[0].Items.ToListAsync();
       items.Count.ShouldBe(100);
   }
   ```

5. **Functional Equivalence Test**
   ```csharp
   [Fact]
   public async Task DecoupledDesign_Should_MatchSourceCentricResults()
   {
       // Compare results from source-centric vs decoupled
       var sourceCentricResults = await RunSourceCentric();
       var decoupledResults = await RunDecoupled();
       
       decoupledResults.ShouldBe(sourceCentricResults);
   }
   ```

#### Integration Tests

1. **With EfCore Tracking Block**
   - Create DbContext on epoch start
   - Track entities through epoch
   - Commit on global alignment
   - Verify context promotion on merges

2. **With Lifecycle Events**
   - Verify OnEpochCreatedAsync triggered
   - Verify OnEpochCompletedAsync triggered
   - Verify OnGlobalEpochAlignedAsync triggered
   - Verify timing is correct

3. **Multi-Block Pipeline**
   - Source → Segmenter → Transform → Sink
   - Verify data flows correctly
   - Verify alignment calculation
   - Verify watermark progression

4. **Multi-Source Fan-In**
   - Two plain sources → Two segmenters → Merge → Sink
   - Verify epoch vector merging
   - Verify alignment across sources

### Performance Validation

Benchmark scenarios (from research prototypes in `/poc/DataFlow.POC.Benchmarks/DecoupledEpochBenchmark.cs`):

1. **Micro-Benchmark: Pure Throughput**
   - Input: 1000 items
   - Workload: No delay (pure enumeration)
   - Measure: ops/sec, allocations, CPU
   - **Acceptance**: < 10% throughput reduction vs source-centric

2. **Realistic Workload**
   - Input: 1000 items
   - Workload: 30ms delay per item (simulated processing)
   - Measure: ops/sec, end-to-end latency
   - **Acceptance**: < 2% throughput reduction vs source-centric

3. **Memory Profile**
   - Epoch sizes: 100, 1000, 10000 items
   - Measure: allocations, GC pressure
   - **Acceptance**: < 10% memory increase

4. **Scalability**
   - Test with varying epoch sizes
   - Test with sequential vs overlapped execution
   - Measure: throughput, latency, memory

**Benchmark Execution**: Use BenchmarkDotNet, run on dedicated hardware, multiple iterations.

### Edge Cases

1. **Empty Input Stream**
   - Should produce zero epochs (not one empty epoch)
   - Validate behavior

2. **Single Item**
   - Should create single epoch with one item
   - Validate sequence numbering

3. **Epoch Boundary on Last Item**
   - Count segmentation with exact multiple
   - Should not create empty final epoch

4. **Key Segmentation - Repeated Keys**
   - Keys: A, A, B, B, A, A
   - Should create 3 epochs (A, B, A)
   - Validate epoch boundaries

5. **Clock Changes Mid-Item**
   - Clock advances during item processing
   - Validate which epoch item belongs to

6. **Cancellation During Segmentation**
   - Cancel while reading epoch
   - Validate cleanup

## Constraints and Requirements

### Technical Constraints

- **NET8.0**: Target framework
- **Async/Await**: All operations must be async
- **Backpressure**: Must preserve natural backpressure through streaming
- **Memory**: Minimal buffering, stream through
- **Cancellation**: Respect cancellation tokens throughout

### Performance Requirements

Based on research expectations:

- **Throughput**: < 10% reduction in micro-benchmarks, < 2% in realistic scenarios
- **Memory**: < 10% increase in memory usage
- **Latency**: Negligible increase in end-to-end latency
- **CPU**: Minimal CPU overhead per item

### Compatibility Requirements

- **Backward Compatible**: During migration phase, both patterns supported
- **API Stability**: No breaking changes to existing source-centric code during migration
- **Subsystem Integration**: Must work with all existing subsystems (EfCore, lifecycle, progress tracking)

### Dependencies

No new external dependencies required. Uses existing:
- `System.Threading.Channels`
- `Microsoft.Extensions.DependencyInjection`

## Alternatives Explored

See [Research Document](/research/epoch-stream-separation/README.md) for detailed analysis.

### Alternative 1: Keep Source-Centric

**Decision**: Not recommended long-term due to coupling issues, but kept during migration.

### Alternative 2: Hybrid Approach

**Decision**: Use as migration strategy, not long-term solution.

### Alternative 3: Unified Interface

**Decision**: Rejected due to complexity and reduced type safety.

## References and Resources

### Documentation

- **Research Report**: `/research/epoch-stream-separation/README.md` (19KB, comprehensive)
- **Design Document**: `/research/epoch-stream-separation/design/decoupled-epoch-architecture.md` (15KB)
- **Architecture Decision Record**: `/research/epoch-stream-separation/adr/2025-11-05-decoupled-epoch-segmentation.md` (12KB)
- **Current Implementation Analysis**: `/research/epoch-stream-separation/notes/current-implementation-analysis.md`

### Prototype Code (To Be Removed After Implementation)

- **Plain Source Interface**: `/poc/DataFlow.POC/Core/IPlainSourceActor.cs`
- **Plain Source Block**: `/poc/DataFlow.POC/Blocks/PlainSourceBlock.cs`
- **Segmenter Block**: `/poc/DataFlow.POC/Blocks/EpochSegmenterBlock.cs`
- **Tests**: `/poc/DataFlow.POC.Tests/DecoupledEpochTests.cs`
- **Benchmarks**: `/poc/DataFlow.POC.Benchmarks/DecoupledEpochBenchmark.cs`

### Prior Work

- **Phase 3 Epoch Segmentation**: `/poc/docs/plans/PHASE3_EPOCH_STREAM_SEGMENTATION.md`
- **Epoch Design**: `/poc/docs/design/epochs.md`
- **POC Glossary**: `/poc/docs/POC_GLOSSARY.md`

## Implementation Phases

### Phase 1: Core Implementation
- [ ] Implement `IPlainSourceActor<T>` interface
- [ ] Implement `PlainSourceBlock<T, TActor>`
- [ ] Implement `EpochSegmentationPolicy`
- [ ] Implement `EpochSegmenterBlock<T>`
- [ ] Unit tests for all components

**Goal**: Core components working and tested

### Phase 2: Integration and Validation
- [ ] Run performance benchmarks
- [ ] Test with EfCore tracking block
- [ ] Test lifecycle event integration
- [ ] Test multi-block scenarios
- [ ] Validate subsystem compatibility

**Goal**: Validated integration with existing systems

### Phase 3: Documentation and Examples
- [ ] API documentation
- [ ] Migration guide
- [ ] Code examples
- [ ] Update POC docs
- [ ] Update glossary

**Goal**: Clear documentation for adoption

### Phase 4: Migration Support (Optional)
- [ ] Adapter for old sources
- [ ] Deprecation warnings
- [ ] Migration tooling
- [ ] Backward compatibility helpers

**Goal**: Smooth migration path

## Success Criteria

This implementation is complete when:

- [ ] All core components implemented and tested
- [ ] Performance benchmarks meet acceptance criteria (< 10% micro, < 2% realistic)
- [ ] Integration tests pass with all subsystems
- [ ] Edge cases handled correctly
- [ ] Documentation complete
- [ ] Migration guide created
- [ ] Code review approved
- [ ] CI/CD pipeline passes

## Questions for Implementation Team

1. **Migration Timeline**: Should we support both patterns indefinitely or plan deprecation?
2. **Adapter Pattern**: Should we provide adapter for legacy sources?
3. **Metrics**: Should segmenter report metrics, or remain transparent?
4. **Graph Visualization**: Should segmenter appear in pipeline diagrams?

## Notes

- **Prototype Status**: Research prototypes exist in POC codebase and will be removed per research workflow after implementation
- **Performance**: Benchmark validation is critical before full adoption
- **Migration**: Consider gradual migration approach to minimize disruption
- **Documentation**: Clear distinction between source-centric (legacy) and decoupled (recommended) patterns

---

**Created**: 2025-11-05  
**Research Issue**: Epoch Stream Separation from Source  
**Research PR**: [Link will be added]
