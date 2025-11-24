# POC Metrics Mapping Analysis

This document maps production metrics to proposed locations in POC code.

## POC Architecture Context

### Key Execution Points

1. **DataFlowGraph.ExecuteAsync()** - Graph-level execution
   - Location: `/poc/DataFlow.POC/Core/DataFlowGraph.cs` line 263
   - Orchestrates entire flow execution
   - Calls `BuildExecutionPipeline()` then `ExecuteBlocksAsync()`

2. **BlockRuntimeModel.ExecuteAsync()** - Block-level execution
   - Location: `/poc/DataFlow.POC/Core/DataFlowGraph.cs` line 636
   - Individual block execution with routing
   - Handles block lifecycle and output routing

3. **ExecutionContext** - Context passed through execution
   - Location: `/poc/DataFlow.POC/Core/IExecutionContext.cs`
   - Contains: CancellationToken, ServiceProvider, InvocationId, RecoveryCheckpoint
   - **Note**: Missing FlowName - would need to be added

4. **Channel Creation** - Typed channels for edges and buffers
   - BufferNodes: line 304-328 in DataFlowGraph.cs
   - Edges: line 331-347 in DataFlowGraph.cs
   - Uses `TypedChannelFactory.CreateTypedChannel()`

## Metrics Mapping Table

### ✅ = Straightforward mapping
### ⚠️ = Requires consideration
### ❌ = Difficult/architectural impact

| Production Metric | POC Location | Status | Notes |
|-------------------|--------------|--------|-------|
| **Flow Started** | DataFlowGraph.ExecuteAsync() start | ✅ | Line 263, before BuildExecutionPipeline() |
| **Flow Completed** | DataFlowGraph.ExecuteAsync() finally block | ✅ | Line 290, after Task.WhenAll |
| **Flow Duration Histogram** | DataFlowGraph.ExecuteAsync() finally | ✅ | Measure ExecuteAsync duration |
| **Flow Execution Count** | DataFlowGraph.ExecuteAsync() finally | ✅ | Increment on completion |
| **Active Flow Count** | DataFlowGraph.ExecuteAsync() ±1 | ✅ | +1 at start, -1 in finally |
| **Block Started** | BlockRuntimeModel.ExecuteAsync() start | ✅ | Line 638, after logging |
| **Block Completed** | BlockRuntimeModel.ExecuteAsync() finally | ✅ | After output routing completes |
| **Block Duration Histogram** | BlockRuntimeModel.ExecuteAsync() finally | ✅ | Measure ExecuteAsync duration |
| **Block Execution Count** | BlockRuntimeModel.ExecuteAsync() finally | ✅ | Increment on completion |
| **Active Block Count** | BlockRuntimeModel.ExecuteAsync() ±1 | ✅ | +1 at start, -1 in finally |
| **Block Operations Completed** | User code in blocks | ✅ | Pass metrics via context, emit from blocks |
| **Data Items Processed** | User code in blocks | ✅ | Pass metrics via context, emit from blocks |
| **Channel Buffer Utilization** | Channel observation | ⚠️ | Needs MonitoredChannel or alternative |
| **Active Channel Count** | Channel registry | ⚠️ | Needs channel tracking mechanism |

## Detailed Mapping

### 1. Flow-Level Metrics

#### Flow Started / Completed / Duration

**Production Location**: `/src/DataFlow/DataFlow.cs` ExecuteAsync()

**POC Location**: `/poc/DataFlow.POC/Core/DataFlowGraph.cs` ExecuteAsync() line 263

**Proposed Implementation**:
```csharp
public async Task ExecuteAsync(IExecutionContext context)
{
    _logger.LogInformation("Starting execution of dataflow: {FlowName}", Name);
    
    // NEW: Emit flow started metric
    var stopwatch = Stopwatch.StartNew();
    _metrics?.FlowStarted(Name, context.InvocationId);
    
    try
    {
        // ... existing execution code ...
        
        // NEW: Mark as successful
        _metrics?.FlowCompleted(Name, context.InvocationId, stopwatch.Elapsed.TotalMilliseconds, success: true);
    }
    catch (Exception ex)
    {
        // NEW: Mark as failed
        _metrics?.FlowCompleted(Name, context.InvocationId, stopwatch.Elapsed.TotalMilliseconds, success: false);
        throw;
    }
    finally
    {
        stopwatch.Stop();
        _logger.LogInformation("Completed execution of dataflow: {FlowName}", Name);
    }
}
```

**Required Changes**:
- Add `IDataFlowMetrics? _metrics` field to DataFlowGraph
- Inject via constructor (optional dependency)
- Add flow name to DataFlowGraph (already exists as `Name` property)

**Status**: ✅ Straightforward

---

### 2. Block-Level Metrics

#### Block Started / Completed / Duration

**Production Location**: `/src/DataFlow/DataFlow.cs` ExecuteBlockAsync()

**POC Location**: `/poc/DataFlow.POC/Core/DataFlowGraph.cs` BlockRuntimeModel.ExecuteAsync() line 636

**Proposed Implementation**:
```csharp
public async Task ExecuteAsync(IExecutionContext context, ILogger<DataFlowGraph> logger)
{
    var stopwatch = Stopwatch.StartNew();
    var flowName = _pipeline.FlowName; // NEW: need to add FlowName to pipeline
    
    // NEW: Emit block started metric
    _metrics?.BlockStarted(_block.Name, flowName, context.InvocationId);
    
    try
    {
        logger.LogDebug("Block {BlockName} starting execution", _block.Name);
        
        // ... existing execution code ...
        
        logger.LogDebug("Block {BlockName} completed successfully", _block.Name);
        
        // NEW: Mark as successful
        _metrics?.BlockCompleted(_block.Name, flowName, context.InvocationId, 
            stopwatch.Elapsed.TotalMilliseconds, success: true);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Block {BlockName} failed", _block.Name);
        
        // NEW: Mark as failed
        _metrics?.BlockCompleted(_block.Name, flowName, context.InvocationId, 
            stopwatch.Elapsed.TotalMilliseconds, success: false);
        
        _pipeline.CompleteOutgoingChannels(_block, _outgoingEdges, _bufferProducers, logger, ex);
        throw;
    }
    finally
    {
        stopwatch.Stop();
    }
}
```

**Required Changes**:
- Add `IDataFlowMetrics? _metrics` to ExecutionPipeline
- Add `FlowName` to ExecutionPipeline
- Pass metrics through from DataFlowGraph to ExecutionPipeline

**Status**: ✅ Straightforward

---

### 3. Processing Metrics

#### Block Operations Completed / Data Items Processed

**Production Pattern**: User code calls metrics methods during processing

**POC Approach**: Add metrics to IExecutionContext for blocks to access

**Proposed Implementation**:

Add to IExecutionContext:
```csharp
public interface IExecutionContext
{
    // ... existing properties ...
    
    /// <summary>
    /// Metrics collector for recording processing metrics.
    /// </summary>
    IDataFlowMetrics? Metrics { get; }
    
    /// <summary>
    /// Name of the current flow being executed.
    /// </summary>
    string? FlowName { get; }
    
    /// <summary>
    /// Name of the current block being executed (set during block execution).
    /// </summary>
    string? CurrentBlockName { get; }
}
```

Usage in user blocks:
```csharp
public override async IAsyncEnumerable<OutputType> ExecuteAsync(
    IAsyncEnumerable<InputType> input,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    await foreach (var batch in input.WithCancellation(cancellationToken))
    {
        // Process batch...
        var processed = ProcessBatch(batch);
        
        // Record metrics
        _context.Metrics?.BlockOperationsCompleted(
            _context.CurrentBlockName, 
            _context.FlowName, 
            _context.InvocationId, 
            count: 1);
        
        _context.Metrics?.ItemsProcessed(
            _context.CurrentBlockName,
            _context.FlowName,
            _context.InvocationId,
            dataLabel: "orders",
            count: processed.Length);
        
        yield return processed;
    }
}
```

**Required Changes**:
- Extend IExecutionContext with Metrics, FlowName, CurrentBlockName
- Set CurrentBlockName in BlockRuntimeModel.ExecuteAsync before calling adapter
- Pass FlowName through DataFlowGraph → ExecutionContext

**Status**: ✅ Straightforward

---

### 4. Channel Metrics

#### Channel Buffer Utilization (ObservableGauge)

**Production Approach**: 
- MonitoredChannel<T> wrapper class
- Wraps System.Threading.Channels.Channel<T>
- Implements IMonitoredChannel with GetMetricSnapshot()
- Registered with metrics system
- ObservableGauge callback polls all registered channels

**POC Consideration**:
POC uses TypedChannelFactory which creates channels via reflection. Need to decide on monitoring approach.

**Option 1: MonitoredChannel Wrapper (similar to production)**

Pros:
- Same pattern as production
- Clean separation of concerns
- Easy to track multiple channels

Cons:
- Requires wrapping ALL channel creations
- POC uses typed channels via reflection
- More complex integration

**Proposed Location**:
- Wrap channels in `TypedChannelFactory.CreateTypedChannel()` line ~313
- Create MonitoredChannel wrapper after channel creation
- Register with metrics system

**Option 2: Polling via Reflection**

Pros:
- No wrapper needed
- Simpler integration
- Works with existing typed channels

Cons:
- Need to keep references to channels
- Requires reflection to access Count property
- Less performant than wrapper

**Proposed Location**:
- Store channel readers/writers in ExecutionPipeline
- Metrics system polls ExecutionPipeline.GetChannelSnapshots()
- Use reflection to access ChannelReader<T>.Count

**Option 3: Simplified Approach - Only Track Active Channel Count**

Pros:
- Minimal implementation
- No per-channel monitoring complexity
- Still provides useful observability

Cons:
- Loses per-channel utilization data
- Less detailed than production

**Proposed Location**:
- Count channels in ExecutionPipeline
- Report total active channel count

**Recommendation**: Start with **Option 3** (track active count only), then evolve to Option 2 if detailed utilization needed. Option 1 requires significant refactoring of TypedChannelFactory.

**Status**: ⚠️ Requires decision on approach

#### Active Channel Count (ObservableGauge)

**Production**: Counts channels in ChannelRegistry

**POC**: Count channels created in ExecutionPipeline

**Proposed Implementation**:
```csharp
public class ExecutionPipeline
{
    // ... existing code ...
    
    public int GetActiveChannelCount()
    {
        return EdgeRuntimeModels.Count + BufferRuntimeModels.Count;
    }
}
```

Then in metrics observable gauge callback:
```csharp
private Measurement<int> GetActiveChannelCount()
{
    // If in active execution, get from current pipeline
    var count = _currentPipeline?.GetActiveChannelCount() ?? 0;
    return new Measurement<int>(count, GlobalTags);
}
```

**Required Changes**:
- Track current ExecutionPipeline in DataFlowGraph or metrics system
- Add GetActiveChannelCount() to ExecutionPipeline
- Observable gauge polls this count

**Status**: ✅ Straightforward (if we only track count, not utilization)

---

## Missing Context in POC

### Flow Name in ExecutionContext

**Current**: IExecutionContext has InvocationId but not FlowName

**Required**: Add FlowName to properly tag metrics

**Proposed Changes**:

1. Add to IExecutionContext:
```csharp
public interface IExecutionContext
{
    // ... existing ...
    string? FlowName { get; }
}
```

2. Set in DataFlowGraph.ExecuteAsync():
```csharp
// Create enhanced context with flow name
var enhancedContext = new EnhancedExecutionContext(context, Name);
await pipeline.ExecuteBlocksAsync(_blocks, ..., enhancedContext, ...);
```

Or extend ExecutionContext directly to include FlowName.

---

## Summary of Required POC Changes

### Minimal Changes (Essential Metrics)

1. **Add IDataFlowMetrics dependency**:
   - Inject into DataFlowGraph constructor (optional)
   - Pass through to ExecutionPipeline

2. **Extend IExecutionContext**:
   - Add FlowName property
   - Add Metrics property
   - Add CurrentBlockName property (for blocks to access)

3. **Emit Flow Metrics**:
   - DataFlowGraph.ExecuteAsync: FlowStarted/Completed
   - Track duration with Stopwatch
   - Track success/failure

4. **Emit Block Metrics**:
   - BlockRuntimeModel.ExecuteAsync: BlockStarted/Completed
   - Set context.CurrentBlockName before execution
   - Track duration with Stopwatch
   - Track success/failure

5. **Active Channel Count**:
   - Add ExecutionPipeline.GetActiveChannelCount()
   - Observable gauge polls count

### Optional/Advanced Changes

6. **Channel Buffer Utilization** (if detailed monitoring needed):
   - Decide on approach (wrapper vs polling vs simplified)
   - Implement chosen approach
   - Register channels with metrics system

7. **Processing Metrics**:
   - Users call context.Metrics.BlockOperationsCompleted()
   - Users call context.Metrics.ItemsProcessed()
   - These methods are opt-in from user blocks

---

## Metrics Coverage Assessment

### Achievable with Minimal Changes ✅

- Flow Started ✅
- Flow Completed ✅
- Flow Duration Histogram ✅
- Flow Execution Count ✅
- Active Flow Count ✅
- Block Started ✅
- Block Completed ✅
- Block Duration Histogram ✅
- Block Execution Count ✅
- Active Block Count ✅
- Block Operations Completed ✅ (user opt-in)
- Data Items Processed ✅ (user opt-in)
- Active Channel Count ✅

**Coverage: 13/14 metrics (93%)**

### Requires Additional Design

- Channel Buffer Utilization ⚠️ (needs approach decision)

**If implemented: 14/14 metrics (100%)**

---

## Architectural Impact Assessment

### Low Impact (Recommended for initial implementation)

- Adding IDataFlowMetrics as optional dependency to DataFlowGraph
- Extending IExecutionContext with FlowName, Metrics, CurrentBlockName
- Emitting metrics at flow and block lifecycle points
- Tracking active channel count

**Estimated Effort**: 1-2 days

### Medium Impact (Optional enhancement)

- Implementing channel buffer utilization monitoring
- Creating MonitoredChannel wrapper
- Modifying TypedChannelFactory to use wrapped channels

**Estimated Effort**: 2-3 days

### Comparison to Production

Production metrics system is **directly applicable** to POC with minimal adaptation. The main differences are:

1. **Architecture**: POC uses graph-based execution vs production's sequential block execution
2. **Channels**: POC uses typed channels via reflection vs production's generic channels
3. **Context**: POC uses IExecutionContext vs production's IDataFlowContext

None of these differences fundamentally block metrics implementation. All metrics can be achieved by:
- Injecting metrics at the right lifecycle points
- Extending context to carry necessary information
- Using similar patterns adapted to POC architecture

---

## Next Steps for Implementation

1. Create IDataFlowMetrics interface for POC (can be same as production)
2. Extend IExecutionContext with required properties
3. Add metrics emission points in DataFlowGraph and BlockRuntimeModel
4. Implement active channel count tracking
5. Test with MetricCollector similar to production tests
6. (Optional) Implement channel buffer utilization if needed
