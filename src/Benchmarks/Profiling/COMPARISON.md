# Comparing Benchmark Runs

This guide explains how to compare benchmark results across different runs to track performance trends and identify regressions.

## Quick Comparison

### Using the Python Visualization Script

```bash
cd src/Benchmarks/Profiling

# Run baseline benchmark
dotnet run -c Release -- etl-profile 50000 3
mv ../../BenchmarkDotNet.Artifacts/profiling ../../BenchmarkDotNet.Artifacts/baseline

# Make code changes...

# Run comparison benchmark  
dotnet run -c Release -- etl-profile 50000 3
mv ../../BenchmarkDotNet.Artifacts/profiling ../../BenchmarkDotNet.Artifacts/comparison

# Compare visually
python visualize_profile.py compare '../../BenchmarkDotNet.Artifacts/*/complex_etl_*.csv'
```

This generates a comparison chart showing CPU and memory trends across all runs.

## JSON Summary Analysis

The JSON summary files contain structured data perfect for automated comparison:

```json
{
  "Timestamp": "2025-10-14T23:23:29Z",
  "Configuration": {
    "RecordCount": 50000,
    "MaxConcurrency": 4,
    "BatchSize": 100,
    "Iterations": 3
  },
  "Results": {
    "AverageDurationMs": 3500,
    "MinDurationMs": 3200,
    "MaxDurationMs": 3800,
    "AverageThroughput": 14285.7
  }
}
```

### Comparison Script Example

```python
import json
import sys
from pathlib import Path

def compare_summaries(baseline_file, current_file):
    with open(baseline_file) as f:
        baseline = json.load(f)
    with open(current_file) as f:
        current = json.load(f)
    
    baseline_throughput = baseline['Results']['AverageThroughput']
    current_throughput = current['Results']['AverageThroughput']
    
    improvement = ((current_throughput - baseline_throughput) / baseline_throughput) * 100
    
    print(f"Throughput Comparison:")
    print(f"  Baseline: {baseline_throughput:.2f} items/sec")
    print(f"  Current:  {current_throughput:.2f} items/sec")
    print(f"  Change:   {improvement:+.2f}%")
    
    if improvement < -5:
        print("⚠️  WARNING: Performance regression detected!")
        sys.exit(1)
    elif improvement > 5:
        print("✅ Performance improvement!")
    else:
        print("➡️  Performance unchanged")

if __name__ == '__main__':
    compare_summaries(sys.argv[1], sys.argv[2])
```

Usage:
```bash
python compare_benchmark.py baseline/summary_*.json comparison/summary_*.json
```

## CSV Time-Series Analysis

For detailed performance characteristics, analyze the CSV files:

```python
import pandas as pd
import matplotlib.pyplot as plt

# Load data
baseline = pd.read_csv('baseline/complex_etl_iter1_*.csv')
current = pd.read_csv('current/complex_etl_iter1_*.csv')

# Compare throughput over time
plt.figure(figsize=(12, 6))
plt.plot(baseline['TimeMs']/1000, baseline['ItemsPerSec'], label='Baseline', linewidth=2)
plt.plot(current['TimeMs']/1000, current['ItemsPerSec'], label='Current', linewidth=2)
plt.xlabel('Time (seconds)')
plt.ylabel('Items/sec')
plt.title('Throughput Comparison')
plt.legend()
plt.grid(True, alpha=0.3)
plt.savefig('throughput_comparison.png', dpi=150)

# Compare key metrics
metrics = {
    'Avg CPU': (baseline['CPU%'].mean(), current['CPU%'].mean()),
    'Peak Memory': (baseline['MemoryMB'].max(), current['MemoryMB'].max()),
    'Avg Throughput': (baseline['ItemsPerSec'].mean(), current['ItemsPerSec'].mean()),
    'GC Gen0': (baseline['GC0'].max(), current['GC0'].max())
}

for metric, (base, curr) in metrics.items():
    change = ((curr - base) / base) * 100 if base != 0 else 0
    print(f"{metric:20s}: {base:8.2f} -> {curr:8.2f} ({change:+.1f}%)")
```

## Dashboard Solutions

### Option 1: Grafana + Prometheus (Recommended)

See [OPENTELEMETRY.md](OPENTELEMETRY.md) for full setup instructions.

**Benefits:**
- Real-time monitoring during benchmark execution
- Historical data retention
- Rich query language (PromQL)
- Alerting capabilities
- Multiple data sources

**Workflow:**
```bash
# Terminal 1: Start Prometheus/Grafana stack
docker-compose up

# Terminal 2: Run benchmark with OTEL
OTEL_EXPORTER_PROMETHEUS_PORT=9464 \
dotnet run -c Release -- etl-profile 100000 5

# Browse to Grafana (localhost:3000) to view live metrics
```

**Comparison Queries:**
```promql
# Compare current vs 1 hour ago
rate(dataflow_block_items_processed_total[30s]) 
  vs 
rate(dataflow_block_items_processed_total[30s] offset 1h)

# Week-over-week comparison
avg_over_time(benchmark:throughput:rate30s[1d]) 
  vs
avg_over_time(benchmark:throughput:rate30s[1d] offset 7d)
```

### Option 2: Custom Dashboard (Python/JavaScript)

Create a simple web dashboard using the JSON summaries:

```python
# dashboard_generator.py
import json
from pathlib import Path
from datetime import datetime

def generate_dashboard(results_dir):
    summaries = []
    for summary_file in Path(results_dir).glob('*/summary_*.json'):
        with open(summary_file) as f:
            data = json.load(f)
            data['file'] = summary_file.name
            summaries.append(data)
    
    # Sort by timestamp
    summaries.sort(key=lambda x: x['Timestamp'])
    
    # Generate HTML
    html = """
    <!DOCTYPE html>
    <html>
    <head>
        <title>Benchmark Dashboard</title>
        <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
    </head>
    <body>
        <h1>Benchmark Performance Trends</h1>
        <canvas id="throughputChart"></canvas>
        <script>
        const data = {
            labels: %s,
            datasets: [{
                label: 'Average Throughput (items/sec)',
                data: %s,
                borderColor: 'rgb(75, 192, 192)',
                tension: 0.1
            }]
        };
        new Chart(document.getElementById('throughputChart'), {
            type: 'line',
            data: data,
            options: {
                scales: {
                    y: { beginAtZero: true }
                }
            }
        });
        </script>
    </body>
    </html>
    """ % (
        json.dumps([s['Timestamp'] for s in summaries]),
        json.dumps([s['Results']['AverageThroughput'] for s in summaries])
    )
    
    with open('dashboard.html', 'w') as f:
        f.write(html)
    
    print("Dashboard generated: dashboard.html")

if __name__ == '__main__':
    generate_dashboard('BenchmarkDotNet.Artifacts')
```

### Option 3: Azure Monitor / Application Insights

For cloud-based tracking:

```bash
# Set Application Insights connection string
export APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=...;..."

# Run with OTLP exporter pointing to Azure
OTEL_EXPORTER_OTLP_ENDPOINT=https://your-region.monitor.azure.com \
OTEL_EXPORTER_OTLP_HEADERS="x-ms-client-id=your-app-id" \
dotnet run -c Release -- etl-profile 50000 3
```

Then create KQL queries in Azure Monitor:
```kql
customMetrics
| where name == "dataflow.block.items.processed"
| summarize avg(value) by bin(timestamp, 1m)
| render timechart
```

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Performance Benchmark

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

jobs:
  benchmark:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      
      - name: Run Benchmark
        run: |
          cd src/Benchmarks
          dotnet run -c Release -- etl-profile 50000 3
      
      - name: Upload Results
        uses: actions/upload-artifact@v3
        with:
          name: benchmark-results-${{ github.sha }}
          path: src/Benchmarks/BenchmarkDotNet.Artifacts/profiling/
      
      - name: Compare with Baseline
        if: github.event_name == 'pull_request'
        run: |
          # Download baseline from main branch
          gh run download --repo ${{ github.repository }} --name benchmark-results-baseline
          
          # Compare
          python compare_benchmark.py \
            benchmark-results-baseline/summary_*.json \
            BenchmarkDotNet.Artifacts/profiling/summary_*.json
        env:
          GH_TOKEN: ${{ github.token }}
      
      - name: Comment PR with Results
        if: github.event_name == 'pull_request'
        uses: actions/github-script@v6
        with:
          script: |
            const fs = require('fs');
            const summary = JSON.parse(fs.readFileSync('BenchmarkDotNet.Artifacts/profiling/summary_*.json'));
            github.rest.issues.createComment({
              issue_number: context.issue.number,
              owner: context.repo.owner,
              repo: context.repo.repo,
              body: `## Benchmark Results\n\n` +
                    `Throughput: ${summary.Results.AverageThroughput.toFixed(2)} items/sec\n` +
                    `Duration: ${summary.Results.AverageDurationMs} ms`
            });
```

### Azure DevOps Example

```yaml
trigger:
  - main
  - develop

pool:
  vmImage: 'ubuntu-latest'

steps:
- task: UseDotNet@2
  inputs:
    version: '8.0.x'

- script: |
    cd src/Benchmarks
    dotnet run -c Release -- etl-profile 50000 3
  displayName: 'Run Benchmark'

- task: PublishBuildArtifacts@1
  inputs:
    pathToPublish: 'src/Benchmarks/BenchmarkDotNet.Artifacts/profiling'
    artifactName: 'benchmark-results'

- task: PythonScript@0
  inputs:
    scriptSource: 'filePath'
    scriptPath: 'scripts/compare_benchmark.py'
    arguments: '$(Pipeline.Workspace)/baseline/summary_*.json BenchmarkDotNet.Artifacts/profiling/summary_*.json'
  displayName: 'Compare with Baseline'
```

## Best Practices

1. **Consistent Environment**
   - Use dedicated machines or containers
   - Disable CPU frequency scaling
   - Control background processes
   - Use same .NET SDK version

2. **Statistical Significance**
   - Run multiple iterations (at least 3-5)
   - Use BenchmarkDotNet mode for statistical analysis
   - Consider warmup iterations

3. **Version Control**
   - Tag benchmark runs with git commit hash
   - Store baseline results in repository
   - Track configuration changes

4. **Automated Alerts**
   - Set up alerts for >10% regressions
   - Notify team on significant changes
   - Require investigation before merge

5. **Documentation**
   - Document any expected performance changes
   - Link to relevant issues/PRs
   - Explain optimization techniques used

## Troubleshooting

### Inconsistent Results

If results vary significantly between runs:
- Ensure machine is idle
- Increase iteration count
- Check for thermal throttling
- Verify consistent input data

### Missing Baseline

If no baseline exists:
- Run benchmark on main branch first
- Store results as artifact
- Create initial baseline before making changes

### Comparison Shows No Metrics

If DataFlow metrics are missing:
- Verify `AddDataFlowMetrics()` is called
- Check meter name is "Uniun.DataFlow"
- Ensure MeterListener is active
- Look for errors in console output

## Further Reading

- [OPENTELEMETRY.md](OPENTELEMETRY.md) - OTEL integration guide
- [README.md](README.md) - Main profiling documentation
- [BenchmarkDotNet Docs](https://benchmarkdotnet.org/) - Statistical analysis
- [Grafana Tutorials](https://grafana.com/tutorials/) - Dashboard creation
