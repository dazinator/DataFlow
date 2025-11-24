# Production Metrics Inventory

This document catalogs all metrics implemented in the production DataFlow code.

## Source Files

- **Interface**: `/src/DataFlow/Metrics/IDataFlowMetrics.cs`
- **Implementation**: `/src/DataFlow/Metrics/DataFlowMetrics.cs`
- **Activity Names**: `/src/DataFlow/Metrics/ActivityNames.cs`
- **Usage**: `/src/DataFlow/DataFlow.cs`

## IDataFlowMetrics Interface Methods

```csharp
public interface IDataFlowMetrics
{
    // Channel Monitoring
    IChannelMonitoringLease RegisterChannel(IMonitoredChannel channel);
    
    // Flow Lifecycle
    void FlowStarted(DataFlowMetricsTagsContext context);
    void FlowCompleted(DataFlowMetricsTagsContext metricsContext, double durationMs);
    
    // Block Lifecycle
    void BlockStarted(BlockMetricsTagsContext metricsContext);
    void BlockCompleted(BlockMetricsTagsContext metricsContext, double durationTotalMs);
    
    // Processing Metrics
    void BlockOperationsCompleted(BlockMetricsTagsContext context, long count);
    void ItemsProcessed(DataItemMetricsContext context, long count);
    
    // Global Tags
    TagList GlobalTags { get; }
}
```

## Metrics Instruments (from DataFlowMetrics.cs)

### 1. Histograms (Time-based distributions)

#### 1.1 Block Processing Duration
- **Name**: `dataflow.block.duration.ms`
- **Type**: `Histogram<double>`
- **Unit**: milliseconds
- **Description**: Time taken to complete execution of a block in a DataFlow
- **Tags**: BlockName, FlowName, FlowInvocationId, Outcome
- **Recorded**: When block completes (success or failure)

#### 1.2 Flow Execution Duration
- **Name**: `dataflow.flow.duration.ms`
- **Type**: `Histogram<double>`
- **Unit**: milliseconds
- **Description**: DataFlow execution time histogram for performance analysis
- **Tags**: FlowName, FlowInvocationId, Outcome
- **Recorded**: When flow completes (success or failure)

### 2. Counters (Monotonically increasing)

#### 2.1 Flow Execution Started Count
- **Name**: `dataflow.flow.executions.start`
- **Type**: `Counter<long>`
- **Unit**: execution
- **Description**: Number of started flow executions
- **Tags**: FlowName, FlowInvocationId
- **Recorded**: When flow starts

#### 2.2 Flow Execution Completion Count
- **Name**: `dataflow.flow.executions`
- **Type**: `Counter<long>`
- **Unit**: execution
- **Description**: Number of completed flow executions
- **Tags**: FlowName, FlowInvocationId, Outcome
- **Recorded**: When flow completes

#### 2.3 Block Execution Count
- **Name**: `dataflow.block.executions`
- **Type**: `Counter<long>`
- **Unit**: execution
- **Description**: Number of completed block executions
- **Tags**: BlockName, FlowName, FlowInvocationId, Outcome
- **Recorded**: When block completes

#### 2.4 Block Operations Completed
- **Name**: `dataflow.block.operations`
- **Type**: `Counter<long>`
- **Unit**: operation
- **Description**: Number of operations a block is completing (e.g., batches, transforms)
- **Tags**: BlockName, FlowName, FlowInvocationId
- **Recorded**: During block processing (user-initiated via BlockOperationsCompleted())

#### 2.5 Data Items Processed
- **Name**: `dataflow.block.items.processed`
- **Type**: `Counter<long>`
- **Unit**: item
- **Description**: Number of business data items processed within stream items
- **Tags**: BlockName, FlowName, FlowInvocationId, DataLabel
- **Recorded**: During block processing (user-initiated via ItemsProcessed())

### 3. UpDownCounters (Can increase and decrease)

#### 3.1 Active Flow Count
- **Name**: `dataflow.flow.active-count`
- **Type**: `UpDownCounter<int>`
- **Unit**: flow
- **Description**: Number of currently executing flows
- **Tags**: FlowName, FlowInvocationId
- **Recorded**: +1 when flow starts, -1 when flow completes

#### 3.2 Active Block Count
- **Name**: `dataflow.block.active-count`
- **Type**: `UpDownCounter<int>`
- **Unit**: block
- **Description**: Number of currently executing blocks
- **Tags**: BlockName, FlowName, FlowInvocationId
- **Recorded**: +1 when block starts, -1 when block completes

### 4. Observable Gauges (Callback-based observation)

#### 4.1 Channel Buffer Utilization
- **Name**: `dataflow.channel.buffer.utilization`
- **Type**: `ObservableGauge<int>`
- **Unit**: percentage (%)
- **Description**: Current utilization of a channel buffer used by a block
- **Tags**: BlockName, FlowName, FlowInvocationId
- **Callback**: Polls all registered MonitoredChannel instances
- **Calculation**: (CurrentCount / Capacity) * 100

#### 4.2 Active Channel Count
- **Name**: `dataflow.channel.active-count`
- **Type**: `ObservableGauge<int>`
- **Unit**: count
- **Description**: Current number of active channels
- **Tags**: Global tags only
- **Callback**: Counts active channels in registry

## Tag Names

From `DataFlowMetrics.TagNames`:

- `dataflow.flow.invocationid` - The invocation id of the flow
- `dataflow.flow.name` - The name of the flow
- `dataflow.outcome` - Indicator of success or failure (`success` or `failure`)
- `dataflow.flow.execution.duration.ms` - The execution duration for a specific execution
- `dataflow.block.name` - The name of the block
- `dataflow.data.label` - Developer-provided label for business data processing

## ActivitySource Implementation

### ActivitySource Configuration

- **Name**: `Uniun.DataFlow`
- **Location**: Static field in `/src/DataFlow/DataFlow.cs`

### Activity Types

#### 1. Flow Activity
- **Name**: `Flow` (constant in `ActivityNames.Flow`)
- **DisplayName**: `Flow {FlowName}` (parameterized)
- **Kind**: Default
- **Tags**:
  - `FlowInvocationId`
  - `FlowName`
  - Global tags from `IDataFlowMetrics.GlobalTags`
- **Status**:
  - `Ok` - when flow completes successfully
  - `Error` - when flow throws exception
- **Error Tags** (on exception):
  - `error.type` - Exception type full name
  - `cancelled` - Set to "true" if OperationCanceledException
- **Duration**: Automatically tracked by Activity

#### 2. Block Activity
- **Name**: `Block` (constant in `ActivityNames.Block`)
- **DisplayName**: `Block {BlockName}` (parameterized)
- **Kind**: `Internal`
- **Parent**: Flow Activity
- **Tags**:
  - `BlockName`
  - `FlowName`
  - Global tags from `IDataFlowMetrics.GlobalTags`
- **Status**:
  - `Ok` - when block completes successfully
  - `Error` - when block throws exception
- **Error Tags** (on exception):
  - `error.type` - Exception type full name
  - `cancelled` - Set to "true" if OperationCanceledException
- **Duration**: Automatically tracked by Activity

### Activity Hierarchy

```
Flow Activity (parent)
├── Block Activity 1 (child)
├── Block Activity 2 (child)
└── Block Activity N (child)
```

All blocks execute concurrently but are children of the same Flow activity.

## MonitoredChannel Pattern

### Purpose
Wrap standard `Channel<T>` to enable metrics collection for channel queue depth.

### Components

#### IMonitoredChannel Interface
```csharp
public interface IMonitoredChannel
{
    ChannelMetricSnapshot? GetMetricSnapshot();
}
```

#### MonitoredChannel<T> Class
- **Wraps**: `Channel<T>`
- **Implements**: `IMonitoredChannel`
- **Registration**: Registers with `IDataFlowMetrics` on construction
- **Monitoring Lifecycle**:
  1. Created via `MonitoredChannelFactory`
  2. `StartMonitoring(IDataFlowContext)` called when flow starts
  3. Returns `IChannelMonitoringLease` (disposable)
  4. Metrics system polls `GetMetricSnapshot()` via ObservableGauge
  5. Lease disposed when monitoring should stop
- **Requirements**: Channel must support `CanCount` (bounded channels)

#### ChannelRegistry (internal to DataFlowMetrics)
- **Pattern**: WeakReference tracking of channels
- **Cleanup**: Timer-based cleanup every 5 minutes
- **Purpose**: Allows channels to be GC'd when no longer in use

### Usage Pattern in Production

```csharp
// 1. Create monitored channel
var channel = channelFactory.CreateMonitoredChannel<T>(blockName, capacity);

// 2. Start monitoring when flow begins
using var lease = channel.StartMonitoring(context);

// 3. Use channel normally
await channel.Writer.WriteAsync(item);
var item = await channel.Reader.ReadAsync();

// 4. Lease automatically disposed when monitoring ends
```

## Metrics Context Classes

### DataFlowMetricsTagsContext
- **Purpose**: Holds flow-level tags for metrics
- **Fields**:
  - `FlowWideTags` - Tags common to all flow metrics
  - `FlowLevelCompletionTags` - Optional tags for completion (includes outcome)
- **Methods**:
  - `Started()` - Calls `IDataFlowMetrics.FlowStarted()`
  - `Completed(duration, isSuccessful)` - Calls `IDataFlowMetrics.FlowCompleted()`

### BlockMetricsTagsContext
- **Purpose**: Holds block-level tags for metrics
- **Inherits**: Flow-wide tags from parent context
- **Methods**: Similar to DataFlowMetricsTagsContext

### DataItemMetricsContext
- **Purpose**: Holds tags for data item processing metrics
- **Additional Tag**: `DataLabel` for categorizing business data

## Summary Statistics

**Total Instruments**: 9
- Histograms: 2
- Counters: 5
- UpDownCounters: 2
- ObservableGauges: 2

**Total Methods**: 6
- Channel registration: 1
- Flow lifecycle: 2
- Block lifecycle: 2
- Processing metrics: 2

**Activity Types**: 2
- Flow activity
- Block activity

**Supporting Infrastructure**:
- MonitoredChannel wrapper for queue depth observation
- ChannelRegistry for channel lifecycle management
- Context classes for tag management
