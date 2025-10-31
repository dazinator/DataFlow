namespace DataFlow.POC.Benchmarks;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Uniun.DataFlow;
using PocExecutionContext = DataFlow.POC.Core.ExecutionContext;

/// <summary>
/// Direct execution comparison benchmark designed for external profiling with dotnet-counters.
/// This runs POC and Non-POC implementations directly without BenchmarkDotNet overhead,
/// enabling time-series metric collection.
/// </summary>
public class DirectComparisonBenchmark
{
    /// <summary>
    /// Run both POC and Non-POC implementations with the specified parameters.
    /// Designed to be profiled with dotnet-counters for time-series analysis.
    /// </summary>
    /// <param name="recordCount">Number of records to process</param>
    /// <param name="maxConcurrency">Maximum concurrency level</param>
    /// <param name="batchSize">Batch size (for extended comparison)</param>
    /// <param name="iterations">Number of times to repeat the test</param>
    /// <param name="useSimplePipeline">If true, use simple pipeline (no routing/batching), else use complex pipeline</param>
    public static async Task RunDirectComparisonAsync(
        int recordCount,
        int maxConcurrency,
        int batchSize,
        int iterations,
        bool useSimplePipeline)
    {
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("DataFlow POC vs Non-POC Direct Comparison");
        Console.WriteLine($"Mode: {(useSimplePipeline ? "Simple" : "Complex")} Pipeline");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();
        Console.WriteLine($"Configuration:");
        Console.WriteLine($"  Records:     {recordCount:N0}");
        Console.WriteLine($"  Concurrency: {maxConcurrency}");
        if (!useSimplePipeline)
        {
            Console.WriteLine($"  Batch Size:  {batchSize}");
        }
        Console.WriteLine($"  Iterations:  {iterations}");
        Console.WriteLine();
        Console.WriteLine("This benchmark runs both implementations sequentially.");
        Console.WriteLine("Use dotnet-counters to monitor real-time performance metrics.");
        Console.WriteLine();

        for (int i = 1; i <= iterations; i++)
        {
            Console.WriteLine($"Iteration {i}/{iterations}");
            Console.WriteLine("-".PadRight(80, '-'));

            // Run Non-POC
            Console.WriteLine("Running Non-POC implementation...");
            var nonPocResult = await RunNonPocDirectAsync(
                recordCount,
                maxConcurrency,
                batchSize,
                useSimplePipeline);
            
            Console.WriteLine($"  Non-POC: {nonPocResult.ElapsedMs:N0} ms | {nonPocResult.RecordsPerSecond:N0} rec/sec");

            // Brief pause between implementations
            await Task.Delay(2000);

            // Run POC
            Console.WriteLine("Running POC implementation...");
            var pocResult = await RunPocDirectAsync(
                recordCount,
                maxConcurrency,
                batchSize,
                useSimplePipeline);
            
            Console.WriteLine($"  POC:     {pocResult.ElapsedMs:N0} ms | {pocResult.RecordsPerSecond:N0} rec/sec");

            // Report comparison
            var ratio = (double)pocResult.ElapsedMs / nonPocResult.ElapsedMs;
            Console.WriteLine($"  Ratio:   {ratio:F2}x (POC vs Non-POC)");
            Console.WriteLine();

            // Pause between iterations
            if (i < iterations)
            {
                await Task.Delay(3000);
            }
        }

        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("Benchmark completed successfully");
        Console.WriteLine("=".PadRight(80, '='));
    }

    private static async Task<BenchmarkResult> RunNonPocDirectAsync(
        int recordCount,
        int maxConcurrency,
        int batchSize,
        bool useSimplePipeline)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddDataFlowMetrics();
        services.AddDataFlows();
        var serviceProvider = services.BuildServiceProvider();

        var sw = Stopwatch.StartNew();

        try
        {
            if (useSimplePipeline)
            {
                var builder = global::Benchmarks.Shared.SimpleEtlDataFlow.BuildDataFlow(
                    serviceProvider,
                    recordCount,
                    maxConcurrency);

                var dataflow = builder.Build();
                var context = new DataFlowContext
                {
                    InvocationId = Guid.NewGuid(),
                    ServiceProvider = serviceProvider,
                    CancellationToken = CancellationToken.None,
                    Name = "SimpleEtlBenchmark"
                };

                await dataflow.ExecuteAsync(context);
            }
            else
            {
                var builder = global::Benchmarks.Shared.ComplexEtlDataFlow.BuildDataFlow(
                    serviceProvider,
                    recordCount,
                    maxConcurrency,
                    batchSize);

                var dataflow = builder.Build();
                var context = new DataFlowContext
                {
                    InvocationId = Guid.NewGuid(),
                    ServiceProvider = serviceProvider,
                    CancellationToken = CancellationToken.None,
                    Name = "ComplexEtlBenchmark"
                };

                await dataflow.ExecuteAsync(context);
            }

            sw.Stop();
        }
        finally
        {
            serviceProvider.Dispose();
        }

        return new BenchmarkResult
        {
            ElapsedMs = sw.ElapsedMilliseconds,
            RecordCount = recordCount
        };
    }

    private static async Task<BenchmarkResult> RunPocDirectAsync(
        int recordCount,
        int maxConcurrency,
        int batchSize,
        bool useSimplePipeline)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        var serviceProvider = services.BuildServiceProvider();

        var sw = Stopwatch.StartNew();

        try
        {
            if (useSimplePipeline)
            {
                var graph = SimpleEtlPOC.BuildDataFlow(
                    serviceProvider,
                    recordCount,
                    maxConcurrency);

                var context = new PocExecutionContext(serviceProvider, CancellationToken.None);
                await graph.ExecuteAsync(context);
            }
            else
            {
                var graph = ComplexEtlPOC.BuildDataFlow(
                    serviceProvider,
                    recordCount,
                    maxConcurrency,
                    batchSize);

                var context = new PocExecutionContext(serviceProvider, CancellationToken.None);
                await graph.ExecuteAsync(context);
            }

            sw.Stop();
        }
        finally
        {
            serviceProvider.Dispose();
        }

        return new BenchmarkResult
        {
            ElapsedMs = sw.ElapsedMilliseconds,
            RecordCount = recordCount
        };
    }

    private class BenchmarkResult
    {
        public long ElapsedMs { get; set; }
        public int RecordCount { get; set; }
        public long RecordsPerSecond => RecordCount * 1000 / Math.Max(1, ElapsedMs);
    }
}
