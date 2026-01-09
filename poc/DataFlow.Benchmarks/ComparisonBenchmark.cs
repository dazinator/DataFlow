namespace DataFlow.POC.Benchmarks;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Uniun.DataFlow;
using PocExecutionContext = DataFlow.POC.Core.ExecutionContext;

/// <summary>
/// Comparison benchmark runner that measures performance of POC vs non-POC implementations
/// </summary>
public class ComparisonBenchmark
{
    public static async Task RunComparisonAsync()
    {
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("DataFlow POC vs Non-POC Performance Comparison");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();

        var testConfigurations = new[]
        {
            new { RecordCount = 1000, MaxConcurrency = 4, BatchSize = 100, Name = "Small Load" },
            new { RecordCount = 10000, MaxConcurrency = 4, BatchSize = 100, Name = "Medium Load" },
            new { RecordCount = 50000, MaxConcurrency = 4, BatchSize = 100, Name = "Large Load" }
        };

        var results = new List<BenchmarkResult>();

        foreach (var config in testConfigurations)
        {
            Console.WriteLine($"Running: {config.Name} ({config.RecordCount:N0} records)");
            Console.WriteLine("-".PadRight(80, '-'));

            // Run non-POC version
            var nonPocResult = await RunNonPocBenchmarkAsync(
                config.RecordCount,
                config.MaxConcurrency,
                config.BatchSize,
                config.Name);
            results.Add(nonPocResult);

            // Small delay between tests
            await Task.Delay(1000);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Task.Delay(1000);

            // Run POC version
            var pocResult = await RunPocBenchmarkAsync(
                config.RecordCount,
                config.MaxConcurrency,
                config.BatchSize,
                config.Name);
            results.Add(pocResult);

            Console.WriteLine();

            // Small delay between configurations
            await Task.Delay(2000);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Task.Delay(1000);
        }

        // Print summary
        PrintSummary(results);

        // Save results to file
        var outputPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "benchmark-results");
        await SaveResultsAsync(results, outputPath);
    }

    private static async Task<BenchmarkResult> RunNonPocBenchmarkAsync(
        int recordCount,
        int maxConcurrency,
        int batchSize,
        string testName)
    {
        Console.WriteLine($"  [Non-POC] Starting...");

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddDataFlowMetrics();
        services.AddDataFlows();
        var serviceProvider = services.BuildServiceProvider();

        var result = new BenchmarkResult
        {
            Implementation = "Non-POC",
            TestName = testName,
            RecordCount = recordCount,
            MaxConcurrency = maxConcurrency,
            BatchSize = batchSize
        };

        // Force GC before measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var initialMemory = GC.GetTotalMemory(true);
        var gen0Before = GC.CollectionCount(0);
        var gen1Before = GC.CollectionCount(1);
        var gen2Before = GC.CollectionCount(2);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Build and execute the non-POC dataflow
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

            stopwatch.Stop();

            result.Success = true;
            result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            result.ThroughputPerSecond = recordCount / stopwatch.Elapsed.TotalSeconds;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Success = false;
            result.ErrorMessage = ex.Message;
            Console.WriteLine($"  [Non-POC] ERROR: {ex.Message}");
        }

        var finalMemory = GC.GetTotalMemory(true);
        result.MemoryUsedBytes = finalMemory - initialMemory;
        result.Gen0Collections = GC.CollectionCount(0) - gen0Before;
        result.Gen1Collections = GC.CollectionCount(1) - gen1Before;
        result.Gen2Collections = GC.CollectionCount(2) - gen2Before;

        Console.WriteLine($"  [Non-POC] Completed in {result.ElapsedMilliseconds:N0} ms | " +
                         $"Throughput: {result.ThroughputPerSecond:N0} rec/sec | " +
                         $"Memory: {result.MemoryUsedBytes / 1024.0:N2} KB");

        serviceProvider.Dispose();
        return result;
    }

    private static async Task<BenchmarkResult> RunPocBenchmarkAsync(
        int recordCount,
        int maxConcurrency,
        int batchSize,
        string testName)
    {
        Console.WriteLine($"  [POC] Starting...");

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        var serviceProvider = services.BuildServiceProvider();

        var result = new BenchmarkResult
        {
            Implementation = "POC",
            TestName = testName,
            RecordCount = recordCount,
            MaxConcurrency = maxConcurrency,
            BatchSize = batchSize
        };

        // Force GC before measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var initialMemory = GC.GetTotalMemory(true);
        var gen0Before = GC.CollectionCount(0);
        var gen1Before = GC.CollectionCount(1);
        var gen2Before = GC.CollectionCount(2);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Build and execute the POC dataflow
            var graph = ComplexEtlPOC.BuildDataFlow(
                serviceProvider,
                recordCount,
                maxConcurrency,
                batchSize);

            var context = new PocExecutionContext(serviceProvider, CancellationToken.None);

            await graph.ExecuteAsync(context);

            stopwatch.Stop();

            result.Success = true;
            result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            result.ThroughputPerSecond = recordCount / stopwatch.Elapsed.TotalSeconds;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Success = false;
            result.ErrorMessage = ex.Message;
            Console.WriteLine($"  [POC] ERROR: {ex.Message}");
        }

        var finalMemory = GC.GetTotalMemory(true);
        result.MemoryUsedBytes = finalMemory - initialMemory;
        result.Gen0Collections = GC.CollectionCount(0) - gen0Before;
        result.Gen1Collections = GC.CollectionCount(1) - gen1Before;
        result.Gen2Collections = GC.CollectionCount(2) - gen2Before;

        Console.WriteLine($"  [POC] Completed in {result.ElapsedMilliseconds:N0} ms | " +
                         $"Throughput: {result.ThroughputPerSecond:N0} rec/sec | " +
                         $"Memory: {result.MemoryUsedBytes / 1024.0:N2} KB");

        serviceProvider.Dispose();
        return result;
    }

    private static void PrintSummary(List<BenchmarkResult> results)
    {
        Console.WriteLine();
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("SUMMARY");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();

        // Group by test name
        var groupedResults = new Dictionary<string, (BenchmarkResult NonPoc, BenchmarkResult Poc)>();
        
        for (int i = 0; i < results.Count; i += 2)
        {
            var nonPoc = results[i];
            var poc = results[i + 1];
            groupedResults[nonPoc.TestName] = (nonPoc, poc);
        }

        foreach (var kvp in groupedResults)
        {
            var testName = kvp.Key;
            var (nonPoc, poc) = kvp.Value;

            Console.WriteLine($"Test: {testName}");
            Console.WriteLine($"Records: {nonPoc.RecordCount:N0} | Concurrency: {nonPoc.MaxConcurrency} | Batch: {nonPoc.BatchSize}");
            Console.WriteLine();

            // Execution Time
            Console.WriteLine("  Execution Time:");
            Console.WriteLine($"    Non-POC: {nonPoc.ElapsedMilliseconds:N0} ms");
            Console.WriteLine($"    POC:     {poc.ElapsedMilliseconds:N0} ms");
            var timeRatio = (double)poc.ElapsedMilliseconds / nonPoc.ElapsedMilliseconds;
            Console.WriteLine($"    Ratio:   {timeRatio:F3}x {(timeRatio < 1 ? "(POC faster)" : "(Non-POC faster)")}");
            Console.WriteLine();

            // Throughput
            Console.WriteLine("  Throughput:");
            Console.WriteLine($"    Non-POC: {nonPoc.ThroughputPerSecond:N0} records/sec");
            Console.WriteLine($"    POC:     {poc.ThroughputPerSecond:N0} records/sec");
            var throughputRatio = poc.ThroughputPerSecond / nonPoc.ThroughputPerSecond;
            Console.WriteLine($"    Ratio:   {throughputRatio:F3}x {(throughputRatio > 1 ? "(POC faster)" : "(Non-POC faster)")}");
            Console.WriteLine();

            // Memory
            Console.WriteLine("  Memory Usage:");
            Console.WriteLine($"    Non-POC: {nonPoc.MemoryUsedBytes / 1024.0:N2} KB");
            Console.WriteLine($"    POC:     {poc.MemoryUsedBytes / 1024.0:N2} KB");
            var memoryRatio = (double)poc.MemoryUsedBytes / nonPoc.MemoryUsedBytes;
            Console.WriteLine($"    Ratio:   {memoryRatio:F3}x {(memoryRatio < 1 ? "(POC uses less)" : "(Non-POC uses less)")}");
            Console.WriteLine();

            // GC Collections
            Console.WriteLine("  GC Collections:");
            Console.WriteLine($"    Non-POC: Gen0={nonPoc.Gen0Collections}, Gen1={nonPoc.Gen1Collections}, Gen2={nonPoc.Gen2Collections}");
            Console.WriteLine($"    POC:     Gen0={poc.Gen0Collections}, Gen1={poc.Gen1Collections}, Gen2={poc.Gen2Collections}");
            Console.WriteLine();
            Console.WriteLine("-".PadRight(80, '-'));
            Console.WriteLine();
        }
    }

    public static async Task SaveResultsAsync(List<BenchmarkResult> results, string outputPath)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
        var filename = Path.Combine(outputPath, $"benchmark-results_{timestamp}.md");

        Directory.CreateDirectory(outputPath);

        using var writer = new StreamWriter(filename);

        await writer.WriteLineAsync("# DataFlow POC vs Non-POC Performance Comparison");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync($"**Date:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        await writer.WriteLineAsync($"**Platform:** {Environment.OSVersion}");
        await writer.WriteLineAsync($"**.NET Version:** {Environment.Version}");
        await writer.WriteLineAsync($"**Processor Count:** {Environment.ProcessorCount}");
        await writer.WriteLineAsync();

        // Group by test name
        var groupedResults = new Dictionary<string, (BenchmarkResult NonPoc, BenchmarkResult Poc)>();
        
        for (int i = 0; i < results.Count; i += 2)
        {
            var nonPoc = results[i];
            var poc = results[i + 1];
            groupedResults[nonPoc.TestName] = (nonPoc, poc);
        }

        foreach (var kvp in groupedResults)
        {
            var testName = kvp.Key;
            var (nonPoc, poc) = kvp.Value;

            await writer.WriteLineAsync($"## {testName}");
            await writer.WriteLineAsync();
            await writer.WriteLineAsync($"**Configuration:** {nonPoc.RecordCount:N0} records, {nonPoc.MaxConcurrency} concurrency, {nonPoc.BatchSize} batch size");
            await writer.WriteLineAsync();

            await writer.WriteLineAsync("### Results");
            await writer.WriteLineAsync();
            await writer.WriteLineAsync("| Metric | Non-POC | POC | Ratio | Winner |");
            await writer.WriteLineAsync("|--------|---------|-----|-------|--------|");

            // Execution Time
            var timeRatio = (double)poc.ElapsedMilliseconds / nonPoc.ElapsedMilliseconds;
            var timeWinner = timeRatio < 1 ? "POC" : "Non-POC";
            await writer.WriteLineAsync($"| Execution Time | {nonPoc.ElapsedMilliseconds:N0} ms | {poc.ElapsedMilliseconds:N0} ms | {timeRatio:F3}x | {timeWinner} |");

            // Throughput
            var throughputRatio = poc.ThroughputPerSecond / nonPoc.ThroughputPerSecond;
            var throughputWinner = throughputRatio > 1 ? "POC" : "Non-POC";
            await writer.WriteLineAsync($"| Throughput | {nonPoc.ThroughputPerSecond:N0} rec/sec | {poc.ThroughputPerSecond:N0} rec/sec | {throughputRatio:F3}x | {throughputWinner} |");

            // Memory
            var memoryRatio = (double)poc.MemoryUsedBytes / nonPoc.MemoryUsedBytes;
            var memoryWinner = memoryRatio < 1 ? "POC" : "Non-POC";
            await writer.WriteLineAsync($"| Memory Usage | {nonPoc.MemoryUsedBytes / 1024.0:N2} KB | {poc.MemoryUsedBytes / 1024.0:N2} KB | {memoryRatio:F3}x | {memoryWinner} |");

            // GC Collections
            await writer.WriteLineAsync($"| Gen0 Collections | {nonPoc.Gen0Collections} | {poc.Gen0Collections} | - | - |");
            await writer.WriteLineAsync($"| Gen1 Collections | {nonPoc.Gen1Collections} | {poc.Gen1Collections} | - | - |");
            await writer.WriteLineAsync($"| Gen2 Collections | {nonPoc.Gen2Collections} | {poc.Gen2Collections} | - | - |");

            await writer.WriteLineAsync();
        }

        Console.WriteLine($"Results saved to: {filename}");
    }
}

public class BenchmarkResult
{
    public string Implementation { get; set; } = string.Empty;
    public string TestName { get; set; } = string.Empty;
    public int RecordCount { get; set; }
    public int MaxConcurrency { get; set; }
    public int BatchSize { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public long ElapsedMilliseconds { get; set; }
    public double ThroughputPerSecond { get; set; }
    public long MemoryUsedBytes { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
}
