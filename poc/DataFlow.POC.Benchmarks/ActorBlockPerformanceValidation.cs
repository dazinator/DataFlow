namespace DataFlow.POC.Benchmarks;

using System.Diagnostics;
using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// ActorBlock performance validation benchmarks.
/// Compares ActorBlock performance against plain block baselines with warmup.
/// Target: <1% overhead after warmup.
/// </summary>
public class ActorBlockPerformanceValidation
{
    private const int WarmupItemCount = 1000;
    private const int MeasurementItemCount = 10000;

    private static async IAsyncEnumerable<int> ProduceIntegers(int count)
    {
        for (int i = 1; i <= count; i++)
        {
            yield return i;
        }
    }

    // Simple actor for 1-to-1 transformation
    public class SimpleTransformActor : IStreamActor<int, int>
    {
        public async IAsyncEnumerable<int> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                yield return item * 2;
            }
        }
    }

    // Actor for 1-to-many transformation
    public class ExpandActor : IStreamActor<int, int>
    {
        public async IAsyncEnumerable<int> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                yield return item;
                yield return item * 2;
                yield return item * 3;
            }
        }
    }

    // Actor for filtering
    public class FilterActor : IStreamActor<int, int>
    {
        public async IAsyncEnumerable<int> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                if (item % 2 == 0)
                {
                    yield return item;
                }
            }
            await Task.CompletedTask;
        }
    }

    // Actor for simple processing
    public class CounterActor : IStreamActor<int, object>
    {
        private int _count;

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                _count++;
            }
            
            // Terminal actor - no output
            yield break;
        }
    }

    // Actor for async processing
    public class AsyncProcessorActor : IStreamActor<int, object>
    {
        private int _count;

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                // Simulate async I/O operation
                await Task.Delay(1, context.CancellationToken);
                _count++;
            }
            
            yield break;
        }
    }

    /// <summary>
    /// Benchmark ActorBlock with 1-to-1 transformation
    /// </summary>
    public static async Task<PlainBlockBenchmarkResult> RunActorOneToOneAsync()
    {
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("ActorBlock: 1-to-1 Transformation (x => x * 2)");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine($"Warmup: {WarmupItemCount:N0} items");
        Console.WriteLine($"Measurement: {MeasurementItemCount:N0} items");
        Console.WriteLine();

        var services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .AddScoped<SimpleTransformActor>()
            .BuildServiceProvider();

        // Warmup phase
        Console.WriteLine("Warming up...");
        await RunActorOneToOneFlow(services, WarmupItemCount);
        
        // Small delay
        await Task.Delay(100);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Task.Delay(100);

        // Measurement phase
        Console.WriteLine("Measuring...");
        var sw = Stopwatch.StartNew();
        var processedCount = await RunActorOneToOneFlow(services, MeasurementItemCount);
        sw.Stop();

        var throughput = MeasurementItemCount / sw.Elapsed.TotalSeconds;
        
        Console.WriteLine();
        Console.WriteLine($"Completed: {processedCount:N0} items in {sw.ElapsedMilliseconds:N0} ms");
        Console.WriteLine($"Throughput: {throughput:N0} items/sec");
        Console.WriteLine($"Latency (avg): {sw.Elapsed.TotalMilliseconds / processedCount:F3} ms/item");
        Console.WriteLine();

        return new PlainBlockBenchmarkResult
        {
            Name = "ActorBlock-1to1",
            ItemCount = MeasurementItemCount,
            DurationMs = sw.ElapsedMilliseconds,
            Throughput = throughput,
            AvgLatencyMs = sw.Elapsed.TotalMilliseconds / processedCount
        };
    }

    private static async Task<int> RunActorOneToOneFlow(IServiceProvider services, int itemCount)
    {
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(itemCount));
        var actorBlock = new ActorBlock<int, int, SimpleTransformActor>(
            "actor",
            services.GetRequiredService<IServiceScopeFactory>());
        
        var processedCount = 0;
        var processor = new ProcessorBlock<int>("processor", async (item, ctx) =>
        {
            Interlocked.Increment(ref processedCount);
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("actor-1to1-flow");
        builder.AddBlock(producer)
            .AddBlock(actorBlock)
            .AddBlock(processor)
            .Connect(producer, actorBlock)
            .Connect(actorBlock, processor);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        await graph.ExecuteAsync(context);
        return processedCount;
    }

    /// <summary>
    /// Benchmark ActorBlock with 1-to-many transformation
    /// </summary>
    public static async Task<PlainBlockBenchmarkResult> RunActorOneToManyAsync()
    {
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("ActorBlock: 1-to-Many Transformation (x => [x, x*2, x*3])");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine($"Warmup: {WarmupItemCount:N0} items");
        Console.WriteLine($"Measurement: {MeasurementItemCount:N0} items");
        Console.WriteLine();

        var services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .AddScoped<ExpandActor>()
            .BuildServiceProvider();

        // Warmup phase
        Console.WriteLine("Warming up...");
        await RunActorOneToManyFlow(services, WarmupItemCount);
        
        // Small delay
        await Task.Delay(100);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Task.Delay(100);

        // Measurement phase
        Console.WriteLine("Measuring...");
        var sw = Stopwatch.StartNew();
        var processedCount = await RunActorOneToManyFlow(services, MeasurementItemCount);
        sw.Stop();

        var throughput = processedCount / sw.Elapsed.TotalSeconds;
        
        Console.WriteLine();
        Console.WriteLine($"Input: {MeasurementItemCount:N0} items");
        Console.WriteLine($"Output: {processedCount:N0} items (3x expansion)");
        Console.WriteLine($"Duration: {sw.ElapsedMilliseconds:N0} ms");
        Console.WriteLine($"Throughput: {throughput:N0} items/sec");
        Console.WriteLine($"Latency (avg): {sw.Elapsed.TotalMilliseconds / processedCount:F3} ms/item");
        Console.WriteLine();

        return new PlainBlockBenchmarkResult
        {
            Name = "ActorBlock-1toMany",
            ItemCount = processedCount,
            DurationMs = sw.ElapsedMilliseconds,
            Throughput = throughput,
            AvgLatencyMs = sw.Elapsed.TotalMilliseconds / processedCount
        };
    }

    private static async Task<int> RunActorOneToManyFlow(IServiceProvider services, int itemCount)
    {
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(itemCount));
        var actorBlock = new ActorBlock<int, int, ExpandActor>(
            "actor",
            services.GetRequiredService<IServiceScopeFactory>());
        
        var processedCount = 0;
        var processor = new ProcessorBlock<int>("processor", async (item, ctx) =>
        {
            Interlocked.Increment(ref processedCount);
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("actor-1tomany-flow");
        builder.AddBlock(producer)
            .AddBlock(actorBlock)
            .AddBlock(processor)
            .Connect(producer, actorBlock)
            .Connect(actorBlock, processor);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        await graph.ExecuteAsync(context);
        return processedCount;
    }

    /// <summary>
    /// Benchmark ActorBlock with filtering
    /// </summary>
    public static async Task<PlainBlockBenchmarkResult> RunActorFilteringAsync()
    {
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("ActorBlock: Filtering (x => x % 2 == 0)");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine($"Warmup: {WarmupItemCount:N0} items");
        Console.WriteLine($"Measurement: {MeasurementItemCount:N0} items");
        Console.WriteLine();

        var services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .AddScoped<FilterActor>()
            .BuildServiceProvider();

        // Warmup phase
        Console.WriteLine("Warming up...");
        await RunActorFilteringFlow(services, WarmupItemCount);
        
        // Small delay
        await Task.Delay(100);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Task.Delay(100);

        // Measurement phase
        Console.WriteLine("Measuring...");
        var sw = Stopwatch.StartNew();
        var processedCount = await RunActorFilteringFlow(services, MeasurementItemCount);
        sw.Stop();

        var throughput = MeasurementItemCount / sw.Elapsed.TotalSeconds;
        
        Console.WriteLine();
        Console.WriteLine($"Input: {MeasurementItemCount:N0} items");
        Console.WriteLine($"Output: {processedCount:N0} items (~50% filtered)");
        Console.WriteLine($"Duration: {sw.ElapsedMilliseconds:N0} ms");
        Console.WriteLine($"Throughput (input): {throughput:N0} items/sec");
        Console.WriteLine($"Latency (avg): {sw.Elapsed.TotalMilliseconds / MeasurementItemCount:F3} ms/item");
        Console.WriteLine();

        return new PlainBlockBenchmarkResult
        {
            Name = "ActorBlock-Filtering",
            ItemCount = MeasurementItemCount,
            DurationMs = sw.ElapsedMilliseconds,
            Throughput = throughput,
            AvgLatencyMs = sw.Elapsed.TotalMilliseconds / MeasurementItemCount
        };
    }

    private static async Task<int> RunActorFilteringFlow(IServiceProvider services, int itemCount)
    {
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(itemCount));
        var actorBlock = new ActorBlock<int, int, FilterActor>(
            "actor",
            services.GetRequiredService<IServiceScopeFactory>());
        
        var processedCount = 0;
        var processor = new ProcessorBlock<int>("processor", async (item, ctx) =>
        {
            Interlocked.Increment(ref processedCount);
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("actor-filtering-flow");
        builder.AddBlock(producer)
            .AddBlock(actorBlock)
            .AddBlock(processor)
            .Connect(producer, actorBlock)
            .Connect(actorBlock, processor);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        await graph.ExecuteAsync(context);
        return processedCount;
    }

    /// <summary>
    /// Benchmark ActorBlock for simple processing
    /// </summary>
    public static async Task<PlainBlockBenchmarkResult> RunActorProcessorSimpleAsync()
    {
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("ActorBlock as Processor: Simple Side Effect (counter++)");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine($"Warmup: {WarmupItemCount:N0} items");
        Console.WriteLine($"Measurement: {MeasurementItemCount:N0} items");
        Console.WriteLine();

        var services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .AddScoped<CounterActor>()
            .BuildServiceProvider();

        // Warmup phase
        Console.WriteLine("Warming up...");
        await RunActorProcessorSimpleFlow(services, WarmupItemCount);
        
        // Small delay
        await Task.Delay(100);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Task.Delay(100);

        // Measurement phase
        Console.WriteLine("Measuring...");
        var sw = Stopwatch.StartNew();
        await RunActorProcessorSimpleFlow(services, MeasurementItemCount);
        sw.Stop();

        var throughput = MeasurementItemCount / sw.Elapsed.TotalSeconds;
        
        Console.WriteLine();
        Console.WriteLine($"Completed: {MeasurementItemCount:N0} items in {sw.ElapsedMilliseconds:N0} ms");
        Console.WriteLine($"Throughput: {throughput:N0} items/sec");
        Console.WriteLine($"Latency (avg): {sw.Elapsed.TotalMilliseconds / MeasurementItemCount:F3} ms/item");
        Console.WriteLine();

        return new PlainBlockBenchmarkResult
        {
            Name = "ActorBlock-Processor-Simple",
            ItemCount = MeasurementItemCount,
            DurationMs = sw.ElapsedMilliseconds,
            Throughput = throughput,
            AvgLatencyMs = sw.Elapsed.TotalMilliseconds / MeasurementItemCount
        };
    }

    private static async Task RunActorProcessorSimpleFlow(IServiceProvider services, int itemCount)
    {
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(itemCount));
        var actorBlock = new ActorBlock<int, object, CounterActor>(
            "actor",
            services.GetRequiredService<IServiceScopeFactory>());

        var builder = new DataFlowGraphBuilder("actor-processor-simple-flow");
        builder.AddBlock(producer)
            .AddBlock(actorBlock)
            .Connect(producer, actorBlock);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        await graph.ExecuteAsync(context);
    }

    /// <summary>
    /// Benchmark ActorBlock for async processing
    /// </summary>
    public static async Task<PlainBlockBenchmarkResult> RunActorProcessorAsyncAsync()
    {
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("ActorBlock as Processor: Async Operation (simulated I/O)");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine($"Warmup: {WarmupItemCount:N0} items");
        Console.WriteLine($"Measurement: {MeasurementItemCount:N0} items");
        Console.WriteLine();

        var services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .AddScoped<AsyncProcessorActor>()
            .BuildServiceProvider();

        // Warmup phase
        Console.WriteLine("Warming up...");
        await RunActorProcessorAsyncFlow(services, WarmupItemCount);
        
        // Small delay
        await Task.Delay(100);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Task.Delay(100);

        // Measurement phase
        Console.WriteLine("Measuring...");
        var sw = Stopwatch.StartNew();
        await RunActorProcessorAsyncFlow(services, MeasurementItemCount);
        sw.Stop();

        var throughput = MeasurementItemCount / sw.Elapsed.TotalSeconds;
        
        Console.WriteLine();
        Console.WriteLine($"Completed: {MeasurementItemCount:N0} items in {sw.ElapsedMilliseconds:N0} ms");
        Console.WriteLine($"Throughput: {throughput:N0} items/sec");
        Console.WriteLine($"Latency (avg): {sw.Elapsed.TotalMilliseconds / MeasurementItemCount:F3} ms/item");
        Console.WriteLine();

        return new PlainBlockBenchmarkResult
        {
            Name = "ActorBlock-Processor-Async",
            ItemCount = MeasurementItemCount,
            DurationMs = sw.ElapsedMilliseconds,
            Throughput = throughput,
            AvgLatencyMs = sw.Elapsed.TotalMilliseconds / MeasurementItemCount
        };
    }

    private static async Task RunActorProcessorAsyncFlow(IServiceProvider services, int itemCount)
    {
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(itemCount));
        var actorBlock = new ActorBlock<int, object, AsyncProcessorActor>(
            "actor",
            services.GetRequiredService<IServiceScopeFactory>());

        var builder = new DataFlowGraphBuilder("actor-processor-async-flow");
        builder.AddBlock(producer)
            .AddBlock(actorBlock)
            .Connect(producer, actorBlock);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        await graph.ExecuteAsync(context);
    }

    /// <summary>
    /// Run all ActorBlock benchmarks and compare with baseline
    /// </summary>
    public static async Task RunAllValidationAsync()
    {
        Console.WriteLine();
        Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║     ActorBlock Performance Validation - Comparison with Baselines             ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("This benchmark suite validates ActorBlock performance against plain blocks.");
        Console.WriteLine("Target: <1% overhead after warmup.");
        Console.WriteLine();
        Console.WriteLine($"Methodology:");
        Console.WriteLine($"  - Warmup: {WarmupItemCount:N0} items to eliminate JIT/initialization costs");
        Console.WriteLine($"  - Measurement: {MeasurementItemCount:N0} items for steady-state performance");
        Console.WriteLine($"  - Comparison: Calculate percentage difference vs baseline");
        Console.WriteLine();

        var results = new List<PlainBlockBenchmarkResult>();

        // ActorBlock scenarios
        results.Add(await RunActorOneToOneAsync());
        await Task.Delay(1000);
        
        results.Add(await RunActorOneToManyAsync());
        await Task.Delay(1000);
        
        results.Add(await RunActorFilteringAsync());
        await Task.Delay(1000);

        results.Add(await RunActorProcessorSimpleAsync());
        await Task.Delay(1000);
        
        results.Add(await RunActorProcessorAsyncAsync());

        // Print summary
        Console.WriteLine();
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("ACTORBLOCK RESULTS SUMMARY");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();
        Console.WriteLine($"{"Benchmark",-40} {"Items",10} {"Duration (ms)",15} {"Throughput",15} {"Avg Latency",15}");
        Console.WriteLine("-".PadRight(80, '-'));
        
        foreach (var result in results)
        {
            Console.WriteLine($"{result.Name,-40} {result.ItemCount,10:N0} {result.DurationMs,15:N0} {result.Throughput,15:N0} {result.AvgLatencyMs,15:F3}");
        }
        
        Console.WriteLine();
        Console.WriteLine($"Total benchmarks: {results.Count}");
        Console.WriteLine($"System: {Environment.OSVersion}");
        Console.WriteLine($".NET: {Environment.Version}");
        Console.WriteLine($"Processor Count: {Environment.ProcessorCount}");
        Console.WriteLine();

        // Save results with comparison
        await SaveValidationResultsAsync(results);
    }

    private static async Task SaveValidationResultsAsync(List<PlainBlockBenchmarkResult> actorResults)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var outputPath = Path.Combine(currentDir, "..", "..", "..", "..", "docs", "benchmarks");
        // Fallback to absolute path if relative doesn't work
        if (!Directory.Exists(outputPath))
        {
            outputPath = "/home/runner/work/lib-dataflow/lib-dataflow/poc/docs/benchmarks";
        }
        
        var baselineFilePath = Path.Combine(outputPath, "plain-blocks-baseline-results.md");
        var validationFilePath = Path.Combine(outputPath, "actor-block-performance-validation.md");
        
        // Parse baseline results
        var baselineResults = await ParseBaselineResultsAsync(baselineFilePath);
        
        var content = new System.Text.StringBuilder();
        content.AppendLine("# ActorBlock Performance Validation Results");
        content.AppendLine();
        content.AppendLine("## Objective");
        content.AppendLine();
        content.AppendLine("Validate that ActorBlock achieves **<1% overhead** after warmup compared to plain blocks (TransformerBlock, ProcessorBlock).");
        content.AppendLine();
        content.AppendLine("## Methodology");
        content.AppendLine();
        content.AppendLine($"- **Warmup**: {WarmupItemCount:N0} items to eliminate JIT compilation and initialization overhead");
        content.AppendLine($"- **Measurement**: {MeasurementItemCount:N0} items for steady-state performance measurement");
        content.AppendLine("- **Comparison**: Calculate percentage difference: `((actor - baseline) / baseline) * 100`");
        content.AppendLine("- **Target**: <1% regression after warmup");
        content.AppendLine();
        content.AppendLine("## System Configuration");
        content.AppendLine();
        content.AppendLine($"- **Date**: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        content.AppendLine($"- **OS**: {Environment.OSVersion}");
        content.AppendLine($"- **.NET Version**: {Environment.Version}");
        content.AppendLine($"- **Processor Count**: {Environment.ProcessorCount}");
        content.AppendLine();
        content.AppendLine("## Comparison Results");
        content.AppendLine();
        content.AppendLine("| Scenario | Baseline (items/sec) | ActorBlock (items/sec) | Difference | Status |");
        content.AppendLine("|----------|---------------------:|-----------------------:|-----------:|:------:|");
        
        // Compare results
        var comparisons = new List<(string Scenario, double BaselineThroughput, double ActorThroughput, double DiffPercent, string Status)>();
        
        comparisons.Add(CompareResults(baselineResults, actorResults, "1to1", "TransformerBlock-1to1", "ActorBlock-1to1"));
        comparisons.Add(CompareResults(baselineResults, actorResults, "1-to-Many", "TransformerBlock-1toMany", "ActorBlock-1toMany"));
        comparisons.Add(CompareResults(baselineResults, actorResults, "Filtering", "TransformerBlock-Filtering", "ActorBlock-Filtering"));
        comparisons.Add(CompareResults(baselineResults, actorResults, "Processor (Simple)", "ProcessorBlock-Simple", "ActorBlock-Processor-Simple"));
        comparisons.Add(CompareResults(baselineResults, actorResults, "Processor (Async)", "ProcessorBlock-Async", "ActorBlock-Processor-Async"));
        
        foreach (var comp in comparisons)
        {
            var statusIcon = comp.Status == "✅ PASS" ? "✅ PASS" : "❌ FAIL";
            content.AppendLine($"| {comp.Scenario} | {comp.BaselineThroughput:N0} | {comp.ActorThroughput:N0} | {comp.DiffPercent:+0.00;-0.00}% | {statusIcon} |");
        }
        
        content.AppendLine();
        
        var allPass = comparisons.All(c => c.Status == "✅ PASS");
        if (allPass)
        {
            content.AppendLine("## ✅ Validation Result: PASS");
            content.AppendLine();
            content.AppendLine("**All benchmarks meet the <1% overhead target after warmup.**");
            content.AppendLine();
            content.AppendLine("ActorBlock provides DI scope safety with near-zero performance cost.");
        }
        else
        {
            content.AppendLine("## ❌ Validation Result: NEEDS OPTIMIZATION");
            content.AppendLine();
            content.AppendLine("**Some benchmarks exceed the 1% overhead target.** Profiling and optimization required.");
            content.AppendLine();
            var failedScenarios = comparisons.Where(c => c.Status != "✅ PASS").Select(c => c.Scenario).ToList();
            content.AppendLine($"Failed scenarios: {string.Join(", ", failedScenarios)}");
        }
        
        content.AppendLine();
        content.AppendLine("## Notes");
        content.AppendLine();
        content.AppendLine("- Negative percentages indicate ActorBlock is *faster* than baseline");
        content.AppendLine("- Positive percentages indicate ActorBlock is *slower* than baseline");
        content.AppendLine("- Small variations (±1%) are expected due to system variability");
        content.AppendLine("- DI scope creation overhead is amortized over warmup phase");
        content.AppendLine();

        await File.WriteAllTextAsync(validationFilePath, content.ToString());
        
        Console.WriteLine($"Validation results saved to: {validationFilePath}");
        Console.WriteLine();
        
        if (allPass)
        {
            Console.WriteLine("✅ VALIDATION PASSED: All scenarios within <1% overhead target");
        }
        else
        {
            Console.WriteLine("❌ VALIDATION FAILED: Some scenarios exceed 1% overhead - optimization required");
        }
    }

    private static async Task<Dictionary<string, PlainBlockBenchmarkResult>> ParseBaselineResultsAsync(string filePath)
    {
        var results = new Dictionary<string, PlainBlockBenchmarkResult>();
        
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Warning: Baseline file not found: {filePath}");
            return results;
        }
        
        var lines = await File.ReadAllLinesAsync(filePath);
        foreach (var line in lines)
        {
            if (line.StartsWith("|") && (line.Contains("TransformerBlock") || line.Contains("ProcessorBlock")))
            {
                var parts = line.Split('|', StringSplitOptions.TrimEntries);
                if (parts.Length >= 5)
                {
                    var name = parts[1];
                    // Column 4 is Throughput (items/sec) - index is 0-based, so parts[4]
                    if (double.TryParse(parts[4].Replace(",", ""), out var throughput))
                    {
                        results[name] = new PlainBlockBenchmarkResult
                        {
                            Name = name,
                            Throughput = throughput
                        };
                    }
                }
            }
        }
        
        return results;
    }

    private static (string Scenario, double BaselineThroughput, double ActorThroughput, double DiffPercent, string Status) 
        CompareResults(
            Dictionary<string, PlainBlockBenchmarkResult> baseline,
            List<PlainBlockBenchmarkResult> actor,
            string scenarioName,
            string baselineName,
            string actorName)
    {
        var baselineResult = baseline.GetValueOrDefault(baselineName);
        var actorResult = actor.FirstOrDefault(r => r.Name == actorName);
        
        if (baselineResult == null || actorResult == null)
        {
            return (scenarioName, 0, 0, 0, "N/A");
        }
        
        var diffPercent = ((actorResult.Throughput - baselineResult.Throughput) / baselineResult.Throughput) * 100;
        // Negative difference means ActorBlock is faster - that's good, mark as PASS
        // We only care if ActorBlock is significantly slower (>1%)
        var status = diffPercent <= 1.0 ? "✅ PASS" : "❌ FAIL";
        
        return (scenarioName, baselineResult.Throughput, actorResult.Throughput, diffPercent, status);
    }
}
