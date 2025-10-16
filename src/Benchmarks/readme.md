## Benchmarks

This directory contains performance benchmarks for the DataFlow library. See also the [Profiling Benchmarks](Profiling/README.md) for time-series performance analysis.


#### Running the benchmarks

```
dotnet build -c Release
dotnet run -c Release
```

To specify which ones to run

```
dotnet run -c Release [arg]
```

choices are:

| Benchmark | Description | Cli Arg |
|-----------|-------------|-------------|
| Diagnose | Doesn't run any tradition benchmark just executes a very minimal data flow directly without involving benchmark dotnet at all and writes any error to console | diagnose |
| Minimal | Benchmark a minimal data flow vs TPL with simple 100 items doing pretty much nothing | minimal |
| Simple | Benchmark a simple data flow against TPL with varying workload and concurreny | simple |
| RateLimitBlock Memory | Benchmark a rate limited flow vs non rate limited with respect to memory consumption | memory-rate |
| ETL Benchmark | Sophisticated ETL benchmark with BenchmarkDotNet for aggregate statistics | etl-benchmark |
| ETL Direct | Complex ETL direct execution for external profiling (dotnet-trace, dotnet-counters, OpenTelemetry) | etl-direct |


e.g `dotnet run -c Release memory-rate`
