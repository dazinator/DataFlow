# Exploration Notes: Decoupled Epoch Design Progress

## Work Completed

### 1. Prototype Implementation ✅
Created a fully functional prototype of the decoupled design:

#### New Components
- **`IPlainSourceActor<T>`** - Interface for sources without epoch knowledge
- **`PlainSourceBlock<T, TActor>`** - Block hosting plain source actors
- **`EpochSegmenterBlock<T>`** - External segmentation block with policies

#### Segmentation Policies
- **None** - Pass-through, single epoch for entire stream
- **Count** - Segment by item count
- **Key** - Segment by key selector function  
- **Clock** - Segment by epoch clock
- **Custom** - Custom segmentation function

### 2. Test Validation ✅
Created comprehensive tests demonstrating:
- Plain sources work without segmentation
- Segmenter correctly applies count-based segmentation
- Pass-through mode works (no segmentation)
- Key-based segmentation groups by key changes
- Clock-based segmentation reacts to clock
- **Functional equivalence** between source-centric and decoupled approaches

All 6 tests pass successfully.

### 3. Benchmark Setup ✅
Created benchmark infrastructure comparing:
- Baseline (no epochs)
- Source-centric approach
- Decoupled with segmentation
- Decoupled without segmentation
- Memory profiles

## Key Findings So Far

### Technical Feasibility
✅ **Decoupled design is technically viable**
- Can separate epoch concerns from source
- Clean interface boundaries
- Functional equivalence demonstrated

### API Ergonomics - Initial Observations

#### Source-Centric (Current)
```csharp
public class OrderSource : SourceActorBase<Order>
{
    public override async IAsyncEnumerable<IEpochStream<Order>> ProduceEpochsAsync(...)
    {
        var data = FetchOrders();
        await foreach (var epochStream in EpochSegmenter.SegmentByKey(
            data,
            order => order.Date,
            "order-source"))
        {
            yield return epochStream;
        }
    }
}
```

**Observation**: Source must know about and call `EpochSegmenter`.

#### Decoupled (Prototype)
```csharp
// Source: No epoch knowledge
public class OrderSource : PlainSourceActorBase<Order>
{
    public override async IAsyncEnumerable<Order> ProduceAsync(...)
    {
        return FetchOrders(); // Just produce data
    }
}

// Pipeline: Segmentation applied externally
var source = new PlainSourceBlock<Order, OrderSource>("source", scopeFactory);
var segmenter = new EpochSegmenterBlock<Order>(
    "segmenter",
    EpochSegmentationPolicy.ByKey<Order, DateTime>(o => o.Date, "order-source"));

// Flow: source → segmenter → downstream
```

**Observation**: Cleaner separation, source is simpler, segmentation policy is external.

### Composability Benefits Identified

1. **Source Reusability**
   - Same source can be used with different segmentation strategies
   - Same source can be used without epochs
   - Sources become domain-focused, not epoch-aware

2. **Policy Flexibility**
   - Change segmentation strategy without modifying source
   - Test different policies easily
   - Dynamic policy changes possible

3. **Optional Epochs**
   - Pipelines can opt out of epochs entirely
   - Pass-through mode available
   - No need for duplicate "non-epoch" sources

## Remaining Work

### 1. Complete Benchmark Analysis 🔄
- [ ] Run benchmarks on different configurations
- [ ] Measure throughput impact
- [ ] Measure memory overhead
- [ ] Analyze CPU usage
- [ ] Test with realistic workloads (30ms delay)
- [ ] Compare small vs large epoch sizes

### 2. Subsystem Impact Analysis 🔄
Need to test and document impact on:

#### EpochVector
- [ ] Test that vectors work correctly with decoupled design
- [ ] Verify ancestry and merge operations
- [ ] Confirm source identification works

#### Lifecycle Events
- [ ] Test `OnEpochCreatedAsync` triggering
- [ ] Test `OnEpochCompletedAsync` triggering
- [ ] Test `OnGlobalEpochAlignedAsync` triggering
- [ ] Verify timing is correct

#### EfCore Tracking Block
- [ ] Test per-epoch DbContext creation
- [ ] Test context promotion on merged epochs
- [ ] Verify transaction boundaries remain safe
- [ ] Test with actual EfCore operations

#### CompletionBasedEpochProgress
- [ ] Verify progress tracking works
- [ ] Test watermark calculation
- [ ] Confirm alignment semantics

#### GlobalEpochAlignment
- [ ] Test multi-block coordination
- [ ] Verify watermark computation
- [ ] Test with fan-in scenarios

### 3. Complete API Ergonomics Analysis 🔄
- [ ] Document common patterns with both approaches
- [ ] Compare code complexity
- [ ] Analyze error handling differences
- [ ] Evaluate debugging experience

### 4. Create Comprehensive Documentation 🔄
- [ ] Research findings document with mermaid diagrams
- [ ] Architecture comparison diagrams
- [ ] Data flow visualizations
- [ ] Before/after subsystem behavior documentation

### 5. Create Design Documents 🔄
- [ ] Decoupled architecture design
- [ ] API specifications
- [ ] Integration patterns
- [ ] Migration guide

### 6. Create ADRs 🔄
- [ ] ADR for decoupled vs source-centric decision
- [ ] Rationale with pros/cons
- [ ] Performance impact analysis
- [ ] Migration path consideration

### 7. Create Implementation Issue 🔄
- [ ] Complete implementation-ready handover
- [ ] Test scenarios
- [ ] Performance requirements
- [ ] Edge cases

## Next Immediate Steps

1. **Run benchmarks** and collect performance data
2. **Test subsystem integration** (especially EfCore tracking and lifecycle events)
3. **Document findings** with detailed analysis
4. **Create comparison diagrams** showing both approaches
5. **Draft ADR** with recommendation

## Questions to Answer

### Performance
- What is the overhead of the additional segmenter block?
- Does it impact throughput significantly?
- Memory allocation differences?

### Subsystems
- Do all subsystems work correctly with decoupled design?
- Any breaking changes or required adaptations?
- Documentation gaps?

### Migration
- How difficult would it be to migrate existing code?
- Breaking change implications?
- Backward compatibility options?

### Recommendation
- Given all findings, which approach should we recommend?
- What are the trade-offs?
- Is it worth the migration effort?
