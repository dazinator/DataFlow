namespace DataFlow.POC.Benchmarks;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Benchmark runner that outputs results in a format compatible with Python benchmarks
/// for direct comparison and visualization.
/// </summary>
public static class PythonComparativeBenchmark
{
    public record BenchmarkConfig(
        int RecordCount,
        int MaxConcurrency,
        string Name
    );

    public record BenchmarkResult(
        string Library,
        int RecordCount,
        int Concurrency,
        double ExecutionTimeSec,
        double ThroughputPerSec,
        double PeakMemoryMB,
        int GcGen0,
        int GcGen1,
        int GcGen2,
        double? CpuPercent
    );

    public static async Task RunComparativeBenchmarkAsync(string[] args)
    {
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine(".NET DataFlow POC Comparative Benchmark");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();

        // Parse command line arguments
        var configs = ParseConfigs(args);
        var results = new List<BenchmarkResult>();

        foreach (var config in configs)
        {
            Console.WriteLine($"Running: {config.Name} ({config.RecordCount:N0} records, {config.MaxConcurrency} workers)");
            Console.WriteLine("-".PadRight(80, '-'));

            var result = await RunSingleBenchmarkAsync(config);
            results.Add(result);

            PrintResult(result);
            Console.WriteLine();

            // Brief pause between tests
            await Task.Delay(2000);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Task.Delay(1000);
        }

        // Save results to python-benchmarks/results directory
        // Navigate from DataFlow.POC.Benchmarks/bin/Debug/net8.0 up to poc/ directory
        var currentDir = Directory.GetCurrentDirectory();
        var pocPath = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", ".."));
        var outputDir = Path.Combine(pocPath, "python-benchmarks", "results");
        Directory.CreateDirectory(outputDir);
        await SaveResultsAsync(results, outputDir);

        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("Benchmark complete!");
        Console.WriteLine("=".PadRight(80, '='));
    }

    private static List<BenchmarkConfig> ParseConfigs(string[] args)
    {
        // Default configurations matching Python benchmarks
        var recordCounts = new[] { 1000, 5000, 10000 };
        var concurrencyLevels = new[] { 1, 2, 4 };

        // Parse custom configurations if provided
        if (args.Length > 1 && args[0] == "comparative")
        {
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--sizes" && i + 1 < args.Length)
                {
                    var sizes = args[i + 1].Split(',');
                    recordCounts = Array.ConvertAll(sizes, int.Parse);
                    i++;
                }
                else if (args[i] == "--concurrency" && i + 1 < args.Length)
                {
                    var levels = args[i + 1].Split(',');
                    concurrencyLevels = Array.ConvertAll(levels, int.Parse);
                    i++;
                }
            }
        }

        var configs = new List<BenchmarkConfig>();
        foreach (var recordCount in recordCounts)
        {
            foreach (var concurrency in concurrencyLevels)
            {
                configs.Add(new BenchmarkConfig(
                    recordCount,
                    concurrency,
                    $"{recordCount}rec_{concurrency}workers"
                ));
            }
        }

        return configs;
    }

    private static async Task<BenchmarkResult> RunSingleBenchmarkAsync(BenchmarkConfig config)
    {
        // Force GC before benchmark
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Record initial GC counts
        var gen0Before = GC.CollectionCount(0);
        var gen1Before = GC.CollectionCount(1);
        var gen2Before = GC.CollectionCount(2);

        // Record initial memory
        var memoryBefore = GC.GetTotalMemory(false);

        // Start timer
        var stopwatch = Stopwatch.StartNew();

        // Run the benchmark
        // Create minimal ServiceProvider required by SimpleEtlPOC.BuildDataFlow and ExecutionContext
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        
        var graph = SimpleEtlPOC.BuildDataFlow(serviceProvider, config.RecordCount, config.MaxConcurrency);
        var context = new DataFlow.POC.Core.ExecutionContext(serviceProvider, CancellationToken.None);
        await graph.ExecuteAsync(context);

        stopwatch.Stop();

        // Record final GC counts
        var gen0After = GC.CollectionCount(0);
        var gen1After = GC.CollectionCount(1);
        var gen2After = GC.CollectionCount(2);

        // Record peak memory (force GC to get accurate reading)
        var memoryAfter = GC.GetTotalMemory(true);
        var peakMemoryMB = (memoryAfter - memoryBefore) / (1024.0 * 1024.0);

        // Calculate metrics
        var executionTimeSec = stopwatch.Elapsed.TotalSeconds;
        var throughputPerSec = config.RecordCount / executionTimeSec;

        return new BenchmarkResult(
            Library: "DotNet-POC",
            RecordCount: config.RecordCount,
            Concurrency: config.MaxConcurrency,
            ExecutionTimeSec: executionTimeSec,
            ThroughputPerSec: throughputPerSec,
            PeakMemoryMB: Math.Max(0, peakMemoryMB),
            GcGen0: gen0After - gen0Before,
            GcGen1: gen1After - gen1Before,
            GcGen2: gen2After - gen2Before,
            CpuPercent: null // CPU % not easily measurable in .NET without external tools
        );
    }

    private static void PrintResult(BenchmarkResult result)
    {
        Console.WriteLine($"Library: {result.Library}");
        Console.WriteLine($"Execution Time: {result.ExecutionTimeSec:F3} seconds");
        Console.WriteLine($"Throughput: {result.ThroughputPerSec:F0} records/second");
        Console.WriteLine($"Peak Memory: {result.PeakMemoryMB:F2} MB");
        Console.WriteLine($"GC Collections: Gen0={result.GcGen0}, Gen1={result.GcGen1}, Gen2={result.GcGen2}");
    }

    private static async Task SaveResultsAsync(List<BenchmarkResult> results, string outputDir)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        // Save as CSV (compatible with Python benchmark format)
        var csvPath = Path.Combine(outputDir, $"dotnet_results_{timestamp}.csv");
        await using (var writer = new StreamWriter(csvPath))
        {
            // Write header
            await writer.WriteLineAsync("Library,RecordCount,Concurrency,ExecutionTime(s),Throughput(rec/s),PeakMemory(MB),GC_Gen0,GC_Gen1,GC_Gen2,CPU%");

            // Write data
            foreach (var result in results)
            {
                await writer.WriteLineAsync(
                    $"{result.Library}," +
                    $"{result.RecordCount}," +
                    $"{result.Concurrency}," +
                    $"{result.ExecutionTimeSec:F3}," +
                    $"{result.ThroughputPerSec:F0}," +
                    $"{result.PeakMemoryMB:F2}," +
                    $"{result.GcGen0}," +
                    $"{result.GcGen1}," +
                    $"{result.GcGen2}," +
                    $"{(result.CpuPercent.HasValue ? result.CpuPercent.Value.ToString("F1") : "N/A")}"
                );
            }
        }

        Console.WriteLine($"Results saved to: {csvPath}");

        // Save as JSON
        var jsonPath = Path.Combine(outputDir, $"dotnet_results_{timestamp}.json");
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        var jsonResults = results.ConvertAll(r => new
        {
            library = r.Library,
            record_count = r.RecordCount,
            concurrency = r.Concurrency,
            execution_time_sec = r.ExecutionTimeSec,
            throughput_per_sec = r.ThroughputPerSec,
            peak_memory_mb = r.PeakMemoryMB,
            gc_collections = new
            {
                gen0 = r.GcGen0,
                gen1 = r.GcGen1,
                gen2 = r.GcGen2
            },
            cpu_percent = r.CpuPercent
        });

        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(jsonResults, jsonOptions));
        Console.WriteLine($"Results saved to: {jsonPath}");
    }
}
