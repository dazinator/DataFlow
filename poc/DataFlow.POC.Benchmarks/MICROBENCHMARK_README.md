# Side-Channel Microbenchmarks

This directory contains BenchmarkDotNet microbenchmarks for the side-channel competing edge architecture.

## Running the Microbenchmarks

```bash
cd poc/DataFlow.POC.Benchmarks
dotnet run -c Release -- sidechannel-micro
```

## Benchmark Categories

### 1. Write Operations
- **StandardCompeting_WriteToSingleChannel** (Baseline): Write all items to one channel
- **SideChannel_WriteWithRouting**: Route data to data channel, broadcast control to control channels

### 2. Read Operations
- **StandardCompeting_ReadFromSingleChannel**: Read from single channel
- **SideChannel_ReadFromMergedChannels**: Read from merged data + control channels

### 3. Isolated Component Benchmarks
- **Isolated_MergeOperation**: Pure merge cost (2 channels → 1)
- **Isolated_ControlSignalDetection**: Overhead of `IsControlSignal()` checks
- **Isolated_SingleChannelWrite**: Baseline channel write/read
- **Isolated_DualChannelWriteWithRouting**: Dual channel with routing logic

## What These Benchmarks Measure

The microbenchmarks isolate specific mechanisms:

1. **Routing overhead**: Cost of checking `IsControlSignal()` and routing
2. **Broadcast cost**: Writing control signals to multiple channels concurrently
3. **Merge overhead**: Combining two channels into one
4. **Channel configuration impact**: Effect of `singleReader`/`singleWriter` optimizations

## Expected Results

Based on the architecture:

- **Write routing overhead**: ~5-10% due to signal detection and conditional logic
- **Broadcast cost**: Proportional to number of consumers (2x writes for 2 consumers)
- **Merge overhead**: Additional channel operations for combining streams
- **Read performance**: Similar to baseline with merge channel buffering

## Interpreting Results

BenchmarkDotNet provides:
- **Mean**: Average execution time
- **Error**: Standard error of the mean
- **StdDev**: Standard deviation
- **Ratio**: Comparison to baseline
- **Allocated**: Memory allocations

Focus on:
1. **Ratio to baseline**: Is overhead acceptable (< 1.15 = less than 15%)?
2. **Standard deviation**: High variance indicates unstable measurements
3. **Memory allocations**: Side-channel should have similar allocation patterns

## Configuration

- **Runtime**: .NET 8.0
- **Job**: Simple (3 warmup, 10 iterations)
- **Memory Diagnoser**: Enabled for allocation tracking
- **Test Data**: 10,000 items + 100 control signals

## Example Output

```
| Method                                    | Mean      | Error    | StdDev   | Ratio | Allocated |
|------------------------------------------ |----------:|---------:|---------:|------:|----------:|
| StandardCompeting_WriteToSingleChannel    | 15.23 ms  | 0.201 ms | 0.188 ms |  1.00 |  1.05 MB  |
| SideChannel_WriteWithRouting              | 16.89 ms  | 0.243 ms | 0.227 ms |  1.11 |  1.12 MB  |
| Isolated_MergeOperation                   |  2.34 ms  | 0.045 ms | 0.042 ms |  0.15 |  0.23 MB  |
```

## Notes

- Results may vary based on system load, CPU, and .NET runtime version
- Run in Release mode for accurate measurements
- Close other applications to reduce system noise
- Consider running multiple times and comparing median results
- These are microbenchmarks; real-world performance depends on actual workloads

## Comparison with Full Benchmarks

The **full benchmarks** (`sidechannel`) test end-to-end scenarios with:
- Complete DataFlow pipeline execution
- Block coordination and lifecycle
- Service provider integration
- Multiple iterations with warm-up

The **microbenchmarks** (`sidechannel-micro`) isolate:
- Individual channel operations
- Routing and merge logic
- Pure overhead without pipeline coordination

Both are valuable:
- **Full benchmarks**: Real-world performance expectations
- **Microbenchmarks**: Understanding component-level costs
