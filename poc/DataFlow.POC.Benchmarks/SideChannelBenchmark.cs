namespace DataFlow.POC.Benchmarks;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataFlow.POC.Benchmarks.DeprecatedBlocks;
using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using PocExecutionContext = DataFlow.POC.Core.ExecutionContext;

/// <summary>
/// Improved benchmark comparing performance overhead of side-channel competing edges vs standard competing edges.
/// Uses multiple iterations, warm-up runs, and realistic workloads to get accurate measurements.
/// </summary>
public class SideChannelBenchmark
{
    public class BenchmarkResult
    {
        public string Scenario { get; set; } = string.Empty;
        public string Strategy { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public int ControlSignalCount { get; set; }
        public int ConsumerCount { get; set; }
        public TimeSpan Duration { get; set; }
        public double ThroughputPerSecond { get; set; }
        public long MemoryBytes { get; set; }
        public int Iteration { get; set; }
    }

    public static async Task RunBenchmarkAsync()
    {
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("Side-Channel Competing Edge Performance Benchmark");
        Console.WriteLine("Improved methodology with warm-up and multiple iterations");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();

        var results = new List<BenchmarkResult>();

        // Smaller set of configurations for thorough testing
        var configs = new[]
        {
            new { Items = 10000, ControlSignals = 0, Name = "10K items, no control signals" },
            new { Items = 10000, ControlSignals = 100, Name = "10K items, 100 control signals" },
            new { Items = 50000, ControlSignals = 0, Name = "50K items, no control signals" },
            new { Items = 50000, ControlSignals = 500, Name = "50K items, 500 control signals" },
        };

        const int iterationsPerTest = 3; // Run each test multiple times
        const int warmupRuns = 2; // Warm-up runs before measurement

        Console.WriteLine("Running warm-up iterations...");
        // Warm-up phase
        for (int i = 0; i < warmupRuns; i++)
        {
            await RunStandardCompetingAsync(1000, 0, "warmup", isWarmup: true);
            await RunSideChannelCompetingAsync(1000, 0, "warmup", isWarmup: true);
            await CleanupAsync();
        }

        Console.WriteLine("\nStarting measurement phase...\n");

        foreach (var config in configs)
        {
            Console.WriteLine($"Testing: {config.Name}");
            Console.WriteLine("-".PadRight(80, '-'));

            // Alternate between strategies to avoid systematic bias
            for (int iter = 1; iter <= iterationsPerTest; iter++)
            {
                Console.WriteLine($"  Iteration {iter}/{iterationsPerTest}");

                // Test both strategies with alternating order per iteration
                if (iter % 2 == 1)
                {
                    var standardResult = await RunStandardCompetingAsync(
                        config.Items, config.ControlSignals, config.Name, iteration: iter);
                    results.Add(standardResult);
                    await CleanupAsync();

                    var sideChannelResult = await RunSideChannelCompetingAsync(
                        config.Items, config.ControlSignals, config.Name, iteration: iter);
                    results.Add(sideChannelResult);
                    await CleanupAsync();
                }
                else
                {
                    var sideChannelResult = await RunSideChannelCompetingAsync(
                        config.Items, config.ControlSignals, config.Name, iteration: iter);
                    results.Add(sideChannelResult);
                    await CleanupAsync();

                    var standardResult = await RunStandardCompetingAsync(
                        config.Items, config.ControlSignals, config.Name, iteration: iter);
                    results.Add(standardResult);
                    await CleanupAsync();
                }
            }
            Console.WriteLine();
        }

        // Print and save results
        PrintResults(results);
        await SaveResultsAsync(results);
    }

    private static async Task<BenchmarkResult> RunStandardCompetingAsync(
        int itemCount,
        int controlSignalCount,
        string scenario,
        bool isWarmup = false,
        int iteration = 0)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var processedCount = 0;
        var processLock = new object();

        var producer = new ProducerBlock<IDataEnvelope>(
            "producer",
            ctx => ProduceEnvelopes(itemCount, controlSignalCount, ctx));

        var consumer1 = new EnvelopeProcessorBlock<int>(new BlockContext("consumer1"),
            processData: async (value, ctx) =>
            {
                lock (processLock) processedCount++;
                await SimulateWork();
            });

        var consumer2 = new EnvelopeProcessorBlock<int>(new BlockContext("consumer2"),
            processData: async (value, ctx) =>
            {
                lock (processLock) processedCount++;
                await SimulateWork();
            });

        var builder = GraphHelpers.CreateGraphBuilder("standard-competing-flow");
        builder.AddBlock(producer)
            .AddBlock(consumer1)
            .AddBlock(consumer2);

        // Use standard competing strategy (baseline)
        var strategy = EnvelopeEdgeStrategyFactory.CreateCompeting();
        builder.AddEdge(new Edge(producer, new[] { consumer1, consumer2 }, strategy));

        var graph = builder.Build();
        var context = new PocExecutionContext(services, CancellationToken.None);

        var sw = Stopwatch.StartNew();
        var memoryBefore = GC.GetTotalMemory(true);

        await graph.ExecuteAsync(context);

        sw.Stop();
        var memoryAfter = GC.GetTotalMemory(false);

        if (!isWarmup)
        {
            Console.WriteLine($"    Standard Competing: {sw.ElapsedMilliseconds:N0} ms, " +
                             $"{itemCount / sw.Elapsed.TotalSeconds:N0} items/sec, " +
                             $"{(memoryAfter - memoryBefore) / 1024.0:N0} KB");
        }

        return new BenchmarkResult
        {
            Scenario = scenario,
            Strategy = "Standard Competing",
            ItemCount = itemCount,
            ControlSignalCount = controlSignalCount,
            ConsumerCount = 2,
            Duration = sw.Elapsed,
            ThroughputPerSecond = itemCount / sw.Elapsed.TotalSeconds,
            MemoryBytes = memoryAfter - memoryBefore,
            Iteration = iteration
        };
    }

    private static async Task<BenchmarkResult> RunSideChannelCompetingAsync(
        int itemCount,
        int controlSignalCount,
        string scenario,
        bool isWarmup = false,
        int iteration = 0)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var processedCount = 0;
        var controlSignalsReceived = 0;
        var processLock = new object();

        var producer = new ProducerBlock<IDataEnvelope>(
            "producer",
            ctx => ProduceEnvelopes(itemCount, controlSignalCount, ctx));

        var consumer1 = new EnvelopeProcessorBlock<int>(new BlockContext("consumer1"),
            processData: async (value, ctx) =>
            {
                lock (processLock) processedCount++;
                await SimulateWork();
            },
            processControl: async (signal, ctx) =>
            {
                lock (processLock) controlSignalsReceived++;
                await Task.CompletedTask;
            });

        var consumer2 = new EnvelopeProcessorBlock<int>(new BlockContext("consumer2"),
            processData: async (value, ctx) =>
            {
                lock (processLock) processedCount++;
                await SimulateWork();
            },
            processControl: async (signal, ctx) =>
            {
                lock (processLock) controlSignalsReceived++;
                await Task.CompletedTask;
            });

        var builder = GraphHelpers.CreateGraphBuilder("side-channel-competing-flow");
        builder.AddBlock(producer)
            .AddBlock(consumer1)
            .AddBlock(consumer2);

        // Use side-channel competing strategy
        var strategy = EnvelopeEdgeStrategyFactory.CreateCompetingWithSideChannel();
        builder.AddEdge(new Edge(producer, new[] { consumer1, consumer2 }, strategy));

        var graph = builder.Build();
        var context = new PocExecutionContext(services, CancellationToken.None);

        var sw = Stopwatch.StartNew();
        var memoryBefore = GC.GetTotalMemory(true);

        await graph.ExecuteAsync(context);

        sw.Stop();
        var memoryAfter = GC.GetTotalMemory(false);

        var strategyName = controlSignalCount > 0 
            ? "Side-Channel (with control)" 
            : "Side-Channel (no control)";

        if (!isWarmup)
        {
            Console.WriteLine($"    {strategyName}: {sw.ElapsedMilliseconds:N0} ms, " +
                             $"{itemCount / sw.Elapsed.TotalSeconds:N0} items/sec, " +
                             $"{(memoryAfter - memoryBefore) / 1024.0:N0} KB");
        }

        return new BenchmarkResult
        {
            Scenario = scenario,
            Strategy = strategyName,
            ItemCount = itemCount,
            ControlSignalCount = controlSignalCount,
            ConsumerCount = 2,
            Duration = sw.Elapsed,
            ThroughputPerSecond = itemCount / sw.Elapsed.TotalSeconds,
            MemoryBytes = memoryAfter - memoryBefore,
            Iteration = iteration
        };
    }

    private static async IAsyncEnumerable<IDataEnvelope> ProduceEnvelopes(
        int itemCount,
        int controlSignalCount,
        IExecutionContext ctx)
    {
        var controlSignalInterval = controlSignalCount > 0 
            ? itemCount / controlSignalCount 
            : int.MaxValue;

        for (int i = 0; i < itemCount; i++)
        {
            yield return new DataItem<int>(i);

            // Insert control signals at regular intervals
            if (controlSignalCount > 0 && (i + 1) % controlSignalInterval == 0 && i < itemCount - 1)
            {
                yield return new CheckpointBarrier(Guid.NewGuid(), DateTime.UtcNow);
            }
        }

        await Task.CompletedTask;
    }

    private static Task SimulateWork()
    {
        // Simulate minimal work to test overhead
        // Keep it truly minimal to measure channel overhead
        return Task.CompletedTask;
    }

    private static async Task CleanupAsync()
    {
        await Task.Delay(500);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Task.Delay(500);
    }

    private static void PrintResults(List<BenchmarkResult> results)
    {
        Console.WriteLine();
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("BENCHMARK RESULTS SUMMARY (Multiple Iterations)");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();

        // Group by scenario and strategy, calculate statistics
        var grouped = results
            .GroupBy(r => new { r.Scenario, r.Strategy })
            .Select(g => new
            {
                g.Key.Scenario,
                g.Key.Strategy,
                ItemCount = g.First().ItemCount,
                ControlSignalCount = g.First().ControlSignalCount,
                AvgDuration = g.Average(r => r.Duration.TotalMilliseconds),
                MinDuration = g.Min(r => r.Duration.TotalMilliseconds),
                MaxDuration = g.Max(r => r.Duration.TotalMilliseconds),
                StdDev = CalculateStdDev(g.Select(r => r.Duration.TotalMilliseconds)),
                AvgThroughput = g.Average(r => r.ThroughputPerSecond),
                AvgMemory = g.Average(r => r.MemoryBytes / 1024.0)
            })
            .ToList();

        // Group by scenario for comparison
        var byScenario = grouped.GroupBy(g => g.Scenario).ToList();

        foreach (var scenarioGroup in byScenario)
        {
            Console.WriteLine($"Scenario: {scenarioGroup.Key}");
            Console.WriteLine("-".PadRight(80, '-'));

            var baseline = scenarioGroup.FirstOrDefault(g => g.Strategy == "Standard Competing");
            
            foreach (var result in scenarioGroup)
            {
                var overheadPct = baseline != null && result != baseline
                    ? ((result.AvgDuration - baseline.AvgDuration) / baseline.AvgDuration * 100)
                    : 0;

                Console.WriteLine($"  {result.Strategy,-30} | " +
                                $"Avg: {result.AvgDuration,6:N1} ms | " +
                                $"Range: {result.MinDuration,6:N1}-{result.MaxDuration,6:N1} ms | " +
                                $"StdDev: {result.StdDev,5:N2} ms | " +
                                $"{(overheadPct != 0 ? $"{overheadPct:+0.0;-0.0}%" : "baseline")}");
            }
            Console.WriteLine();
        }

        // Calculate average overhead across all scenarios
        var standardAvgs = grouped.Where(g => g.Strategy == "Standard Competing").ToList();
        var sideChannelNoControlAvgs = grouped.Where(g => g.Strategy == "Side-Channel (no control)").ToList();

        if (standardAvgs.Any() && sideChannelNoControlAvgs.Any())
        {
            var avgOverhead = sideChannelNoControlAvgs.Select((r, i) =>
            {
                var baseline = standardAvgs[i];
                return (r.AvgDuration - baseline.AvgDuration) / baseline.AvgDuration * 100;
            }).Average();

            Console.WriteLine($"Overall average overhead (side-channel vs standard): {avgOverhead:+0.0;-0.0}%");
            Console.WriteLine();
            
            if (Math.Abs(avgOverhead) > 20)
            {
                Console.WriteLine("⚠ WARNING: Overhead exceeds 20%. Results may be affected by:");
                Console.WriteLine("  - JIT compilation artifacts");
                Console.WriteLine("  - GC timing variations");
                Console.WriteLine("  - System background processes");
                Console.WriteLine("  Consider running with more iterations or in isolation.");
            }
        }
    }

    private static double CalculateStdDev(IEnumerable<double> values)
    {
        var valueList = values.ToList();
        if (valueList.Count <= 1) return 0;
        
        var avg = valueList.Average();
        var sumOfSquares = valueList.Sum(v => Math.Pow(v - avg, 2));
        return Math.Sqrt(sumOfSquares / (valueList.Count - 1));
    }

    private static async Task SaveResultsAsync(List<BenchmarkResult> results)
    {
        var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "benchmark-results");
        Directory.CreateDirectory(outputDir);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var outputPath = Path.Combine(outputDir, $"side-channel-benchmark_{timestamp}.csv");

        var lines = new List<string>
        {
            "Scenario,Strategy,ItemCount,ControlSignalCount,ConsumerCount,Iteration,DurationMs,ThroughputPerSec,MemoryBytes"
        };

        foreach (var result in results)
        {
            lines.Add($"{result.Scenario},{result.Strategy},{result.ItemCount}," +
                     $"{result.ControlSignalCount},{result.ConsumerCount},{result.Iteration}," +
                     $"{result.Duration.TotalMilliseconds:F2},{result.ThroughputPerSecond:F2}," +
                     $"{result.MemoryBytes}");
        }

        await File.WriteAllLinesAsync(outputPath, lines);
        Console.WriteLine($"Results saved to: {outputPath}");
    }
}
