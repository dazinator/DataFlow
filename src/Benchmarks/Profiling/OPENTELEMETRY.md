# OpenTelemetry Integration for Benchmarks

This guide shows how to run the profiling benchmarks with OpenTelemetry to export metrics to collectors like Prometheus, Grafana, or Azure Monitor.

## Why Use OpenTelemetry?

OpenTelemetry provides:
- **Live dashboards** - View metrics in real-time during benchmark execution
- **Historical tracking** - Compare performance across multiple runs
- **Standard tooling** - Works with Prometheus, Grafana, Jaeger, etc.
- **Rich metrics** - Captures both system metrics (CPU, memory) and DataFlow metrics (throughput, operations)

## Setup

### 1. Install OpenTelemetry Packages

The benchmark project already includes the necessary packages. If you need to add them to another project:

```bash
dotnet add package OpenTelemetry.Exporter.Console
dotnet add package OpenTelemetry.Exporter.Prometheus.AspNetCore
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
dotnet add package OpenTelemetry.Extensions.Hosting
```

### 2. Run with OpenTelemetry Console Exporter

The simplest option - metrics are printed to console:

```bash
cd src/Benchmarks
OTEL_METRICS_EXPORTER=console \
OTEL_METRIC_EXPORT_INTERVAL=1000 \
dotnet run -c Release -- etl-profile 10000 1
```

This will output metrics to console every second during the benchmark run.

### 3. Run with Prometheus Exporter

Export metrics to Prometheus for visualization:

```bash
# Start Prometheus (if using Docker)
docker run -p 9090:9090 -v $(pwd)/prometheus.yml:/etc/prometheus/prometheus.yml prom/prometheus

# Run benchmark with Prometheus exporter
OTEL_EXPORTER_PROMETHEUS_PORT=9464 \
dotnet run -c Release -- etl-profile 50000 3
```

Access metrics at: http://localhost:9464/metrics

### 4. Run with OTLP Exporter (for Grafana, Azure Monitor, etc.)

Export to any OTLP-compatible collector:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 \
OTEL_EXPORTER_OTLP_PROTOCOL=grpc \
OTEL_METRICS_EXPORTER=otlp \
dotnet run -c Release -- etl-profile 50000 3
```

## Available Metrics

### System Metrics (via .NET runtime)
- `process.cpu.utilization` - CPU usage percentage
- `process.memory.working_set` - Working set memory
- `process.runtime.dotnet.gc.collections.count` - GC collections by generation
- `process.runtime.dotnet.thread_pool.threads.count` - Thread pool metrics

### DataFlow Metrics
- `dataflow.flow.duration.ms` - Flow execution time (histogram)
- `dataflow.block.duration.ms` - Block execution time (histogram)
- `dataflow.flow.active-count` - Number of active flows (gauge)
- `dataflow.block.active-count` - Number of active blocks (gauge)
- `dataflow.block.operations` - Operations completed by blocks (counter)
- `dataflow.block.items.processed` - Business items processed (counter)
- `dataflow.channel.buffer.utilization` - Channel buffer utilization % (gauge)

## Example: Grafana Dashboard

### 1. Start Grafana Stack

```bash
# docker-compose.yml
version: '3'
services:
  prometheus:
    image: prom/prometheus
    ports:
      - "9090:9090"
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
  
  grafana:
    image: grafana/grafana
    ports:
      - "3000:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
```

### 2. Configure Prometheus

```yaml
# prometheus.yml
global:
  scrape_interval: 1s

scrape_configs:
  - job_name: 'dataflow-benchmark'
    static_configs:
      - targets: ['host.docker.internal:9464']
```

### 3. Run Benchmark

```bash
OTEL_EXPORTER_PROMETHEUS_PORT=9464 \
dotnet run -c Release -- etl-profile 100000 5
```

### 4. Create Grafana Dashboard

Import the provided dashboard JSON or create panels for:

- **Throughput**: Rate of `dataflow.block.items.processed`
- **Latency**: P50/P95/P99 of `dataflow.block.duration.ms`
- **Resource Usage**: CPU, Memory, GC collections
- **Concurrency**: `dataflow.block.active-count`
- **Backpressure**: `dataflow.channel.buffer.utilization`

## Example Queries

### PromQL Queries for Analysis

```promql
# Operations per second (throughput)
rate(dataflow_block_operations_total[30s])

# Average block execution time
rate(dataflow_block_duration_ms_sum[30s]) / rate(dataflow_block_duration_ms_count[30s])

# Items processed per second by block
sum by (block_name) (rate(dataflow_block_items_processed_total[30s]))

# CPU utilization during benchmark
rate(process_cpu_seconds_total[30s]) * 100

# Memory growth over time
process_memory_working_set_bytes / 1024 / 1024
```

## Comparing Benchmark Runs

### Using Prometheus Recording Rules

```yaml
# prometheus-rules.yml
groups:
  - name: dataflow_benchmark
    interval: 10s
    rules:
      - record: benchmark:throughput:rate30s
        expr: rate(dataflow_block_items_processed_total[30s])
      
      - record: benchmark:latency:p95
        expr: histogram_quantile(0.95, rate(dataflow_block_duration_ms_bucket[30s]))
      
      - record: benchmark:cpu:avg
        expr: avg(rate(process_cpu_seconds_total[30s])) * 100
```

### Tagging Runs for Comparison

Add custom tags to benchmark runs:

```bash
DATAFLOW_BENCHMARK_VERSION="v1.0" \
DATAFLOW_BENCHMARK_COMMIT="abc123" \
dotnet run -c Release -- etl-profile 50000 3
```

Then query by version:
```promql
rate(dataflow_block_items_processed_total{benchmark_version="v1.0"}[30s])
```

## Best Practices

1. **Consistent Environment**: Run benchmarks on dedicated machines without other workloads
2. **Warmup**: First iteration is often slower - consider running 1-2 warmup iterations
3. **Sample Rates**: Use 1s metric export interval for detailed analysis
4. **Retention**: Configure Prometheus to retain benchmark metrics for historical comparison
5. **Annotations**: Add Grafana annotations to mark significant changes (library updates, configuration changes)

## Troubleshooting

### Metrics Not Appearing

Check that:
- MeterListener is subscribing to "Uniun.DataFlow" meter
- Metrics are being emitted (check console exporter first)
- Prometheus is scraping the correct endpoint
- Firewall allows connections

### High Overhead

If OTEL causes performance impact:
- Increase export interval to 5-10s
- Use delta temporality for counters
- Disable unnecessary metrics
- Use sampling for high-cardinality histograms

### Missing DataFlow Metrics

Ensure DataFlow metrics are registered:
```csharp
services.AddDataFlowMetrics();
services.AddDataFlows();
```

## Next Steps

- Create custom Grafana dashboards for your specific flows
- Set up alerting for performance regressions
- Integrate with CI/CD to track metrics over time
- Compare performance across different configurations
