# Implementation Handover: Fix Epoch Stream Routing Architecture

**Type**: Implementation  
**Priority**: Critical  
**Research Reference**: Research Architectural Mismatch (ref: PR #24, Issue #15)
**Research Documentation**: `/research/architectural-mismatch-epoch-routing/`

---

## Objective

Fix the architectural mismatch where edges route epoch stream CONTAINERS instead of data ITEMS, breaking broadcast and selective routing semantics for realistic channel-based streams.

---

## Problem Summary

**Current Behavior (BROKEN)**:
- Blocks output `IAsyncEnumerable<IEpochStream<T>>`
- Edges enumerate and route `IEpochStream<T>` containers
- Broadcast edges share the SAME container with all downstream blocks
- Only ONE consumer can enumerate the container's items (channels are not re-enumerable)
- **Result**: Broadcast routing broken for channel-based epoch streams

**Validated By**:
- Test: `BroadcastEdge_WithChannelBasedEpochStreams_FailsBecauseNotReEnumerable`
- Consumer A: 5 items, Consumer B: 0 items
- Channel readers can only be enumerated once

---

## Recommended Solution (From Research)

**Option B: Change block outputs to plain items, manage epochs at graph level**

### Architecture

**Blocks**:
- Output: `IAsyncEnumerable<T>` (plain data items)
- No epoch types in block signatures
- Focus on business logic (data transformation)

**Graph Infrastructure**:
- Manages epoch boundaries via coordination signals
- Creates DI scopes per epoch
- Blocks access epoch context via `IExecutionContext`

**Edges**:
- Route plain data items (T)
- No special epoch-aware logic needed
- Broadcast/selective routing work naturally

### Benefits

1. ✅ **Fixes Architectural Mismatch**
   - Broadcast routing duplicates data items correctly
   - Selective routing operates on item properties
   - Buffer blocks have clear semantics

2. ✅ **Clean Architecture**
   - Blocks: Business logic
   - Edges: Data routing
   - Graph: Epoch coordination

3. ✅ **Aligns with Existing Infrastructure**
   - `ConfigureEpochs` already follows this pattern
   - Proven approach in codebase

---

## Implementation Approach

### Phase 1: Design Graph-Level Epoch Coordinator

**Components**:
1. **Epoch Coordinator Service**
   - Tracks epoch boundaries
   - Manages DI scope lifecycle
   - Provides epoch context to blocks

2. **Epoch Boundary Signaling**
   - Detect when epochs change
   - Coordinate across multiple sources
   - Signal to downstream blocks

3. **DI Scope Management**
   - Create scope per epoch
   - Dispose scope after epoch complete
   - Provide scoped services to blocks

**API Design** (Example/Pseudocode):
```csharp
// Graph configuration
graph.ConfigureEpochs(config =>
{
    config.AddSource("source1", triggerPolicy: OnNewData);
    config.AddSource("source2", triggerPolicy: OnNewData);
    config.CoordinateEpochs(); // Infrastructure manages boundaries
});

// Block implementation (plain data items) - EXAMPLE
public class MySourceBlock : IBlock<object, int>
{
    private readonly int[] _data = { 1, 2, 3, 4, 5 }; // Example data
    
    public async IAsyncEnumerable<int> ExecuteAsync(
        IAsyncEnumerable<object> input,
        IExecutionContext context)
    {
        // Access epoch context if needed
        var epochContext = context.GetEpochContext(); // Optional
        
        // Output plain data items
        foreach (var item in _data)
        {
            yield return item; // Plain int, not IEpochStream<int>
        }
    }
}
```

### Phase 2: Implement Core Infrastructure

**Tasks**:
1. Create `IEpochCoordinator` interface
2. Implement `EpochCoordinator` service  
3. Add epoch boundary detection logic
4. Implement DI scope lifecycle management
5. Integrate with graph execution pipeline

**Key Classes**:
- `IEpochCoordinator` - Service interface
- `EpochCoordinator` - Implementation
- `EpochBoundaryDetector` - Detects epoch changes
- `EpochScopeManager` - Manages DI scopes

### Phase 3: Migration Path

**Refactor Existing Epoch-Aware Blocks**:

Before (epoch-aware blocks):
```csharp
public class OldEpochSourceBlock : IBlock<object, IEpochStream<int>>
{
    public IAsyncEnumerable<IEpochStream<int>> ExecuteAsync(...)
    {
        var epochVector = EpochVector.FromSingleSource("source", 1);
        yield return new EpochStream<int>(epochVector, items);
    }
}
```

After (plain blocks with graph coordination) - EXAMPLE:
```csharp
public class NewSourceBlock : IBlock<object, int>
{
    private readonly int[] _items = { 1, 2, 3, 4, 5 }; // Example data
    
    public async IAsyncEnumerable<int> ExecuteAsync(...)
    {
        // Just output items - graph manages epochs
        foreach (var item in _items)
        {
            yield return item;
        }
    }
}

// Graph configuration
graph.ConfigureEpochs(config =>
{
    config.AddSource("source", triggerPolicy: OnNewData);
});
```

**Migration Utilities**:
- Create helpers to convert epoch-aware blocks
- Provide compatibility layer (if needed during transition)
- Update examples and documentation

### Phase 4: Testing and Validation

**Test Scenarios**:
1. ✅ Broadcast routing works with epoch coordination
2. ✅ Selective routing operates on data item properties
3. ✅ Buffer blocks buffer data items correctly
4. ✅ DI scopes created and disposed per epoch
5. ✅ Multi-source epoch coordination works
6. ✅ Performance comparable to current approach

**Validation Tests**:
- Update existing epoch tests to use new approach
- Add integration tests with realistic scenarios
- Performance benchmarks (compare with current)
- Memory leak detection (scope disposal)

---

## Success Criteria

### Functional
- [ ] Broadcast edges duplicate data items to all consumers
- [ ] Selective routing routes items by property values
- [ ] Buffer blocks buffer individual data items
- [ ] DI scopes work correctly per epoch
- [ ] Multi-source epoch coordination works

### Non-Functional
- [ ] Performance within 10% of current approach
- [ ] No memory leaks from scope management
- [ ] All existing epoch tests pass (after migration)
- [ ] Documentation updated and complete

### Code Quality
- [ ] Code review completed with no major issues
- [ ] Security scan (CodeQL) passes
- [ ] All tests pass
- [ ] Examples updated

---

## Test Scenarios (Critical)

### Test 1: Broadcast Routing with Plain Items and Epoch Coordination

**Example test specification** (for implementation team):

```csharp
[Fact]  // Will be added by implementation team
public async Task BroadcastEdge_WithEpochCoordination_DuplicatesItemsCorrectly()
{
    // Arrange
    var sourceData = new List<int> { 1, 2, 3, 4, 5 };
    var graph = new DataFlowGraph("test", logger);
    
    // Source outputs plain int items
    var source = new PlainSourceBlock(sourceData);
    var consumerA = new ConsumerBlock("A");
    var consumerB = new ConsumerBlock("B");
    
    graph.AddBlock(source);
    graph.AddBlock(consumerA);
    graph.AddBlock(consumerB);
    
    // Configure graph-level epoch coordination
    graph.ConfigureEpochs(config =>
    {
        config.AddSource("source", triggerPolicy: OnNewData);
    });
    
    // Create broadcast edge
    var edge = new Edge(source, new[] { consumerA, consumerB },
        new BroadcastEdgeStrategy());
    graph.AddEdge(edge);
    
    // Act
    await graph.ExecuteAsync(context);
    
    // Assert
    consumerA.ReceivedItems.ShouldBe(new[] { 1, 2, 3, 4, 5 });
    consumerB.ReceivedItems.ShouldBe(new[] { 1, 2, 3, 4, 5 });
    // Both consumers receive all items ✅
}
```

### Test 2: Selective Routing by Item Properties

**Example test specification** (for implementation team):

```csharp
[Fact]  // Will be added by implementation team
public async Task SelectiveRouting_WithEpochCoordination_RoutesItemsByProperty()
{
    // Arrange
    var sourceData = new List<int> { 1, 2, 3, 4, 5, 6 };
    var graph = new DataFlowGraph("test", logger);
    
    var source = new PlainSourceBlock(sourceData);
    var evenConsumer = new ConsumerBlock("Even");
    var oddConsumer = new ConsumerBlock("Odd");
    
    // Selective routing based on item value (even vs odd)
    var edge = new Edge(source, 
        new[] { evenConsumer, oddConsumer },
        new SelectiveEdgeStrategy<int>(
            item => item % 2 == 0 ? 0 : 1));
    
    // Act
    await graph.ExecuteAsync(context);
    
    // Assert
    evenConsumer.ReceivedItems.ShouldBe(new[] { 2, 4, 6 });
    oddConsumer.ReceivedItems.ShouldBe(new[] { 1, 3, 5 });
    // Routing operates on ITEM properties ✅
}
```

### Test 3: DI Scope Per Epoch

**Example test specification** (for implementation team):

```csharp
[Fact]  // Will be added by implementation team
public async Task EpochCoordination_CreatesScopePerEpoch()
{
    // Arrange
    var scopeIds = new List<Guid>();
    
    public class ScopedService { public Guid Id { get; } = Guid.NewGuid(); }
    
    services.AddScoped<ScopedService>();
    
    // Act
    // Configure 3 epochs
    graph.ConfigureEpochs(config =>
    {
        config.AddSource("source", triggerPolicy: OnBatchComplete);
    });
    
    // Source produces 3 batches (3 epochs)
    await graph.ExecuteAsync(context);
    
    // Assert  
    scopeIds.Distinct().Count().ShouldBe(3);
    // Each epoch gets its own DI scope ✅
}
```

---

## Performance Requirements

- **Latency**: Within 10% of current epoch-aware blocks
- **Throughput**: No degradation for plain (non-epoch) flows
- **Memory**: No memory leaks from scope management
- **Benchmarks**: Validate with realistic workloads (10K+ items/epoch)

---

## Migration Strategy

### Stage 1: Add Infrastructure (Non-Breaking)
- Implement `EpochCoordinator` and `ConfigureEpochs` API
- Existing epoch-aware blocks continue to work
- New blocks can use graph-level coordination

### Stage 2: Migrate Examples (Demonstration)
- Update examples to use new approach
- Show migration path
- Document benefits

### Stage 3: Deprecate Epoch-Aware Blocks (Optional)
- Mark old pattern as deprecated
- Provide migration guide
- Eventually remove `IEpochStream<T>` from block signatures

---

## Design References

- **Research Documentation**: `/research/architectural-mismatch-epoch-routing/README.md`
- **Test Evidence**: `/poc/DataFlow.POC.Tests/Research/EpochRoutingArchitectureTests.cs`
- **Existing Pattern**: `/poc/DataFlow.POC/Core/SingleEpochExtensions.cs`
- **Topology Guides**: `/poc/docs/guides/topology-*.md`

---

## Implementation Checklist

### Phase 1: Design
- [ ] Design `IEpochCoordinator` interface
- [ ] Design epoch boundary signaling mechanism
- [ ] Design DI scope management
- [ ] Review design with stakeholders

### Phase 2: Core Implementation
- [ ] Implement `EpochCoordinator` service
- [ ] Implement epoch boundary detection
- [ ] Implement DI scope lifecycle management
- [ ] Integrate with graph execution

### Phase 3: Testing
- [ ] Create unit tests for coordinator
- [ ] Create integration tests (broadcast, selective routing)
- [ ] Create performance benchmarks
- [ ] Create memory leak detection tests

### Phase 4: Migration
- [ ] Create migration guide
- [ ] Update examples to use new approach
- [ ] Provide compatibility utilities (if needed)
- [ ] Update documentation

### Phase 5: Validation
- [ ] All tests pass
- [ ] Performance requirements met
- [ ] Code review completed
- [ ] Security scan passes
- [ ] Documentation complete

---

## Questions for Stakeholders

1. **Migration Timeline**: Should we maintain compatibility with epoch-aware blocks, or can we break the API?
2. **Scope Management**: Should scopes be automatically disposed, or should blocks opt-in?
3. **Multi-Source Coordination**: What policy for coordinating epochs across multiple sources? (AND/OR logic)
4. **Performance Targets**: What are acceptable performance bounds for epoch coordination overhead?

---

## Estimated Effort

- **Phase 1 (Design)**: 2-3 days
- **Phase 2 (Implementation)**: 5-7 days
- **Phase 3 (Testing)**: 3-4 days
- **Phase 4 (Migration)**: 2-3 days
- **Phase 5 (Validation)**: 1-2 days

**Total**: 13-19 days (approximately 3-4 weeks)

---

## Dependencies

- **Blocked By**: None (research complete)
- **Blocks**: Issue #15 (epoch blocks) - may be closed/revised based on this work
- **Related**: PR #24, Issue #20 - may need revision

---

## Risk Assessment

**High Risk**:
- DI scope management complexity (memory leaks, disposal)
- Multi-source epoch coordination edge cases
- Performance overhead of graph-level coordination

**Medium Risk**:
- Migration complexity for existing epoch-aware blocks
- Breaking API changes if deprecating old pattern

**Low Risk**:
- Edge routing changes (minimal - just route plain items)
- Testing infrastructure (existing patterns available)

---

## References

- **Research**: `/research/architectural-mismatch-epoch-routing/`
- **Test Evidence**: Test file demonstrating the mismatch
- **Architectural Options**: Evaluated in research README
- **ConfigureEpochs Pattern**: Existing implementation to build upon

---

## For Implementation Team

**Start Here**:
1. Read research documentation: `/research/architectural-mismatch-epoch-routing/README.md`
2. Review test evidence: `EpochRoutingArchitectureTests.cs`
3. Understand current `ConfigureEpochs` pattern
4. Design `IEpochCoordinator` interface
5. Follow implementation checklist above

**Questions?**: Reference research documentation or create discussion issue.

---

**Status**: Ready for implementation  
**Research Confidence**: High - architectural mismatch confirmed with test evidence  
**Recommendation Confidence**: High - Option B aligns with existing patterns and provides cleanest solution
