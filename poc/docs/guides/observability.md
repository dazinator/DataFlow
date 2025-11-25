# Observability Guide for DataFlow POC

This guide explains how to configure and use observability features (metrics and distributed tracing) in the DataFlow POC library for .NET applications.

## Table of Contents

1. [Overview](#overview)
2. [Basic Setup](#basic-setup)
3. [Configuring Metrics Collection](#configuring-metrics-collection)
4. [Configuring Distributed Tracing](#configuring-distributed-tracing)
5. [Using Global Tags](#using-global-tags)
6. [Block-Level Metrics](#block-level-metrics)
7. [Custom Data Item Tracking](#custom-data-item-tracking)
8. [Available Metrics](#available-metrics)
9. [Exporting to Monitoring Systems](#exporting-to-monitoring-systems)

## Overview

DataFlow POC provides comprehensive observability through:
- **OpenTelemetry Metrics**: 13 core metrics tracking flow and block execution
- **Distributed Tracing**: Activity-based tracing with parent-child relationships
- **Custom Tags**: Global and execution-specific tags for filtering and grouping

**Parity**: 93% (13 of 14 production metrics)

## Basic Setup

### 1. Install Required Packages

```bash
dotnet add package OpenTelemetry
dotnet add package OpenTelemetry.Exporter.Console
dotnet add package OpenTelemetry.Extensions.Hosting
```

### 2. Configure in Program.cs / Startup

```csharp
using DataFlow.POC.Core;
using DataFlow.POC.Observability;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add OpenTelemetry with DataFlow POC instrumentation
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddDataFlowPOC()              // Add DataFlow POC meters
            .AddConsoleExporter();         // Export to console (for demo)
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddDataFlowPOC()              // Add DataFlow POC activity source
            .AddConsoleExporter();         // Export to console (for demo)
    });

var app = builder.Build();
app.Run();
```

## Configuring Metrics Collection

### Option 1: Using DataFlowGraph Constructor (Recommended)

When creating a graph directly, pass metrics through the constructor:

```csharp
using DataFlow.POC.Core;
using DataFlow.POC.Observability;
using Microsoft.Extensions.DependencyInjection;

// Get services from DI
var serviceProvider = services.BuildServiceProvider();
var logger = serviceProvider.GetRequiredService<ILogger<DataFlowGraph>>();
var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();

// Create metrics instance
var meterAccessor = new MeterAccessor(meterFactory);
var metrics = new DataFlowMetrics(meterAccessor);

// Create graph with metrics
var graph = new DataFlowGraph("MyFlow", logger, metrics);
```

### Option 2: Using ExecutionContext (Advanced)

Pass metrics through the execution context for more control:

```csharp
// Create metrics with custom global tags
var globalTags = new TagList
{
    { "environment", "production" },
    { "version", "1.0.0" }
};
var metrics = new DataFlowMetrics(meterAccessor, globalTags);

// Create execution context with metrics
var context = new ExecutionContext(
    serviceProvider,
    cancellationToken,
    Guid.NewGuid(),
    recoveryCheckpoint: null,
    metrics: metrics);

// Execute graph - metrics will be collected
await graph.ExecuteAsync(context);
```

## Configuring Distributed Tracing

Distributed tracing is automatically enabled when you add the DataFlow POC activity source:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddDataFlowPOC()                    // Add DataFlow POC activities
            .AddAspNetCoreInstrumentation()      // Correlate with HTTP requests
            .AddConsoleExporter();
    });
```

**Activity Hierarchy:**
- **Flow Activity** (parent): Represents the entire dataflow execution
- **Block Activities** (children): One per block execution

Each activity is tagged with:
- `dataflow.flow.name` - Name of the flow
- `dataflow.block.name` - Name of the block (block activities only)
- `dataflow.flow.invocationId` - Unique execution ID
- Error information (if failures occur)

## Using Global Tags

Global tags are added to all metrics and activities. Use them for environment-wide labels:

```csharp
var globalTags = new TagList
{
    { "environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") },
    { "service.name", "my-service" },
    { "service.version", "1.2.3" },
    { "datacenter", "us-west-2" }
};

var metrics = new DataFlowMetrics(meterAccessor, globalTags);
```

**Best Practices:**
- Use global tags for static, deployment-level metadata
- Keep tag cardinality low (avoid unique values like IDs)
- Common tags: environment, service name, version, region, datacenter

## Block-Level Metrics

Blocks can access metrics through the execution context to emit custom metrics.

### Accessing Metrics in a Block

```csharp
using DataFlow.POC.Core;
using DataFlow.POC.Observability;

public class MyProcessingBlock : IBlock<string, string>
{
    public string Name => "MyProcessor";
    public Type InputType => typeof(string);
    public Type OutputType => typeof(string);

    public async IAsyncEnumerable<string> ExecuteAsync(
        IAsyncEnumerable<string> input,
        IExecutionContext context)
    {
        // Access metrics from context
        var metrics = context.Metrics;
        
        await foreach (var item in input)
        {
            // Process item
            var result = ProcessItem(item);
            
            // Emit custom metric (if metrics available)
            if (metrics != null)
            {
                // Note: Block-level metrics are automatically emitted by the framework
                // This example shows how blocks COULD augment metrics if needed
            }
            
            yield return result;
        }
    }
    
    private string ProcessItem(string item) => item.ToUpper();
}
```

### Important Notes on Block-Level Metrics

**Framework-Managed Metrics:**
The framework automatically emits these metrics for all blocks:
- `dataflow.block.execution.count` - Number of block executions
- `dataflow.block.duration.ms` - Block execution duration
- `dataflow.block.active.count` - Currently executing blocks

**Blocks Cannot Directly Augment Tags:**
Blocks access metrics via `IExecutionContext.Metrics`, but this provides access to the `IDataFlowMetrics` interface which has limited methods. Blocks cannot add custom tags to framework metrics.

**Alternative: Custom Application Metrics:**
If blocks need custom metrics with specific tags, they should create their own metrics:

```csharp
public class MyBlockWithCustomMetrics : IBlock<string, string>
{
    private readonly Counter<long> _customCounter;
    
    public MyBlockWithCustomMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("MyApplication");
        _customCounter = meter.CreateCounter<long>("my.custom.items.processed");
    }
    
    public async IAsyncEnumerable<string> ExecuteAsync(
        IAsyncEnumerable<string> input,
        IExecutionContext context)
    {
        await foreach (var item in input)
        {
            // Process and emit custom metric
            var result = ProcessItem(item);
            _customCounter.Add(1, new TagList { { "category", GetCategory(item) } });
            
            yield return result;
        }
    }
    
    private string ProcessItem(string item) => item.ToUpper();
    private string GetCategory(string item) => item.Length > 10 ? "long" : "short";
}
```

## Custom Data Item Tracking

For advanced scenarios where you want to track processing of business data items within stream items, the framework provides limited support through the metrics context classes.

**Note:** This feature is primarily used internally by the framework. User blocks typically don't need to create data item contexts manually.

## Available Metrics

### Flow Metrics

| Metric Name | Type | Description |
|-------------|------|-------------|
| `dataflow.flow.execution.started.count` | Counter | Number of flow executions started |
| `dataflow.flow.execution.count` | Counter | Number of flow executions completed |
| `dataflow.flow.duration.ms` | Histogram | Flow execution duration distribution |
| `dataflow.flow.active.count` | UpDownCounter | Currently executing flows |

### Block Metrics

| Metric Name | Type | Description |
|-------------|------|-------------|
| `dataflow.block.execution.count` | Counter | Number of block executions completed |
| `dataflow.block.duration.ms` | Histogram | Block execution duration distribution |
| `dataflow.block.active.count` | UpDownCounter | Currently executing blocks |
| `dataflow.block.operations.completed` | Counter | Operations completed by blocks |

### Processing Metrics

| Metric Name | Type | Description |
|-------------|------|-------------|
| `dataflow.data.items.processed` | Counter | Business data items processed |

### Channel Metrics

| Metric Name | Type | Description |
|-------------|------|-------------|
| `dataflow.channel.active.count` | ObservableGauge | Active channel count (total) |

**Note:** Per-channel buffer utilization is not included in Phase 1 (deferred to Phase 2).

### Common Tags

All metrics include these tags:
- `dataflow.flow.name` - Name of the flow
- `dataflow.flow.invocationId` - Unique execution identifier
- `dataflow.block.name` - Block name (block-level metrics only)
- `outcome` - "success" or "failure" (completion metrics only)
- Plus any global tags configured

## Exporting to Monitoring Systems

### Prometheus

```csharp
dotnet add package OpenTelemetry.Exporter.Prometheus.AspNetCore

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddDataFlowPOC()
            .AddPrometheusExporter();
    });

app.UseOpenTelemetryPrometheusScrapingEndpoint();
```

### Application Insights

```csharp
dotnet add package Azure.Monitor.OpenTelemetry.Exporter

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddDataFlowPOC()
            .AddAzureMonitorMetricExporter(options =>
            {
                options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
            });
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddDataFlowPOC()
            .AddAzureMonitorTraceExporter(options =>
            {
                options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
            });
    });
```

### OTLP (OpenTelemetry Protocol)

For Grafana, Jaeger, or other OTLP-compatible backends:

```csharp
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddDataFlowPOC()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://localhost:4317");
            });
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddDataFlowPOC()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://localhost:4317");
            });
    });
```

## Troubleshooting

### Metrics Not Appearing

1. **Verify meter is added**: Ensure `.AddDataFlowPOC()` is called on `MeterProviderBuilder`
2. **Check exporter**: Verify an exporter is configured (Console, Prometheus, etc.)
3. **Metrics instance**: Ensure `DataFlowMetrics` is created and passed to graph or context
4. **MeterFactory**: Verify `IMeterFactory` is available in DI container

### Activities Not Traced

1. **Verify activity source**: Ensure `.AddDataFlowPOC()` is called on `TracerProviderBuilder`
2. **Check sampling**: Verify tracing sampler is not filtering out activities
3. **ActivitySource name**: The source name is "DataFlow.POC"

### Performance Considerations

- **Metrics overhead**: < 5% with metrics enabled (null checks when disabled)
- **Observable gauges**: Pull-based, no continuous overhead
- **Global tags**: Cached, minimal per-metric cost
- **Activities**: Minimal overhead when no listener is registered

## Best Practices

1. **Use global tags** for static metadata (environment, version, service)
2. **Configure once** at application startup
3. **Let the framework manage** block and flow metrics automatically
4. **Create custom application metrics** if blocks need specific tracking
5. **Monitor cardinality** - avoid high-cardinality tags (e.g., unique IDs in tags)
6. **Use appropriate exporters** for your monitoring infrastructure
7. **Sample traces** in high-traffic scenarios to reduce overhead

## Next Steps

- See [Dependency Injection Registration Guide](dependency-injection-registration.md) for DI patterns
- See [Testing Guide](testing-guide.md) for testing with metrics
- Review OpenTelemetry documentation: https://opentelemetry.io/docs/
