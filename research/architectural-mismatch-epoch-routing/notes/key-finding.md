# Key Finding: Architectural Mismatch Confirmed

## Date: 2025-11-26

### Architectural Mismatch Validated

**Test Results**: 
- `BroadcastEdge_WithEpochStreams_SharesSameContainer` - ✅ PASSED (async iterator works)
- `BroadcastEdge_WithChannelBasedEpochStreams_FailsBecauseNotReEnumerable` - ⚠️ **ARCHITECTURAL MISMATCH CONFIRMED**

### The Problem

**Edges route epoch stream CONTAINERS, not data items.**

When a broadcast edge receives an `IEpochStream<int>`:
1. The same `IEpochStream<int>` object reference is written to all downstream channels
2. All downstream consumers receive the SAME container object
3. The container's `.Items` property is an `IAsyncEnumerable<int>`
4. **IAsyncEnumerable can only be enumerated ONCE** (unless backed by re-enumerable source)

### Test Evidence

**Test 1: Async Iterator (Re-Enumerable)**
```
Source: async IAsyncEnumerable<int> CreateAsyncEnumerable(List<int> items)
Result: Both consumers receive all 5 items (total: 10)
Reason: Async iterators create NEW enumerators on each GetAsyncEnumerator() call
```

**Test 2: Channel-Based (NOT Re-Enumerable)**
```
Source: channel.Reader.ReadAllAsync()
Result: Consumer A receives 5 items, Consumer B receives 0 items (total: 5)
Reason: Channel readers can only be enumerated ONCE - first consumer exhausts the stream
```

### Why This Matters

**Channel-based streams are the REALISTIC case:**
- Most data flows use channels for backpressure and buffering
- `ChannelReader<T>.ReadAllAsync()` is NOT re-enumerable
- Real-world epoch sources will use channels, not async iterators

**The architectural mismatch breaks:**
1. **Broadcast routing** - Only first consumer gets data
2. **Selective routing** - Would route entire epoch containers, not individual items
3. **Buffer blocks** - Would buffer containers, not items

### Root Cause

From code analysis:

```csharp
// DataFlowGraph.cs - EnumerateAndRouteTypedStreamAsync
private static async Task EnumerateAndRouteTypedStreamGenericAsync<T>(...)
{
    var stream = (IAsyncEnumerable<T>)typedStream;  // T = IEpochStream<int>
    await foreach (var item in stream)              // item = IEpochStream<int> container
    {
        await router.RouteTypedItemAsync(item, ...); // Routes container!
    }
}
```

The generic parameter `T` is the block's output type. For blocks outputting `IAsyncEnumerable<IEpochStream<int>>`, `T = IEpochStream<int>`.

**Therefore**:
- Edges enumerate and route **epoch stream containers**
- NOT the data items within those containers

### Implications

1. **Broadcast edges**: 
   - Share same container reference among consumers
   - Only one consumer can enumerate the container's items
   - **Broken for channel-based epoch streams**

2. **Selective routing edges**:
   - Would route based on container properties (e.g., epoch vector)
   - Cannot route based on individual data item properties
   - **Cannot do key-based routing of data items**

3. **Buffer blocks**:
   - Would buffer epoch stream containers
   - Not clear what "buffering a container" means
   - **Semantics unclear or broken**

### Architectural Options

#### Option A: Unwrap Epochs at Edge Boundary
- Add epoch-aware routing that unwraps `IEpochStream<T>` containers
- Enumerate items from each epoch stream
- Route individual items through edges
- **Pro**: Edge routing semantics work correctly
- **Con**: Need epoch-specific routing logic

#### Option B: Change Block Outputs
- Blocks output `IAsyncEnumerable<T>` (plain data items)
- Infrastructure manages epochs separately (via `ConfigureEpochs`)
- Epochs managed at graph level, not block level
- **Pro**: Edges naturally route data items
- **Con**: Need infrastructure to coordinate epoch boundaries

#### Option C: Require Cloning/Duplication
- Broadcast edge must clone or buffer epoch stream contents
- Each downstream consumer gets independent copy
- **Pro**: Works with current architecture
- **Con**: Memory overhead, complexity, performance impact

#### Option D: Document Limitation
- Epoch streams only work with 1:1 connections
- Broadcast/selective routing not supported with epochs
- **Pro**: Simplest, no code changes
- **Con**: Major limitation, breaks use cases

### Recommendation

**Option B appears most promising**: 
- Aligns with `ConfigureEpochs` infrastructure already in codebase
- Blocks output plain data items
- Graph-level epoch coordination
- Edge routing works naturally
- Clean separation of concerns

**Next Steps**:
1. Analyze how `ConfigureEpochs` currently works
2. Design graph-level epoch coordination
3. Create implementation specification
4. Document migration path from epoch-aware blocks

### References

- Test file: `/poc/DataFlow.POC.Tests/Research/EpochRoutingArchitectureTests.cs`
- Test output demonstrating mismatch
- Code analysis in `/research/architectural-mismatch-epoch-routing/notes/exploration-notes.md`
