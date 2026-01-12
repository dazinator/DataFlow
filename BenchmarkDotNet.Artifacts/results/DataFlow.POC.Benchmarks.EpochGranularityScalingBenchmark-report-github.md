```

BenchmarkDotNet v0.13.12, Ubuntu 24.04.3 LTS (Noble Numbat)
Intel Xeon Platinum 8370C CPU 2.80GHz, 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.101
  [Host]     : .NET 8.0.22 (8.0.2225.52707), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-XSBZJV : .NET 8.0.22 (8.0.2225.52707), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

IterationCount=5  WarmupCount=3  

```
| Method                | EpochCount | Mean     | Error     | StdDev    | Ratio | Gen0     | Allocated | Alloc Ratio  |
|---------------------- |----------- |---------:|----------:|----------:|------:|---------:|----------:|-------------:|
| **Baseline_NoEpochs**     | **1**          | **3.588 ms** | **0.0230 ms** | **0.0060 ms** |  **1.00** |        **-** |       **6 B** |         **1.00** |
| WithEpochs_Sequential | 1          | 6.667 ms | 0.0796 ms | 0.0123 ms |  1.86 |        - |     694 B |       115.67 |
| WithEpochs_Overlapped | 1          | 6.312 ms | 0.0557 ms | 0.0145 ms |  1.76 |        - |     670 B |       111.67 |
|                       |            |          |           |           |       |          |           |              |
| **Baseline_NoEpochs**     | **10**         | **3.574 ms** | **0.0201 ms** | **0.0052 ms** |  **1.00** |        **-** |       **3 B** |         **1.00** |
| WithEpochs_Sequential | 10         | 6.676 ms | 0.0475 ms | 0.0123 ms |  1.87 |        - |    3430 B |     1,143.33 |
| WithEpochs_Overlapped | 10         | 6.137 ms | 0.0572 ms | 0.0148 ms |  1.72 |        - |    3406 B |     1,135.33 |
|                       |            |          |           |           |       |          |           |              |
| **Baseline_NoEpochs**     | **100**        | **3.474 ms** | **0.0276 ms** | **0.0072 ms** |  **1.00** |        **-** |       **3 B** |         **1.00** |
| WithEpochs_Sequential | 100        | 6.646 ms | 0.0249 ms | 0.0065 ms |  1.91 |        - |   30790 B |    10,263.33 |
| WithEpochs_Overlapped | 100        | 6.223 ms | 0.0335 ms | 0.0052 ms |  1.79 |        - |   30766 B |    10,255.33 |
|                       |            |          |           |           |       |          |           |              |
| **Baseline_NoEpochs**     | **1000**       | **3.476 ms** | **0.0101 ms** | **0.0026 ms** |  **1.00** |        **-** |       **3 B** |         **1.00** |
| WithEpochs_Sequential | 1000       | 6.797 ms | 0.0179 ms | 0.0028 ms |  1.96 |   7.8125 |  304390 B |   101,463.33 |
| WithEpochs_Overlapped | 1000       | 6.334 ms | 0.0425 ms | 0.0110 ms |  1.82 |   7.8125 |  304366 B |   101,455.33 |
|                       |            |          |           |           |       |          |           |              |
| **Baseline_NoEpochs**     | **10000**      | **3.498 ms** | **0.0191 ms** | **0.0050 ms** |  **1.00** |        **-** |       **3 B** |         **1.00** |
| WithEpochs_Sequential | 10000      | 8.591 ms | 0.1838 ms | 0.0477 ms |  2.46 | 109.3750 | 3040396 B | 1,013,465.33 |
| WithEpochs_Overlapped | 10000      | 8.463 ms | 0.0954 ms | 0.0248 ms |  2.42 | 109.3750 | 3040372 B | 1,013,457.33 |
