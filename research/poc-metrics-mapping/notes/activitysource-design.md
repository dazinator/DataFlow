# ActivitySource Implementation Design for POC

This document proposes an ActivitySource implementation for the POC codebase based on production patterns.

## Production ActivitySource Analysis

### Configuration

- **ActivitySource Name**: `Uniun.DataFlow`
- **Location**: Static field in DataFlow class
- **Lifetime**: Application lifetime (static)

### Activity Hierarchy

```
Flow Activity (parent)
├── Block Activity 1 (child)
├── Block Activity 2 (child)
└── Block Activity N (child)
```

### Activity Types

1. **Flow Activity**
   - Name: "Flow"
   - DisplayName: "Flow {FlowName}" (parameterized)
   - Tags: FlowName, FlowInvocationId, GlobalTags
   - Status: Ok/Error
   - Parent: None (root activity)

2. **Block Activity**
   - Name: "Block"
   - DisplayName: "Block {BlockName}" (parameterized)
   - Kind: Internal
   - Tags: BlockName, FlowName, GlobalTags
   - Status: Ok/Error
   - Parent: Flow Activity

### Error Handling

Both activities set:
- `Status = Error` with exception message
- `error.type` tag with exception type
- `cancelled = "true"` tag for OperationCanceledException

---

## POC Architecture Considerations

### Key Differences from Production

1. **Execution Model**:
   - Production: Sequential block execution via Task.WhenAll
   - POC: Graph-based execution with concurrent blocks via ExecutionPipeline

2. **Activity Context Propagation**:
   - Production: Flow activity is parent, blocks are started with `parentActivity?.Context`
   - POC: Need to propagate Flow activity context to BlockRuntimeModel.ExecuteAsync()

3. **Concurrent Block Execution**:
   - Production: All blocks start after Flow activity is created, can use parent context
   - POC: Blocks run in Task.Run(), need to capture and propagate context

4. **Additional Tracing Points** (optional):
   - Epoch operations
   - Edge routing operations
   - Buffer node operations

---

## Proposed POC ActivitySource Design

### Option 1: Minimal (Match Production Exactly)

**Only trace Flow and Block activities, matching production pattern**

#### Implementation

**Location 1: DataFlowGraph.ExecuteAsync()** (Flow Activity)

```csharp
public async Task ExecuteAsync(IExecutionContext context)
{
    using var flowActivity = ActivitySource.StartActivity(ActivityNames.Flow);
    
    if (flowActivity is not null)
    {
        flowActivity.AddTag(ActivityNames.TagNames.FlowInvocationId, context.InvocationId);
        flowActivity.AddTag(ActivityNames.TagNames.FlowName, Name);
        flowActivity.DisplayName = $"{ActivityNames.Flow} {{FlowName}}";
        
        // Add global tags if available
        if (_metrics?.GlobalTags != null)
        {
            flowActivity.AddTags(_metrics.GlobalTags);
        }
    }
    
    try
    {
        _logger.LogInformation("Starting execution of dataflow: {FlowName}", Name);
        
        // Pass flow activity context to pipeline
        var pipeline = BuildExecutionPipeline();
        await pipeline.ExecuteBlocksAsync(_blocks, _outgoingEdges, _incomingEdges, 
            context, _logger, flowActivity);
        
        flowActivity?.SetStatus(ActivityStatusCode.Ok);
    }
    catch (Exception ex)
    {
        flowActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        flowActivity?.SetTag("error.type", ex.GetType().FullName);
        
        if (ex is OperationCanceledException)
        {
            flowActivity?.SetTag("cancelled", "true");
        }
        
        throw;
    }
    finally
    {
        _logger.LogInformation("Completed execution of dataflow: {FlowName}", Name);
    }
}
```

**Location 2: BlockRuntimeModel.ExecuteAsync()** (Block Activity)

```csharp
public async Task ExecuteAsync(IExecutionContext context, ILogger<DataFlowGraph> logger, Activity? parentActivity)
{
    using var blockActivity = ActivitySource.StartActivity(
        ActivityNames.Block,
        ActivityKind.Internal,
        parentActivity?.Context ?? default);
    
    if (blockActivity is not null)
    {
        blockActivity.AddTag(ActivityNames.TagNames.BlockName, _block.Name);
        blockActivity.AddTag(ActivityNames.TagNames.FlowName, _pipeline.FlowName);
        blockActivity.DisplayName = $"{ActivityNames.Block} {{BlockName}}";
        
        // Add global tags if available
        if (_metrics?.GlobalTags != null)
        {
            blockActivity.AddTags(_metrics.GlobalTags);
        }
    }
    
    try
    {
        logger.LogDebug("Block {BlockName} starting execution", _block.Name);
        
        // ... existing execution code ...
        
        blockActivity?.SetStatus(ActivityStatusCode.Ok);
    }
    catch (Exception ex)
    {
        blockActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        blockActivity?.SetTag("error.type", ex.GetType().FullName);
        
        if (ex is OperationCanceledException)
        {
            blockActivity?.SetTag("cancelled", "true");
        }
        
        logger.LogError(ex, "Block {BlockName} failed", _block.Name);
        _pipeline.CompleteOutgoingChannels(_block, _outgoingEdges, _bufferProducers, logger, ex);
        throw;
    }
}
```

**Required Changes**:
- Add static `ActivitySource` field to DataFlowGraph
- Pass `Activity? parentActivity` through ExecuteBlocksAsync → StartBlockTask → BlockRuntimeModel.ExecuteAsync
- Add FlowName to ExecutionPipeline (for block activity tagging)

**Pros**:
- ✅ Exact parity with production
- ✅ Familiar pattern for users migrating from production
- ✅ Minimal implementation
- ✅ Clear hierarchy: Flow → Blocks

**Cons**:
- ❌ Doesn't trace POC-specific concepts (epochs, edges, buffers)
- ❌ Less insight into graph execution details

**Recommendation**: ✅ **Start with this approach** - provides parity with production

---

### Option 2: Enhanced (POC-Specific Tracing)

**Add additional activity types for POC concepts**

#### Additional Activity Types

1. **EpochOperation Activity** (optional)
   - Parent: Flow Activity
   - Tags: EpochId, OperationName
   - Location: EpochOperation.ExecuteAsync()

2. **EdgeRouting Activity** (optional)
   - Parent: Block Activity
   - Tags: EdgeName, RoutingStrategy
   - Location: TypedEdgeRouter.RouteAsync()

3. **BufferOperation Activity** (optional)
   - Parent: Block Activity
   - Tags: BufferName, OperationType (read/write)
   - Location: Buffer read/write operations

#### Implementation Example (Epoch)

```csharp
// In EpochOperation.ExecuteAsync()
using var epochActivity = ActivitySource.StartActivity(
    "EpochOperation",
    ActivityKind.Internal,
    flowActivity?.Context ?? default);

if (epochActivity is not null)
{
    epochActivity.AddTag("EpochId", context.Epoch.EpochId);
    epochActivity.AddTag("OperationName", _operation.Name);
    epochActivity.DisplayName = $"Epoch Operation {{{_operation.Name}}}";
}
```

**Pros**:
- ✅ More detailed observability of POC execution
- ✅ Helps understand epoch lifecycle
- ✅ Helps debug routing issues
- ✅ Shows buffer contention

**Cons**:
- ❌ More complex implementation
- ❌ Diverges from production pattern
- ❌ Potential performance overhead (many activities)
- ❌ Requires propagating Activity context through more layers

**Recommendation**: ⚠️ **Consider for future enhancement** - not needed for initial parity

---

## Proposed Implementation Plan

### Phase 1: Production Parity (Recommended)

Implement Option 1 (Minimal):
1. Add ActivitySource to DataFlowGraph
2. Implement Flow activity in DataFlowGraph.ExecuteAsync()
3. Implement Block activity in BlockRuntimeModel.ExecuteAsync()
4. Pass parent activity context through execution pipeline
5. Test with ActivityListener similar to production tests

**Deliverables**:
- Flow and Block activities emitted
- Proper parent-child relationships
- Error tagging
- Parameterized display names

**Effort**: 1-2 days

### Phase 2: POC Enhancements (Optional)

Add POC-specific activities:
1. Epoch operation activities
2. Edge routing activities
3. Buffer operation activities

**Deliverables**:
- Enhanced tracing for POC concepts
- Better debugging capabilities

**Effort**: 2-3 days

---

## Activity Names and Constants

Create constants similar to production:

```csharp
// Location: /poc/DataFlow.POC/Observability/ActivityNames.cs
namespace DataFlow.POC.Observability;

public static class ActivityNames
{
    // Activity names
    public const string Flow = "Flow";
    public const string Block = "Block";
    
    // POC-specific (Phase 2)
    public const string EpochOperation = "EpochOperation";
    public const string EdgeRouting = "EdgeRouting";
    public const string BufferOperation = "BufferOperation";
    
    public static class TagNames
    {
        public const string FlowInvocationId = "FlowInvocationId";
        public const string FlowName = "FlowName";
        public const string BlockName = "BlockName";
        
        // POC-specific (Phase 2)
        public const string EpochId = "EpochId";
        public const string OperationName = "OperationName";
        public const string EdgeName = "EdgeName";
        public const string RoutingStrategy = "RoutingStrategy";
        public const string BufferName = "BufferName";
        public const string OperationType = "OperationType";
    }
}
```

---

## Testing Strategy

### Unit Tests (similar to production)

**Test File**: `/poc/DataFlow.POC.Tests/Observability/ActivityTracingTests.cs`

```csharp
[Fact]
public async Task Flow_Execution_Creates_Flow_Activity()
{
    // Arrange
    var activities = new List<Activity>();
    using var listener = new ActivityListener
    {
        ShouldListenTo = source => source.Name == "DataFlow.POC",
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        ActivityStarted = activity => activities.Add(activity)
    };
    ActivitySource.AddActivityListener(listener);
    
    // Act
    var graph = CreateSimpleGraph();
    await graph.ExecuteAsync(new ExecutionContext(...));
    
    // Assert
    var flowActivity = activities.Single(a => a.OperationName == "Flow");
    Assert.NotNull(flowActivity);
    Assert.Contains("FlowName", flowActivity.Tags.Select(t => t.Key));
}

[Fact]
public async Task Block_Execution_Creates_Child_Activities()
{
    // ... similar test for block activities ...
}

[Fact]
public async Task Failed_Execution_Sets_Error_Status()
{
    // ... test error handling ...
}
```

### Integration Tests

Test with actual OpenTelemetry exporters:

```csharp
[Fact]
public async Task Activities_Export_To_OpenTelemetry()
{
    // Arrange
    var exportedActivities = new List<Activity>();
    using var tracerProvider = Sdk.CreateTracerProviderBuilder()
        .AddSource("DataFlow.POC")
        .AddInMemoryExporter(exportedActivities)
        .Build();
    
    // Act
    await graph.ExecuteAsync(context);
    
    // Assert
    Assert.NotEmpty(exportedActivities);
    // Verify hierarchy, tags, etc.
}
```

---

## OpenTelemetry Integration

### Package Reference

Add to POC project:

```xml
<PackageReference Include="OpenTelemetry" Version="1.6.0" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.6.0" />
```

### Registration Pattern

Similar to production's MeterProvider extensions:

```csharp
// Location: /poc/DataFlow.POC/Observability/TracerProviderExtensions.cs
namespace DataFlow.POC.Observability;

public static class TracerProviderExtensions
{
    public static TracerProviderBuilder AddDataFlowPOC(this TracerProviderBuilder builder)
    {
        return builder.AddSource("DataFlow.POC");
    }
}
```

Usage:

```csharp
services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddDataFlowPOC()
        .AddOtlpExporter());
```

---

## Comparison: Production vs POC

| Aspect | Production | POC (Proposed) |
|--------|-----------|----------------|
| **ActivitySource Name** | "Uniun.DataFlow" | "DataFlow.POC" |
| **Flow Activity** | ✅ Yes | ✅ Yes |
| **Block Activity** | ✅ Yes | ✅ Yes |
| **Hierarchy** | Flow → Blocks | Flow → Blocks |
| **Error Handling** | Status + error.type tag | Same |
| **Cancellation Handling** | cancelled tag | Same |
| **Display Names** | Parameterized | Same |
| **Global Tags** | From IDataFlowMetrics | Same (if metrics available) |
| **Domain-specific Activities** | ❌ No | ⚠️ Optional (epochs, edges) |

**Parity Level**: **100%** for Phase 1 (Flow + Block activities)

---

## Architectural Impact

### Low Impact (Phase 1)

- Adding ActivitySource static field to DataFlowGraph
- Wrapping execution methods with activity scopes
- Passing Activity? through execution pipeline
- Adding FlowName to ExecutionPipeline

**Estimated Effort**: 1-2 days

### Medium Impact (Phase 2 - Optional)

- Adding activity scopes to epoch operations
- Adding activity scopes to edge routing
- Propagating activity context deeper into execution
- Additional testing

**Estimated Effort**: 2-3 days

---

## Recommendations

### For Initial Implementation

1. ✅ **Implement Phase 1** (Flow + Block activities)
   - Provides exact parity with production
   - Minimal architectural changes
   - Clear upgrade path for future enhancements

2. ✅ **Create ActivityNames constants**
   - Consistent with production pattern
   - Easy to maintain

3. ✅ **Write comprehensive tests**
   - Verify activity hierarchy
   - Test error scenarios
   - Test OpenTelemetry export

4. ❌ **Defer Phase 2** (POC-specific activities)
   - Not required for parity
   - Can be added later if needed
   - Requires more design consideration

### For Future Enhancements

1. ⚠️ **Consider Epoch Activities** if:
   - Epoch debugging is frequently needed
   - Epoch lifecycle issues are common
   - Users request more detailed tracing

2. ⚠️ **Consider Edge/Buffer Activities** if:
   - Routing issues are hard to debug
   - Buffer contention needs investigation
   - Performance profiling requires this detail

---

## Summary

**Production Parity**: ✅ **Achievable with minimal changes**

The POC can implement the exact same ActivitySource pattern as production:
- Same activity types (Flow, Block)
- Same hierarchy
- Same error handling
- Same tagging patterns

The only differences are:
1. ActivitySource name: "DataFlow.POC" vs "Uniun.DataFlow"
2. Execution model: Graph-based vs sequential (doesn't affect activities)
3. Context propagation: Requires passing Activity through ExecutionPipeline

All of these are implementation details that don't affect the observable behavior or user experience.

**Next Steps**:
1. Implement Phase 1 (Flow + Block activities)
2. Test with production-like test scenarios
3. Validate OpenTelemetry integration
4. Consider Phase 2 enhancements based on user feedback
