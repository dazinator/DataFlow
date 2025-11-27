# Implementation: Unified Epoch Model with Intelligent Edge Unwrap/Wrap

## Context and Objectives

### Problem Statement

DataFlow's current epoch coordination has a fundamental architectural mismatch: edges route epoch stream **containers** (`IEpochStream<T>`) instead of data **items** (`T`). This breaks edge-level routing semantics (broadcast, selective, competing) when epoch streams use channel-based data sources.

**Impact**:
- ❌ Broadcast routing: Only one consumer receives data (container sharing)
- ❌ Selective routing: Cannot inspect item properties (routing containers, not items)
- ❌ Competing consumers: All items go to single consumer (container not distributed)
- ✅ 1:1 connections: Work by accident

### Research Background

Research was conducted to validate architectural approaches and provide implementation-ready specifications.

**Research Documentation**: `/research/unified-epoch-model/`

**Key Research Artifacts**:
- Main findings: `/research/unified-epoch-model/README.md`
- Design docs: `/research/unified-epoch-model/design/`
- ADR: `/research/unified-epoch-model/design/ADR-unified-epoch-model.md`
- Comparison analysis: `/research/unified-epoch-model/design/analysis-and-recommendation.md`

**Key Findings from Research**:
1. **Container Routing Confirmed**: Edges route `IEpochStream<T>` containers, not data items
2. **Two Options Analyzed**: Graph-per-epoch vs Edge unwrap/wrap
3. **Option 2 Recommended**: Intelligent edge unwrap/wrap superior on 7/10 criteria
4. **Critical Insight**: Option 1 doesn't eliminate per-item checks, negating epoch benefits
5. **Performance Target**: <10% overhead (expect <5% with Option 2)

### Objectives

What this implementation should achieve:

- [x] Fix broadcast routing for epoch streams (all consumers get all items)
- [x] Fix selective routing for epoch streams (route by item properties)
- [x] Fix competing consumer routing for epoch streams (proper load balancing)
- [x] Preserve epoch boundary efficiency (no per-item checks needed)
- [x] Maintain backward compatibility (no API changes)
- [x] Keep performance overhead <10% (target <5%)
- [x] Maintain clear separation of concerns (edges handle routing)

---

## Implementation Guidance

### Recommended Approach

**Intelligent Edge Unwrap/Wrap**: Edges detect `IEpochStream<T>` types, unwrap to enumerate individual items, route items using edge strategy logic, and re-wrap into new epoch streams for each downstream consumer.

**Key Principles**:
1. **Edges are epoch-aware**: Detect epoch stream types and handle unwrap/wrap
2. **Strategy-specific channel management**: Different topologies use different channel setups
3. **Epoch metadata propagation**: Vector and epoch instance flow through channel creation
4. **Natural backpressure**: Channels provide backpressure as in current implementation
5. **Localized complexity**: Changes isolated to edge layer, blocks unchanged

### Design References

Supporting documentation created during research:

- **Research README**: `/research/unified-epoch-model/README.md`
- **Option 2 Design**: `/research/unified-epoch-model/design/option2-edge-unwrap-wrap.md`
- **Architecture Decision Record**: `/research/unified-epoch-model/design/ADR-unified-epoch-model.md`
- **Comparison Analysis**: `/research/unified-epoch-model/design/analysis-and-recommendation.md`
- **Context Analysis**: `/research/unified-epoch-model/notes/context-analysis.md`

---

## Implementation Plan

### Phase 1: Core Infrastructure (Week 1)

**Estimated Effort**: 3-5 days

#### Tasks

1. **Add Epoch Stream Type Detection**
   - Location: `/poc/DataFlow.POC/Core/ReflectionHelper.cs`
   - Modify `EnumerateAndRouteTypedStreamGenericAsync<T>()` to detect `IEpochStream<T>`
   - Route to new unwrap/wrap handler for epoch streams
   
   ```csharp
   if (typeof(T).IsGenericType && 
       typeof(T).GetGenericTypeDefinition() == typeof(IEpochStream<>))
   {
       // Use reflection to call generic method with extracted item type
       var itemType = typeof(T).GetGenericArguments()[0];
       var method = typeof(ReflectionHelper)
           .GetMethod(nameof(EnumerateAndRouteEpochStreamAsync), BindingFlags.NonPublic | BindingFlags.Static)
           .MakeGenericMethod(itemType);
       await (Task)method.Invoke(null, new object[] { stream, routers, cancellationToken });
   }
   ```

2. **Implement ChannelBackedEpochStream**
   - Location: `/poc/DataFlow.POC/Core/ChannelBackedEpochStream.cs` (new file)
   - Wrap channels as `IEpochStream<T>`
   - Expose writer for edge routing
   - Handle completion signaling
   
   ```csharp
   public class ChannelBackedEpochStream<T> : IEpochStream<T>
   {
       private readonly Channel<T> _channel;
       
       public EpochVector EpochVector { get; }
       public IEpoch Epoch { get; }
       public IAsyncEnumerable<T> Items => _channel.Reader.ReadAllAsync();
       
       public ChannelWriter<T> GetWriter() => _channel.Writer;
       public void CompleteWriting() => _channel.Writer.Complete();
   }
   ```

3. **Create Base Unwrap/Wrap Logic**
   - Location: `/poc/DataFlow.POC/Core/ReflectionHelper.cs`
   - Generic method to unwrap source epoch stream
   - Route individual items using existing strategy logic
   - Complete downstream channels when done
   
   ```csharp
   private static async Task EnumerateAndRouteEpochStreamAsync<TItem>(
       IAsyncEnumerable<IEpochStream<TItem>> epochStreamSource,
       List<ITypedEdgeRouter> routers,
       CancellationToken cancellationToken)
   {
       await foreach (var epochStream in epochStreamSource.WithCancellation(cancellationToken))
       {
           // 1. Create downstream epoch streams (strategy-specific)
           var downstreamStreams = await CreateDownstreamEpochStreamsAsync<TItem>(
               routers, epochStream, cancellationToken);
           
           // 2. Route items from source to downstream streams
           await foreach (var item in epochStream.Items.WithCancellation(cancellationToken))
           {
               await RouteItemToDownstreamStreamsAsync(item, routers, downstreamStreams, cancellationToken);
           }
           
           // 3. Complete all downstream channels
           await CompleteDownstreamStreamsAsync(downstreamStreams);
       }
   }
   ```

**Acceptance Criteria**:
- ✅ Epoch stream types detected correctly
- ✅ ChannelBackedEpochStream implements IEpochStream<T>
- ✅ Unwrap/wrap logic compiles and runs
- ✅ Unit tests pass for core infrastructure

---

### Phase 2: Strategy Adaptations (Week 2)

**Estimated Effort**: 5-7 days

#### Task 1: Broadcast Strategy

**Location**: `/poc/DataFlow.POC/Core/EdgeStrategy.cs`

**Changes**: Add epoch stream handling to `BroadcastEdgeStrategy`

**Channel Setup**: One channel per downstream block (items duplicated)

```csharp
// In CreateDownstreamEpochStreams for broadcast:
var downstreamStreams = new Dictionary<IBlock, ChannelBackedEpochStream<T>>();

foreach (var targetBlock in targetBlocks)
{
    var channel = Channel.CreateBounded<T>(_bufferCapacity);
    downstreamStreams[targetBlock] = new ChannelBackedEpochStream<T>(
        epochStream.EpochVector,
        epochStream.Epoch,
        channel);
}

return downstreamStreams;
```

**Routing Logic**:
```csharp
// In RouteItemToDownstreamStreams for broadcast:
// Write item to ALL downstream channels (concurrent)
var writeTasks = downstreamStreams.Values
    .Select(stream => stream.GetWriter().WriteAsync(item, cancellationToken).AsTask());
await Task.WhenAll(writeTasks);
```

**Tests**:
- 3 consumers each receive all 100 items from epoch stream
- Epoch vector matches in all downstream streams
- Backpressure propagates correctly

#### Task 2: Selective Strategy

**Location**: `/poc/DataFlow.POC/Core/EdgeStrategy.cs`

**Changes**: Add epoch stream handling to `SelectiveEdgeStrategy`

**Channel Setup**: One channel per route (items filtered)

```csharp
// In CreateDownstreamEpochStreams for selective:
var routeStreams = new Dictionary<string, ChannelBackedEpochStream<T>>();

foreach (var route in _routes)
{
    var channel = Channel.CreateBounded<T>(_bufferCapacity);
    routeStreams[route.Name] = new ChannelBackedEpochStream<T>(
        epochStream.EpochVector,
        epochStream.Epoch,
        channel);
}

return routeStreams;
```

**Routing Logic**:
```csharp
// In RouteItemToDownstreamStreams for selective:
var routeName = _routeSelector(item);
if (routeStreams.TryGetValue(routeName, out var stream))
{
    await stream.GetWriter().WriteAsync(item, cancellationToken);
}
```

**Tests**:
- Even/odd routing splits 100 items correctly (50 each)
- Items routed based on properties
- Epoch vectors match

#### Task 3: Competing Strategy

**Location**: `/poc/DataFlow.POC/Core/EdgeStrategy.cs`

**Changes**: Add epoch stream handling to `CompetingEdgeStrategy`

**Channel Setup**: One shared channel (items load-balanced)

```csharp
// In CreateDownstreamEpochStreams for competing:
var sharedChannel = Channel.CreateBounded<T>(_bufferCapacity);

// All targets share the same channel
var downstreamStreams = new Dictionary<IBlock, ChannelBackedEpochStream<T>>();
foreach (var targetBlock in targetBlocks)
{
    downstreamStreams[targetBlock] = new ChannelBackedEpochStream<T>(
        epochStream.EpochVector,
        epochStream.Epoch,
        sharedChannel); // SAME channel instance
}

return downstreamStreams;
```

**Routing Logic**:
```csharp
// In RouteItemToDownstreamStreams for competing:
// Write to shared channel (consumers compete)
var sharedStream = downstreamStreams.Values.First(); // All same instance
await sharedStream.GetWriter().WriteAsync(item, cancellationToken);
```

**Tests**:
- 3 consumers process all 100 items exactly once
- Load distribution is reasonable
- Epoch vectors match

**Acceptance Criteria**:
- ✅ Broadcast strategy works with epoch streams
- ✅ Selective strategy works with epoch streams
- ✅ Competing strategy works with epoch streams
- ✅ All topology tests pass

---

### Phase 3: Testing & Validation (Week 3)

**Estimated Effort**: 5-7 days

#### Functional Tests

**Location**: `/poc/DataFlow.POC.Tests/EpochRouting/`

**Test Scenarios**:

1. **Broadcast Topology**
   ```csharp
   [Fact]
   public async Task BroadcastEdge_WithChannelBasedEpochStreams_AllConsumersGetAllItems()
   {
       // Setup: Source emits epoch stream with 100 items
       // Broadcast to 3 downstream blocks
       // Assert: Each block receives all 100 items
       // Assert: Epoch vectors match
   }
   ```

2. **Selective Routing**
   ```csharp
   [Fact]
   public async Task SelectiveEdge_WithEpochStreams_RoutesItemsByProperty()
   {
       // Setup: Source emits epoch stream with even/odd items
       // Selective routing on item % 2
       // Assert: Even route gets 50 items, odd route gets 50 items
       // Assert: Epoch vectors match
   }
   ```

3. **Competing Consumers**
   ```csharp
   [Fact]
   public async Task CompetingEdge_WithEpochStreams_LoadBalancesItems()
   {
       // Setup: Source emits epoch stream with 100 items
       // 3 competing consumers
       // Assert: All items processed exactly once
       // Assert: Load distribution reasonable (±20%)
   }
   ```

4. **Epoch Vector Propagation**
   ```csharp
   [Fact]
   public async Task EpochRouting_PreservesEpochVectorThroughTopology()
   {
       // Setup: Source A {A=1}, Source B {B=1}
       // Fan-in to create {A=1, B=1}
       // Broadcast downstream
       // Assert: All downstream blocks see {A=1, B=1}
   }
   ```

5. **Backpressure**
   ```csharp
   [Fact]
   public async Task EpochRouting_BackpressureWorksCorrectly()
   {
       // Setup: Slow downstream consumer
       // Fast upstream producer
       // Assert: Upstream is throttled
       // Assert: No data loss
       // Assert: No deadlocks
   }
   ```

#### Performance Tests

**Location**: `/poc/DataFlow.POC.Benchmarks/EpochRouting/`

**Benchmarks**:

1. **Unwrap/Wrap Overhead**
   ```csharp
   [Benchmark]
   public async Task BroadcastRouting_PlainItems() { /* baseline */ }
   
   [Benchmark]
   public async Task BroadcastRouting_EpochStreams() { /* with unwrap/wrap */ }
   ```

2. **Throughput**
   ```csharp
   [Benchmark]
   public async Task ProcessManyItems_AcrossEpochs()
   {
       // Process 1M items across 10 epochs
       // Measure: Total time
       // Compare: With current approach (if available)
   }
   ```

3. **Memory Usage**
   ```csharp
   [MemoryDiagnoser]
   [Benchmark]
   public async Task MemoryProfiling_ChannelBackedEpochStreams()
   {
       // Measure: Channel buffer usage
       // Measure: GC pressure
   }
   ```

**Performance Targets**:
- Overhead <10% vs baseline (target <5%)
- Throughput comparable to plain block processing
- Memory overhead reasonable for typical workloads

**Acceptance Criteria**:
- ✅ All functional tests pass
- ✅ Performance overhead <10%
- ✅ No deadlocks or data loss
- ✅ Memory usage acceptable

---

### Phase 4: Documentation (Week 4)

**Estimated Effort**: 3-5 days

#### Tasks

1. **Update Epoch Processing Guide**
   - Location: `/poc/docs/guides/using-epochs.md`
   - Document that epoch streams now work with all topologies
   - Remove limitations section about 1:1 connections
   - Add examples for broadcast, selective, competing

2. **Update Topology Guides**
   - Broadcast: `/poc/docs/guides/topology-broadcast.md`
   - Selective: `/poc/docs/guides/topology-selective-routing.md`
   - Competing: `/poc/docs/guides/topology-competing-consumers.md`
   - Add epoch stream examples to each

3. **Add Architecture Documentation**
   - Location: `/poc/docs/architecture/epoch-routing.md` (new)
   - Explain edge unwrap/wrap mechanism
   - Include diagrams showing channel backing
   - Document ChannelBackedEpochStream design

4. **Update Migration Guide**
   - Location: `/MIGRATION_GUIDE.md`
   - Add entry for this change (internal fix, no user action needed)
   - Document that existing epoch-aware code now works with all topologies

**Acceptance Criteria**:
- ✅ Documentation updated and accurate
- ✅ Examples provided for all topologies
- ✅ Architecture clearly explained with diagrams

---

## API/Interface Design

### New Types

#### ChannelBackedEpochStream

```csharp
/// <summary>
/// Epoch stream backed by a channel for edge routing.
/// </summary>
public class ChannelBackedEpochStream<T> : IEpochStream<T>
{
    private readonly Channel<T> _channel;
    
    public EpochVector EpochVector { get; }
    public IEpoch Epoch { get; }
    public IAsyncEnumerable<T> Items => _channel.Reader.ReadAllAsync();
    
    public ChannelBackedEpochStream(
        EpochVector epochVector,
        IEpoch epoch,
        Channel<T> channel)
    {
        EpochVector = epochVector;
        Epoch = epoch;
        _channel = channel;
    }
    
    internal ChannelWriter<T> GetWriter() => _channel.Writer;
    internal void CompleteWriting() => _channel.Writer.Complete();
}
```

### Modified Interfaces

#### EdgeStrategy (Internal Changes)

```csharp
public abstract class EdgeStrategy
{
    // Existing method - unchanged signature
    public abstract Task RouteTypedItemAsync<T>(
        T item,
        Dictionary<IBlock, ChannelWriter<T>> typedWriters,
        CancellationToken cancellationToken);
    
    // New internal method for epoch stream routing
    protected internal virtual Task<Dictionary<IBlock, ChannelBackedEpochStream<TItem>>> 
        CreateDownstreamEpochStreamsAsync<TItem>(
            IEpochStream<TItem> sourceEpochStream,
            IReadOnlyList<IBlock> targetBlocks,
            CancellationToken cancellationToken)
    {
        // Default implementation - can be overridden by strategies
    }
}
```

---

## Component Architecture

```
┌────────────────────────────────────────────────────────────┐
│                   Source Block                              │
│  outputs: IAsyncEnumerable<IEpochStream<int>>              │
└─────────────────────┬──────────────────────────────────────┘
                      │
                      │ Yields: IEpochStream<int> {vector={A=1}, items=channel}
                      ▼
     ┌────────────────────────────────────────┐
     │     Edge Routing (ReflectionHelper)     │
     │  - Detect IEpochStream<T> type          │
     │  - Call EnumerateAndRouteEpochStreamAsync│
     └────────────────┬───────────────────────┘
                      │
                      ▼
     ┌────────────────────────────────────────┐
     │  Unwrap/Wrap Logic                      │
     │  1. Extract epoch metadata              │
     │  2. Create downstream channels          │
     │  3. Enumerate source items              │
     │  4. Route items via strategy            │
     │  5. Complete channels when done         │
     └────┬──────────────┬────────────────────┘
          │              │
          │              │ Creates: ChannelBackedEpochStream per target
          ▼              ▼
    ┌─────────┐    ┌─────────┐
    │ Block A │    │ Block B │
    │ receives│    │ receives│
    │ IEpoch- │    │ IEpoch- │
    │ Stream  │    │ Stream  │
    └─────────┘    └─────────┘
         │              │
         │              └── Enumerates .Items: [1,2,3,4,5] ✓
         └───────────────── Enumerates .Items: [1,2,3,4,5] ✓
```

---

## Key Implementation Considerations

### 1. Type Detection Performance

**Consideration**: Reflection to detect `IEpochStream<T>` types may have overhead.

**Mitigation**: 
- Cache type check results in static dictionary (or use HashSet for known types)
- Perform check once per block output type, not per item
- Use compile-time known types where possible

```csharp
// Option 1: Concurrent dictionary (good for dynamic types)
private static readonly ConcurrentDictionary<Type, bool> _isEpochStreamTypeCache = new();

private static bool IsEpochStreamType(Type type)
{
    return _isEpochStreamTypeCache.GetOrAdd(type, t =>
        t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEpochStream<>));
}

// Option 2: HashSet (more efficient for known limited set of types)
private static readonly HashSet<Type> _knownEpochStreamTypes = new();
private static readonly object _typeCacheLock = new();

private static bool IsEpochStreamType(Type type)
{
    if (!type.IsGenericType) return false;
    
    lock (_typeCacheLock)
    {
        if (_knownEpochStreamTypes.Contains(type)) return true;
        
        var isEpochStream = type.GetGenericTypeDefinition() == typeof(IEpochStream<>);
        if (isEpochStream)
        {
            _knownEpochStreamTypes.Add(type);
        }
        return isEpochStream;
    }
}
```

### 2. Channel Buffer Sizing

**Consideration**: What buffer size for downstream channels?

**Strategy**: Use same buffer capacity as configured for edge strategy.

```csharp
var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(_bufferCapacity)
{
    FullMode = BoundedChannelFullMode.Wait,
    SingleReader = true,
    SingleWriter = false
});
```

**Rationale**: Maintains backpressure behavior consistent with current implementation.

### 3. Epoch Stream Completion

**Consideration**: When to complete downstream channels?

**Strategy**: Complete after enumerating all items from source epoch stream.

```csharp
await foreach (var item in sourceEpochStream.Items)
{
    // Route item...
}

// Source exhausted - complete all downstream channels
foreach (var stream in downstreamStreams.Values)
{
    stream.CompleteWriting();
}
```

**Edge Case**: What if source epoch stream throws exception?

```csharp
try
{
    await foreach (var item in sourceEpochStream.Items)
    {
        // Route item...
    }
}
catch (Exception ex)
{
    // Complete with error
    foreach (var stream in downstreamStreams.Values)
    {
        stream.GetWriter().Complete(ex);
    }
    throw;
}
```

### 4. Concurrent Routing

**Consideration**: Should items be routed concurrently to downstream channels?

**Strategy**: Yes, for broadcast (as currently done).

```csharp
// For broadcast: write to all channels concurrently
if (downstreamStreams.Count > 1)
{
    var writeTasks = downstreamStreams.Values
        .Select(s => s.GetWriter().WriteAsync(item, cancellationToken).AsTask());
    await Task.WhenAll(writeTasks);
}
else
{
    // Single downstream: no Task.WhenAll overhead
    await downstreamStreams.Values.First().GetWriter().WriteAsync(item, cancellationToken);
}
```

**Rationale**: Avoids serialization bottleneck, matches current broadcast behavior.

### 5. Epoch Vector and Epoch Instance Sharing

**Consideration**: Should downstream epoch streams share same EpochVector and IEpoch instances?

**Strategy**: Yes - reference the same objects.

```csharp
new ChannelBackedEpochStream<T>(
    sourceEpochStream.EpochVector,  // SAME reference
    sourceEpochStream.Epoch,        // SAME reference
    channel);
```

**Rationale**: 
- Epoch correlation maintained
- DI scope shared across all consumers of same epoch
- Matches semantic meaning (same epoch, different streams)

---

## Test Scenarios and Edge Cases

### Edge Cases

1. **Empty Epoch Stream**
   - Source emits epoch stream with 0 items
   - Expected: Downstream receives epoch stream with 0 items
   - Test: Verify channels complete without error

2. **Exception During Enumeration**
   - Source epoch stream throws exception mid-enumeration
   - Expected: Exception propagates, channels complete with error
   - Test: Verify no deadlocks, resources cleaned up

3. **Cancellation During Routing**
   - Cancellation token triggered while routing items
   - Expected: Routing stops, channels complete, no data loss up to cancellation
   - Test: Verify graceful cancellation

4. **Slow Consumer Backpressure**
   - One consumer is very slow
   - Expected: Upstream is throttled by slowest consumer (broadcast)
   - Test: Verify no unbounded buffering

5. **Competing Consumer with No Consumers**
   - Competing edge with 0 target blocks
   - Expected: Items are discarded (or throw exception?)
   - Test: Define and validate behavior

### Performance Edge Cases

1. **Large Epoch Streams**
   - Epoch stream with 1M items
   - Test: Verify performance scales linearly
   - Measure: Memory usage, GC pressure

2. **Many Downstream Consumers**
   - Broadcast to 100 downstream blocks
   - Test: Verify concurrent writes don't cause contention
   - Measure: Throughput vs number of consumers

3. **Fine-Grained Epochs**
   - 1000 epochs with 10 items each
   - Test: Channel creation overhead acceptable
   - Measure: Overhead per epoch

---

## Performance Requirements

### Targets

| Metric | Target | Measurement Method |
|--------|--------|-------------------|
| Unwrap/Wrap Overhead | <10% (prefer <5%) | Benchmark vs plain routing |
| Throughput | >90% of baseline | Process 1M items, measure time |
| Memory Overhead | <20% increase | Memory profiler during test |
| Channel Creation Time | <500ns per channel | Micro-benchmark |
| Type Detection Time | <100ns per call | Micro-benchmark (cached) |

### Validation Benchmarks

```csharp
[Benchmark(Baseline = true)]
public async Task Baseline_PlainItemRouting()
{
    // Route 1M plain items through broadcast edge
}

[Benchmark]
public async Task EpochStreamRouting_WithUnwrapWrap()
{
    // Route 1M items via epoch streams through broadcast edge
    // Target: <10% slower than baseline
}
```

---

## Alternatives Explored

### Alternative 1: Graph Per Epoch

**Approach**: Create new graph instance for each epoch.

**Rejected Because**:
- Doesn't eliminate per-item epoch checks
- High per-epoch overhead (10-50 μs)
- Complex concurrency management
- Stateful blocks problematic
- Breaking API changes required

**Score**: 2/11 criteria better, 6/11 worse

See: `/research/unified-epoch-model/design/option1-graph-per-epoch.md`

### Alternative 2: Document Limitation

**Approach**: Accept that epoch blocks only work with 1:1 connections.

**Rejected Because**:
- Major functional limitation
- Breaks important use cases
- Forces inefficient workarounds

### Alternative 3: Plain Blocks with Context Lookup

**Approach**: Blocks use `IAsyncEnumerable<T>`, check `context.CurrentEpoch` per item.

**Rejected Because**:
- Per-item overhead
- Loses epoch boundary efficiency
- Attempted in PR #29 and #30, not ideal

---

## Migration and Backward Compatibility

### Breaking Changes

**None** - This is an internal fix that makes existing epoch-aware code work correctly.

### User Impact

**Positive**: 
- Epoch-aware blocks now work with broadcast, selective, and competing topologies
- No code changes required
- Existing tests should pass

**Neutral**:
- Slight performance change (<5% overhead expected, but fixes major bugs)

### Migration Steps

**For Users**: None required

**For Repository**:
1. Merge this implementation
2. Update documentation to remove "1:1 only" limitation
3. Add examples showing epoch streams with all topologies

---

## References

### Research Documentation

- **Main Research**: `/research/unified-epoch-model/README.md`
- **Option 2 Design**: `/research/unified-epoch-model/design/option2-edge-unwrap-wrap.md`
- **ADR**: `/research/unified-epoch-model/design/ADR-unified-epoch-model.md`
- **Comparison**: `/research/unified-epoch-model/design/analysis-and-recommendation.md`

### Related Issues

- [Issue #15](https://github.com/uniun-technology/dataflow/issues/15) - Original epoch blocks issue
- [Issue #26](https://github.com/uniun-technology/dataflow/issues/26) - Architectural mismatch discovery
- [PR #29](https://github.com/uniun-technology/dataflow/pull/29) - Interim solution Phase 1
- [PR #30](https://github.com/uniun-technology/dataflow/pull/30) - Interim solution Phase 2-4

### Code References

- [ReflectionHelper.cs](/poc/DataFlow.POC/Core/ReflectionHelper.cs) - Current edge routing
- [EdgeStrategy.cs](/poc/DataFlow.POC/Core/EdgeStrategy.cs) - Strategy implementations
- [EpochStream.cs](/poc/DataFlow.POC/Core/EpochStream.cs) - Epoch stream types
- [Architectural Mismatch Research](../architectural-mismatch-epoch-routing/README.md) - Original discovery

---

## Timeline Estimate

**Total**: 4 weeks

- **Week 1**: Core infrastructure (3-5 days)
- **Week 2**: Strategy adaptations (5-7 days)
- **Week 3**: Testing & validation (5-7 days)
- **Week 4**: Documentation (3-5 days)

**Buffer**: +1 week for unexpected issues

---

## Success Criteria

- [ ] All topology patterns work with epoch streams (broadcast, selective, competing)
- [ ] Epoch boundary detection via stream boundaries (no per-item checks)
- [ ] Performance overhead <10% (target <5%)
- [ ] Epoch vector propagation maintains causality
- [ ] Backpressure works without deadlocks
- [ ] All existing tests pass
- [ ] New tests validate all topologies
- [ ] Documentation updated and accurate
- [ ] No breaking API changes

---

## Next Steps

1. Review this handover document with stakeholders
2. Create GitHub issue with this content
3. Assign to implementation duty
4. Begin Phase 1 implementation

---

**Research Completed**: 2025-11-27  
**Ready for Implementation**: Yes  
**Estimated Start**: Upon assignment
