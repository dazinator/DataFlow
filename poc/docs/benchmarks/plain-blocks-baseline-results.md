# Plain Blocks Baseline Performance Results

## Methodology

- **Warmup**: 1,000 items to eliminate JIT compilation and initialization overhead
- **Measurement**: 10,000 items for steady-state performance measurement
- **Metrics**:
  - Throughput: Items processed per second (higher is better)
  - Average Latency: Milliseconds per item (lower is better)

## System Configuration

- **Date**: 2025-11-05 23:55:26 UTC
- **OS**: Unix 6.11.0.1018
- **.NET Version**: 8.0.21
- **Processor Count**: 2

## Baseline Results

| Benchmark | Items | Duration (ms) | Throughput (items/sec) | Avg Latency (ms/item) |
|-----------|------:|---------------:|-----------------------:|----------------------:|
| TransformerBlock-1to1 | 10,000 | 16 | 611,598 | 0.002 |
| TransformerBlock-1toMany | 30,000 | 63 | 476,007 | 0.002 |
| TransformerBlock-Filtering | 10,000 | 17 | 556,041 | 0.002 |
| ProcessorBlock-Simple | 10,000 | 15 | 664,037 | 0.002 |
| ProcessorBlock-Async | 10,000 | 11,418 | 876 | 1.142 |

## TransformerBlock Scenarios

### 1-to-1 Transformation
- **Operation**: `x => x * 2`
- **Use Case**: Simple value transformation

### 1-to-Many Transformation
- **Operation**: `x => [x, x*2, x*3]`
- **Use Case**: Data expansion, generating multiple outputs per input

### Filtering Transformation
- **Operation**: `x => x % 2 == 0 ? [x] : []`
- **Use Case**: Data filtering, conditional pass-through

## ProcessorBlock Scenarios

### Simple Side Effect
- **Operation**: `counter++` (synchronous)
- **Use Case**: Simple state updates, counters

### Async Operation
- **Operation**: `await Task.Delay(1)` (simulated I/O)
- **Use Case**: Database writes, API calls, file I/O

## Notes

- These baselines establish the performance characteristics before consolidation
- ActorBlock equivalents should match within <1% overhead after warmup
- Any regression >1% requires investigation and optimization

