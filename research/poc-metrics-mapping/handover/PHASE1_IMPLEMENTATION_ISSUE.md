# Implementation: POC Core Metrics and ActivitySource (Phase 1)

**Research Reference**: Research completed in `/research/poc-metrics-mapping/`

**Research Documentation**: See [Research README](/research/poc-metrics-mapping/README.md)

**Related Issues**: This is Phase 1. For enhanced channel monitoring (Phase 2), see separate backlog issue.

---

## Objective

Implement core metrics collection and ActivitySource tracing in the POC codebase to achieve **93% parity** with production DataFlow metrics implementation.

## Scope: Phase 1 Only

This issue covers **Phase 1: Core Metrics** which provides 13 of 14 production metrics:

1. **IDataFlowMetrics Interface** - Metrics collection interface
2. **Flow Lifecycle Metrics** - Flow started, completed, duration, active count
3. **Block Lifecycle Metrics** - Block started, completed, duration, execution count, active count
4. **Processing Metrics** - Operations completed, items processed (user opt-in)
5. **ActivitySource Tracing** - Flow and Block activities with proper hierarchy
6. **Active Channel Count** - Simple channel count tracking (total count only)

**Not in Scope** (Phase 2 - separate issue):
- ❌ Per-channel buffer utilization metrics
- ❌ Channel metadata tracking
- ❌ Reflection-based channel polling

---

## Success Criteria

### Functional Requirements

- [ ] All 13 Phase 1 metrics emit correctly:
  - Flow: started, completed, duration, active count (4 metrics)
  - Block: started, completed, duration, execution count, active count (5 metrics)
  - Processing: operations completed, items processed (2 metrics)
  - Channels: active count (1 metric)
  - ActivitySource: Flow + Block activities (1 metric system)
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
- [ ] Side-by-side metric output comparison with production (for implemented metrics)

### Performance Requirements

- [ ] Minimal overhead when metrics are disabled (null check)
- [ ] No performance regression in hot path
- [ ] Observable gauges use pull-based polling (no continuous overhead)
- [ ] Graph execution time: < 5% increase with metrics enabled
- [ ] Memory allocation: Modest increase for metric tags only

---

## Implementation Checklist

### 1. Create Metrics Infrastructure

- [ ] Create `/poc/DataFlow.POC/Observability/` folder
- [ ] Create `IDataFlowMetrics.cs` interface
  - Include all methods from production interface
  - Document which methods are implemented in Phase 1
- [ ] Create `DataFlowMetrics.cs` implementation
  - Implement 13 metrics (skip channel buffer utilization for now)
  - Add `GetActiveChannelCount()` method (simple count)
- [ ] Create `ActivityNames.cs` constants
  - Flow and Block activity names
  - Tag name constants
- [ ] Create metric tag context classes
  - `DataFlowMetricsTagsContext`
  - `BlockMetricsTagsContext`
  - `DataItemMetricsContext`
- [ ] Create `IMeterAccessor.cs` and implementation

### 2. Extend ExecutionContext

- [ ] Add `FlowName` property to `IExecutionContext`
- [ ] Add `Metrics` property to `IExecutionContext` (nullable)
- [ ] Add `CurrentBlockName` property to `IExecutionContext`
- [ ] Update `ExecutionContext` implementation with new properties

### 3. Add Metrics to DataFlowGraph

- [ ] Add `IDataFlowMetrics?` field to DataFlowGraph
- [ ] Inject via constructor (optional dependency)
- [ ] Add `ActivitySource` static field
- [ ] Emit flow started metric in `ExecuteAsync()`
- [ ] Wrap execution with Flow activity
- [ ] Emit flow completed metric in finally block
- [ ] Set activity status on success/error
- [ ] Add error tags for exceptions (error.type, cancelled)

### 4. Add Metrics to BlockRuntimeModel

- [ ] Add metrics reference to ExecutionPipeline
- [ ] Add FlowName to ExecutionPipeline
- [ ] Pass Activity context through ExecuteBlocksAsync → StartBlockTask → ExecuteAsync
- [ ] Emit block started metric in `BlockRuntimeModel.ExecuteAsync()`
- [ ] Wrap execution with Block activity (child of Flow activity)
- [ ] Set context.CurrentBlockName before block execution
- [ ] Emit block completed metric in finally block
- [ ] Set activity status on success/error

### 5. Add Active Channel Count Tracking

- [ ] Add `GetActiveChannelCount()` to ExecutionPipeline
  - Count: `EdgeRuntimeModels.Count + BufferRuntimeModels.Count`
- [ ] Add observable gauge for active channel count in DataFlowMetrics
- [ ] Test channel count changes during execution

### 6. Create OpenTelemetry Extensions

- [ ] Create `MeterProviderBuilderExtensions.cs`
  - `AddDataFlowPOC()` method to register meter
- [ ] Create `TracerProviderBuilderExtensions.cs`
  - `AddDataFlowPOC()` method to register activity source
- [ ] Test with actual OTLP exporter

### 7. Write Comprehensive Tests

- [ ] Create `/poc/DataFlow.POC.Tests/Observability/` folder
- [ ] Create `MetricsTests.cs`
  - Test each of 13 metrics individually
  - Test metric tags are correct
- [ ] Create `ActivityTracingTests.cs`
  - Test Flow activity creation
  - Test Block activity creation
  - Test parent-child relationship
  - Test error status handling
- [ ] Create `ErrorHandlingTests.cs`
  - Test exception scenarios
  - Test cancellation scenarios
- [ ] Create integration tests with OpenTelemetry collector

### 8. Validation

- [ ] Run all tests and ensure they pass
- [ ] Compare metric output with production (for implemented metrics)
- [ ] Verify activity hierarchy with tracing tools (e.g., Jaeger, Zipkin)
- [ ] Performance test (ensure < 5% overhead)
- [ ] Test with OpenTelemetry collector and verify export

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
public async Task Should_Track_Active_Channel_Count()
{
    // Arrange
    var collector = new MetricCollector<int>(
        meterFactory, "DataFlow.POC", "dataflow.channel.active-count");
    
    var graph = CreateGraphWithMultipleBuffers(); // Creates 3 channels
    
    // Act
    var executeTask = graph.ExecuteAsync(context);
    await Task.Delay(100);
    collector.RecordObservableInstruments();
    
    // Assert
    var measurements = collector.GetMeasurementSnapshot();
    var count = measurements.Single().Value;
    Assert.Equal(3, count);
}
```

### Activity Hierarchy Tests

```csharp
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
```

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

### Must Have

- [ ] ✅ All 13 core metrics emit correctly
- [ ] ✅ ActivitySource creates Flow and Block activities with proper hierarchy
- [ ] ✅ Error and cancellation handling works
- [ ] ✅ All tests pass
- [ ] ✅ Side-by-side metric comparison with production shows parity for implemented metrics
- [ ] ✅ OpenTelemetry integration works
- [ ] ✅ Performance overhead < 5%
- [ ] ✅ Metrics are optional (null checks prevent coupling)

### Out of Scope (See Phase 2 Issue)

- ❌ Channel buffer utilization per channel
- ❌ Per-channel queue depth percentage
- ❌ Reflection-based polling of channel metrics

---

## Estimated Effort

**Phase 1 (this issue)**: 1-2 days

**Success Metric**: 93% parity with production metrics (13/14 metrics implemented)

---

## Notes

- Metrics should be optional (null checks) to avoid coupling
- Follow production patterns for consistency
- Reuse production test patterns (MetricCollector, ActivityListener)
- Use TDD approach (write tests first)
- Active channel count is simple total (EdgeRuntimeModels + BufferRuntimeModels count)
- Per-channel utilization deferred to Phase 2 (separate issue)

---

## Next Steps After Completion

Once Phase 1 is complete and validated:
1. Close this issue
2. Evaluate if Phase 2 (enhanced channel monitoring) is needed
3. If needed, implement Phase 2 from backlog issue
4. If not needed, we have achieved 93% parity which provides excellent observability

---

## Questions?

See research documentation:
- [Research README](/research/poc-metrics-mapping/README.md) - Start here
- [POC Mapping Analysis](/research/poc-metrics-mapping/notes/poc-mapping-analysis.md) - Detailed mappings
- [ActivitySource Design](/research/poc-metrics-mapping/notes/activitysource-design.md) - Activity implementation
