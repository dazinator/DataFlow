/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/PerformanceTests.cs(15,30): warning CS7022: The entry point of the program is global code; ignoring 'PerformanceTests.Main(string[])' entry point. [/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj]
/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/SimpleEtlPOC.cs(134,60): warning CS1998: This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread. [/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj]
/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/ComplexEtlPOC.cs(257,60): warning CS1998: This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread. [/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj]
/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/ComplexEtlPOC.cs(296,60): warning CS1998: This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread. [/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj]
/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/ComplexEtlPOC.cs(306,60): warning CS1998: This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread. [/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj]
/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/TypedChannelBenchmarks.cs(260,48): warning CS1998: This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread. [/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj]
/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/TypedChannelBenchmarks.cs(268,51): warning CS1998: This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread. [/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj]
/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/TypedChannelBenchmarks.cs(276,56): warning CS1998: This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread. [/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj]
/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/PerformanceTests.cs(281,48): warning CS1998: This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread. [/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj]
/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/PerformanceTests.cs(289,51): warning CS1998: This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread. [/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj]
/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/PerformanceTests.cs(297,56): warning CS1998: This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread. [/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj]
================================================================================
DataFlow POC vs Non-POC Extended Performance Comparison
================================================================================

This benchmark tests various combinations of parameters:
- Different record counts (load levels)
- Different concurrency levels
- Different batch sizes

Testing: Load: 1K records
  Parameters: Records=1,000, Concurrency=4, Batch=100
--------------------------------------------------------------------------------
    [Non-POC] 777 ms | 1,287 rec/sec | 185.68 KB
    [POC] 521 ms | 1,917 rec/sec | 143.64 KB

Testing: Load: 5K records
  Parameters: Records=5,000, Concurrency=4, Batch=100
--------------------------------------------------------------------------------
    [Non-POC] 1,482 ms | 3,373 rec/sec | 132.00 KB
    [POC] 1,511 ms | 3,308 rec/sec | 71.33 KB

Testing: Load: 10K records
  Parameters: Records=10,000, Concurrency=4, Batch=100
--------------------------------------------------------------------------------
    [Non-POC] 3,025 ms | 3,305 rec/sec | 101.23 KB
    [POC] 3,024 ms | 3,306 rec/sec | 55.74 KB

Testing: Concurrency: 1 (10K records)
  Parameters: Records=10,000, Concurrency=1, Batch=100
--------------------------------------------------------------------------------
    [Non-POC] 11,771 ms | 850 rec/sec | 96.86 KB
    [POC] 11,712 ms | 854 rec/sec | 42.51 KB

Testing: Concurrency: 2 (10K records)
  Parameters: Records=10,000, Concurrency=2, Batch=100
--------------------------------------------------------------------------------
    [Non-POC] 5,953 ms | 1,680 rec/sec | 98.62 KB
    [POC] 5,898 ms | 1,695 rec/sec | 46.97 KB

Testing: Concurrency: 8 (10K records)
  Parameters: Records=10,000, Concurrency=8, Batch=100
--------------------------------------------------------------------------------
    [Non-POC] 1,523 ms | 6,563 rec/sec | 92.75 KB
    [POC] 1,526 ms | 6,549 rec/sec | 75.12 KB

Testing: Batch: 50 (10K records)
  Parameters: Records=10,000, Concurrency=4, Batch=50
--------------------------------------------------------------------------------
    [Non-POC] 3,027 ms | 3,303 rec/sec | 106.69 KB
    [POC] 2,985 ms | 3,349 rec/sec | 55.56 KB

Testing: Batch: 200 (10K records)
  Parameters: Records=10,000, Concurrency=4, Batch=200
--------------------------------------------------------------------------------
    [Non-POC] 2,948 ms | 3,391 rec/sec | 101.00 KB
    [POC] 2,968 ms | 3,368 rec/sec | 55.61 KB

Testing: Batch: 500 (10K records)
  Parameters: Records=10,000, Concurrency=4, Batch=500
--------------------------------------------------------------------------------
    [Non-POC] 2,950 ms | 3,389 rec/sec | 105.61 KB
    [POC] 2,938 ms | 3,403 rec/sec | 55.62 KB


================================================================================
SUMMARY - EXTENDED COMPARISON
================================================================================

Load: 1K records
  Config: 1,000 records | Concurrency: 4 | Batch: 100
  Time:   Non-POC=777ms, POC=521ms (ratio=0.67x)
  Memory: Non-POC=186KB, POC=144KB (ratio=0.77x)

Load: 5K records
  Config: 5,000 records | Concurrency: 4 | Batch: 100
  Time:   Non-POC=1,482ms, POC=1,511ms (ratio=1.02x)
  Memory: Non-POC=132KB, POC=71KB (ratio=0.54x)

Load: 10K records
  Config: 10,000 records | Concurrency: 4 | Batch: 100
  Time:   Non-POC=3,025ms, POC=3,024ms (ratio=1.00x)
  Memory: Non-POC=101KB, POC=56KB (ratio=0.55x)

Concurrency: 1 (10K records)
  Config: 10,000 records | Concurrency: 1 | Batch: 100
  Time:   Non-POC=11,771ms, POC=11,712ms (ratio=0.99x)
  Memory: Non-POC=97KB, POC=43KB (ratio=0.44x)

Concurrency: 2 (10K records)
  Config: 10,000 records | Concurrency: 2 | Batch: 100
  Time:   Non-POC=5,953ms, POC=5,898ms (ratio=0.99x)
  Memory: Non-POC=99KB, POC=47KB (ratio=0.48x)

Concurrency: 8 (10K records)
  Config: 10,000 records | Concurrency: 8 | Batch: 100
  Time:   Non-POC=1,523ms, POC=1,526ms (ratio=1.00x)
  Memory: Non-POC=93KB, POC=75KB (ratio=0.81x)

Batch: 50 (10K records)
  Config: 10,000 records | Concurrency: 4 | Batch: 50
  Time:   Non-POC=3,027ms, POC=2,985ms (ratio=0.99x)
  Memory: Non-POC=107KB, POC=56KB (ratio=0.52x)

Batch: 200 (10K records)
  Config: 10,000 records | Concurrency: 4 | Batch: 200
  Time:   Non-POC=2,948ms, POC=2,968ms (ratio=1.01x)
  Memory: Non-POC=101KB, POC=56KB (ratio=0.55x)

Batch: 500 (10K records)
  Config: 10,000 records | Concurrency: 4 | Batch: 500
  Time:   Non-POC=2,950ms, POC=2,938ms (ratio=1.00x)
  Memory: Non-POC=106KB, POC=56KB (ratio=0.53x)

Extended results saved to: /home/runner/work/lib-dataflow/lib-dataflow/benchmark-results/extended-benchmark_2025-10-30_22-35-42.md
