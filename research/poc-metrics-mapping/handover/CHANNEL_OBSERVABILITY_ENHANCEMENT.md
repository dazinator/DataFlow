# Enhancement: Improve POC Channel Observability Metrics

**Research Reference**: Research completed in `/research/poc-metrics-mapping/`

**Research Documentation**: See [Channel Monitoring Analysis](/research/poc-metrics-mapping/notes/channel-monitoring-analysis.md)

**Related Issues**: This is Phase 2+. Phase 1 (Core Metrics) should be completed first.

---

## Objective

Enhance channel observability in the POC codebase to achieve **100% parity** with production DataFlow metrics by implementing per-channel buffer utilization monitoring.

## Context

Phase 1 implementation provides 93% metrics parity (13/14 metrics) including:
- ✅ Flow and block lifecycle metrics
- ✅ ActivitySource tracing
- ✅ **Active channel count** (simple total)

**Missing**: Per-channel buffer utilization (queue depth percentage per channel)

This enhancement adds the 14th metric to reach 100% parity with production.

---

## Problem Statement

### Current State (After Phase 1)

The POC tracks the **total number of active channels** but does not provide visibility into:
- How full each channel is (utilization percentage)
- Which channels are becoming bottlenecks
- Queue depth trends per channel

### Production Capability

Production code uses `MonitoredChannel<T>` wrapper to track:
- Per-channel queue depth (current count)
- Per-channel capacity
- Per-channel utilization percentage (count/capacity * 100)
- Tags for each channel (BlockName, FlowName, etc.)

### Gap

Without per-channel utilization metrics:
- ❌ Cannot identify hot/full channels during performance issues
- ❌ Cannot correlate channel pressure with performance degradation
- ❌ Cannot tune channel capacities based on actual usage
- ❌ Missing 7% of production observability

---

## Proposed Solution

Implement **Option 2: Polling via Reflection** from research analysis.

### Why This Option?

| Approach | Parity | Effort | Complexity | Recommended |
|----------|--------|--------|------------|-------------|
| Option 1: MonitoredChannel wrapper | 100% | 3-4d | High | ❌ No - too complex |
| **Option 2: Reflection polling** | **100%** | **2-3d** | **Medium** | **✅ Yes** |
| Option 3: Active count only | 93% | 0.5d | Low | ✅ Done in Phase 1 |

**Rationale**:
- ✅ Achieves 100% parity without wrapper complexity
- ✅ Works with existing typed channel architecture
- ✅ No changes to TypedChannelFactory required
- ✅ Reflection overhead only during metric collection (pull-based)
- ✅ Can be implemented incrementally on top of Phase 1

---

## Detailed Approach

### 1. Channel Metadata Tracking

Create `ChannelMetadata` class to store channel information:

```csharp
public class ChannelMetadata
{
    public object Reader { get; set; }        // ChannelReader<T>
    public object Writer { get; set; }        // ChannelWriter<T>
    public Type DataType { get; set; }        // T
    public int Capacity { get; set; }         // Max capacity
    public string ChannelName { get; set; }   // Identifier
    public TagList Tags { get; set; }         // Metric tags
}
```

### 2. Track Channels in ExecutionPipeline

Extend `ExecutionPipeline` to track channel metadata:

```csharp
public class ExecutionPipeline
{
    public List<ChannelMetadata> TrackedChannels { get; } = new();
    
    // When creating channels (in BuildExecutionPipeline):
    var metadata = new ChannelMetadata
    {
        Reader = reader,
        Writer = writer,
        DataType = dataType,
        Capacity = capacity,
        ChannelName = bufferNode.GetName(),
        Tags = CreateChannelTags(bufferNode, flowName, invocationId)
    };
    TrackedChannels.Add(metadata);
}
```

### 3. Reflection-Based Polling

Add polling method to get channel utilizations:

```csharp
private IEnumerable<Measurement<int>> GetChannelUtilizations(ExecutionPipeline pipeline)
{
    foreach (var metadata in pipeline.TrackedChannels)
    {
        // Use reflection to get Count property from ChannelReader<T>
        var countProp = metadata.Reader.GetType().GetProperty("Count");
        if (countProp != null)
        {
            var count = (int)countProp.GetValue(metadata.Reader);
            if (metadata.Capacity > 0)
            {
                var utilization = (int)((double)count / metadata.Capacity * 100);
                yield return new Measurement<int>(utilization, metadata.Tags);
            }
        }
    }
}
```

### 4. Observable Gauge Integration

Register observable gauge that polls all channels:

```csharp
_channelBufferUtilization = meter.CreateObservableGauge<int>(
    "dataflow.channel.buffer.utilization",
    () => GetChannelUtilizations(_currentPipeline),
    unit: "%",
    description: "Current utilization of channel buffer");
```

---

## Implementation Checklist

### 1. Create Channel Metadata Infrastructure

- [ ] Create `ChannelMetadata.cs` class
  - Reader, Writer, DataType, Capacity, ChannelName, Tags
- [ ] Add `TrackedChannels` list to ExecutionPipeline
- [ ] Create helper method to build channel tags

### 2. Track Channels During Graph Build

- [ ] Modify `BuildExecutionPipeline()` in DataFlowGraph
  - Create metadata when creating buffer channels
  - Create metadata when creating edge channels
  - Add to TrackedChannels list
- [ ] Pass FlowName and InvocationId for tagging

### 3. Implement Reflection-Based Polling

- [ ] Create `GetChannelUtilizations()` method
  - Use reflection to access `ChannelReader<T>.Count`
  - Calculate utilization percentage
  - Return measurements with tags
- [ ] Handle edge cases:
  - Null/disposed channels
  - Zero capacity (unbounded channels - skip)
  - Reflection failures (log and skip)

### 4. Add Observable Gauge

- [ ] Add `_channelBufferUtilization` field to DataFlowMetrics
- [ ] Register in constructor with meter
- [ ] Wire to polling method
- [ ] Track current pipeline reference for polling

### 5. Pipeline Lifecycle Management

- [ ] Add mechanism to set current pipeline in metrics system
  - When graph execution starts
  - When graph execution completes
- [ ] Handle null pipeline (no active execution)
- [ ] Consider concurrent executions (if needed)

### 6. Testing

- [ ] Create `ChannelUtilizationTests.cs`
- [ ] Test empty channel (0% utilization)
- [ ] Test partially full channel (50% utilization)
- [ ] Test full channel (100% utilization)
- [ ] Test multiple channels with different utilizations
- [ ] Test channel tags are correct
- [ ] Test reflection failure handling
- [ ] Integration test with MetricCollector

---

## Test Scenarios

### Per-Channel Utilization Test

```csharp
[Fact]
public async Task Should_Report_Channel_Utilization_Per_Channel()
{
    // Arrange
    var collector = new MetricCollector<int>(
        meterFactory, 
        "DataFlow.POC", 
        "dataflow.channel.buffer.utilization");
    
    var graph = CreateGraphWithBuffers(); // Creates 2 channels
    
    // Act
    // Fill one channel to 50%
    await FillChannelPartially("buffer1", 50);
    // Fill another channel to 75%
    await FillChannelPartially("buffer2", 75);
    
    collector.RecordObservableInstruments();
    
    // Assert
    var measurements = collector.GetMeasurementSnapshot();
    Assert.Equal(2, measurements.Count); // 2 channels
    
    var buffer1 = measurements
        .Single(m => m.Tags.Any(t => t.Key == "dataflow.block.name" && t.Value == "buffer1"));
    Assert.Equal(50, buffer1.Value);
    
    var buffer2 = measurements
        .Single(m => m.Tags.Any(t => t.Key == "dataflow.block.name" && t.Value == "buffer2"));
    Assert.Equal(75, buffer2.Value);
}
```

### Side-by-Side Comparison Test

```csharp
[Fact]
public async Task POC_Channel_Metrics_Should_Match_Production_Pattern()
{
    // Verify POC emits same metrics as production:
    // - Same metric names
    // - Same tag names
    // - Same calculation (count/capacity * 100)
    
    // Compare POC output with production reference
}
```

---

## Success Criteria

### Functional Requirements

- [ ] ✅ Channel buffer utilization metric emits correctly
- [ ] ✅ Per-channel utilization calculated accurately (count/capacity * 100)
- [ ] ✅ Each channel has correct tags (ChannelName, FlowName, InvocationId)
- [ ] ✅ Multiple channels tracked independently
- [ ] ✅ Reflection handles edge cases gracefully
- [ ] ✅ 100% parity with production metrics achieved (14/14 metrics)

### Testing Requirements

- [ ] ✅ Unit tests for channel metadata tracking
- [ ] ✅ Unit tests for utilization calculation
- [ ] ✅ Unit tests for reflection-based polling
- [ ] ✅ Integration tests with MetricCollector
- [ ] ✅ Side-by-side comparison with production shows parity

### Performance Requirements

- [ ] ✅ Reflection overhead acceptable (only during metric collection)
- [ ] ✅ No performance regression in graph execution
- [ ] ✅ Memory overhead minimal (metadata storage only)
- [ ] ✅ Observable gauge uses pull-based polling (no continuous overhead)

---

## Design References

### Research Documentation

- **Channel Monitoring Analysis**: `/research/poc-metrics-mapping/notes/channel-monitoring-analysis.md`
  - See "Option 2: Polling via Reflection" section
  - Complete implementation details
  - Comparison with other options
- **Main Research**: `/research/poc-metrics-mapping/README.md`
- **POC Mapping**: `/research/poc-metrics-mapping/notes/poc-mapping-analysis.md`

### Production Code References

- **MonitoredChannel**: `/src/DataFlow/Metrics/MonitoredChannel.cs`
- **IMonitoredChannel**: `/src/DataFlow/Metrics/IMonitoredChannel.cs`
- **ChannelRegistry**: Internal to DataFlowMetrics.cs

---

## Risks and Mitigations

### Risk 1: Reflection Performance

**Risk**: Reflection overhead might impact metric collection performance

**Mitigation**: 
- Observable gauge is pull-based (only when metrics collected)
- Cache PropertyInfo for Count property
- Skip reflection if channel is disposed/null
- Benchmark to ensure < 1ms per channel

### Risk 2: Channel Lifecycle

**Risk**: Channels might be disposed while polling

**Mitigation**:
- Try-catch around reflection calls
- Skip channels that throw exceptions
- Log warnings for investigation
- Clean up metadata when pipeline completes

### Risk 3: Concurrent Executions

**Risk**: Multiple graphs executing concurrently might conflict

**Mitigation**:
- For MVP: Assume single execution at a time
- If needed: Track multiple pipelines per invocation ID
- Document limitation if concurrent not supported

---

## Dependencies

### Required

- ✅ Phase 1 implementation complete
- ✅ IDataFlowMetrics interface exists
- ✅ DataFlowMetrics class exists
- ✅ ExecutionPipeline accessible from metrics system

### Optional

- ⚠️ Performance profiling tools (to measure reflection overhead)
- ⚠️ Channel lifecycle event system (for automatic cleanup)

---

## Acceptance Criteria

### Must Have

- [ ] ✅ Channel buffer utilization metric emits for all channels
- [ ] ✅ Utilization calculated correctly via reflection
- [ ] ✅ All tests pass
- [ ] ✅ 100% parity with production metrics (14/14)
- [ ] ✅ Performance overhead acceptable
- [ ] ✅ Side-by-side comparison validates parity

### Nice to Have

- [ ] ⚠️ Cache reflection PropertyInfo for performance
- [ ] ⚠️ Support concurrent graph executions
- [ ] ⚠️ Automatic metadata cleanup on pipeline disposal

---

## Estimated Effort

**Phase 2 (this issue)**: 2-3 days

**Success Metric**: 100% parity with production metrics (14/14 metrics implemented)

---

## Alternatives Considered

### Option 1: MonitoredChannel Wrapper

**Pros**: Exact production parity, no reflection
**Cons**: High complexity, requires TypedChannelFactory refactoring, 3-4 days effort
**Decision**: Rejected - too complex for POC

### Option 3: Sampled Utilization

**Pros**: Lower overhead, simpler implementation
**Cons**: Only one channel sampled, might miss bottlenecks
**Decision**: Rejected - doesn't meet 100% parity goal

---

## Future Enhancements (Out of Scope)

If reflection polling proves insufficient, consider:
- **Option 1**: Implement MonitoredChannel wrapper via reflection
- **Custom approach**: Create POC-specific monitoring that leverages ExecutionPipeline
- **Epoch-aware**: Add epoch-specific channel metrics (POC enhancement beyond production)

---

## Notes

- This is a **backlog item** - implement only if per-channel visibility is needed
- Phase 1 (93% parity) provides excellent observability for most scenarios
- Trigger: Users report channel bottlenecks that can't be diagnosed with active count alone
- Success pattern: Identify and fix channel capacity issues using utilization metrics

---

## Questions?

See research documentation:
- [Channel Monitoring Analysis](/research/poc-metrics-mapping/notes/channel-monitoring-analysis.md) - Complete analysis
- [Research README](/research/poc-metrics-mapping/README.md) - Overview
