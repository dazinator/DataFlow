```

BenchmarkDotNet v0.13.12, Ubuntu 24.04.3 LTS (Noble Numbat)
Intel Xeon Platinum 8370C CPU 2.80GHz, 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.101
  [Host]   : .NET 8.0.22 (8.0.2225.52707), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  .NET 8.0 : .NET 8.0.22 (8.0.2225.52707), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                          | AsyncWorkPercentage | Mean     | Error   | StdDev  | Ratio | Allocated | Alloc Ratio |
|-------------------------------- |-------------------- |---------:|--------:|--------:|------:|----------:|------------:|
| **Baseline_VariableAsyncWork**      | **0**                   | **371.0 μs** | **1.79 μs** | **1.67 μs** |  **1.00** |     **168 B** |        **1.00** |
| EpochPipeline_VariableAsyncWork | 0                   | 649.9 μs | 1.56 μs | 1.38 μs |  1.75 |   13025 B |       77.53 |
|                                 |                     |          |         |         |       |           |             |
| **Baseline_VariableAsyncWork**      | **25**                  | **362.3 μs** | **0.38 μs** | **0.32 μs** |  **1.00** |     **168 B** |        **1.00** |
| EpochPipeline_VariableAsyncWork | 25                  | 642.8 μs | 2.29 μs | 2.03 μs |  1.77 |   13025 B |       77.53 |
|                                 |                     |          |         |         |       |           |             |
| **Baseline_VariableAsyncWork**      | **50**                  | **364.8 μs** | **0.43 μs** | **0.38 μs** |  **1.00** |     **168 B** |        **1.00** |
| EpochPipeline_VariableAsyncWork | 50                  | 644.5 μs | 2.48 μs | 2.20 μs |  1.77 |   13025 B |       77.53 |
|                                 |                     |          |         |         |       |           |             |
| **Baseline_VariableAsyncWork**      | **75**                  | **362.3 μs** | **0.48 μs** | **0.38 μs** |  **1.00** |     **168 B** |        **1.00** |
| EpochPipeline_VariableAsyncWork | 75                  | 652.2 μs | 0.90 μs | 0.75 μs |  1.80 |   13025 B |       77.53 |
|                                 |                     |          |         |         |       |           |             |
| **Baseline_VariableAsyncWork**      | **100**                 | **366.0 μs** | **0.95 μs** | **0.79 μs** |  **1.00** |     **168 B** |        **1.00** |
| EpochPipeline_VariableAsyncWork | 100                 | 646.1 μs | 1.03 μs | 0.86 μs |  1.77 |   13025 B |       77.53 |
