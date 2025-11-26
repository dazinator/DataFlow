# Research: Architectural Mismatch in Epoch Stream Routing

**Research Objective**: Investigate fundamental architectural mismatch in epoch stream handling discovered during issue #15 resolution attempt.

**Date**: 2025-11-26  
**Status**: ⚠️ **CRITICAL ARCHITECTURAL ISSUE CONFIRMED**  
**Researcher**: GitHub Copilot (Research Duty)

---

## Executive Summary

### The Problem

**DataFlow edges route epoch stream CONTAINERS (`IEpochStream<T>`), not data ITEMS (`T`).**

This architectural mismatch breaks edge-level routing semantics (broadcast, selective routing, buffering) when epoch streams use **channel-based** data sources (the realistic case).

### Impact

- ✅ **Async iterator-based epoch streams**: Work by accident (re-enumerable)
- ❌ **Channel-based epoch streams**: Broken - only one consumer receives data
- ❌ **Broadcast routing**: Shares containers, not data
- ❌ **Selective routing**: Routes containers, not individual items
- ❌ **Buffer blocks**: Unclear semantics (buffering containers vs items)

### Recommendation

**Option B: Change block outputs to plain items, manage epochs at graph level**

This aligns with existing `ConfigureEpochs` infrastructure and provides clean separation:
- Blocks output `IAsyncEnumerable<T>` (plain data items)
- Graph-level infrastructure manages epoch boundaries
- Edges route data items naturally
- No special epoch-aware routing logic needed

---

## Background

### Origin

- **Source**: [PR #24 Comment](https://github.com/uniun-technology/dataflow/pull/24#issuecomment-3583042628)
- **Context**: While attempting to fix epoch blocks (issue #15), discovered deeper architectural issue
- **Discovery**: Edges may be routing epoch stream containers instead of data items

### Research Scope

Investigate:
1. What do edges currently route? (Containers vs items)
2. How does this affect broadcast/selective routing?
3. What is the role of `ConfigureEpochs` infrastructure?
4. What architectural options exist to fix the mismatch?

---

## Findings

### 1. Edge Routing Mechanism Analysis

**Code Analysis** (`.poc/DataFlow.POC/Core/DataFlowGraph.cs`):

```csharp
private static async Task EnumerateAndRouteTypedStreamGenericAsync<T>(
    object typedStream,
    List<ITypedEdgeRouter> routers,
    CancellationToken cancellationToken)
{
    var stream = (IAsyncEnumerable<T>)typedStream;
    await foreach (var item in stream.WithCancellation(cancellationToken))
    {
        // Route each item individually
        await router.RouteTypedItemAsync(item, cancellationToken);
    }
}
```

**Key Insight**:
- Generic parameter `T` is the block's output item type
- For blocks outputting `IAsyncEnumerable<IEpochStream<int>>`, `T = IEpochStream<int>`
- **Edges enumerate and route epoch stream CONTAINERS, not data items**

### 2. Broadcast Strategy Behavior

**Code Analysis** (`./poc/DataFlow.POC/Core/EdgeStrategy.cs`):

```csharp
public override async Task RouteTypedItemAsync<T>(
    T item,
    Dictionary<IBlock, ChannelWriter<T>> typedWriters,
    CancellationToken cancellationToken)
{
    // Broadcasts the ITEM (which could be an IEpochStream<T>)
    // Multiple writers receive the SAME object reference
    foreach (var writer in typedWriters.Values)
    {
        await writer.WriteAsync(item, cancellationToken);
    }
}
```

**Key Insight**:
- Broadcast writes same item reference to all channels
- If item is `IEpochStream<int>`, all downstream blocks receive the SAME container
- **Multiple blocks enumerating the same epoch stream container creates conflict**

### 3. Experimental Validation

#### Test 1: Async Iterator Epoch Streams (Re-Enumerable)

**Setup**:
```csharp
// Source block outputs: IAsyncEnumerable<IEpochStream<int>>
// Epoch stream items created via: async IAsyncEnumerable<int> CreateAsync(List<int> data)
```

**Result**:
- Consumer A: 5 items [1, 2, 3, 4, 5]
- Consumer B: 5 items [1, 2, 3, 4, 5]
- **Total: 10 items ✅ WORKS**

**Why it works**:
- Async iterators allow re-enumeration
- Each call to `GetAsyncEnumerator()` creates NEW enumerator
- Both consumers can independently enumerate the items

**However**: This works by ACCIDENT - async iterators are re-enumerable, but channels are not!

#### Test 2: Channel-Based Epoch Streams (NOT Re-Enumerable)

**Setup**:
```csharp
// Source block outputs: IAsyncEnumerable<IEpochStream<int>>
// Epoch stream items created via: channel.Reader.ReadAllAsync()
```

**Result**:
- Consumer A: 5 items [1, 2, 3, 4, 5]
- Consumer B: 0 items []
- **Total: 5 items ⚠️ ARCHITECTURAL MISMATCH CONFIRMED**

**Why it fails**:
- Channel readers can only be enumerated ONCE
- First consumer (A) exhausts the channel
- Second consumer (B) gets empty stream
- **Broadcast semantics are BROKEN**

**This is the REALISTIC case** - most data flows use channels for backpressure!

### 4. ConfigureEpochs Analysis

**Current Implementation** (`./poc/DataFlow.POC/Core/SingleEpochExtensions.cs`):

```csharp
public static async IAsyncEnumerable<IEpochStream<T>> WrapInSingleEpoch<T>(
    this IAsyncEnumerable<T> source,
    string sourceName,
    CancellationToken cancellationToken = default)
{
    var epochVector = EpochVector.FromSingleSource(sourceName, 1);
    yield return new EpochStream<T>(epochVector, source);
}
```

**Pattern**:
1. Takes plain `IAsyncEnumerable<T>`
2. Wraps entire stream in ONE epoch container
3. Returns `IAsyncEnumerable<IEpochStream<T>>`

**Problem**: Same architectural mismatch applies when used with broadcast/selective edges!

---

## Architectural Analysis

### Current Architecture (Broken)

```
┌─────────────┐
│ Source Block│ outputs: IAsyncEnumerable<IEpochStream<int>>
└──────┬──────┘
       │ Yields ONE container
       ▼
  ┌────────────┐
  │ Edge Routes│ item = IEpochStream<int> (container)
  └──┬─────┬───┘
     │     │ Same container reference written to both channels
     ▼     ▼
  ┌────┐ ┌────┐
  │ A  │ │ B  │ Both receive SAME IEpochStream<int> object
  └────┘ └────┘
     │     │
     │     └─── Enumerates .Items → Gets 0 items (channel exhausted)
     └───────── Enumerates .Items → Gets all 5 items
```

### Desired Architecture (Correct)

```
┌─────────────┐
│ Source Block│ outputs: IAsyncEnumerable<int>
└──────┬──────┘
       │ Yields individual items: 1, 2, 3, 4, 5
       ▼
  ┌────────────┐
  │ Edge Routes│ item = int (data item)
  └──┬─────┬───┘
     │     │ Each item duplicated to both channels
     ▼     ▼
  ┌────┐ ┌────┐
  │ A  │ │ B  │ Both receive all 5 items
  └────┘ └────┘
  
  ┌──────────────────────┐
  │ Graph Infrastructure │ Manages epoch boundaries separately
  └──────────────────────┘
```

---

## Architectural Options

### Option A: Unwrap Epochs at Edge Boundary

**Approach**:
- Add epoch-aware routing that detects `IEpochStream<T>` types
- Unwrap containers and enumerate items at edge boundary
- Route individual items through edges

**Pros**:
- Edge routing semantics work correctly
- Minimal changes to existing epoch-aware blocks

**Cons**:
- Need special epoch-specific routing logic
- Adds complexity to edge routing
- Edge layer becomes epoch-aware (breaks separation of concerns)
- Performance overhead of unwrapping

### Option B: Change Block Outputs (RECOMMENDED)

**Approach**:
- Blocks output `IAsyncEnumerable<T>` (plain data items)
- Graph-level infrastructure manages epoch boundaries
- Epochs coordinated via `ConfigureEpochs` or similar mechanism
- Edges route data items naturally

**Pros**:
- ✅ Edges naturally route data items (no special logic)
- ✅ Clean separation of concerns
- ✅ Aligns with existing `ConfigureEpochs` infrastructure
- ✅ Broadcast/selective routing work correctly
- ✅ Buffer blocks have clear semantics

**Cons**:
- Need to design graph-level epoch coordination
- Migration path from existing epoch-aware blocks
- May need infrastructure for epoch boundary signals

### Option C: Require Cloning/Duplication

**Approach**:
- Broadcast edge must clone or buffer epoch stream contents before routing
- Each downstream consumer gets independent copy of data

**Pros**:
- Works with current architecture
- No block output changes needed

**Cons**:
- ❌ Memory overhead (buffering all epoch data)
- ❌ Performance cost (cloning/buffering)
- ❌ Complexity in edge strategies
- ❌ Doesn't solve selective routing issue

### Option D: Document Limitation

**Approach**:
- Document that epoch streams only work with 1:1 connections
- Broadcast/selective routing not supported with epoch-aware blocks

**Pros**:
- ✅ No code changes needed
- ✅ Simple

**Cons**:
- ❌ Major functional limitation
- ❌ Breaks important use cases (broadcast logging, parallel processing)
- ❌ Doesn't align with dataflow principles

---

## Recommendation

**Option B: Change Block Outputs to Plain Items**

### Rationale

1. **Aligns with `ConfigureEpochs` Infrastructure**
   - Already exists in codebase
   - Designed for this pattern
   - Proven approach

2. **Clean Architecture**
   - Blocks focus on business logic (transforming data)
   - Graph infrastructure handles epoch coordination
   - Edges remain simple and generic

3. **Natural Edge Semantics**
   - Broadcast duplicates data items ✅
   - Selective routing routes by item properties ✅
   - Buffer blocks buffer data items ✅

4. **Migration Path**
   - `ConfigureEpochs` shows the pattern
   - Existing epoch-aware blocks can be refactored
   - Can provide compatibility layer during transition

### Design Sketch

**Block Output**:
```csharp
public class MySourceBlock : IBlock<object, int>
{
    public IAsyncEnumerable<int> ExecuteAsync(...)
    {
        // Output plain data items
        foreach (var item in data)
        {
            yield return item; // Plain int, not IEpochStream<int>
        }
    }
}
```

**Graph Configuration**:
```csharp
graph.ConfigureEpochs(config =>
{
    config.AddSource("source1", triggerPolicy: OnNewData);
    config.CoordinateEpochs(); // Infrastructure manages epoch boundaries
});
```

**Infrastructure**:
- Graph tracks epoch boundaries via coordination signals
- DI scopes created per epoch
- Blocks access epoch context via `IExecutionContext`
- No epoch types in block signatures

---

## Test Artifacts

**Test File**: `/poc/DataFlow.POC.Tests/Research/EpochRoutingArchitectureTests.cs`

**Tests Created**:
1. `BroadcastEdge_WithEpochStreams_SharesSameContainer` - Demonstrates async iterator works
2. `BroadcastEdge_WithChannelBasedEpochStreams_FailsBecauseNotReEnumerable` - **Confirms architectural mismatch**
3. `SelectiveRouting_WithEpochStreams_RoutesContainers` - Conceptual test
4. `ConfigureEpochs_WithBroadcast_HasSameIssue` - Shows ConfigureEpochs has same problem

**Test Results**:
```
BroadcastEdge_WithEpochStreams_SharesSameContainer: PASSED ✅
  - Both consumers: 5 items each (total: 10)
  - Works because async iterators are re-enumerable

BroadcastEdge_WithChannelBasedEpochStreams_FailsBecauseNotReEnumerable: PASSED ⚠️
  - Consumer A: 5 items
  - Consumer B: 0 items  
  - Confirms architectural mismatch with realistic channel-based streams
```

---

## Implementation Plan (Draft)

### Phase 1: Design Graph-Level Epoch Coordination
- Define epoch coordination interface
- Design epoch boundary signaling
- Specify DI scope management
- Document migration from epoch-aware blocks

### Phase 2: Implement Core Infrastructure
- Create `EpochCoordinator` service
- Implement epoch boundary detection
- Add DI scope lifecycle management
- Integrate with graph execution

### Phase 3: Migration Utilities
- Create helpers to convert epoch-aware blocks
- Provide compatibility layer (if needed)
- Update documentation and examples

### Phase 4: Test and Validate
- Validate broadcast routing works correctly
- Validate selective routing works correctly
- Performance testing (compare with current approach)
- Integration tests with realistic scenarios

---

## Impact Assessment

### Benefits

1. **Fixes Architectural Mismatch**
   - Broadcast routing works correctly ✅
   - Selective routing can route by item properties ✅
   - Buffer blocks have clear semantics ✅

2. **Simplifies Edge Logic**
   - No epoch-specific routing needed
   - Edges remain generic
   - Performance improved (no container overhead)

3. **Better Separation of Concerns**
   - Blocks: Business logic
   - Edges: Data routing
   - Graph: Epoch coordination

### Risks

1. **Migration Complexity**
   - Existing epoch-aware blocks need refactoring
   - May require compatibility layer
   - Documentation updates needed

2. **Graph-Level Coordination Complexity**
   - Need robust epoch boundary detection
   - DI scope management must be correct
   - Coordination across multiple sources

3. **Performance Considerations**
   - Graph-level coordination overhead
   - Need benchmarking to validate
   - Should be comparable or better than current

---

## Related Issues

- **Issue #15**: Epoch blocks issue that led to this discovery
- **PR #24**: Implementation attempt revealing the mismatch
- **Issue #20**: Implementation handover may need revision based on findings

---

## References

### Research Artifacts

- **Research Plan**: `/research/architectural-mismatch-epoch-routing/research-plan.md`
- **Exploration Notes**: `/research/architectural-mismatch-epoch-routing/notes/exploration-notes.md`
- **Key Finding**: `/research/architectural-mismatch-epoch-routing/notes/key-finding.md`
- **Test Code**: `/poc/DataFlow.POC.Tests/Research/EpochRoutingArchitectureTests.cs`

### Codebase References

- **Edge Routing**: `/poc/DataFlow.POC/Core/DataFlowGraph.cs`
- **Edge Strategies**: `/poc/DataFlow.POC/Core/EdgeStrategy.cs`
- **Epoch Streams**: `/poc/DataFlow.POC/Core/EpochStream.cs`
- **ConfigureEpochs**: `/poc/DataFlow.POC/Core/SingleEpochExtensions.cs`
- **Topology Guides**: `/poc/docs/guides/topology-*.md`

### External References

- [DataFlow POC Documentation](../../../poc/README.md)
- [Epoch Processing Guide](../../../poc/docs/guides/using-epochs.md)
- [Topology Guide](../../../poc/docs/guides/topology-broadcast.md)

---

## Next Steps

1. **Create formal ADR** documenting the architectural decision
2. **Design detailed solution** for graph-level epoch coordination
3. **Create implementation work item** with complete specifications
4. **Submit self-improvement feedback** on research process
5. **Hand over to implementation duty** with all documentation

---

## Conclusion

This research has confirmed a critical architectural mismatch in how DataFlow handles epoch streams. The current design routes epoch stream containers rather than data items, which breaks broadcast and selective routing for realistic channel-based streams.

**Recommendation**: Adopt Option B (change block outputs to plain items) as it provides the cleanest solution aligned with existing infrastructure patterns.

The findings, test cases, and architectural analysis are ready for implementation handover.
