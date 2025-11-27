# ADR: Unified Epoch Model with Intelligent Edge Unwrap/Wrap

**Status**: Proposed  
**Date**: 2025-11-27  
**Decision Makers**: Research Duty (GitHub Copilot)  
**Stakeholders**: DataFlow Library Users, Implementation Team

---

## Context

DataFlow uses epochs to batch process related data items together. The current implementation has a fundamental architectural mismatch:

**Problem**: Edges route epoch stream **containers** (`IEpochStream<T>`) rather than data **items** (`T`).

**Impact**:
- Broadcast routing fails with channel-based epoch streams
- Only one consumer receives data when containers are shared
- Selective routing can't inspect item properties
- Most multi-consumer topologies are broken

**Evidence**:
```csharp
// Current edge routing code
await foreach (var item in stream.WithCancellation(cancellationToken))
{
    // For IAsyncEnumerable<IEpochStream<int>>, item is the CONTAINER
    // not the data items within
    await router.RouteTypedItemAsync(item, cancellationToken); // ❌
}
```

**Test Results**:
- Async iterator epoch streams: Work by accident (re-enumerable)
- Channel-based epoch streams: Consumer A gets all items, Consumer B gets none

**Current Workarounds**:
1. Use plain blocks + check `context.CurrentEpoch` per item (inefficient)
2. Document limitation: epoch blocks only work with 1:1 connections (unacceptable)

---

## Decision

**We will implement Option 2: Intelligent Edge Unwrap/Wrap**

Edges will detect `IEpochStream<T>` types, unwrap to enumerate individual items, route items using edge strategy logic, and re-wrap into new epoch streams for each downstream consumer.

---

## Alternatives Considered

### Option 1: Epochs as Separate Graph Executions

**Approach**: Each epoch runs as its own sub-graph instance with dedicated block instances.

**Rejected Because**:
1. ❌ **Doesn't eliminate per-item checks**: Blocks with `IAsyncEnumerable<T>` signature would still need to check `context.CurrentEpoch` per item to detect epoch boundaries, negating the whole purpose of epochs
2. ❌ **High per-epoch overhead**: Graph re-instantiation, DI resolution, channel creation adds 10-50 microseconds per epoch
3. ❌ **Complex concurrency management**: Must coordinate multiple sub-graph instances, manage resource limits, handle inter-epoch ordering
4. ❌ **Stateful blocks problematic**: Block instances are recreated per epoch, breaking state continuity
5. ❌ **Breaking API changes**: All epoch-aware blocks must be rewritten

**Score**: 2/11 criteria better than Option 2, 6/11 worse

### Option 3: Document Limitation

**Approach**: Accept that epoch blocks only work with 1:1 connections, document this limitation.

**Rejected Because**:
1. ❌ Major functional limitation affecting core use cases
2. ❌ Breaks broadcast logging, parallel processing, selective routing
3. ❌ Doesn't align with dataflow principles
4. ❌ Users would be forced to use inefficient workarounds

### Option 4: Plain Blocks with Graph-Level Coordination

**Approach**: Blocks output `IAsyncEnumerable<T>`, access epoch via `IExecutionContext.CurrentEpoch`.

**Rejected Because**:
1. ❌ Must check `CurrentEpoch` per item to detect epoch changes
2. ❌ Per-item dictionary lookup or thread-local overhead
3. ❌ Loses efficiency benefit of epoch stream boundaries
4. ❌ Makes epoch awareness implicit rather than explicit in type system

**Note**: This was attempted in PR #29 and PR #30 as interim solution.

---

## Rationale

### Why Option 2 is Superior

1. **Preserves Epoch Boundary Semantics** ✅
   - Stream boundaries = epoch boundaries (no per-item checks)
   - Type-safe epoch awareness (`IEpochStream<T>`)
   - Efficient epoch processing

2. **Low Overhead** ✅
   - One-time channel setup per epoch (~50-500ns)
   - No graph re-instantiation (0ns)
   - No per-item epoch context lookup (0ns)
   - **vs Option 1**: 10-50 microseconds per epoch
   - **vs Graph-level**: ~10-50ns per item

3. **Natural Backpressure** ✅
   - Channels provide built-in backpressure
   - Upstream waits if downstream is slow
   - Works across epoch boundaries
   - **vs Option 1**: Complex cross-sub-execution coordination needed

4. **Simple Concurrency** ✅
   - Uses existing block execution model
   - No sub-execution orchestration
   - No new concurrency tuning parameters
   - **vs Option 1**: Must manage N concurrent sub-graphs

5. **Works with Stateful Blocks** ✅
   - Block instances persist across epochs
   - State naturally maintained
   - **vs Option 1**: Requires external state storage

6. **Easy Migration** ✅
   - Internal fix, no API changes
   - Existing epoch-aware blocks work immediately
   - Existing tests pass without modification
   - **vs Option 1**: Breaking changes, rewrite all epoch blocks

7. **Right Separation of Concerns** ✅
   - Blocks: Business logic
   - Edges: Data routing + epoch coordination
   - Graph: Topology and execution
   - **Edges already manage routing and channels** - adding epoch awareness fits naturally

### Trade-offs Accepted

**Increased Edge Layer Complexity**: Edge routing logic becomes more complex.

**Justification**:
- Edge layer is the **right place** for this complexity
- Edges already handle routing and channel management
- Separation of concerns is maintained
- Complexity is **localized** rather than distributed
- ~800 LOC vs ~1500 LOC for Option 1

**Channel Backing Indirection**: Downstream epoch streams are backed by channels, not original sources.

**Justification**:
- This is **already how edges work** - they use channels
- Channels enable backpressure and decoupling
- ChannelBackedEpochStream makes this explicit
- No fundamental change to architecture

---

## Implementation Details

### Phase 1: Core Infrastructure

**Epoch Stream Detection**:
```csharp
// In edge routing
if (typeof(T).IsGenericType && 
    typeof(T).GetGenericTypeDefinition() == typeof(IEpochStream<>))
{
    // Route to epoch-aware handler
    await EnumerateAndRouteEpochStreamAsync(...);
}
else
{
    // Route items directly (existing path)
    await EnumerateAndRouteItemsAsync(...);
}
```

**ChannelBackedEpochStream**:
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

### Phase 2: Strategy Adaptations

**Broadcast Strategy**:
```csharp
// Create one channel per downstream block
foreach (var target in targets)
{
    var channel = Channel.CreateBounded<T>(bufferSize);
    downstreamEpochStreams[target] = new ChannelBackedEpochStream<T>(
        epochVector, epoch, channel);
}

// Route items to all channels
await foreach (var item in sourceEpochStream.Items)
{
    foreach (var stream in downstreamEpochStreams.Values)
    {
        await stream.GetWriter().WriteAsync(item);
    }
}
```

**Competing Strategy**:
```csharp
// Create one shared channel
var sharedChannel = Channel.CreateBounded<T>(bufferSize);

// All targets share the same channel
foreach (var target in targets)
{
    downstreamEpochStreams[target] = new ChannelBackedEpochStream<T>(
        epochVector, epoch, sharedChannel);
}
```

**Selective Strategy**:
```csharp
// Create one channel per route
foreach (var route in routes)
{
    var channel = Channel.CreateBounded<T>(bufferSize);
    routeEpochStreams[route.Name] = new ChannelBackedEpochStream<T>(
        epochVector, epoch, channel);
}

// Route items based on predicate
await foreach (var item in sourceEpochStream.Items)
{
    var routeName = routeSelector(item);
    await routeEpochStreams[routeName].GetWriter().WriteAsync(item);
}
```

---

## Consequences

### Positive

1. **Broadcast routing works correctly** ✅
   - Each downstream block receives all items
   - No container sharing bugs

2. **Selective routing works correctly** ✅
   - Can route based on item properties
   - Epoch boundaries maintained

3. **Competing consumers work correctly** ✅
   - Load balancing across consumers
   - Proper resource utilization

4. **Performance improved** ✅
   - <5% overhead expected (vs >10% for Option 1)
   - No per-item epoch checks

5. **Easy migration** ✅
   - Internal fix, users see bug fixed
   - No code changes required

6. **Maintainable architecture** ✅
   - Clear separation of concerns
   - Complexity localized to edge layer

### Negative

1. **Edge layer more complex** ⚠️
   - Unwrap/wrap logic adds ~800 LOC
   - **Mitigation**: Encapsulate in helpers, good tests

2. **Debugging indirection** ⚠️
   - Epoch streams are re-created by edges
   - **Mitigation**: Clear naming (ChannelBackedEpochStream), logging

3. **Need understanding of channel backing** ⚠️
   - Users debugging may see channel-backed streams
   - **Mitigation**: Documentation with diagrams

### Neutral

1. **Different strategies have different channel setups**
   - Broadcast: N channels, Competing: 1 channel, Selective: M channels
   - This is **already true** for non-epoch routing
   - Epoch routing follows same patterns

---

## Validation

### Success Criteria

| Criterion | Target | Validation Method |
|-----------|--------|------------------|
| Topology Support | All patterns work | Functional tests |
| Performance | <10% overhead | Benchmarks |
| Epoch Boundaries | No per-item checks | Code review |
| Backpressure | No deadlocks | Stress tests |
| Migration | No API changes | Existing tests pass |

### Test Plan

**Functional Tests**:
- Broadcast: 3 consumers each receive all 100 items from epoch
- Selective: Even/odd routing splits 100 items correctly
- Competing: 3 consumers process all 100 items exactly once
- Fan-in: Two epochs merge, downstream gets both with correct vectors

**Performance Tests**:
- Process 1M items across 10 epochs
- Measure unwrap/wrap overhead
- Compare with baseline (target: <5% slowdown)

**Integration Tests**:
- Real-world scenarios with all topologies
- Error handling and cancellation
- Concurrent epoch processing

---

## References

### Research Documents
- [Research README](./README.md) - Complete findings
- [Option 2 Design](./design/option2-edge-unwrap-wrap.md) - Detailed design
- [Analysis & Recommendation](./design/analysis-and-recommendation.md) - Comparison

### Related Issues
- [Issue #15](https://github.com/uniun-technology/dataflow/issues/15) - Original epoch blocks issue
- [Issue #26](https://github.com/uniun-technology/dataflow/issues/26) - Architectural mismatch discovery
- [PR #29](https://github.com/uniun-technology/dataflow/pull/29) - Interim solution Phase 1
- [PR #30](https://github.com/uniun-technology/dataflow/pull/30) - Interim solution Phase 2-4 (archived)

### Code References
- [ReflectionHelper.cs](/poc/DataFlow.POC/Core/ReflectionHelper.cs) - Current edge routing
- [EdgeStrategy.cs](/poc/DataFlow.POC/Core/EdgeStrategy.cs) - Strategy implementations
- [EpochStream.cs](/poc/DataFlow.POC/Core/EpochStream.cs) - Epoch stream types

---

## Decision

**RECOMMENDED**: Proceed with implementation of Option 2 (Intelligent Edge Unwrap/Wrap)

**Rationale**: Option 2 provides the best balance of:
- Correctness (fixes all topology patterns)
- Performance (<5% overhead)
- Maintainability (localized complexity)
- Migration ease (no API changes)

**Timeline**: 4 weeks (4 phases)

**Risk**: Low - design is well-validated, complexity is manageable, clear test strategy

---

**Decision Date**: 2025-11-27  
**Status**: Awaiting Implementation
