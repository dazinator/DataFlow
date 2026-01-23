namespace DataFlow.POC.Benchmarks;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Uniun.DataFlow;
using PocExecutionContext = DataFlow.POC.Core.ExecutionContext;
using DataFlow.POC.Registry;

/// <summary>
/// Simple comparison benchmark that tests basic pipeline: DataSource → Validators → Enrichers
/// This removes routing, broadcasting, and batching to isolate core concurrency scaling.
/// </summary>
public class SimpleComparisonBenchmark
{
    public static async Task RunSimpleComparisonAsync()
    {
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("Simple ETL POC vs Non-POC Performance Comparison");
        Console.WriteLine("Pipeline: DataSource → Validators → Enrichers → Collector");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();
        Console.WriteLine("This simplified benchmark focuses on the core pipeline without:");
        Console.WriteLine("- Routing (no category-based routing)");
        Console.WriteLine("- Broadcasting (no fan-out to multiple paths)");
        Console.WriteLine("- Batching (no batch aggregation)");
        Console.WriteLine();

        var testConfigurations = new[]
        {
            // Test different concurrency levels with 10K records
            new { RecordCount = 10000, MaxConcurrency = 1, Name = "Concurrency: 1 (10K records)" },
            new { RecordCount = 10000, MaxConcurrency = 2, Name = "Concurrency: 2 (10K records)" },
            new { RecordCount = 10000, MaxConcurrency = 4, Name = "Concurrency: 4 (10K records)" },
            new { RecordCount = 10000, MaxConcurrency = 8, Name = "Concurrency: 8 (10K records)" },
        };

        var results = new List<BenchmarkResult>();

        foreach (var config in testConfigurations)
        {
            Console.WriteLine($"Testing: {config.Name}");
            Console.WriteLine($"  Parameters: Records={config.RecordCount:N0}, Concurrency={config.MaxConcurrency}");
            Console.WriteLine("-".PadRight(80, '-'));

            // Run non-POC version
            var nonPocResult = await RunNonPocBenchmarkAsync(
                config.RecordCount,
                config.MaxConcurrency,
                config.Name);
            results.Add(nonPocResult);

            // Delay and GC
            await Task.Delay(1000);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Task.Delay(1000);

            // Run POC version
            var pocResult = await RunPocBenchmarkAsync(
                config.RecordCount,
                config.MaxConcurrency,
                config.Name);
            results.Add(pocResult);

            Console.WriteLine();

            // Delay between tests
            await Task.Delay(2000);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Task.Delay(1000);
        }

        // Print and save results
        PrintSummary(results);
        
        var outputPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "benchmark-results");
        await SaveResultsAsync(results, outputPath);
    }

    private static async Task<BenchmarkResult> RunNonPocBenchmarkAsync(
        int recordCount,
        int maxConcurrency,
        string testName)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDataFlowMetrics();
        services.AddDataFlows();
        
        var serviceProvider = services.BuildServiceProvider();

        var flowBuilder = global::Benchmarks.Shared.SimpleEtlDataFlow.BuildDataFlow(
            serviceProvider,
            recordCount,
            maxConcurrency);

        var dataflow = flowBuilder.Build();
        var context = new DataFlowContext
        {
            InvocationId = Guid.NewGuid(),
            ServiceProvider = serviceProvider,
            CancellationToken = CancellationToken.None,
            Name = "SimpleEtlBenchmark"
        };

        var sw = Stopwatch.StartNew();
        var memoryBefore = GC.GetTotalMemory(forceFullCollection: true);

        try
        {
            await dataflow.ExecuteAsync(context);
            sw.Stop();
            
            var memoryAfter = GC.GetTotalMemory(forceFullCollection: true);
            var memoryUsed = (memoryAfter - memoryBefore) / 1024.0;

            var result = new BenchmarkResult
            {
                TestName = testName,
                IsPOC = false,
                RecordCount = recordCount,
                MaxConcurrency = maxConcurrency,
                ElapsedMs = sw.ElapsedMilliseconds,
                MemoryUsedKB = memoryUsed
            };

            Console.WriteLine($"    [Non-POC] {result.ElapsedMs:N0} ms | {result.RecordsPerSecond:N0} rec/sec | {result.MemoryUsedKB:N2} KB");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [Non-POC] ERROR: {ex.Message}");
            throw;
        }
    }

    private static async Task<BenchmarkResult> RunPocBenchmarkAsync(
        int recordCount,
        int maxConcurrency,
        string testName)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        SimpleEtlPOC.ConfigureDataFlow(services, recordCount, maxConcurrency);
        var serviceProvider = services.BuildServiceProvider();

        var graph = serviceProvider.GetRequiredKeyedService<DataFlowGraph>("simple-etl:graph");

        var sw = Stopwatch.StartNew();
        var memoryBefore = GC.GetTotalMemory(forceFullCollection: true);

        try
        {
            using var scope = serviceProvider.CreateScope();
            var context = new PocExecutionContext(scope.ServiceProvider, CancellationToken.None);
            await graph.ExecuteAsync(context);
            sw.Stop();
            
            var memoryAfter = GC.GetTotalMemory(forceFullCollection: true);
            var memoryUsed = (memoryAfter - memoryBefore) / 1024.0;

            var result = new BenchmarkResult
            {
                TestName = testName,
                IsPOC = true,
                RecordCount = recordCount,
                MaxConcurrency = maxConcurrency,
                ElapsedMs = sw.ElapsedMilliseconds,
                MemoryUsedKB = memoryUsed
            };

            Console.WriteLine($"    [POC] {result.ElapsedMs:N0} ms | {result.RecordsPerSecond:N0} rec/sec | {result.MemoryUsedKB:N2} KB");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [POC] ERROR: {ex.Message}");
            throw;
        }
    }

    private static void PrintSummary(List<BenchmarkResult> results)
    {
        Console.WriteLine();
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("SUMMARY - SIMPLE ETL COMPARISON");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();

        var groupedResults = results.GroupBy(r => r.TestName).ToList();

        foreach (var group in groupedResults)
        {
            var nonPoc = group.FirstOrDefault(r => !r.IsPOC);
            var poc = group.FirstOrDefault(r => r.IsPOC);

            if (nonPoc != null && poc != null)
            {
                var ratio = (double)poc.ElapsedMs / nonPoc.ElapsedMs;
                var memoryRatio = poc.MemoryUsedKB / nonPoc.MemoryUsedKB;

                Console.WriteLine($"{group.Key}");
                Console.WriteLine($"  Config: {poc.RecordCount:N0} records | Concurrency: {poc.MaxConcurrency}");
                Console.WriteLine($"  Time:   Non-POC={nonPoc.ElapsedMs}ms, POC={poc.ElapsedMs}ms (ratio={ratio:F2}x)");
                Console.WriteLine($"  Memory: Non-POC={nonPoc.MemoryUsedKB:N0}KB, POC={poc.MemoryUsedKB:N0}KB (ratio={memoryRatio:F2}x)");
                Console.WriteLine();
            }
        }
    }

    private static async Task SaveResultsAsync(List<BenchmarkResult> results, string outputPath)
    {
        Directory.CreateDirectory(outputPath);
        
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var filename = Path.Combine(outputPath, $"simple-benchmark_{timestamp}.md");

        var markdown = GenerateMarkdownReport(results);
        await File.WriteAllTextAsync(filename, markdown);
        
        Console.WriteLine($"Simple benchmark results saved to: {filename}");
    }

    private static string GenerateMarkdownReport(List<BenchmarkResult> results)
    {
        var report = new System.Text.StringBuilder();
        
        report.AppendLine("# Simple ETL Benchmark Results");
        report.AppendLine();
        report.AppendLine($"**Date:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine();
        report.AppendLine("## Pipeline");
        report.AppendLine("DataSource → Validators → Enrichers → Collector");
        report.AppendLine();
        report.AppendLine("This simplified benchmark removes routing, broadcasting, and batching to isolate core concurrency scaling.");
        report.AppendLine();
        report.AppendLine("## Results");
        report.AppendLine();
        report.AppendLine("| Test | Records | Concurrency | Non-POC (ms) | POC (ms) | Ratio | Non-POC Mem (KB) | POC Mem (KB) |");
        report.AppendLine("|------|---------|-------------|--------------|----------|-------|------------------|--------------|");

        var groupedResults = results.GroupBy(r => r.TestName).ToList();
        foreach (var group in groupedResults)
        {
            var nonPoc = group.FirstOrDefault(r => !r.IsPOC);
            var poc = group.FirstOrDefault(r => r.IsPOC);

            if (nonPoc != null && poc != null)
            {
                var ratio = (double)poc.ElapsedMs / nonPoc.ElapsedMs;
                report.AppendLine($"| {group.Key} | {poc.RecordCount:N0} | {poc.MaxConcurrency} | {nonPoc.ElapsedMs} | {poc.ElapsedMs} | {ratio:F2}x | {nonPoc.MemoryUsedKB:N0} | {poc.MemoryUsedKB:N0} |");
            }
        }

        report.AppendLine();
        report.AppendLine("## Analysis");
        report.AppendLine();
        report.AppendLine("### Expected Behavior");
        report.AppendLine("With concurrency scaling, execution time should decrease roughly proportionally:");
        report.AppendLine("- Concurrency 1: Baseline");
        report.AppendLine("- Concurrency 2: ~50% of baseline");
        report.AppendLine("- Concurrency 4: ~25% of baseline");
        report.AppendLine("- Concurrency 8: ~12-15% of baseline (accounting for overhead)");
        report.AppendLine();
        report.AppendLine("### Observations");
        
        var groupedByConfig = results.GroupBy(r => r.TestName).ToList();
        foreach (var group in groupedByConfig)
        {
            var nonPoc = group.FirstOrDefault(r => !r.IsPOC);
            var poc = group.FirstOrDefault(r => r.IsPOC);

            if (nonPoc != null && poc != null)
            {
                var ratio = (double)poc.ElapsedMs / nonPoc.ElapsedMs;
                var speedupNonPoc = group.Key.Contains("Concurrency: 1") ? 1.0 : 0.0;
                var speedupPoc = group.Key.Contains("Concurrency: 1") ? 1.0 : 0.0;
                
                report.AppendLine($"- **{group.Key}**: POC is {ratio:F2}x slower than non-POC");
            }
        }

        return report.ToString();
    }

    private class BenchmarkResult
    {
        public string TestName { get; set; } = "";
        public bool IsPOC { get; set; }
        public int RecordCount { get; set; }
        public int MaxConcurrency { get; set; }
        public long ElapsedMs { get; set; }
        public double MemoryUsedKB { get; set; }
        public long RecordsPerSecond => RecordCount * 1000 / Math.Max(1, ElapsedMs);
    }
}
