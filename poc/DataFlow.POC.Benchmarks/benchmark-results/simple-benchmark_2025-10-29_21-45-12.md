/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/SimpleEtlPOC.cs(147,59): warning CS8425: Async-iterator 'SimpleEtlPOC.EnrichRecord(SimpleEtlPOC.ValidatedRecord, CancellationToken)' has one or more parameters of type 'CancellationToken' but none of them is decorated with the 'EnumeratorCancellation' attribute, so the cancellation token parameter from the generated 'IAsyncEnumerable<>.GetAsyncEnumerator' will be unconsumed [/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj]
/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/ComplexEtlPOC.cs(270,59): warning CS8425: Async-iterator 'ComplexEtlPOC.EnrichRecord(ComplexEtlPOC.ValidatedRecord, CancellationToken)' has one or more parameters of type 'CancellationToken' but none of them is decorated with the 'EnumeratorCancellation' attribute, so the cancellation token parameter from the generated 'IAsyncEnumerable<>.GetAsyncEnumerator' will be unconsumed [/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj]
/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/PerformanceTests.cs(15,30): warning CS7022: The entry point of the program is global code; ignoring 'PerformanceTests.Main(string[])' entry point. [/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj]
/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/TypedChannelBenchmarks.cs(260,48): warning CS1998: This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread. [/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj]
/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/TypedChannelBenchmarks.cs(268,51): warning CS1998: This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread. [/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj]
/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/TypedChannelBenchmarks.cs(276,56): warning CS1998: This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread. [/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj]
/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/SimpleEtlPOC.cs(134,60): warning CS1998: This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread. [/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj]
/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/ComplexEtlPOC.cs(257,60): warning CS1998: This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread. [/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj]
/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/ComplexEtlPOC.cs(296,60): warning CS1998: This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread. [/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj]
/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/ComplexEtlPOC.cs(306,60): warning CS1998: This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread. [/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj]
/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/PerformanceTests.cs(281,48): warning CS1998: This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread. [/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj]
/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/PerformanceTests.cs(289,51): warning CS1998: This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread. [/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj]
/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/PerformanceTests.cs(297,56): warning CS1998: This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread. [/home/runner/work/lib-dataflow/lib-dataflow/poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj]
================================================================================
Simple ETL POC vs Non-POC Performance Comparison
Pipeline: DataSource → Validators → Enrichers → Collector
================================================================================

This simplified benchmark focuses on the core pipeline without:
- Routing (no category-based routing)
- Broadcasting (no fan-out to multiple paths)
- Batching (no batch aggregation)

Testing: Concurrency: 1 (10K records)
  Parameters: Records=10,000, Concurrency=1
--------------------------------------------------------------------------------
    [Non-POC] 12,043 ms | 830 rec/sec | 9,160.09 KB
    [POC] 11,647 ms | 858 rec/sec | 9,792.52 KB

Testing: Concurrency: 2 (10K records)
  Parameters: Records=10,000, Concurrency=2
--------------------------------------------------------------------------------
    [Non-POC] 5,797 ms | 1,725 rec/sec | 8,177.62 KB
    [POC] 5,830 ms | 1,715 rec/sec | 9,806.31 KB

Testing: Concurrency: 4 (10K records)
  Parameters: Records=10,000, Concurrency=4
--------------------------------------------------------------------------------
    [Non-POC] 2,898 ms | 3,450 rec/sec | 7,622.52 KB
    [POC] 2,957 ms | 3,381 rec/sec | 9,677.49 KB

Testing: Concurrency: 8 (10K records)
  Parameters: Records=10,000, Concurrency=8
--------------------------------------------------------------------------------
    [Non-POC] 1,491 ms | 6,706 rec/sec | 7,436.23 KB
    [POC] 1,492 ms | 6,702 rec/sec | 9,676.58 KB


================================================================================
SUMMARY - SIMPLE ETL COMPARISON
================================================================================

Concurrency: 1 (10K records)
  Config: 10,000 records | Concurrency: 1
  Time:   Non-POC=12043ms, POC=11647ms (ratio=0.97x)
  Memory: Non-POC=9,160KB, POC=9,793KB (ratio=1.07x)

Concurrency: 2 (10K records)
  Config: 10,000 records | Concurrency: 2
  Time:   Non-POC=5797ms, POC=5830ms (ratio=1.01x)
  Memory: Non-POC=8,178KB, POC=9,806KB (ratio=1.20x)

Concurrency: 4 (10K records)
  Config: 10,000 records | Concurrency: 4
  Time:   Non-POC=2898ms, POC=2957ms (ratio=1.02x)
  Memory: Non-POC=7,623KB, POC=9,677KB (ratio=1.27x)

Concurrency: 8 (10K records)
  Config: 10,000 records | Concurrency: 8
  Time:   Non-POC=1491ms, POC=1492ms (ratio=1.00x)
  Memory: Non-POC=7,436KB, POC=9,677KB (ratio=1.30x)

Simple benchmark results saved to: /home/runner/work/lib-dataflow/lib-dataflow/benchmark-results/simple-benchmark_2025-10-29_21-46-26.md
