# Control Signal Strategy Benchmarks

This directory contains comprehensive benchmarks for comparing different control signal propagation strategies in the DataFlow POC framework.

## Quick Start

### Run Strategy Comparison Benchmark

Compares all control signal strategies with identical workloads:

```bash
cd poc/DataFlow.POC.Benchmarks
dotnet run -c Release -- control-signal-strategies
```

**Strategies Benchmarked**:
- Baseline (pure data, no control signals)
- Original Side-Channel (from PR #113)
- Optimized Side-Channel (Phase 2 implementation)
- Out-of-Band Epoch Control Plane (Phase 2 implementation)
- Event-Based Advisory Plane (existing prototype)

**Workload**: 10,000 data items + 100 control signals

### Run Routing Overhead Microbenchmark

Measures per-item type checking and routing overhead:

```bash
cd poc/DataFlow.POC.Benchmarks
dotnet run -c Release -- control-signal-routing
```

**Scenarios**:
- Baseline: Direct write without type checking
- Original: Type check with awaiting broadcast
- Optimized: Type check with TryWrite fast path
- Epoch: No type checking (zero overhead)

## Benchmark Results

Results will be saved to `BenchmarkDotNet.Artifacts/results/` directory.

### Interpreting Results

**Throughput Metrics**:
- **Mean**: Average execution time
- **Ratio**: Compared to baseline (target: ≤1.02 for ≤2% overhead)
- **Allocated**: Memory allocated per operation

**Performance Targets** (from PR #113 feedback):
- ✅ Throughput: ≤2% overhead vs baseline
- ✅ Memory: ≤10% overhead
- ✅ Latency: Backpressure within one buffer depth
- ✅ Ordering: Verified alignment per epoch

## Available Benchmarks

### 1. ControlSignalStrategyComparison

Full dataflow comparison with:
- Pure data baseline
- All control signal strategies
- Multiple consumer scalability test

**Parameters**:
- Data items: 10,000
- Control signals: 100 (every 100 data items)
- Buffer capacity: 100

### 2. ControlSignalRoutingMicrobenchmark

Isolated routing overhead:
- Channel write operations only
- Type checking overhead
- Fast path vs slow path comparison

**Parameters**:
- Items: 10,000
- Control signals: 100
- Separate data and control channels

### 3. SideChannelMicrobenchmark (Existing)

Original side-channel microbenchmarks from PR #113:
- Single channel vs dual channel writes
- Merge operation overhead
- Competing consumer coordination

```bash
dotnet run -c Release -- sidechannel-micro
```

## Benchmark Configuration

All benchmarks use:
- **Runtime**: .NET 8.0
- **Warmup**: 3 iterations
- **Iterations**: 10
- **Memory Diagnostics**: Enabled
- **Job**: Simple (no GC collection between runs)

## Performance Analysis

### Expected Results

Based on Phase 2 investigation targets:

| Strategy | Throughput Ratio | Memory Overhead | Use Case |
|----------|-----------------|-----------------|----------|
| Baseline | 1.00 | 0% | Pure data (no control) |
| Optimized Side-Channel | ≤1.02 | ≤10% | Existing envelopes |
| Epoch Control Plane | ~1.00 | ≤5% | Checkpointing |
| Event-Based | ~1.00 | ≤5% | Advisory signals |
| Original Side-Channel | 1.05-1.15 | ~30% | Current implementation |

### Key Observations

**Optimized Side-Channel**:
- TryWrite fast path reduces awaiting overhead
- Reduced merge buffer (50 vs 100) saves memory
- Sequential broadcast for ≤10 consumers eliminates Task.WhenAll

**Epoch Control Plane**:
- Zero type checking on data path
- Control signals via events, not channels
- Near-baseline performance for pure data workloads

**Event-Based**:
- Lightweight for infrequent signals
- No channel overhead
- Best for advisory notifications

## Running Custom Workloads

### Modify Benchmark Parameters

Edit `ControlSignalStrategyBenchmark.cs`:

```csharp
private const int DataItemCount = 10000;  // Change data volume
private const int ControlSignalCount = 100; // Change signal frequency
```

Then rebuild and run:

```bash
dotnet build -c Release
dotnet run -c Release -- control-signal-strategies
```

### Add Consumer Count Variations

The multi-consumer benchmark tests scalability with 5 consumers. To test different counts, modify:

```csharp
for (int i = 0; i < 5; i++)  // Change consumer count
{
    // ...
}
```

## Profiling with External Tools

For deeper analysis, use dotnet-counters or dotMemory:

```bash
# Install dotnet-counters
dotnet tool install --global dotnet-counters

# Profile during benchmark
dotnet-counters monitor --process-id <pid> System.Runtime Microsoft.AspNetCore.Hosting
```

## Troubleshooting

### Benchmarks Take Too Long

Reduce iterations in `[SimpleJob]` attribute:

```csharp
[SimpleJob(RuntimeMoniker.Net80, warmupCount: 1, iterationCount: 3)]
```

### Out of Memory

Reduce data item count:

```csharp
private const int DataItemCount = 1000;  // Smaller workload
```

### Inconsistent Results

- Ensure no other processes are consuming resources
- Run in Release mode (`-c Release`)
- Close other applications
- Use a dedicated benchmark machine

## Additional Resources

- **Phase 2 Investigation**: `../PHASE2_CONTROL_SIGNAL_INVESTIGATION.md`
- **Original Investigation**: `../CONTROL_SIGNAL_INVESTIGATION_SUMMARY.md`
- **BenchmarkDotNet Docs**: https://benchmarkdotnet.org/

## Comparison with Python Benchmarks

The POC also includes Python benchmarks for comparison with other frameworks:

```bash
cd poc/python-benchmarks
python benchmark.py
```

See `../PYTHON_BENCHMARK_SUMMARY.md` for results.

## Next Steps

After running benchmarks:

1. **Analyze Results**: Check if targets are met (≤2% overhead, ≤10% memory)
2. **Create Report**: Document findings in `PHASE2_PERFORMANCE_REPORT.md`
3. **Make Recommendations**: Update strategy selection guide
4. **Implement Pluggable Selection**: Add enum-based strategy selection to builder

---

**Status**: Ready for execution
**Created**: Phase 2 Investigation (Nov 2025)
**Target Validation**: ≤2% overhead for Optimized Side-Channel
