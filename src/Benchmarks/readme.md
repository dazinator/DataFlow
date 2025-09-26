## Benchmarks


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


e.g `dotnet run -c Release memory-rate`
