# Current Implementation Analysis

## Current Source-Centric Architecture

### Key Components

#### 1. ISourceActor<T>
```csharp
public interface ISourceActor<T>
{
    IAsyncEnumerable<IEpochStream<T>> ProduceEpochsAsync(IActorExecutionContext context);
}
```

**Responsibilities**:
- Produce `IEpochStream<T>` directly
- Control epoch boundaries
- Manage epoch vector creation and progression
- Couple data generation with epoch segmentation logic

#### 2. EpochSourceBlock<T, TActor>
```csharp
public sealed class EpochSourceBlock<T, TActor> : BlockBase<object, IEpochStream<T>>
    where TActor : ISourceActor<T>
```

**Responsibilities**:
- Host the source actor with DI scope
- Pass through epoch streams from actor
- Minimal wrapper around `ISourceActor<T>`

#### 3. EpochSegmenter (Utility Class)
```csharp
public static class EpochSegmenter
{
    static IAsyncEnumerable<IEpochStream<T>> SegmentByEpoch<T>(...);
    static IAsyncEnumerable<IEpochStream<T>> SegmentByKey<T, TKey>(...);
}
```

**Current Usage**:
- Static utility methods
- Used **within** source actors, not as a pipeline block
- Provides segmentation logic that sources can use
- Not a first-class block in the pipeline

## Data Flow in Current Design

```
┌─────────────────┐
│  ISourceActor   │
│ ProduceEpochs() │──┐
└─────────────────┘  │
                     │ IAsyncEnumerable<IEpochStream<T>>
                     │
                     ▼
┌──────────────────────────┐
│  EpochSourceBlock        │
│  (minimal wrapper)       │
└──────────────────────────┘
                     │
                     │ IAsyncEnumerable<IEpochStream<T>>
                     ▼
┌──────────────────────────┐
│  Downstream Blocks       │
│  (Transform, Process)    │
└──────────────────────────┘
```

## Current Segmentation Patterns

### Pattern 1: Key-Based Segmentation in Source
```csharp
public class OrderSourceActor : SourceActorBase<Order>
{
    public override async IAsyncEnumerable<IEpochStream<Order>> ProduceEpochsAsync(...)
    {
        var rawData = ProduceRawOrders(); // IAsyncEnumerable<Order>
        
        // Segmentation happens inside the source
        await foreach (var epochStream in EpochSegmenter.SegmentByKey(
            rawData,
            order => order.Date.Date,
            "order-source"))
        {
            yield return epochStream;
        }
    }
}
```

**Observation**: Source must know about epochs and segmentation strategy.

### Pattern 2: Clock-Based Segmentation in Source
```csharp
public class StreamSourceActor : SourceActorBase<Event>
{
    private readonly IEpochClock _clock;
    
    public override async IAsyncEnumerable<IEpochStream<Event>> ProduceEpochsAsync(...)
    {
        var rawData = ProduceRawEvents(); // IAsyncEnumerable<Event>
        
        // Segmentation with clock happens inside the source
        await foreach (var epochStream in EpochSegmenter.SegmentByEpoch(
            rawData,
            _clock,
            new EpochSegmenterConfig { ExecutionPolicy = EpochExecutionPolicy.Sequential }))
        {
            yield return epochStream;
        }
    }
}
```

**Observation**: Source must know about epoch clocks and execution policies.

### Pattern 3: Manual Epoch Control in Source
```csharp
public class DatabaseSourceActor : SourceActorBase<Record>
{
    public override async IAsyncEnumerable<IEpochStream<Record>> ProduceEpochsAsync(...)
    {
        long sequence = 1;
        
        while (!context.CancellationToken.IsCancellationRequested)
        {
            var records = await FetchBatchFromDatabase();
            var epoch = CreateEpoch("db-source", sequence++);
            
            // Manually create epoch streams
            yield return CreateEpochStream(epoch, records.ToAsyncEnumerable());
        }
    }
}
```

**Observation**: Source has full control but also full responsibility for epoch logic.

## Current Strengths

### 1. Control and Flexibility
- Sources have complete control over epoch boundaries
- Can align epochs with natural domain boundaries (e.g., database pages, API batches)
- No additional pipeline stage needed

### 2. Type Safety
- `ISourceActor<T>` contract is clear: produces `IEpochStream<T>`
- Compile-time guarantee that sources produce epoched data
- No ambiguity about whether epochs are present

### 3. Minimal Indirection
- Direct path from source to downstream blocks
- No intermediate segmenter block adding latency
- Fewer pipeline stages to reason about

## Current Weaknesses

### 1. Tight Coupling
- **Source logic + Epoch logic intertwined**
  - Cannot reuse source without epoch knowledge
  - Cannot test source data generation separately from epoch segmentation
  - Cannot change segmentation strategy without modifying source

### 2. Reduced Composability
- **Cannot reuse sources across different epoch strategies**
  - A source that segments by day cannot easily be repurposed to segment by hour
  - Requires creating a new source actor for each segmentation need
  
- **Cannot use same source with and without epochs**
  - If some pipelines need epochs and others don't, need duplicate sources
  - No "opt-out" mode for epoch-less scenarios

### 3. Generalization Challenges
- **Hard to coordinate epochs across multiple sources**
  - Each source manages its own epoch vector independently
  - External coordination of epoch boundaries is complex
  
- **Limited policy flexibility**
  - Segmentation policy is baked into source implementation
  - Cannot dynamically change policies at runtime
  - Cannot A/B test different segmentation strategies easily

### 4. Testing Complexity
- **Cannot unit test source data generation separately**
  - Source tests must also deal with epoch streams
  - Simple data generation tests become epoch-aware tests
  
- **Mock/fake sources must understand epochs**
  - Test helpers must implement full `ISourceActor<T>` interface
  - More complex test setup

## Impact on Subsystems (Current Design)

### EpochVector and Ancestry
- **Current**: EpochVector created by sources, propagated through pipeline
- **Works well**: Vector operations (merge, subsumption) are independent of source

### Lifecycle Events
- **Current**: Events triggered based on epoch stream boundaries
- **OnEpochCreatedAsync**: Fired when block starts processing epoch stream
- **OnEpochCompletedAsync**: Fired when block finishes epoch stream
- **OnGlobalEpochAlignedAsync**: Fired when all blocks complete an epoch

### EfCore Tracking Block
- **Current**: Creates per-epoch DbContext when epoch stream begins
- **Dependency**: Relies on `IEpochStream<T>` to demarcate transaction boundaries
- **Context Promotion**: Uses epoch vector ancestry to reuse contexts on merge

### CompletionBasedEpochProgress
- **Current**: Tracks when epoch streams complete per block
- **Dependency**: Stream exhaustion indicates epoch completion

### GlobalEpochAlignment
- **Current**: Computes watermark from per-block completion states
- **Dependency**: Each block reports completion after epoch stream exhaustion

## Questions for Decoupled Design

1. **How would sources emit data?**
   - Plain `IAsyncEnumerable<T>` instead of `IEpochStream<T>`?
   - New interface `IPlainSourceActor<T>`?

2. **Where would EpochSegmenterBlock fit?**
   - Source → Segmenter → Transform → Sink?
   - How to configure segmentation policy?

3. **How to handle "no epochs" scenarios?**
   - Pass-through mode in segmenter?
   - Optional segmenter in pipeline?

4. **Impact on type signatures?**
   - Do downstream blocks still expect `IEpochStream<T>`?
   - Or do they accept both `T` and `IEpochStream<T>`?

5. **Performance implications?**
   - Additional block in pipeline = extra async overhead?
   - Extra allocations for segmentation?

## Next Steps

1. Prototype `EpochSegmenterBlock` as first-class pipeline block
2. Create plain source interfaces without epoch knowledge
3. Test subsystem compatibility
4. Benchmark performance differences
5. Evaluate API ergonomics with code examples
