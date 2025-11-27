# Option 1: Epochs as Separate Graph Executions

**Date**: 2025-11-27  
**Status**: Design & Prototyping

---

## Overview

**Core Concept**: Treat each epoch as its own sub-execution with a dedicated graph instance. Blocks process plain data items (`IAsyncEnumerable<T>`), and epochs are isolated at the execution level rather than the data level.

---

## Architecture

### High-Level Model

```
┌──────────────────────────────────────────────────────┐
│            Epoch Orchestrator (Master)                │
│  - Receives epoch streams from sources                │
│  - Creates sub-execution per epoch                    │
│  - Manages concurrent epoch executions                │
│  - Coordinates lifecycle and cleanup                  │
└──────────────────────────────────────────────────────┘
                        │
        ┌───────────────┼───────────────┐
        ▼               ▼               ▼
┌─────────────┐  ┌─────────────┐  ┌─────────────┐
│ Epoch 1     │  │ Epoch 2     │  │ Epoch 3     │
│ Sub-Graph   │  │ Sub-Graph   │  │ Sub-Graph   │
│ Instance    │  │ Instance    │  │ Instance    │
└─────────────┘  └─────────────┘  └─────────────┘
     │                │                │
     │ Runs same     │                │
     │ topology      │                │
     ▼               ▼                ▼
[Block A]        [Block A]        [Block A]
     │                │                │
     ▼                ▼                ▼
[Block B]        [Block B]        [Block B]
```

### Key Components

#### 1. Epoch Orchestrator

**Responsibilities**:
- Monitor source blocks for epoch boundaries
- Create sub-execution for each new epoch
- Feed epoch data into sub-execution input
- Track concurrent sub-executions
- Cleanup completed sub-executions

**Pseudo-code**:
```csharp
public class EpochOrchestrator
{
    private readonly GraphConfiguration _graphConfig;
    private readonly IServiceProvider _rootServiceProvider;
    private readonly ConcurrentDictionary<EpochVector, SubExecution> _activeEpochs;
    
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        // Monitor sources for epoch streams
        await foreach (var epochStream in MonitorSourcesAsync(cancellationToken))
        {
            // Create new sub-execution for this epoch
            var subExecution = CreateSubExecution(epochStream);
            
            // Run in background
            _ = Task.Run(() => subExecution.RunAsync(cancellationToken));
            
            // Track for cleanup
            _activeEpochs[epochStream.EpochVector] = subExecution;
        }
    }
    
    private SubExecution CreateSubExecution(IEpochStream epochStream)
    {
        // Create epoch-scoped service provider
        var epochScope = _rootServiceProvider.CreateScope();
        
        // Instantiate graph from configuration
        var graph = GraphFactory.CreateGraph(_graphConfig, epochScope.ServiceProvider);
        
        // Inject epoch data as graph input
        return new SubExecution(graph, epochStream, epochScope);
    }
}
```

#### 2. Graph Configuration (Immutable)

**Responsibilities**:
- Define block types and wiring
- Specify edge strategies
- Configuration-only, no execution

**Pseudo-code**:
```csharp
public class GraphConfiguration
{
    public List<BlockDefinition> Blocks { get; }
    public List<EdgeDefinition> Edges { get; }
    
    public class BlockDefinition
    {
        public string Name { get; set; }
        public Type BlockType { get; set; }
        public object? Configuration { get; set; }
    }
    
    public class EdgeDefinition
    {
        public string SourceBlockName { get; set; }
        public string TargetBlockName { get; set; }
        public EdgeStrategy Strategy { get; set; }
    }
}
```

#### 3. Sub-Execution (Per-Epoch)

**Responsibilities**:
- Instantiate blocks from configuration
- Wire blocks according to edge definitions
- Inject epoch data into graph input
- Execute graph to completion
- Cleanup resources when done

**Pseudo-code**:
```csharp
public class SubExecution
{
    private readonly DataFlowGraph _graph;
    private readonly IEpochStream _epochData;
    private readonly IServiceScope _epochScope;
    
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Feed epoch data into graph input
            await _graph.ExecuteAsync(_epochData.Items, cancellationToken);
        }
        finally
        {
            // Cleanup
            _epochScope.Dispose();
        }
    }
}
```

#### 4. Block Contracts (Simplified)

**Block Signature**: `IAsyncEnumerable<T>` → `IAsyncEnumerable<T>`

**Example**:
```csharp
public class TransformBlock : IBlock<int, string>
{
    public async IAsyncEnumerable<string> ExecuteAsync(
        IAsyncEnumerable<int> input,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in input.WithCancellation(cancellationToken))
        {
            yield return $"Item: {item}";
        }
    }
}
```

**No epoch awareness needed in blocks** - they process plain items!

---

## Advantages

### 1. Simple Block Contracts

✅ Blocks work with plain `IAsyncEnumerable<T>`  
✅ No epoch stream containers  
✅ No per-item epoch checks  
✅ Easy to test and reason about  

### 2. Clean Epoch Isolation

✅ Each epoch has its own graph instance  
✅ No cross-epoch interference  
✅ Easy resource cleanup  
✅ Natural DI scope boundaries  

### 3. Natural DI Scoping

✅ Each epoch gets its own service scope  
✅ Scoped services (like DbContext) work naturally  
✅ Automatic disposal when epoch completes  

### 4. Simplified EpochActorBlock

✅ EpochActorBlock no longer needs to manage multiple actor instances  
✅ One actor instance per sub-execution = one actor per epoch  
✅ `RequestRotate()` still works for intra-epoch DI rotation  

### 5. Works with All Topologies

✅ Broadcast, selective, competing all work naturally  
✅ Edges route plain data items  
✅ No container routing problems  

---

## Challenges

### 1. Graph Re-Instantiation Overhead

**Question**: Is creating a new graph instance per epoch too expensive?

**Analysis Needed**:
- Measure time to instantiate blocks
- Measure time to wire edges
- Measure time to setup channels
- Compare with per-item epoch check overhead

**Mitigation Strategies**:
- Object pooling for blocks (if stateless)
- Pre-compile edge wiring logic
- Reuse channel factories
- Lazy initialization where possible

### 2. Concurrent Execution Management

**Question**: How to manage multiple parallel epoch executions?

**Challenges**:
- Track active sub-executions
- Prevent resource exhaustion (limit concurrent epochs)
- Handle slow epochs blocking new ones
- Cleanup completed epochs

**Design Options**:
- Semaphore to limit max concurrent epochs
- Background cleanup task
- Epoch timeout mechanism

### 3. Stateful Blocks

**Question**: What about blocks that maintain state across epochs?

**Problem**: If each epoch gets new block instances, state is lost

**Solutions**:
- **Option A**: Block state lives in DI container (scoped or singleton)
- **Option B**: Orchestrator maintains state and injects into blocks
- **Option C**: Blocks declare state persistence requirements

**Example** (Option A):
```csharp
public class StatefulBlock : IBlock<int, int>
{
    private readonly IStateStore _stateStore; // Singleton service
    
    public StatefulBlock(IStateStore stateStore)
    {
        _stateStore = stateStore;
    }
    
    public async IAsyncEnumerable<int> ExecuteAsync(...)
    {
        var state = await _stateStore.GetStateAsync();
        // Use state...
        await _stateStore.SaveStateAsync(state);
    }
}
```

### 4. API Design

**Question**: How do users configure "run this graph for each epoch"?

**Design Option 1** - Explicit Epoch Mode:
```csharp
var builder = new DataFlowBuilder();
builder.ConfigureEpochMode(mode => 
{
    mode.EnableEpochPerExecution = true;
    mode.MaxConcurrentEpochs = 3;
});

builder
    .AddSource("source", ...)
    .AddTransform("transform", ...)
    .AddProcessor("processor", ...);
```

**Design Option 2** - Implicit from Source:
```csharp
var builder = new DataFlowBuilder();
builder
    .AddEpochSource("source", ...) // Source declares it produces epochs
    .AddTransform("transform", ...) // Normal blocks
    .AddProcessor("processor", ...);

// Orchestrator detects epoch source and uses sub-execution model
```

---

## Performance Analysis

### Overhead Sources

1. **Graph Instantiation**: Creating blocks, DI resolution
2. **Edge Wiring**: Setting up channels, creating routers
3. **Cleanup**: Disposing scopes, cleaning up channels

### Baseline Comparison

| Approach | Per-Epoch Overhead | Per-Item Overhead |
|----------|-------------------|-------------------|
| Current (epoch blocks) | Low (~0ns) | High (container routing) |
| Option 1 (graph per epoch) | High (TBD - measure) | Low (plain routing) |
| Graph-level (plain blocks) | Low (~0ns) | Medium (per-item epoch check) |

### Key Questions

1. Is graph instantiation overhead amortized over many items per epoch?
2. Can we optimize instantiation with object pooling?
3. How does it compare to per-item epoch context lookup?

**Hypothesis**: For epochs with many items (common case), graph instantiation overhead is negligible compared to processing all items.

---

## Test Scenarios

### Functional Tests

1. **Single Epoch**
   - Source emits one epoch stream
   - Graph processes all items
   - Output is correct

2. **Multiple Sequential Epochs**
   - Source emits epochs 1, 2, 3
   - Each gets its own sub-execution
   - All complete successfully

3. **Concurrent Epochs**
   - Source emits epochs 1, 2, 3 rapidly
   - Multiple sub-executions run in parallel
   - All complete independently

4. **DI Scope Isolation**
   - Each epoch gets its own scope
   - Scoped services are isolated
   - Disposal happens correctly

5. **Topology Validation**
   - Broadcast: Each downstream gets all items
   - Selective: Items routed correctly
   - Competing: Load balancing works

### Performance Tests

1. **Instantiation Overhead**
   - Measure graph creation time
   - Compare with baseline approaches

2. **Throughput**
   - Process 1M items across 10 epochs
   - Measure total time
   - Compare with current approach

3. **Concurrency Scaling**
   - Run 1, 2, 4, 8 concurrent epochs
   - Measure resource usage
   - Measure completion time

4. **Memory Profiling**
   - Track memory per sub-execution
   - Check for leaks
   - Measure GC pressure

---

## Open Questions

1. **Block Reusability**: Can we reuse block instances across epochs if they're stateless?
2. **Warm-Up**: Can we pre-instantiate graph templates to reduce first-epoch latency?
3. **Lifecycle Hooks**: Do blocks need epoch start/end callbacks?
4. **Error Handling**: If one epoch fails, does it affect others?
5. **Cancellation**: How to cancel a specific epoch vs all epochs?
6. **Metrics**: How to track per-epoch metrics vs aggregate metrics?

---

## Next Steps

### Design Phase
- [ ] Finalize API design for epoch mode configuration
- [ ] Design stateful block pattern
- [ ] Design concurrent execution management
- [ ] Design error handling strategy

### Prototype Phase
- [ ] Implement EpochOrchestrator
- [ ] Implement GraphConfiguration
- [ ] Implement SubExecution
- [ ] Create test graph with broadcast/selective edges

### Validation Phase
- [ ] Run functional tests
- [ ] Run performance benchmarks
- [ ] Compare with baseline
- [ ] Document findings

---

## Success Criteria

✅ Graph instantiation overhead <5% of total execution time  
✅ All topology patterns work correctly  
✅ DI scoping is clean and predictable  
✅ Concurrent epoch management is robust  
✅ API design is intuitive  
✅ Performance is comparable to baseline  

---

## References

- [Research Plan](../research-plan.md)
- [Context Analysis](../notes/context-analysis.md)
- [Architectural Mismatch Research](../../architectural-mismatch-epoch-routing/README.md)
- [POC Graph Implementation](/poc/DataFlow.POC/Core/DataFlowGraph.cs)
