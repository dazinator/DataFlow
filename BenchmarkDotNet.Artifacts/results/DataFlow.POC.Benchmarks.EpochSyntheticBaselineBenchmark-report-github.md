```

BenchmarkDotNet v0.13.12, Ubuntu 24.04.3 LTS (Noble Numbat)
Intel Xeon Platinum 8370C CPU 2.80GHz, 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.101
  [Host]   : .NET 8.0.22 (8.0.2225.52707), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  .NET 8.0 : .NET 8.0.22 (8.0.2225.52707), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                                  | Mean     | Error   | StdDev  | Ratio | Gen0   | Allocated | Alloc Ratio |
|---------------------------------------- |---------:|--------:|--------:|------:|-------:|----------:|------------:|
| Baseline_PureDataFlow_NoEpochs          | 331.5 μs | 0.72 μs | 0.64 μs |  1.00 |      - |     168 B |        1.00 |
| Phase4_StreamingSegmentation_Sequential | 554.5 μs | 2.48 μs | 2.20 μs |  1.67 |      - |   13025 B |       77.53 |
| Phase4_StreamingSegmentation_Overlapped | 556.9 μs | 2.02 μs | 1.79 μs |  1.68 |      - |   13025 B |       77.53 |
| Phase4_GlobalAlignment                  | 629.4 μs | 1.96 μs | 1.63 μs |  1.90 | 2.9297 |   89217 B |      531.05 |
