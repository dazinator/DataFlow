```

BenchmarkDotNet v0.13.12, Ubuntu 24.04.3 LTS (Noble Numbat)
Intel Xeon Platinum 8370C CPU 2.80GHz, 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.101
  [Host]   : .NET 8.0.22 (8.0.2225.52707), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  .NET 8.0 : .NET 8.0.22 (8.0.2225.52707), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                   | DelayMicroseconds | TaskType  | Mean     | Error   | StdDev  | Ratio | Gen0   | Allocated | Alloc Ratio |
|------------------------- |------------------ |---------- |---------:|--------:|--------:|------:|-------:|----------:|------------:|
| **Baseline_WithDelays**      | **0**                 | **Task**      | **208.7 μs** | **0.60 μs** | **0.50 μs** |  **1.00** |      **-** |     **168 B** |        **1.00** |
| EpochPipeline_WithDelays | 0                 | Task      | 438.4 μs | 0.92 μs | 0.82 μs |  2.10 | 0.4883 |   13024 B |       77.52 |
|                          |                   |           |          |         |         |       |        |           |             |
| **Baseline_WithDelays**      | **0**                 | **ValueTask** | **234.3 μs** | **0.93 μs** | **0.87 μs** |  **1.00** |      **-** |     **168 B** |        **1.00** |
| EpochPipeline_WithDelays | 0                 | ValueTask | 481.5 μs | 1.22 μs | 1.02 μs |  2.05 | 0.4883 |   13024 B |       77.52 |
|                          |                   |           |          |         |         |       |        |           |             |
| **Baseline_WithDelays**      | **1**                 | **Task**      | **218.5 μs** | **0.42 μs** | **0.38 μs** |  **1.00** |      **-** |     **168 B** |        **1.00** |
| EpochPipeline_WithDelays | 1                 | Task      | 455.6 μs | 1.14 μs | 0.89 μs |  2.08 | 0.4883 |   13024 B |       77.52 |
|                          |                   |           |          |         |         |       |        |           |             |
| **Baseline_WithDelays**      | **1**                 | **ValueTask** | **245.9 μs** | **0.82 μs** | **0.73 μs** |  **1.00** |      **-** |     **168 B** |        **1.00** |
| EpochPipeline_WithDelays | 1                 | ValueTask | 484.2 μs | 0.85 μs | 0.71 μs |  1.97 | 0.4883 |   13024 B |       77.52 |
|                          |                   |           |          |         |         |       |        |           |             |
| **Baseline_WithDelays**      | **5**                 | **Task**      | **216.5 μs** | **0.28 μs** | **0.22 μs** |  **1.00** |      **-** |     **168 B** |        **1.00** |
| EpochPipeline_WithDelays | 5                 | Task      | 459.0 μs | 1.68 μs | 1.48 μs |  2.12 | 0.4883 |   13024 B |       77.52 |
|                          |                   |           |          |         |         |       |        |           |             |
| **Baseline_WithDelays**      | **5**                 | **ValueTask** | **245.7 μs** | **0.84 μs** | **0.75 μs** |  **1.00** |      **-** |     **168 B** |        **1.00** |
| EpochPipeline_WithDelays | 5                 | ValueTask | 499.5 μs | 0.67 μs | 0.56 μs |  2.03 | 0.4883 |   13024 B |       77.52 |
|                          |                   |           |          |         |         |       |        |           |             |
| **Baseline_WithDelays**      | **10**                | **Task**      | **217.1 μs** | **0.57 μs** | **0.51 μs** |  **1.00** |      **-** |     **168 B** |        **1.00** |
| EpochPipeline_WithDelays | 10                | Task      | 453.4 μs | 0.58 μs | 0.45 μs |  2.09 | 0.4883 |   13024 B |       77.52 |
|                          |                   |           |          |         |         |       |        |           |             |
| **Baseline_WithDelays**      | **10**                | **ValueTask** | **248.0 μs** | **0.64 μs** | **0.54 μs** |  **1.00** |      **-** |     **168 B** |        **1.00** |
| EpochPipeline_WithDelays | 10                | ValueTask | 492.6 μs | 1.03 μs | 0.96 μs |  1.99 | 0.4883 |   13024 B |       77.52 |
