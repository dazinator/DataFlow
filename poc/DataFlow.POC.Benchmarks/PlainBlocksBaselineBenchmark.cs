namespace DataFlow.POC.Benchmarks;

using System.Diagnostics;
using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Baseline benchmarks for TransformerBlock and ProcessorBlock.
/// Captures steady-state performance with warmup to establish performance baselines
/// before consolidation around ActorBlock pattern.
/// </summary>
public class PlainBlocksBaselineBenchmark
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

    /// <summary>
    /// Benchmark TransformerBlock with simple 1-to-1 transformation: x => x * 2
    /// </summary>
    public static async Task<PlainBlockBenchmarkResult> RunTransformerOneToOneAsync()
    {
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("TransformerBlock Baseline: 1-to-1 Transformation (x => x * 2)");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine($"Warmup: {WarmupItemCount:N0} items");
        Console.WriteLine($"Measurement: {MeasurementItemCount:N0} items");
        Console.WriteLine();

        var services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .BuildServiceProvider();

        // Warmup phase
        Console.WriteLine("Warming up...");
        await RunTransformerOneToOneFlow(services, WarmupItemCount);
        
        // Small delay
        await Task.Delay(100);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Task.Delay(100);

        // Measurement phase
        Console.WriteLine("Measuring...");
        var sw = Stopwatch.StartNew();
        var processedCount = await RunTransformerOneToOneFlow(services, MeasurementItemCount);
        sw.Stop();

        var throughput = MeasurementItemCount / sw.Elapsed.TotalSeconds;
        
        Console.WriteLine();
        Console.WriteLine($"Completed: {processedCount:N0} items in {sw.ElapsedMilliseconds:N0} ms");
        Console.WriteLine($"Throughput: {throughput:N0} items/sec");
        Console.WriteLine($"Latency (avg): {sw.Elapsed.TotalMilliseconds / processedCount:F3} ms/item");
        Console.WriteLine();

        return new PlainBlockBenchmarkResult
        {
            Name = "TransformerBlock-1to1",
            ItemCount = MeasurementItemCount,
            DurationMs = sw.ElapsedMilliseconds,
            Throughput = throughput,
            AvgLatencyMs = sw.Elapsed.TotalMilliseconds / processedCount
        };
    }

    private static async Task<int> RunTransformerOneToOneFlow(IServiceProvider services, int itemCount)
    {
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(itemCount));
        var transformer = new SimpleTransformerBlock<int, int>("transformer", x => x * 2);
        
        var processedCount = 0;
        var processor = new ProcessorBlock<int>("processor", async (item, ctx) =>
        {
            Interlocked.Increment(ref processedCount);
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("transformer-1to1-flow");
        builder.AddBlock(producer)
            .AddBlock(transformer)
            .AddBlock(processor)
            .Connect(producer, transformer)
            .Connect(transformer, processor);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        await graph.ExecuteAsync(context);
        return processedCount;
    }

    /// <summary>
    /// Benchmark TransformerBlock with 1-to-many transformation: x => [x, x*2, x*3]
    /// </summary>
    public static async Task<PlainBlockBenchmarkResult> RunTransformerOneToManyAsync()
    {
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("TransformerBlock Baseline: 1-to-Many Transformation (x => [x, x*2, x*3])");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine($"Warmup: {WarmupItemCount:N0} items");
        Console.WriteLine($"Measurement: {MeasurementItemCount:N0} items");
        Console.WriteLine();

        var services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .BuildServiceProvider();

        // Warmup phase
        Console.WriteLine("Warming up...");
        await RunTransformerOneToManyFlow(services, WarmupItemCount);
        
        // Small delay
        await Task.Delay(100);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Task.Delay(100);

        // Measurement phase
        Console.WriteLine("Measuring...");
        var sw = Stopwatch.StartNew();
        var processedCount = await RunTransformerOneToManyFlow(services, MeasurementItemCount);
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
            Name = "TransformerBlock-1toMany",
            ItemCount = processedCount,
            DurationMs = sw.ElapsedMilliseconds,
            Throughput = throughput,
            AvgLatencyMs = sw.Elapsed.TotalMilliseconds / processedCount
        };
    }

    private static async Task<int> RunTransformerOneToManyFlow(IServiceProvider services, int itemCount)
    {
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(itemCount));
        
        var transformer = new TransformerBlock<int, int>("transformer", 
            (x, ctx) =>
            {
                async IAsyncEnumerable<int> Generate()
                {
                    yield return x;
                    yield return x * 2;
                    yield return x * 3;
                    await Task.CompletedTask;
                }
                return Generate();
            });
        
        var processedCount = 0;
        var processor = new ProcessorBlock<int>("processor", async (item, ctx) =>
        {
            Interlocked.Increment(ref processedCount);
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("transformer-1tomany-flow");
        builder.AddBlock(producer)
            .AddBlock(transformer)
            .AddBlock(processor)
            .Connect(producer, transformer)
            .Connect(transformer, processor);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        await graph.ExecuteAsync(context);
        return processedCount;
    }

    /// <summary>
    /// Benchmark TransformerBlock with filtering transformation: x => x % 2 == 0 ? [x] : []
    /// </summary>
    public static async Task<PlainBlockBenchmarkResult> RunTransformerFilteringAsync()
    {
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("TransformerBlock Baseline: Filtering (x => x % 2 == 0)");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine($"Warmup: {WarmupItemCount:N0} items");
        Console.WriteLine($"Measurement: {MeasurementItemCount:N0} items");
        Console.WriteLine();

        var services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .BuildServiceProvider();

        // Warmup phase
        Console.WriteLine("Warming up...");
        await RunTransformerFilteringFlow(services, WarmupItemCount);
        
        // Small delay
        await Task.Delay(100);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Task.Delay(100);

        // Measurement phase
        Console.WriteLine("Measuring...");
        var sw = Stopwatch.StartNew();
        var processedCount = await RunTransformerFilteringFlow(services, MeasurementItemCount);
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
            Name = "TransformerBlock-Filtering",
            ItemCount = MeasurementItemCount,
            DurationMs = sw.ElapsedMilliseconds,
            Throughput = throughput,
            AvgLatencyMs = sw.Elapsed.TotalMilliseconds / MeasurementItemCount
        };
    }

    private static async Task<int> RunTransformerFilteringFlow(IServiceProvider services, int itemCount)
    {
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(itemCount));
        
        var transformer = new TransformerBlock<int, int>("transformer", 
            (x, ctx) =>
            {
                async IAsyncEnumerable<int> Filter()
                {
                    if (x % 2 == 0)
                    {
                        yield return x;
                    }
                    await Task.CompletedTask;
                }
                return Filter();
            });
        
        var processedCount = 0;
        var processor = new ProcessorBlock<int>("processor", async (item, ctx) =>
        {
            Interlocked.Increment(ref processedCount);
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("transformer-filtering-flow");
        builder.AddBlock(producer)
            .AddBlock(transformer)
            .AddBlock(processor)
            .Connect(producer, transformer)
            .Connect(transformer, processor);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        await graph.ExecuteAsync(context);
        return processedCount;
    }

    /// <summary>
    /// Benchmark ProcessorBlock with simple side effect: counter++
    /// </summary>
    public static async Task<PlainBlockBenchmarkResult> RunProcessorSimpleAsync()
    {
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("ProcessorBlock Baseline: Simple Side Effect (counter++)");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine($"Warmup: {WarmupItemCount:N0} items");
        Console.WriteLine($"Measurement: {MeasurementItemCount:N0} items");
        Console.WriteLine();

        var services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .BuildServiceProvider();

        // Warmup phase
        Console.WriteLine("Warming up...");
        await RunProcessorSimpleFlow(services, WarmupItemCount);
        
        // Small delay
        await Task.Delay(100);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Task.Delay(100);

        // Measurement phase
        Console.WriteLine("Measuring...");
        var sw = Stopwatch.StartNew();
        var processedCount = await RunProcessorSimpleFlow(services, MeasurementItemCount);
        sw.Stop();

        var throughput = MeasurementItemCount / sw.Elapsed.TotalSeconds;
        
        Console.WriteLine();
        Console.WriteLine($"Completed: {processedCount:N0} items in {sw.ElapsedMilliseconds:N0} ms");
        Console.WriteLine($"Throughput: {throughput:N0} items/sec");
        Console.WriteLine($"Latency (avg): {sw.Elapsed.TotalMilliseconds / processedCount:F3} ms/item");
        Console.WriteLine();

        return new PlainBlockBenchmarkResult
        {
            Name = "ProcessorBlock-Simple",
            ItemCount = MeasurementItemCount,
            DurationMs = sw.ElapsedMilliseconds,
            Throughput = throughput,
            AvgLatencyMs = sw.Elapsed.TotalMilliseconds / processedCount
        };
    }

    private static async Task<int> RunProcessorSimpleFlow(IServiceProvider services, int itemCount)
    {
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(itemCount));
        
        var processedCount = 0;
        var processor = new ProcessorBlock<int>("processor", async (item, ctx) =>
        {
            Interlocked.Increment(ref processedCount);
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("processor-simple-flow");
        builder.AddBlock(producer)
            .AddBlock(processor)
            .Connect(producer, processor);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        await graph.ExecuteAsync(context);
        return processedCount;
    }

    /// <summary>
    /// Benchmark ProcessorBlock with async operation simulating I/O
    /// </summary>
    public static async Task<PlainBlockBenchmarkResult> RunProcessorAsyncAsync()
    {
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("ProcessorBlock Baseline: Async Operation (simulated I/O)");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine($"Warmup: {WarmupItemCount:N0} items");
        Console.WriteLine($"Measurement: {MeasurementItemCount:N0} items");
        Console.WriteLine();

        var services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .BuildServiceProvider();

        // Warmup phase
        Console.WriteLine("Warming up...");
        await RunProcessorAsyncFlow(services, WarmupItemCount);
        
        // Small delay
        await Task.Delay(100);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Task.Delay(100);

        // Measurement phase
        Console.WriteLine("Measuring...");
        var sw = Stopwatch.StartNew();
        var processedCount = await RunProcessorAsyncFlow(services, MeasurementItemCount);
        sw.Stop();

        var throughput = MeasurementItemCount / sw.Elapsed.TotalSeconds;
        
        Console.WriteLine();
        Console.WriteLine($"Completed: {processedCount:N0} items in {sw.ElapsedMilliseconds:N0} ms");
        Console.WriteLine($"Throughput: {throughput:N0} items/sec");
        Console.WriteLine($"Latency (avg): {sw.Elapsed.TotalMilliseconds / processedCount:F3} ms/item");
        Console.WriteLine();

        return new PlainBlockBenchmarkResult
        {
            Name = "ProcessorBlock-Async",
            ItemCount = MeasurementItemCount,
            DurationMs = sw.ElapsedMilliseconds,
            Throughput = throughput,
            AvgLatencyMs = sw.Elapsed.TotalMilliseconds / processedCount
        };
    }

    private static async Task<int> RunProcessorAsyncFlow(IServiceProvider services, int itemCount)
    {
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(itemCount));
        
        var processedCount = 0;
        var processor = new ProcessorBlock<int>("processor", async (item, ctx) =>
        {
            // Simulate async I/O operation (e.g., database write)
            await Task.Delay(1, ctx.CancellationToken);
            Interlocked.Increment(ref processedCount);
        });

        var builder = new DataFlowGraphBuilder("processor-async-flow");
        builder.AddBlock(producer)
            .AddBlock(processor)
            .Connect(producer, processor);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        await graph.ExecuteAsync(context);
        return processedCount;
    }

    /// <summary>
    /// Run all baseline benchmarks and generate results document
    /// </summary>
    public static async Task RunAllBaselinesAsync()
    {
        Console.WriteLine();
        Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║         Plain Blocks Baseline Benchmarks - Performance Capture                ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("This benchmark suite captures baseline performance for TransformerBlock and");
        Console.WriteLine("ProcessorBlock before consolidation around ActorBlock pattern.");
        Console.WriteLine();
        Console.WriteLine($"Methodology:");
        Console.WriteLine($"  - Warmup: {WarmupItemCount:N0} items to eliminate JIT/initialization costs");
        Console.WriteLine($"  - Measurement: {MeasurementItemCount:N0} items for steady-state performance");
        Console.WriteLine($"  - Metrics: Throughput (items/sec), Average Latency (ms/item)");
        Console.WriteLine();

        var results = new List<PlainBlockBenchmarkResult>();

        // TransformerBlock scenarios
        results.Add(await RunTransformerOneToOneAsync());
        await Task.Delay(1000);
        
        results.Add(await RunTransformerOneToManyAsync());
        await Task.Delay(1000);
        
        results.Add(await RunTransformerFilteringAsync());
        await Task.Delay(1000);

        // ProcessorBlock scenarios
        results.Add(await RunProcessorSimpleAsync());
        await Task.Delay(1000);
        
        results.Add(await RunProcessorAsyncAsync());

        // Print summary
        Console.WriteLine();
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("BASELINE RESULTS SUMMARY");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();
        Console.WriteLine($"{"Benchmark",-30} {"Items",10} {"Duration (ms)",15} {"Throughput",15} {"Avg Latency",15}");
        Console.WriteLine("-".PadRight(80, '-'));
        
        foreach (var result in results)
        {
            Console.WriteLine($"{result.Name,-30} {result.ItemCount,10:N0} {result.DurationMs,15:N0} {result.Throughput,15:N0} {result.AvgLatencyMs,15:F3}");
        }
        
        Console.WriteLine();
        Console.WriteLine($"Total benchmarks: {results.Count}");
        Console.WriteLine($"System: {Environment.OSVersion}");
        Console.WriteLine($".NET: {Environment.Version}");
        Console.WriteLine($"Processor Count: {Environment.ProcessorCount}");
        Console.WriteLine();

        // Save results to file
        await SaveBaselineResultsAsync(results);
    }

    private static async Task SaveBaselineResultsAsync(List<PlainBlockBenchmarkResult> results)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var outputPath = Path.Combine(currentDir, "..", "..", "..", "..", "docs", "benchmarks");
        // Fallback to absolute path if relative doesn't work
        if (!Directory.Exists(outputPath))
        {
            outputPath = "/home/runner/work/lib-dataflow/lib-dataflow/poc/docs/benchmarks";
        }
        Directory.CreateDirectory(outputPath);
        
        var filePath = Path.Combine(outputPath, "plain-blocks-baseline-results.md");
        
        var content = new System.Text.StringBuilder();
        content.AppendLine("# Plain Blocks Baseline Performance Results");
        content.AppendLine();
        content.AppendLine("## Methodology");
        content.AppendLine();
        content.AppendLine($"- **Warmup**: {WarmupItemCount:N0} items to eliminate JIT compilation and initialization overhead");
        content.AppendLine($"- **Measurement**: {MeasurementItemCount:N0} items for steady-state performance measurement");
        content.AppendLine("- **Metrics**:");
        content.AppendLine("  - Throughput: Items processed per second (higher is better)");
        content.AppendLine("  - Average Latency: Milliseconds per item (lower is better)");
        content.AppendLine();
        content.AppendLine("## System Configuration");
        content.AppendLine();
        content.AppendLine($"- **Date**: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        content.AppendLine($"- **OS**: {Environment.OSVersion}");
        content.AppendLine($"- **.NET Version**: {Environment.Version}");
        content.AppendLine($"- **Processor Count**: {Environment.ProcessorCount}");
        content.AppendLine();
        content.AppendLine("## Baseline Results");
        content.AppendLine();
        content.AppendLine("| Benchmark | Items | Duration (ms) | Throughput (items/sec) | Avg Latency (ms/item) |");
        content.AppendLine("|-----------|------:|---------------:|-----------------------:|----------------------:|");
        
        foreach (var result in results)
        {
            content.AppendLine($"| {result.Name} | {result.ItemCount:N0} | {result.DurationMs:N0} | {result.Throughput:N0} | {result.AvgLatencyMs:F3} |");
        }
        
        content.AppendLine();
        content.AppendLine("## TransformerBlock Scenarios");
        content.AppendLine();
        content.AppendLine("### 1-to-1 Transformation");
        content.AppendLine("- **Operation**: `x => x * 2`");
        content.AppendLine("- **Use Case**: Simple value transformation");
        content.AppendLine();
        content.AppendLine("### 1-to-Many Transformation");
        content.AppendLine("- **Operation**: `x => [x, x*2, x*3]`");
        content.AppendLine("- **Use Case**: Data expansion, generating multiple outputs per input");
        content.AppendLine();
        content.AppendLine("### Filtering Transformation");
        content.AppendLine("- **Operation**: `x => x % 2 == 0 ? [x] : []`");
        content.AppendLine("- **Use Case**: Data filtering, conditional pass-through");
        content.AppendLine();
        content.AppendLine("## ProcessorBlock Scenarios");
        content.AppendLine();
        content.AppendLine("### Simple Side Effect");
        content.AppendLine("- **Operation**: `counter++` (synchronous)");
        content.AppendLine("- **Use Case**: Simple state updates, counters");
        content.AppendLine();
        content.AppendLine("### Async Operation");
        content.AppendLine("- **Operation**: `await Task.Delay(1)` (simulated I/O)");
        content.AppendLine("- **Use Case**: Database writes, API calls, file I/O");
        content.AppendLine();
        content.AppendLine("## Notes");
        content.AppendLine();
        content.AppendLine("- These baselines establish the performance characteristics before consolidation");
        content.AppendLine("- ActorBlock equivalents should match within <1% overhead after warmup");
        content.AppendLine("- Any regression >1% requires investigation and optimization");
        content.AppendLine();

        await File.WriteAllTextAsync(filePath, content.ToString());
        
        Console.WriteLine($"Results saved to: {filePath}");
    }
}

public class PlainBlockBenchmarkResult
{
    public string Name { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public long DurationMs { get; set; }
    public double Throughput { get; set; }
    public double AvgLatencyMs { get; set; }
}
