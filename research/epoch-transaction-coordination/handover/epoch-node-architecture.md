# Epoch Node Architecture - Design Validation & Implementation Handover

**Status**: Design Validation  
**Date**: 2025-11-15  
**Author**: Copilot (Architecture Design)  
**Reviewer**: Implementation Team

---

## Executive Summary

This document validates the architectural design for **EpochSourceNode** and **EpochProcessorNode** - two specialized graph-level nodes that integrate epoch lifecycle management into the DataFlow execution model. The design follows the established BufferNode pattern for specialized node types with custom edge connections.

**Key Design Goals:**
1. Integrate epoch lifecycle into graph execution model (epochs become first-class graph citizens)
2. Enable configurable concurrency for epoch processing (serial vs parallel)
3. Provide hooks for transaction lifecycle (begin, commit, checkpoint)
4. Maintain performance at high data volumes

---

## 1. Architecture Overview

### 1.1 Node Types

**EpochSourceNode**
- **Purpose**: Creates and publishes epochs to an internal epoch stream
- **Configuration**: Epoch size/policy, source coordination
- **Responsibility**: Manages epoch creation and coordination across multiple sources
- **Execution**: Runs as a graph node, completes when all sources signal completion

**EpochProcessorNode**
- **Purpose**: Consumes epochs from the stream and drains their operation queues
- **Configuration**: Lifecycle hooks, error handling policies
- **Responsibility**: Executes serialized operations, invokes hooks, handles errors
- **Execution**: Runs as a graph node, one or more instances for concurrency control

### 1.2 Connection Pattern (BufferNode-style)

```csharp
// Configuration API
var dataFlow = new DataFlowBuilder()
    .ConfigureEpochs(config =>
    {
        config.SetPolicy(EpochPolicy.ByCount(100)); // Epoch every 100 items
        config.AddProcessor("processor1"); // Serial processing
        // config.AddProcessor("processor2"); // Add for parallel processing
        
        config.OnBeginEpoch(async (epoch, ct) =>
        {
            await epoch.QueueSerializedOperationAsync<DbContext>(async db =>
            {
                await db.Database.BeginTransactionAsync(ct);
            }, ct);
        });
        
        config.OnCommitEpoch(async (epoch, ct) =>
        {
            await epoch.QueueSerializedOperationAsync<DbContext>(async db =>
            {
                await db.SaveChangesAsync(ct);
                await db.Database.CommitTransactionAsync(ct);
            }, ct);
        });
    })
    .AddProducer<Order>("orders", ...)
    .AddTransform<Order, ValidatedOrder>("validator", ...)
    .ReceiveFrom("orders")
    .Build();
```

### 1.3 Graph Topology

```
┌──────────────┐
│ Source Block │──┐
└──────────────┘  │
                  ├──> ┌─────────────────┐     ┌───────────────────┐
┌──────────────┐  │    │ EpochSourceNode │────>│ EpochProcessorNode│
│ Source Block │──┘    │   (Coordinator) │     │   (Worker 1)      │
└──────────────┘       └─────────────────┘  ┌─>└───────────────────┘
                                │            │
                                └────────────┤  ┌───────────────────┐
                                             └─>│ EpochProcessorNode│
                                                │   (Worker 2)      │
                                                └───────────────────┘
```

---

## 2. Performance Analysis

### 2.1 Channel vs Alternative Queueing Primitives

**Current: `System.Threading.Channels.Channel<T>`**

✅ **Pros:**
- Highly optimized for producer/consumer scenarios
- Built-in async/await support
- Backpressure mechanisms
- Widely used in modern .NET (ASP.NET Core, etc.)
- Zero-allocation in many scenarios (value types)
- Excellent throughput (millions of items/sec)

❌ **Cons:**
- Cannot be reused after completion (must create new instance)
- Unbounded channels can grow indefinitely if consumer is slow

**Alternative 1: `ConcurrentQueue<T>` + `SemaphoreSlim`**

❌ **Cons:**
- Manual coordination required
- No built-in async/await support
- More complex error handling
- Higher allocation overhead for signaling

**Alternative 2: `System.Threading.Tasks.Dataflow.BufferBlock<T>`**

❌ **Cons:**
- Heavier weight than Channel
- Less flexible configuration
- Not as widely adopted in modern code

**Recommendation: ✅ Stick with `Channel<T>`**

Channels are the idiomatic choice for this architecture. The non-reusability is acceptable since:
1. Epochs are relatively short-lived
2. Channel creation overhead is minimal (~200-500 bytes)
3. GC can handle the allocation rate efficiently

### 2.2 Object Pooling Analysis

**Question:** Should we pool Epoch objects or their operation channels?

**Epoch Objects:**
❌ **Not recommended**
- Epochs contain DI scopes that must be disposed
- Pooling adds complexity without clear benefit
- Epoch lifecycle is already managed efficiently

**Operation Channels:**
❌ **Not feasible**
- Channels cannot be reused after completion
- Attempting to pool would require complex reset logic
- Better to accept GC overhead

**SerializedServiceExecutor:**
❌ **Not recommended**
- Tied to service lifetime (epoch scope)
- Minimal allocation overhead
- Disposal logic requires careful handling

**OperationRequest objects:**
⚠️ **Possible but not priority**
- Could use `ArrayPool<OperationRequest>` or `ObjectPool<OperationRequest>`
- Benefit: Reduced GC pressure for high-volume scenarios
- Cost: Added complexity
- **Recommendation:** Defer until profiling shows it's needed

### 2.3 High-Volume Performance Considerations

**Scenario: 1 million items/sec, epochs of 1000 items**
- ~1000 epochs/sec created
- ~1000 channels/sec created and disposed
- Allocation: ~500KB/sec for channels (negligible)

**Scenario: 10 million items/sec, epochs of 100 items**
- ~100k epochs/sec created
- ~100k channels/sec created and disposed
- Allocation: ~50MB/sec for channels (manageable with Gen0 GC)

**Mitigation Strategies:**
1. **Larger epochs**: Reduce epoch frequency (1000+ items/epoch)
2. **Bounded channels**: Prevent runaway memory growth
3. **Processor pooling**: Reuse processor nodes (they're long-lived)
4. **Gen0 optimization**: Short-lived channels are collected efficiently

---

## 3. Proposed Implementation Plan

### Phase 1: Core Infrastructure

**3.1.1 Create EpochSourceNode**
```csharp
public sealed class EpochSourceNode
{
    private readonly Channel<IEpoch> _epochStream;
    private readonly IEpochCoordinator _coordinator;
    private readonly EpochPolicy _policy;
    
    public EpochSourceNode(IServiceScopeFactory scopeFactory, EpochPolicy policy)
    {
        _coordinator = new EpochCoordinator(scopeFactory);
        _policy = policy;
        _epochStream = Channel.CreateUnbounded<IEpoch>(new UnboundedChannelOptions
        {
            SingleReader = false, // Multiple processors
            SingleWriter = true,  // Single coordinator
            AllowSynchronousContinuations = false
        });
    }
    
    public ChannelReader<IEpoch> EpochReader => _epochStream.Reader;
    
    public async Task<IEpoch> GetOrCreateEpochAsync(string sourceId, EpochVector vector)
    {
        var epoch = await _coordinator.GetOrCreateEpochAsync(sourceId, vector);
        
        // Publish to stream
        await _epochStream.Writer.WriteAsync(epoch);
        
        return epoch;
    }
    
    public void SignalCompletion()
    {
        _epochStream.Writer.Complete();
    }
}
```

**3.1.2 Create EpochProcessorNode**
```csharp
public sealed class EpochProcessorNode : IAsyncDisposable
{
    private readonly ChannelReader<IEpoch> _epochReader;
    private readonly EpochHooks _hooks;
    private readonly Task _processingTask;
    
    public EpochProcessorNode(EpochSourceNode source, EpochHooks hooks)
    {
        _epochReader = source.EpochReader;
        _hooks = hooks;
        _processingTask = Task.Run(() => ProcessEpochsAsync());
    }
    
    private async Task ProcessEpochsAsync()
    {
        await foreach (var epoch in _epochReader.ReadAllAsync())
        {
            try
            {
                // Execute pre-epoch hook
                if (_hooks.OnBeginEpoch != null)
                {
                    await _hooks.OnBeginEpoch(epoch, CancellationToken.None);
                }
                
                // Drain epoch operation queue
                await epoch.WhenAllOperationsCompletedAsync();
                
                // Execute post-epoch hook
                if (_hooks.OnCommitEpoch != null)
                {
                    await _hooks.OnCommitEpoch(epoch, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                // Handle error (fail flow)
                if (_hooks.OnEpochError != null)
                {
                    await _hooks.OnEpochError(epoch, ex, CancellationToken.None);
                }
                throw; // Propagate to fail graph execution
            }
            finally
            {
                await epoch.DisposeAsync();
            }
        }
    }
    
    public Task CompletionTask => _processingTask;
    
    public async ValueTask DisposeAsync()
    {
        await _processingTask;
    }
}
```

**3.1.3 Define EpochHooks**
```csharp
public sealed class EpochHooks
{
    public Func<IEpoch, CancellationToken, Task>? OnBeginEpoch { get; set; }
    public Func<IEpoch, CancellationToken, Task>? OnCommitEpoch { get; set; }
    public Func<IEpoch, Exception, CancellationToken, Task>? OnEpochError { get; set; }
}
```

### Phase 2: Graph Integration

**3.2.1 Add specialized edge type**
```csharp
internal sealed class EpochProcessorEdge : Edge
{
    public EpochProcessorEdge(EpochSourceNode source, EpochProcessorNode processor)
        : base(source, processor, EdgeStrategy.Direct)
    {
    }
}
```

**3.2.2 Add configuration API**
```csharp
public static class EpochConfigurationExtensions
{
    public static DataFlowBuilder ConfigureEpochs(
        this DataFlowBuilder builder,
        Action<EpochConfiguration> configure)
    {
        var config = new EpochConfiguration();
        configure(config);
        
        // Create source node
        var sourceNode = new EpochSourceNode(
            builder.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            config.Policy);
        
        // Create processor nodes
        foreach (var processorConfig in config.Processors)
        {
            var processor = new EpochProcessorNode(sourceNode, config.Hooks);
            builder.AddEpochProcessor(processor);
        }
        
        builder.SetEpochSource(sourceNode);
        
        return builder;
    }
}
```

### Phase 3: Migration & Testing

**3.3.1 Update existing tests**
- Modify tests to use new configuration API
- Validate backward compatibility
- Add concurrency tests (1 vs multiple processors)

**3.3.2 Add performance benchmarks**
- Measure epoch creation overhead
- Measure channel allocation GC impact
- Measure throughput with 1-4 processors

---

## 4. Benchmarking Strategy

### 4.1 Targeted Benchmarks

**Benchmark 1: Epoch Creation Overhead**
```csharp
[Benchmark]
public async Task CreateAndDisposeEpoch()
{
    var epoch = await _coordinator.GetOrCreateEpochAsync("test", vector);
    await epoch.DisposeAsync();
}
```
**Target**: < 1μs per epoch

**Benchmark 2: Channel Allocation Impact**
```csharp
[Benchmark]
public void CreateChannels()
{
    for (int i = 0; i < 1000; i++)
    {
        var channel = Channel.CreateUnbounded<OperationRequest>();
        channel.Writer.Complete();
    }
}
```
**Target**: < 100μs for 1000 channels, minimal Gen1/2 collections

**Benchmark 3: Serialized Operation Throughput**
```csharp
[Benchmark]
public async Task QueueOperations()
{
    for (int i = 0; i < 1000; i++)
    {
        await _epoch.QueueSerializedOperationAsync<TestService>(async svc =>
        {
            await Task.Yield();
        });
    }
    await _epoch.WhenAllOperationsCompletedAsync();
}
```
**Target**: > 100k operations/sec

**Benchmark 4: Multi-Processor Throughput**
```csharp
[Params(1, 2, 4)]
public int ProcessorCount { get; set; }

[Benchmark]
public async Task ProcessEpochsWithProcessors()
{
    // Create epochs and measure completion time
    // with different processor counts
}
```
**Target**: Linear scaling up to 4 processors

### 4.2 GC Pressure Analysis

Use `dotnet-counters` and `PerfView` to measure:
- Gen0/Gen1/Gen2 collection frequency
- Allocation rate (bytes/sec)
- Time in GC (%)
- Large object heap growth

**Acceptable thresholds:**
- Gen0 collections: OK (short-lived objects)
- Gen1 collections: < 10/sec
- Gen2 collections: < 1/sec
- Time in GC: < 5%

---

## 5. Trade-offs & Decisions

### 5.1 Channel Reusability

**Decision:** ❌ Do not pool channels
**Rationale:**
- Channels cannot be reused after completion
- Creation overhead is minimal (~500 bytes)
- GC handles short-lived objects efficiently
- Complexity not justified

### 5.2 Epoch Object Pooling

**Decision:** ❌ Do not pool epoch objects
**Rationale:**
- Epochs contain DI scopes that must be disposed
- Lifetime management becomes complex
- No clear performance benefit

### 5.3 Processor Concurrency Model

**Decision:** ✅ Allow multiple processor nodes
**Rationale:**
- Enables tunable throughput vs order guarantees
- Serial processing (1 processor) guarantees deterministic commit order
- Parallel processing (N processors) maximizes throughput
- Developer choice based on requirements

### 5.4 Hook Execution Model

**Decision:** ✅ Async hooks with error propagation
**Rationale:**
- Aligns with async/await throughout DataFlow
- Allows database operations in hooks
- Errors fail the entire flow (correct behavior)

---

## 6. Migration Path

### 6.1 Backward Compatibility

**Current API:**
```csharp
var epoch = await coordinator.GetOrCreateEpochAsync("source", vector);
await epoch.QueueSerializedOperationAsync<DbContext>(...);
await epoch.WhenAllOperationsCompletedAsync();
```

**New API:**
```csharp
// Configuration moved to builder
builder.ConfigureEpochs(config =>
{
    config.SetPolicy(...);
    config.AddProcessor("proc1");
});

// Usage in blocks unchanged
var epoch = await coordinator.GetOrCreateEpochAsync("source", vector);
await epoch.QueueSerializedOperationAsync<DbContext>(...);
// WhenAllOperationsCompletedAsync called internally by processor
```

**Migration strategy:**
1. Make `ConfigureEpochs` optional (defaults to single processor)
2. Existing tests continue to work
3. New tests use configuration API

### 6.2 Test Migration Checklist

- [ ] Update `SerializedExecutionTests` for new API
- [ ] Update `EpochTransactionIntegrationTests` for processor nodes
- [ ] Add multi-processor concurrency tests
- [ ] Add hook execution tests
- [ ] Add error handling tests

---

## 7. Open Questions for Implementation Team

1. **Graph Execution Integration:**
   - How do processor nodes integrate with `DataFlowGraph.ExecuteAsync()`?
   - Should processors be part of topology or managed separately?

2. **Error Handling Strategy:**
   - Should epoch errors fail the entire flow or just that epoch?
   - How to handle partial failures in multi-processor scenarios?

3. **Checkpoint Integration:**
   - How do checkpoints interact with epoch completion?
   - Should checkpointing be a special hook or separate mechanism?

4. **Cancellation Propagation:**
   - How does flow cancellation affect in-flight epochs?
   - Should processors finish current epoch or abort immediately?

5. **Metrics & Observability:**
   - What metrics should be exposed (queued/executed counts, processor throughput)?
   - Integration with existing telemetry?

---

## 8. Implementation Handover

### 8.1 Deliverables

**Core Components:**
1. `EpochSourceNode.cs` - Epoch stream management
2. `EpochProcessorNode.cs` - Epoch processing with hooks
3. `EpochHooks.cs` - Hook definitions
4. `EpochConfiguration.cs` - Configuration API
5. `EpochConfigurationExtensions.cs` - Builder extensions

**Testing:**
1. Unit tests for source/processor nodes
2. Integration tests for multi-processor scenarios
3. Performance benchmarks (4 scenarios above)

**Documentation:**
1. Update `/poc/docs/design/epochs.md` with node architecture
2. Add usage guide for hooks and processors
3. Migration guide for existing tests

### 8.2 Acceptance Criteria

**Functional:**
- [ ] Epochs are created and published to stream
- [ ] Processors drain operation queues sequentially
- [ ] Hooks execute at correct lifecycle points
- [ ] Multiple processors work concurrently
- [ ] Errors propagate correctly

**Performance:**
- [ ] Epoch creation < 1μs
- [ ] Throughput > 100k ops/sec (single processor)
- [ ] Linear scaling with multiple processors (up to 4)
- [ ] Gen0/1/2 collections within acceptable thresholds
- [ ] Memory growth is bounded

**Quality:**
- [ ] All existing tests pass
- [ ] New tests cover all scenarios
- [ ] Code follows existing patterns (BufferNode style)
- [ ] Documentation is complete

---

## 9. Conclusion

The EpochSourceNode/ProcessorNode architecture is sound and aligns well with existing DataFlow patterns. The use of `System.Threading.Channels` is appropriate and performant. Object pooling is not necessary given the GC characteristics of short-lived channels.

**Recommendations:**
1. ✅ Proceed with implementation as outlined
2. ✅ Focus on Phase 1 (core infrastructure) first
3. ✅ Run benchmarks early to validate assumptions
4. ⏳ Defer pooling optimizations until profiling shows need
5. ✅ Maintain backward compatibility via optional configuration

**Next Steps:**
1. Implementation team reviews this document
2. Addresses open questions in section 7
3. Implements Phase 1 components
4. Runs benchmarks and validates performance
5. Proceeds with Phases 2-3 based on results

---

**Document Status**: Ready for Implementation Handover  
**Reviewer Sign-off**: _Pending Implementation Team Review_
