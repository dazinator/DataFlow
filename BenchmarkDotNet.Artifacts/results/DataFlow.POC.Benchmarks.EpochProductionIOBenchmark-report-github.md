```

BenchmarkDotNet v0.13.12, Ubuntu 24.04.3 LTS (Noble Numbat)
Intel Xeon Platinum 8370C CPU 2.80GHz, 1 CPU, 2 logical cores and 1 physical core
.NET SDK 10.0.101
  [Host]     : .NET 8.0.22 (8.0.2225.52707), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-GTVTZU : .NET 8.0.22 (8.0.2225.52707), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

IterationCount=5  WarmupCount=3  

```
| Method                               | Mean    | Error    | StdDev   | Ratio | Allocated | Alloc Ratio |
|------------------------------------- |--------:|---------:|---------:|------:|----------:|------------:|
| Baseline_RealisticIO                 | 3.000 s | 0.0121 s | 0.0031 s |  1.00 |  17.66 KB |        1.00 |
| EpochPipeline_RealisticIO_100Epochs  | 3.010 s | 0.0149 s | 0.0039 s |  1.00 |  55.39 KB |        3.14 |
| EpochPipeline_RealisticIO_1000Epochs | 3.010 s | 0.0094 s | 0.0015 s |  1.00 | 322.58 KB |       18.26 |
| EpochPipeline_RealisticIO_10Epochs   | 3.012 s | 0.0053 s | 0.0008 s |  1.00 |  21.72 KB |        1.23 |
