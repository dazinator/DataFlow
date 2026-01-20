```

BenchmarkDotNet v0.13.12, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.101
  [Host]     : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX2
  Job-WTGITC : .NET 8.0.23 (8.0.2325.60607), X64 RyuJIT AVX2

IterationCount=5  WarmupCount=3  

```
| Method                               | Mean    | Error    | StdDev   | Ratio | Allocated | Alloc Ratio |
|------------------------------------- |--------:|---------:|---------:|------:|----------:|------------:|
| Baseline_RealisticIO                 | 2.991 s | 0.0022 s | 0.0006 s |  1.00 |  17.66 KB |        1.00 |
| EpochPipeline_RealisticIO_100Epochs  | 3.000 s | 0.0038 s | 0.0010 s |  1.00 |  55.39 KB |        3.14 |
| EpochPipeline_RealisticIO_1000Epochs | 3.000 s | 0.0060 s | 0.0009 s |  1.00 | 322.58 KB |       18.26 |
| EpochPipeline_RealisticIO_10Epochs   | 3.001 s | 0.0013 s | 0.0002 s |  1.00 |  21.72 KB |        1.23 |
