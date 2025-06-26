# DataFlow Metrics Documentation

## Overview

The Uniun DataFlow library automatically emits comprehensive metrics to monitor the performance, health, and resource utilization of data processing pipelines. These metrics are exposed via .NET's standard `System.Diagnostics.Metrics` API and can be collected by any OpenTelemetry-compatible metrics collector.

## Metric Collection Setup

### Prerequisites
- .NET 6.0 or later
- OpenTelemetry metrics collection configured
- Prometheus or compatible metrics backend

### Configuration
```csharp
// Add DataFlow metrics to your service collection
services.AddDataFlowMetrics();
services.AddDataFlows();

// Configure OpenTelemetry to collect DataFlow metrics
services.AddOpenTelemetry()
    .WithMetrics(builder => builder
        .AddMeter("Uniun.DataFlow")  // DataFlow meter name
        .AddPrometheusExporter());
```

## Metrics Reference

### Flow-Level Metrics

#### `dataflow.flow.duration.ms`
**Type**: `Histogram<double>`  
**Unit**: `ms` (milliseconds)  
**Description**: Time taken to execute a DataFlow from start to completion  

**Labels**:
| Label | Type | Description | Example Values |
|-------|------|-------------|----------------|
| `dataflow.flow.invocationid` | `string` | Unique identifier for the flow execution instance | `550e8400-e29b-41d4-a716-446655440000` |
| `dataflow.flow.name` | `string` | Name of the DataFlow configuration | `OrderProcessingFlow`, `DataIngestionPipeline` |
| `dataflow.outcome` | `string` | Execution outcome indicator | `success`, `failure` |

**Usage**: Monitor overall flow performance, identify slow flows, calculate throughput
**Typical Values**: 100ms - 30s depending on flow complexity

---

#### Example Flow Metrics
```prometheus
# HELP dataflow_flow_duration_ms Time taken to execute a DataFlow from start to completion
# TYPE dataflow_flow_duration_ms histogram
dataflow_flow_duration_ms_bucket{dataflow_flow_invocationid="123e4567-e89b-12d3-a456-426614174000",dataflow_flow_name="OrderProcessingFlow",dataflow_outcome="success",le="100"} 45
dataflow_flow_duration_ms_bucket{dataflow_flow_invocationid="123e4567-e89b-12d3-a456-426614174000",dataflow_flow_name="OrderProcessingFlow",dataflow_outcome="success",le="+Inf"} 100
dataflow_flow_duration_ms_sum{dataflow_flow_invocationid="123e4567-e89b-12d3-a456-426614174000",dataflow_flow_name="OrderProcessingFlow",dataflow_outcome="success"} 15420.5
dataflow_flow_duration_ms_count{dataflow_flow_invocationid="123e4567-e89b-12d3-a456-426614174000",dataflow_flow_name="OrderProcessingFlow",dataflow_outcome="success"} 100
```

### Block-Level Metrics

#### `dataflow.block.duration.ms`
**Type**: `Histogram<double>`  
**Unit**: `ms` (milliseconds)  
**Description**: Time taken to complete execution of a block in a DataFlow  

**Labels**:
| Label | Type | Description | Example Values |
|-------|------|-------------|----------------|
| `dataflow.flow.invocationid` | `string` | Unique identifier for the flow execution instance | `550e8400-e29b-41d4-a716-446655440000` |
| `dataflow.flow.name` | `string` | Name of the DataFlow containing this block | `OrderProcessingFlow` |
| `dataflow.block.name` | `string` | Name of the specific block being measured | `order-validator`, `payment-processor`, `email-sender` |
| `dataflow.outcome` | `string` | Block execution outcome | `success`, `failure` |

**Usage**: Identify bottleneck blocks, monitor block-level performance, detect failing components
**Typical Values**: 10ms - 10s depending on block type and workload

---

### Channel Monitoring Metrics

#### `dataflow.channel.buffer.utilization`
**Type**: `ObservableGauge<int>`  
**Unit**: `%` (percentage)  
**Description**: Current utilization of a channel buffer used by a block  

**Labels**:
| Label | Type | Description | Example Values |
|-------|------|-------------|----------------|
| `dataflow.block.name` | `string` | Name of the block that owns this channel | `data-transformer`, `batch-processor` |
| `dataflow.block.capacity` | `string` | Maximum capacity of the channel buffer | `100`, `500`, `1000` |
| `dataflow.flow.name` | `string` | Name of the DataFlow containing this channel | `DataIngestionPipeline` |
| `dataflow.flow.invocationid` | `string` | Flow execution instance identifier | `550e8400-e29b-41d4-a716-446655440000` |

**Usage**: Monitor backpressure, identify channel bottlenecks, optimize buffer sizes
**Value Range**: 0-100 (percentage)
**Alerting Thresholds**: 
- Warning: >70%
- Critical: >90%

---

#### `dataflow.channel.active-count`
**Type**: `ObservableGauge<int>`  
**Unit**: count  
**Description**: Current number of active channels in the DataFlow system  

**Labels**:
| Label | Type | Description | Example Values |
|-------|------|-------------|----------------|
| `dataflow.active-channel-count` | `int` | Total count of currently active channels | `5`, `12`, `25` |

**Usage**: Monitor system resource usage, track channel lifecycle, capacity planning
**Typical Values**: 1-50 depending on flow complexity

---

### Example Channel Metrics
```prometheus
# HELP dataflow_channel_buffer_utilization Current utilization of channel buffer capacity between blocks
# TYPE dataflow_channel_buffer_utilization gauge
dataflow_channel_buffer_utilization{dataflow_block_name="order-validator",dataflow_block_capacity="100",dataflow_flow_name="OrderProcessingFlow",dataflow_flow_invocationid="123e4567-e89b-12d3-a456-426614174000"} 75

# HELP dataflow_channel_active_count Current number of active channels
# TYPE dataflow_channel_active_count gauge
dataflow_channel_active_count{dataflow_active_channel_count="8"} 8
```

## Global Tags

All metrics can include additional global tags configured via `DataFlowsOptions.MetricTags`:

```csharp
services.Configure<DataFlowsOptions>(options =>
{
    options.MetricTags.Add("dataflow.flow.tenantname", "customer-a");
});
```

**Common Global Tags**:
| Tag | Description | Example Values |
|-----|-------------|----------------|
| `dataflow.flow.tenantname` | Multi-tenant identifier for flow execution | `customer-a`, `customer-b`, `internal` |

## Metric Collection Frequency

| Metric Type | Collection Method | Frequency |
|-------------|-------------------|-----------|
| Flow Duration | Event-based | On flow completion |
| Block Duration | Event-based | On block completion |
| Channel Utilization | Observable | Every scrape interval (typically 15-30s) |
| Active Channel Count | Observable | Every scrape interval (typically 15-30s) |

## Performance Considerations

### Overhead
- **Histogram metrics**: ~1-5μs per recording
- **Observable gauges**: Calculated on-demand during scrape
- **Total overhead**: <5% of flow execution time under normal conditions

### Cardinality
- **Flow metrics**: Low cardinality (bounded by number of flow types)
- **Block metrics**: Medium cardinality (bounded by number of blocks × flows)
- **Channel metrics**: Medium cardinality (bounded by number of active channels)

**Estimated cardinality per service**:
- Small service: ~100-500 metric series
- Medium service: ~500-2000 metric series  
- Large service: ~2000-10000 metric series

## Monitoring and Alerting Recommendations

### Key Performance Indicators (KPIs)

1. **Flow Success Rate**
   ```promql
   sum(rate(dataflow_flow_duration_ms_count{dataflow_outcome="success"}[5m])) / 
   sum(rate(dataflow_flow_duration_ms_count[5m])) * 100
   ```

2. **Flow P95 Duration**
   ```promql
   histogram_quantile(0.95, 
     sum(rate(dataflow_flow_duration_ms_bucket[5m])) by (le, dataflow_flow_name))
   ```

3. **Channel Saturation**
   ```promql
   max(dataflow_channel_buffer_utilization) by (dataflow_flow_name)
   ```

### Recommended Alerts

#### Critical Alerts
- **Flow Failure Rate** > 5% over 5 minutes
- **Channel Utilization** > 90% for 2 minutes
- **Flow P95 Duration** > 2x baseline for 5 minutes

#### Warning Alerts  
- **Flow Failure Rate** > 1% over 5 minutes
- **Channel Utilization** > 70% for 5 minutes
- **Flow P95 Duration** > 1.5x baseline for 5 minutes

## Troubleshooting

### Common Issues

1. **Missing Metrics**
   - Verify `AddDataFlowMetrics()` is called
   - Check OpenTelemetry meter configuration includes "Uniun.DataFlow"
   - Ensure metrics endpoint is accessible

2. **High Cardinality**
   - Review flow and block naming conventions
   - Consider reducing label diversity
   - Implement metric sampling if needed

3. **Performance Impact**
   - Monitor metric collection overhead
   - Consider disabling metrics in development if needed
   - Use feature flags for metric collection control

### Debugging Commands

```bash
# Check if metrics endpoint is working
curl http://localhost:5000/metrics | grep dataflow

# Verify metric registration in Prometheus
curl -G http://prometheus:9090/api/v1/label/__name__/values | grep dataflow

# Check metric cardinality
curl -G http://prometheus:9090/api/v1/label/__name__/values | grep dataflow | wc -l
```