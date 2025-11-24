# Implementation: POC Metrics and ActivitySource

**Research Reference**: Research completed in `/research/poc-metrics-mapping/`

**Research Documentation**: See [Research README](/research/poc-metrics-mapping/README.md)

---

## Objective

Implement metrics collection and ActivitySource tracing in the POC codebase to achieve parity with production DataFlow metrics implementation.

## Approach (Validated by Research)

### Phase 1: Core Metrics (93% Parity) - Primary Deliverable

Implement 13 of 14 production metrics plus ActivitySource tracing:

1. **IDataFlowMetrics Interface** - Metrics collection interface
2. **Flow Lifecycle Metrics** - Flow started, completed, duration, active count
3. **Block Lifecycle Metrics** - Block started, completed, duration, execution count, active count
4. **Processing Metrics** - Operations completed, items processed (user opt-in)
5. **ActiveSource Tracing** - Flow and Block activities with proper hierarchy
6. **Active Channel Count** - Simple channel count tracking

### Phase 2: Enhanced Channel Monitoring (Optional)

If detailed per-channel monitoring is needed:
- Implement channel buffer utilization via polling (100% parity)
- See [Channel Monitoring Analysis](/research/poc-metrics-mapping/notes/channel-monitoring-analysis.md)

---

## Success Criteria

### Functional Requirements

- [ ] All Phase 1 metrics emit correctly (13 metrics)
- [ ] ActivitySource creates Flow and Block activities
- [ ] Activity hierarchy: Flow (parent) → Blocks (children)
- [ ] Metrics tagged with FlowName, BlockName, InvocationId
- [ ] Error scenarios set proper activity status and tags
- [ ] Cancellation scenarios handled correctly
- [ ] User blocks can access metrics via IExecutionContext

### Testing Requirements

- [ ] Unit tests for each metric using MetricCollector
- [ ] Activity tests using ActivityListener
- [ ] Integration tests with OpenTelemetry
- [ ] Error scenario tests
- [ ] Cancellation scenario tests
- [ ] Side-by-side metric output comparison with production

### Performance Requirements

- [ ] Minimal overhead when metrics are disabled (null check)
- [ ] No performance regression in hot path
- [ ] Observable gauges use pull-based polling (no continuous overhead)

---

## Test Scenarios

### Metric Emission Tests

```csharp
[Fact]
public async Task Should_Record_Flow_Duration_Histogram()
{
    // Arrange
    var collector = new MetricCollector<double>(
        meterFactory, "DataFlow.POC", "dataflow.flow.duration.ms");
    
    // Act
    await graph.ExecuteAsync(context);
    
    // Assert
    var measurements = collector.GetMeasurementSnapshot();
    Assert.Single(measurements);
    Assert.True(measurements[0].Value > 0);
    Assert.Contains(measurements[0].Tags, t => t.Key == "dataflow.flow.name");
}

[Fact]
public async Task Should_Track_Active_Block_Count()
{
    // Arrange
    var collector = new MetricCollector<int>(
        meterFactory, "DataFlow.POC", "dataflow.block.active-count");
    
    var graph = CreateGraphWithSlowBlocks(); // Blocks that run for 1 second
    
    // Act
    var executeTask = graph.ExecuteAsync(context);
    await Task.Delay(500); // Sample while blocks are running
    collector.RecordObservableInstruments();
    
    // Assert
    var measurements = collector.GetMeasurementSnapshot();
    var activeCount = measurements.Single().Value;
    Assert.True(activeCount > 0); // At least some blocks should be active
    
    await executeTask;
}
```

### Activity Hierarchy Tests

```csharp
[Fact]
public async Task Should_Create_Flow_Activity_As_Parent()
{
    // Arrange
    var activities = new List<Activity>();
    using var listener = CreateActivityListener(activities);
    
    // Act
    await graph.ExecuteAsync(context);
    
    // Assert
    var flowActivity = activities.Single(a => a.OperationName == "Flow");
    Assert.NotNull(flowActivity);
    Assert.Equal("Flow {FlowName}", flowActivity.DisplayName);
}

[Fact]
public async Task Should_Create_Block_Activities_As_Children_Of_Flow()
{
    // Arrange
    var activities = new List<Activity>();
    using var listener = CreateActivityListener(activities);
    
    // Act
    await graph.ExecuteAsync(context);
    
    // Assert
    var flowActivity = activities.Single(a => a.OperationName == "Flow");
    var blockActivities = activities.Where(a => a.OperationName == "Block").ToList();
    
    Assert.NotEmpty(blockActivities);
    foreach (var blockActivity in blockActivities)
    {
        Assert.Equal(flowActivity.Context, blockActivity.ParentContext);
    }
}
```

### Error Handling Tests

```csharp
[Fact]
public async Task Failed_Flow_Should_Set_Error_Status_And_Tags()
{
    // Arrange
    var activities = new List<Activity>();
    using var listener = CreateActivityListener(activities);
    var graph = CreateFailingGraph();
    
    // Act & Assert
    await Assert.ThrowsAsync<InvalidOperationException>(() => 
        graph.ExecuteAsync(context));
    
    var flowActivity = activities.Single(a => a.OperationName == "Flow");
    Assert.Equal(ActivityStatusCode.Error, flowActivity.Status);
    Assert.Contains(flowActivity.Tags, t => t.Key == "error.type");
}

[Fact]
public async Task Cancelled_Flow_Should_Set_Cancelled_Tag()
{
    // Arrange
    var activities = new List<Activity>();
    using var listener = CreateActivityListener(activities);
    var cts = new CancellationTokenSource();
    var context = CreateContextWithCancellation(cts.Token);
    
    // Act & Assert
    var task = graph.ExecuteAsync(context);
    cts.CancelAfter(100);
    await Assert.ThrowsAsync<OperationCanceledException>(() => task);
    
    var flowActivity = activities.Single(a => a.OperationName == "Flow");
    Assert.Contains(flowActivity.Tags, t => t.Key == "cancelled" && t.Value?.ToString() == "true");
}
```

### User Block Metrics Tests

```csharp
[Fact]
public async Task User_Block_Can_Record_Operations_Completed()
{
    // Arrange
    var collector = new MetricCollector<long>(
        meterFactory, "DataFlow.POC", "dataflow.block.operations");
    
    var graph = CreateGraphWithMetricsBlock(); // Block that calls context.Metrics
    
    // Act
    await graph.ExecuteAsync(context);
    
    // Assert
    var measurements = collector.GetMeasurementSnapshot();
    Assert.NotEmpty(measurements);
    var total = measurements.Sum(m => m.Value);
    Assert.True(total > 0);
}
```

---

## Performance Requirements

### Baseline (No Metrics)

Ensure no regression when metrics are disabled:
- Graph execution time: No measurable difference
- Memory allocation: Minimal increase (only for null checks)
- CPU usage: No measurable increase

### With Metrics Enabled

Acceptable overhead:
- Graph execution time: < 5% increase
- Memory allocation: Modest increase for metric tags
- CPU usage: < 5% increase

Observable gauge polling:
- No continuous overhead
- Only when metrics are collected (pull-based)

---

## Design References

### Production Code References

- **Metrics Interface**: `/src/DataFlow/Metrics/IDataFlowMetrics.cs`
- **Metrics Implementation**: `/src/DataFlow/Metrics/DataFlowMetrics.cs`
- **Activity Names**: `/src/DataFlow/Metrics/ActivityNames.cs`
- **ActivitySource Usage**: `/src/DataFlow/DataFlow.cs`
- **OpenTelemetry Extensions**: `/src/DataFlow.OpenTelemetry/MeterProviderBuilderExtensions.cs`

### Research Documentation

- **Main Research**: `/research/poc-metrics-mapping/README.md`
- **Production Inventory**: `/research/poc-metrics-mapping/notes/production-metrics-inventory.md`
- **POC Mapping**: `/research/poc-metrics-mapping/notes/poc-mapping-analysis.md`
- **ActivitySource Design**: `/research/poc-metrics-mapping/notes/activitysource-design.md`
- **Channel Monitoring**: `/research/poc-metrics-mapping/notes/channel-monitoring-analysis.md`

---

## Implementation Checklist

### 1. Create Metrics Infrastructure

- [ ] Create `/poc/DataFlow.POC/Observability/` folder
- [ ] Create `IDataFlowMetrics.cs` interface (can reuse production interface)
- [ ] Create `DataFlowMetrics.cs` implementation
- [ ] Create `ActivityNames.cs` constants
- [ ] Create metric tag context classes (DataFlowMetricsTagsContext, etc.)
- [ ] Create `IMeterAccessor.cs` and implementation

### 2. Extend ExecutionContext

- [ ] Add `FlowName` property to `IExecutionContext`
- [ ] Add `Metrics` property to `IExecutionContext`
- [ ] Add `CurrentBlockName` property to `IExecutionContext`
- [ ] Update `ExecutionContext` implementation

### 3. Add Metrics to DataFlowGraph

- [ ] Add `IDataFlowMetrics?` field to DataFlowGraph
- [ ] Inject via constructor (optional dependency)
- [ ] Add `ActivitySource` static field
- [ ] Emit flow started metric in `ExecuteAsync()`
- [ ] Wrap execution with Flow activity
- [ ] Emit flow completed metric in finally block
- [ ] Set activity status on success/error
- [ ] Add error tags for exceptions

### 4. Add Metrics to BlockRuntimeModel

- [ ] Add metrics reference to ExecutionPipeline
- [ ] Add FlowName to ExecutionPipeline
- [ ] Pass Activity context through ExecuteBlocksAsync → StartBlockTask → ExecuteAsync
- [ ] Emit block started metric in `BlockRuntimeModel.ExecuteAsync()`
- [ ] Wrap execution with Block activity
- [ ] Set context.CurrentBlockName before block execution
- [ ] Emit block completed metric in finally block
- [ ] Set activity status on success/error

### 5. Add Channel Count Tracking

- [ ] Add `GetActiveChannelCount()` to ExecutionPipeline
- [ ] Count EdgeRuntimeModels + BufferRuntimeModels
- [ ] Add observable gauge for active channel count
- [ ] Test channel count changes during execution

### 6. Create OpenTelemetry Extensions

- [ ] Create `MeterProviderBuilderExtensions.cs`
- [ ] Create `TracerProviderBuilderExtensions.cs`
- [ ] Add `AddDataFlowPOC()` methods
- [ ] Test with actual OTLP exporter

### 7. Write Comprehensive Tests

- [ ] Create `/poc/DataFlow.POC.Tests/Observability/` folder
- [ ] Create `MetricsTests.cs` - Test each metric
- [ ] Create `ActivityTracingTests.cs` - Test activities
- [ ] Create `ErrorHandlingTests.cs` - Test error scenarios
- [ ] Test with MetricCollector
- [ ] Test with ActivityListener
- [ ] Integration tests with OpenTelemetry

### 8. Validation

- [ ] Run all tests and ensure they pass
- [ ] Compare metric output with production
- [ ] Verify activity hierarchy with tracing tools
- [ ] Performance test (ensure < 5% overhead)
- [ ] Test with OpenTelemetry collector

---

## Code Locations

### New Files (to create)

```
/poc/DataFlow.POC/Observability/
├── IDataFlowMetrics.cs
├── DataFlowMetrics.cs
├── ActivityNames.cs
├── DataFlowMetricsTagsContext.cs
├── BlockMetricsTagsContext.cs
├── DataItemMetricsContext.cs
├── IMeterAccessor.cs
├── MeterAccessor.cs
├── MeterProviderBuilderExtensions.cs
└── TracerProviderBuilderExtensions.cs

/poc/DataFlow.POC.Tests/Observability/
├── MetricsTests.cs
├── ActivityTracingTests.cs
└── ErrorHandlingTests.cs
```

### Files to Modify

```
/poc/DataFlow.POC/Core/
├── DataFlowGraph.cs (add metrics emission, ActivitySource)
├── IExecutionContext.cs (extend with FlowName, Metrics, CurrentBlockName)
└── ExecutionContext.cs (implement new properties)

/poc/DataFlow.POC.csproj (add OpenTelemetry packages)
```

---

## Dependencies

### NuGet Packages

Add to `/poc/DataFlow.POC.csproj`:

```xml
<PackageReference Include="System.Diagnostics.DiagnosticSource" Version="8.0.0" />
<PackageReference Include="OpenTelemetry" Version="1.6.0" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.6.0" />
```

Add to `/poc/DataFlow.POC.Tests.csproj`:

```xml
<PackageReference Include="Microsoft.Extensions.Diagnostics.Metrics.Testing" Version="8.0.0" />
<PackageReference Include="OpenTelemetry.Exporter.InMemory" Version="1.6.0" />
```

---

## Acceptance Criteria

### Must Have (Phase 1)

- [ ] ✅ All 13 core metrics emit correctly
- [ ] ✅ ActivitySource creates Flow and Block activities
- [ ] ✅ Proper activity hierarchy (Flow → Blocks)
- [ ] ✅ Error and cancellation handling works
- [ ] ✅ All tests pass
- [ ] ✅ Side-by-side metric comparison with production shows parity
- [ ] ✅ OpenTelemetry integration works
- [ ] ✅ Performance overhead < 5%

### Nice to Have (Phase 2)

- [ ] ⚠️ Channel buffer utilization metrics (if needed)
- [ ] ⚠️ Epoch-specific activities (POC enhancement)
- [ ] ⚠️ Edge routing activities (POC enhancement)

---

## Estimated Effort

- **Phase 1** (Core Metrics): 1-2 days
- **Phase 2** (Enhanced Monitoring): 2-3 days (if needed)
- **Total**: 1-2 days for primary deliverable, 3-5 days if Phase 2 needed

---

## Notes

- Start with Phase 1 (93% parity) - this provides excellent observability
- Phase 2 (100% parity) can be implemented later if detailed channel monitoring is needed
- Metrics should be optional (null checks) to avoid coupling
- Follow production patterns for consistency
- Reuse production test patterns (MetricCollector, ActivityListener)
- Use TDD approach (write tests first)

---

## Questions?

See research documentation:
- [Research README](/research/poc-metrics-mapping/README.md) - Start here
- [POC Mapping Analysis](/research/poc-metrics-mapping/notes/poc-mapping-analysis.md) - Detailed mappings
- [ActivitySource Design](/research/poc-metrics-mapping/notes/activitysource-design.md) - Activity implementation
- [Channel Monitoring Analysis](/research/poc-metrics-mapping/notes/channel-monitoring-analysis.md) - Channel options
