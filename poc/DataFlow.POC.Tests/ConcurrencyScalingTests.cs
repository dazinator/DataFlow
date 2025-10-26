namespace DataFlow.POC.Tests;

using System.Collections.Concurrent;
using System.Diagnostics;
using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Tests specifically focused on verifying concurrent execution and scaling behavior.
/// These tests validate that multiple block instances properly compete for items
/// and process them concurrently.
/// </summary>
public class ConcurrencyScalingTests
{
    private readonly ITestOutputHelper _output;

    public ConcurrencyScalingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Multiple_Transformers_With_CompetingEdge_Should_Process_Concurrently()
    {
        // Arrange
        const int itemCount = 100;
        const int concurrency = 4;
        const int processingDelayMs = 10;
        
        var services = new ServiceCollection().BuildServiceProvider();
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(itemCount));

        // Create N transformer instances that will compete for items
        var transformers = new List<TransformerBlock<int, string>>();
        var processingLog = new ConcurrentBag<(string BlockName, int Item, long TimestampMs)>();
        
        for (int i = 0; i < concurrency; i++)
        {
            var blockName = $"transformer-{i}";
            var localLog = processingLog; // Capture for closure
            var transformer = new TransformerBlock<int, string>(
                blockName,
                (item, ctx) => TransformWithLogging(blockName, item, localLog, processingDelayMs));
            transformers.Add(transformer);
        }

        var collector = new ProcessorBlock<string>("collector", async (result, ctx) =>
        {
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("concurrency-test");
        builder.AddBlock(producer);
        foreach (var t in transformers)
            builder.AddBlock(t);
        builder.AddBlock(collector);

        // Producer competes to all transformers
        builder.AddEdge(new Edge(
            producer,
            transformers.Cast<IBlock>().ToList(),
            new CompetingEdgeStrategy(BufferMode.Bounded, 10)));

        // All transformers write to collector
        foreach (var t in transformers)
        {
            builder.Connect(t, collector);
        }

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        var sw = Stopwatch.StartNew();
        await graph.ExecuteAsync(context);
        sw.Stop();

        // Assert
        var log = processingLog.OrderBy(x => x.TimestampMs).ToList();
        
        _output.WriteLine($"Total execution time: {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Items processed: {log.Count}");
        _output.WriteLine($"Expected minimum time (sequential): {itemCount * processingDelayMs}ms");
        _output.WriteLine($"Expected maximum time (parallel): {(itemCount / concurrency) * processingDelayMs}ms");
        
        // Log distribution across transformers
        var distribution = log.GroupBy(x => x.BlockName)
            .OrderBy(g => g.Key)
            .Select(g => new { BlockName = g.Key, Count = g.Count() })
            .ToList();
        
        _output.WriteLine("\nDistribution:");
        foreach (var d in distribution)
        {
            _output.WriteLine($"  {d.BlockName}: {d.Count} items");
        }

        // All items should be processed
        log.Count.ShouldBe(itemCount);
        
        // Each transformer should process some items
        foreach (var t in transformers)
        {
            var count = log.Count(x => x.BlockName == t.Name);
            count.ShouldBeGreaterThan(0, $"{t.Name} should process at least one item");
        }

        // Verify concurrent processing by checking timestamp overlaps
        // If processing is concurrent, some items should have overlapping processing times
        var concurrentGroups = 0;
        for (int i = 0; i < log.Count - 1; i++)
        {
            // Check if next item started processing before current item finished
            var currentFinish = log[i].TimestampMs + processingDelayMs;
            var nextStart = log[i + 1].TimestampMs;
            
            if (nextStart < currentFinish)
            {
                concurrentGroups++;
            }
        }
        
        _output.WriteLine($"\nConcurrent processing detected: {concurrentGroups} overlapping time windows");
        concurrentGroups.ShouldBeGreaterThan(0, "Should have concurrent processing (overlapping timestamps)");
        
        // With concurrency, execution should be faster than sequential
        var sequentialTimeEstimate = itemCount * processingDelayMs;
        var parallelTimeEstimate = (itemCount / concurrency) * processingDelayMs;
        
        _output.WriteLine($"\nActual: {sw.ElapsedMilliseconds}ms, Sequential estimate: {sequentialTimeEstimate}ms, Parallel estimate: {parallelTimeEstimate}ms");
        
        // Allow some overhead, but should be significantly faster than sequential
        ((double)sw.ElapsedMilliseconds).ShouldBeLessThan(sequentialTimeEstimate * 0.7, 
            "With concurrent execution, should be faster than sequential");
    }

    [Fact]
    public async Task Multiple_Processors_With_CompetingEdge_Should_Execute_Concurrently()
    {
        // Arrange
        const int itemCount = 50;
        const int concurrency = 4;
        const int processingDelayMs = 20;
        
        var services = new ServiceCollection().BuildServiceProvider();
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(itemCount));

        // Track which processor handles which item and when
        var processingLog = new ConcurrentBag<(string ProcessorName, int Item, long StartMs, long EndMs)>();
        
        var processors = new List<ProcessorBlock<int>>();
        for (int i = 0; i < concurrency; i++)
        {
            var processorName = $"processor-{i}";
            var processor = new ProcessorBlock<int>(processorName, async (item, ctx) =>
            {
                var start = Stopwatch.GetTimestamp() / (Stopwatch.Frequency / 1000);
                await Task.Delay(processingDelayMs);
                var end = Stopwatch.GetTimestamp() / (Stopwatch.Frequency / 1000);
                
                processingLog.Add((processorName, item, start, end));
            });
            processors.Add(processor);
        }

        var builder = new DataFlowGraphBuilder("processor-concurrency-test");
        builder.AddBlock(producer);
        foreach (var p in processors)
            builder.AddBlock(p);

        // All processors compete for items from producer
        builder.AddEdge(new Edge(
            producer,
            processors.Cast<IBlock>().ToList(),
            new CompetingEdgeStrategy(BufferMode.Bounded, 10)));

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        var sw = Stopwatch.StartNew();
        await graph.ExecuteAsync(context);
        sw.Stop();

        // Assert
        var log = processingLog.OrderBy(x => x.StartMs).ToList();
        
        _output.WriteLine($"Total execution time: {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Items processed: {log.Count}");
        
        // Log distribution
        var distribution = log.GroupBy(x => x.ProcessorName)
            .OrderBy(g => g.Key)
            .Select(g => new { ProcessorName = g.Key, Count = g.Count() })
            .ToList();
        
        _output.WriteLine("\nDistribution:");
        foreach (var d in distribution)
        {
            _output.WriteLine($"  {d.ProcessorName}: {d.Count} items");
        }

        // All items should be processed exactly once
        log.Count.ShouldBe(itemCount);
        
        // Each processor should process at least one item
        foreach (var p in processors)
        {
            var count = log.Count(x => x.ProcessorName == p.Name);
            count.ShouldBeGreaterThan(0, $"{p.Name} should process at least one item");
        }

        // Verify actual concurrent execution
        var maxConcurrentExecutions = 0;
        foreach (var entry in log)
        {
            var concurrent = log.Count(other => 
                other.StartMs < entry.EndMs && other.EndMs > entry.StartMs && other != entry);
            maxConcurrentExecutions = Math.Max(maxConcurrentExecutions, concurrent);
        }
        
        _output.WriteLine($"\nMax concurrent executions observed: {maxConcurrentExecutions}");
        maxConcurrentExecutions.ShouldBeGreaterThanOrEqualTo(1, "Should have at least 2 processors running concurrently");
    }

    [Fact]
    public async Task Chained_Competing_Stages_Should_Maintain_Concurrency()
    {
        // This test verifies that concurrency is maintained across multiple stages
        // Arrange
        const int itemCount = 40;
        const int concurrencyPerStage = 3;
        const int delayMs = 10;
        
        var services = new ServiceCollection().BuildServiceProvider();
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(itemCount));

        // Stage 1: Validators
        var validators = new List<TransformerBlock<int, int>>();
        var validatorLog = new ConcurrentBag<string>();
        for (int i = 0; i < concurrencyPerStage; i++)
        {
            var name = $"validator-{i}";
            var localLog = validatorLog;
            validators.Add(new TransformerBlock<int, int>(name, 
                (item, ctx) => ValidateWithLogging(name, item, localLog, delayMs)));
        }

        // Stage 2: Enrichers
        var enrichers = new List<TransformerBlock<int, string>>();
        var enricherLog = new ConcurrentBag<string>();
        for (int i = 0; i < concurrencyPerStage; i++)
        {
            var name = $"enricher-{i}";
            var localLog = enricherLog;
            enrichers.Add(new TransformerBlock<int, string>(name, 
                (item, ctx) => EnrichWithLogging(name, item, localLog, delayMs)));
        }

        // Collector
        var results = new ConcurrentBag<string>();
        var collector = new ProcessorBlock<string>("collector", async (item, ctx) =>
        {
            results.Add(item);
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("chained-concurrency-test");
        builder.AddBlock(producer);
        foreach (var v in validators) builder.AddBlock(v);
        foreach (var e in enrichers) builder.AddBlock(e);
        builder.AddBlock(collector);

        // Producer -> Validators (competing)
        builder.AddEdge(new Edge(producer, validators.Cast<IBlock>().ToList(), 
            new CompetingEdgeStrategy(BufferMode.Bounded, 10)));

        // Validators -> Enrichers (competing, multiple to multiple)
        foreach (var validator in validators)
        {
            builder.AddEdge(new Edge(validator, enrichers.Cast<IBlock>().ToList(), 
                new CompetingEdgeStrategy(BufferMode.Bounded, 10)));
        }

        // Enrichers -> Collector
        foreach (var enricher in enrichers)
        {
            builder.Connect(enricher, collector);
        }

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        var sw = Stopwatch.StartNew();
        await graph.ExecuteAsync(context);
        sw.Stop();

        // Assert
        _output.WriteLine($"Total execution time: {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Validator log count: {validatorLog.Count}");
        _output.WriteLine($"Enricher log count: {enricherLog.Count}");
        _output.WriteLine($"Results count: {results.Count}");
        
        // All items should flow through
        results.Count.ShouldBe(itemCount);
        validatorLog.Count.ShouldBe(itemCount);
        enricherLog.Count.ShouldBe(itemCount);

        // Each validator should process some items
        for (int i = 0; i < concurrencyPerStage; i++)
        {
            validatorLog.Count(x => x.StartsWith($"validator-{i}:")).ShouldBeGreaterThan(0);
        }
        
        // At least most enrichers should process items (allow for distribution variations)
        var enrichersWithWork = 0;
        for (int i = 0; i < concurrencyPerStage; i++)
        {
            if (enricherLog.Count(x => x.StartsWith($"enricher-{i}:")) > 0)
                enrichersWithWork++;
        }
        enrichersWithWork.ShouldBeGreaterThanOrEqualTo(concurrencyPerStage - 1, 
            "At least most enrichers should process items");

        // With concurrency at both stages, should be much faster than sequential
        var sequentialEstimate = itemCount * delayMs * 2; // 2 stages
        _output.WriteLine($"Sequential estimate: {sequentialEstimate}ms");
        ((double)sw.ElapsedMilliseconds).ShouldBeLessThan(sequentialEstimate * 0.5, 
            "Concurrent execution should be significantly faster");
    }

    private static async IAsyncEnumerable<int> ProduceIntegers(int count)
    {
        for (int i = 1; i <= count; i++)
        {
            yield return i;
        }
    }

    private static async IAsyncEnumerable<string> TransformWithLogging(
        string blockName, 
        int item, 
        ConcurrentBag<(string, int, long)> log, 
        int delayMs)
    {
        var timestamp = Stopwatch.GetTimestamp() / (Stopwatch.Frequency / 1000);
        log.Add((blockName, item, timestamp));
        
        await Task.Delay(delayMs);
        yield return $"{blockName}:{item}";
    }

    private static async IAsyncEnumerable<int> ValidateWithLogging(
        string name, 
        int item, 
        ConcurrentBag<string> log, 
        int delayMs)
    {
        log.Add($"{name}:{item}");
        await Task.Delay(delayMs);
        yield return item;
    }

    private static async IAsyncEnumerable<string> EnrichWithLogging(
        string name, 
        int item, 
        ConcurrentBag<string> log, 
        int delayMs)
    {
        log.Add($"{name}:{item}");
        await Task.Delay(delayMs);
        yield return $"enriched-{item}";
    }

    private static async IAsyncEnumerable<int> ProcessWithDelay(int item, int delayMs)
    {
        await Task.Delay(delayMs);
        yield return item;
    }

    private static async IAsyncEnumerable<string> TransformWithDelay(int item, int delayMs)
    {
        await Task.Delay(delayMs);
        yield return $"item-{item}";
    }

    private static async IAsyncEnumerable<string> TransformWithDelayAndRoute(int item, int delayMs)
    {
        await Task.Delay(delayMs);
        var prefix = item % 2 == 0 ? "even" : "odd";
        yield return $"{prefix}-{item}";
    }

    #region Level 1: Simple Competing Transformers (BASELINE - PASSING)

    [Fact]
    public async Task Level1_Simple_Competing_Transformers_Should_Scale()
    {
        // This is our baseline - we know this works with 3.6x speedup
        // Source → Transformers (competing) → Collector
        
        const int itemCount = 100;
        const int concurrency = 4;
        const int processingDelayMs = 10;
        
        var services = new ServiceCollection().BuildServiceProvider();
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(itemCount));

        var transformers = new List<TransformerBlock<int, string>>();
        var processingLog = new ConcurrentBag<(string BlockName, int Item, long TimestampMs)>();
        
        for (int i = 0; i < concurrency; i++)
        {
            var blockName = $"transformer-{i}";
            var localLog = processingLog;
            var transformer = new TransformerBlock<int, string>(
                blockName,
                (item, ctx) => TransformWithLogging(blockName, item, localLog, processingDelayMs));
            transformers.Add(transformer);
        }

        var collector = new ProcessorBlock<string>("collector", async (result, ctx) =>
        {
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("level1-test");
        builder.AddBlock(producer);
        foreach (var t in transformers)
            builder.AddBlock(t);
        builder.AddBlock(collector);

        builder.AddEdge(new Edge(
            producer,
            transformers.Cast<IBlock>().ToList(),
            new CompetingEdgeStrategy(BufferMode.Bounded, 10)));

        foreach (var t in transformers)
        {
            builder.Connect(t, collector);
        }

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        var sw = Stopwatch.StartNew();
        await graph.ExecuteAsync(context);
        sw.Stop();

        var log = processingLog.OrderBy(x => x.TimestampMs).ToList();
        
        _output.WriteLine($"Level 1: Simple Competing Transformers");
        _output.WriteLine($"  Total time: {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Expected sequential: {itemCount * processingDelayMs}ms");
        _output.WriteLine($"  Items processed: {log.Count}");
        
        var distribution = log.GroupBy(x => x.BlockName).Select(g => (g.Key, g.Count())).ToList();
        foreach (var (name, count) in distribution)
        {
            _output.WriteLine($"    {name}: {count} items");
        }

        log.Count.ShouldBe(itemCount);
        ((double)sw.ElapsedMilliseconds).ShouldBeLessThan(itemCount * processingDelayMs * 0.7);
    }

    #endregion

    #region Level 2: Two-Stage Pipeline with Competing Edges

    [Fact]
    public async Task Level2_TwoStage_Pipeline_Should_Scale()
    {
        // Add second stage: Source → Validators (competing) → Enrichers (competing) → Collector
        // This matches the first two stages of ComplexEtlPOC
        
        const int itemCount = 100;
        const int concurrency = 4;
        const int delayMs = 10;
        
        var services = new ServiceCollection().BuildServiceProvider();
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(itemCount));

        // Stage 1: Validators
        var validators = new List<TransformerBlock<int, int>>();
        for (int i = 0; i < concurrency; i++)
        {
            var name = $"validator-{i}";
            validators.Add(new TransformerBlock<int, int>(name, 
                (item, ctx) => ProcessWithDelay(item, delayMs)));
        }

        // Stage 2: Enrichers
        var enrichers = new List<TransformerBlock<int, string>>();
        for (int i = 0; i < concurrency; i++)
        {
            var name = $"enricher-{i}";
            enrichers.Add(new TransformerBlock<int, string>(name, 
                (item, ctx) => TransformWithDelay(item, delayMs)));
        }

        var collector = new ProcessorBlock<string>("collector", async (item, ctx) =>
        {
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("level2-test");
        builder.AddBlock(producer);
        foreach (var v in validators) builder.AddBlock(v);
        foreach (var e in enrichers) builder.AddBlock(e);
        builder.AddBlock(collector);

        // Producer → Validators (competing)
        builder.AddEdge(new Edge(producer, validators.Cast<IBlock>().ToList(), 
            new CompetingEdgeStrategy(BufferMode.Bounded, 10)));

        // Validators → Enrichers (each validator competes to all enrichers)
        foreach (var validator in validators)
        {
            builder.AddEdge(new Edge(validator, enrichers.Cast<IBlock>().ToList(), 
                new CompetingEdgeStrategy(BufferMode.Bounded, 10)));
        }

        // Enrichers → Collector
        foreach (var enricher in enrichers)
        {
            builder.Connect(enricher, collector);
        }

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        var sw = Stopwatch.StartNew();
        await graph.ExecuteAsync(context);
        sw.Stop();

        var sequentialEstimate = itemCount * delayMs * 2; // 2 stages
        
        _output.WriteLine($"Level 2: Two-Stage Pipeline");
        _output.WriteLine($"  Total time: {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Expected sequential: {sequentialEstimate}ms");
        _output.WriteLine($"  Expected parallel (concurrency={concurrency}): ~{sequentialEstimate / concurrency}ms");

        ((double)sw.ElapsedMilliseconds).ShouldBeLessThan(sequentialEstimate * 0.6, 
            "Two-stage pipeline should scale with concurrency");
    }

    #endregion

    #region Level 3: Add Broadcast After Enrichment

    [Fact]
    public async Task Level3_WithBroadcast_Should_Scale()
    {
        // Add broadcast: Source → Validators → Enrichers → Broadcast → [Collector1, Collector2]
        
        const int itemCount = 100;
        const int concurrency = 4;
        const int delayMs = 10;
        
        var services = new ServiceCollection().BuildServiceProvider();
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(itemCount));

        var validators = new List<TransformerBlock<int, int>>();
        for (int i = 0; i < concurrency; i++)
        {
            validators.Add(new TransformerBlock<int, int>($"validator-{i}", 
                (item, ctx) => ProcessWithDelay(item, delayMs)));
        }

        var enrichers = new List<TransformerBlock<int, string>>();
        for (int i = 0; i < concurrency; i++)
        {
            enrichers.Add(new TransformerBlock<int, string>($"enricher-{i}", 
                (item, ctx) => TransformWithDelay(item, delayMs)));
        }

        var broadcast = new BroadcastBlock<string>("broadcast");

        var collector1 = new ProcessorBlock<string>("collector1", async (item, ctx) =>
        {
            await Task.CompletedTask;
        });

        var collector2 = new ProcessorBlock<string>("collector2", async (item, ctx) =>
        {
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("level3-test");
        builder.AddBlock(producer);
        foreach (var v in validators) builder.AddBlock(v);
        foreach (var e in enrichers) builder.AddBlock(e);
        builder.AddBlock(broadcast);
        builder.AddBlock(collector1);
        builder.AddBlock(collector2);

        builder.AddEdge(new Edge(producer, validators.Cast<IBlock>().ToList(), 
            new CompetingEdgeStrategy(BufferMode.Bounded, 10)));

        foreach (var validator in validators)
        {
            builder.AddEdge(new Edge(validator, enrichers.Cast<IBlock>().ToList(), 
                new CompetingEdgeStrategy(BufferMode.Bounded, 10)));
        }

        foreach (var enricher in enrichers)
        {
            builder.Connect(enricher, broadcast);
        }

        // Broadcast to collectors
        builder.AddEdge(new Edge(broadcast, collector1, BufferMode.Bounded, 10));
        builder.AddEdge(new Edge(broadcast, collector2, BufferMode.Bounded, 10));

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        var sw = Stopwatch.StartNew();
        await graph.ExecuteAsync(context);
        sw.Stop();

        var sequentialEstimate = itemCount * delayMs * 2;
        
        _output.WriteLine($"Level 3: With Broadcast");
        _output.WriteLine($"  Total time: {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Expected sequential: {sequentialEstimate}ms");

        ((double)sw.ElapsedMilliseconds).ShouldBeLessThan(sequentialEstimate * 0.6, 
            "Pipeline with broadcast should still scale");
    }

    #endregion

    #region Level 4: Add Simple Routing

    [Fact]
    public async Task Level4_WithRouting_Should_Scale()
    {
        // Add routing: Source → Validators → Enrichers → Router → [RouteA, RouteB]
        
        const int itemCount = 100;
        const int concurrency = 4;
        const int delayMs = 10;
        
        var services = new ServiceCollection().BuildServiceProvider();
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(itemCount));

        var validators = new List<TransformerBlock<int, int>>();
        for (int i = 0; i < concurrency; i++)
        {
            validators.Add(new TransformerBlock<int, int>($"validator-{i}", 
                (item, ctx) => ProcessWithDelay(item, delayMs)));
        }

        var enrichers = new List<TransformerBlock<int, string>>();
        for (int i = 0; i < concurrency; i++)
        {
            enrichers.Add(new TransformerBlock<int, string>($"enricher-{i}", 
                (item, ctx) => TransformWithDelayAndRoute(item, delayMs)));
        }

        var router = new RouterBlock<string>("router", item => item.StartsWith("even") ? "even" : "odd");
        
        var evenFilter = new RouteFilterBlock<string>("even-filter", "even");
        var oddFilter = new RouteFilterBlock<string>("odd-filter", "odd");
        
        var evenCollector = new ProcessorBlock<string>("even-collector", async (item, ctx) =>
        {
            await Task.CompletedTask;
        });
        
        var oddCollector = new ProcessorBlock<string>("odd-collector", async (item, ctx) =>
        {
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("level4-test");
        builder.AddBlock(producer);
        foreach (var v in validators) builder.AddBlock(v);
        foreach (var e in enrichers) builder.AddBlock(e);
        builder.AddBlock(router);
        builder.AddBlock(evenFilter);
        builder.AddBlock(oddFilter);
        builder.AddBlock(evenCollector);
        builder.AddBlock(oddCollector);

        builder.AddEdge(new Edge(producer, validators.Cast<IBlock>().ToList(), 
            new CompetingEdgeStrategy(BufferMode.Bounded, 10)));

        foreach (var validator in validators)
        {
            builder.AddEdge(new Edge(validator, enrichers.Cast<IBlock>().ToList(), 
                new CompetingEdgeStrategy(BufferMode.Bounded, 10)));
        }

        foreach (var enricher in enrichers)
        {
            builder.Connect(enricher, router);
        }

        builder.Connect(router, evenFilter);
        builder.Connect(router, oddFilter);
        builder.Connect(evenFilter, evenCollector);
        builder.Connect(oddFilter, oddCollector);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        var sw = Stopwatch.StartNew();
        await graph.ExecuteAsync(context);
        sw.Stop();

        var sequentialEstimate = itemCount * delayMs * 2;
        
        _output.WriteLine($"Level 4: With Routing");
        _output.WriteLine($"  Total time: {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Expected sequential: {sequentialEstimate}ms");

        ((double)sw.ElapsedMilliseconds).ShouldBeLessThan(sequentialEstimate * 0.6, 
            "Pipeline with routing should still scale");
    }

    #endregion

    #region Level 5: Full Complexity (Like ComplexEtlPOC)

    [Fact]
    public async Task Level5_FullComplexity_Should_Scale()
    {
        // Full complexity: Broadcast + Routing + Multiple downstream paths with competing processors
        
        const int itemCount = 100;
        const int concurrency = 4;
        const int delayMs = 10;
        
        var services = new ServiceCollection().BuildServiceProvider();
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(itemCount));

        // Validators
        var validators = new List<TransformerBlock<int, int>>();
        for (int i = 0; i < concurrency; i++)
        {
            validators.Add(new TransformerBlock<int, int>($"validator-{i}", 
                (item, ctx) => ProcessWithDelay(item, delayMs)));
        }

        // Enrichers
        var enrichers = new List<TransformerBlock<int, string>>();
        for (int i = 0; i < concurrency; i++)
        {
            enrichers.Add(new TransformerBlock<int, string>($"enricher-{i}", 
                (item, ctx) => TransformWithDelayAndRoute(item, delayMs)));
        }

        var broadcast = new BroadcastBlock<string>("broadcast");
        
        // Broadcast path 1: Collector
        var metricsCollector = new ProcessorBlock<string>("metrics", async (item, ctx) =>
        {
            await Task.CompletedTask;
        });

        // Broadcast path 2: Router
        var router = new RouterBlock<string>("router", item => item.StartsWith("even") ? "even" : "odd");
        
        var evenFilter = new RouteFilterBlock<string>("even-filter", "even");
        var oddFilter = new RouteFilterBlock<string>("odd-filter", "odd");
        
        // Even route: Multiple processors competing
        var evenProcessors = new List<ProcessorBlock<string>>();
        for (int i = 0; i < concurrency; i++)
        {
            evenProcessors.Add(new ProcessorBlock<string>($"even-proc-{i}", async (item, ctx) =>
            {
                await Task.Delay(delayMs / 2); // Less delay to not dominate
            }));
        }
        
        // Odd route: Multiple processors competing
        var oddProcessors = new List<ProcessorBlock<string>>();
        for (int i = 0; i < concurrency; i++)
        {
            oddProcessors.Add(new ProcessorBlock<string>($"odd-proc-{i}", async (item, ctx) =>
            {
                await Task.Delay(delayMs / 2);
            }));
        }

        var builder = new DataFlowGraphBuilder("level5-test");
        builder.AddBlock(producer);
        foreach (var v in validators) builder.AddBlock(v);
        foreach (var e in enrichers) builder.AddBlock(e);
        builder.AddBlock(broadcast);
        builder.AddBlock(metricsCollector);
        builder.AddBlock(router);
        builder.AddBlock(evenFilter);
        builder.AddBlock(oddFilter);
        foreach (var p in evenProcessors) builder.AddBlock(p);
        foreach (var p in oddProcessors) builder.AddBlock(p);

        // Producer → Validators (competing)
        builder.AddEdge(new Edge(producer, validators.Cast<IBlock>().ToList(), 
            new CompetingEdgeStrategy(BufferMode.Bounded, 10)));

        // Validators → Enrichers (competing)
        foreach (var validator in validators)
        {
            builder.AddEdge(new Edge(validator, enrichers.Cast<IBlock>().ToList(), 
                new CompetingEdgeStrategy(BufferMode.Bounded, 10)));
        }

        // Enrichers → Broadcast
        foreach (var enricher in enrichers)
        {
            builder.Connect(enricher, broadcast);
        }

        // Broadcast → Metrics & Router
        builder.AddEdge(new Edge(broadcast, metricsCollector, BufferMode.Bounded, 10));
        builder.AddEdge(new Edge(broadcast, router, BufferMode.Bounded, 10));

        // Router → Filters
        builder.Connect(router, evenFilter);
        builder.Connect(router, oddFilter);

        // Filters → Processors (competing)
        builder.AddEdge(new Edge(evenFilter, evenProcessors.Cast<IBlock>().ToList(),
            new CompetingEdgeStrategy(BufferMode.Bounded, 10)));
        builder.AddEdge(new Edge(oddFilter, oddProcessors.Cast<IBlock>().ToList(),
            new CompetingEdgeStrategy(BufferMode.Bounded, 10)));

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        var sw = Stopwatch.StartNew();
        await graph.ExecuteAsync(context);
        sw.Stop();

        var sequentialEstimate = itemCount * delayMs * 3; // 3 delay stages
        
        _output.WriteLine($"Level 5: Full Complexity");
        _output.WriteLine($"  Total time: {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Expected sequential: {sequentialEstimate}ms");
        _output.WriteLine($"  Expected parallel: ~{sequentialEstimate / concurrency}ms");

        // This is where we might see the scaling issue
        ((double)sw.ElapsedMilliseconds).ShouldBeLessThan(sequentialEstimate * 0.6, 
            "Full complexity pipeline should still scale");
    }

    #endregion

    #region Level 6: Add BatchBlock

    [Fact]
    public async Task Level6_WithBatchBlock_Should_Scale()
    {
        // Add batching: Source → Validators → Enrichers → Broadcast → [Metrics, BatchPath]
        // BatchPath: Router → Filter → Batch → Aggregator → Writer
        
        const int itemCount = 1000; // Increased to make batching meaningful
        const int concurrency = 4;
        const int delayMs = 5;
        const int batchSize = 50;
        
        var services = new ServiceCollection().BuildServiceProvider();
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(itemCount));

        var validators = new List<TransformerBlock<int, int>>();
        for (int i = 0; i < concurrency; i++)
        {
            validators.Add(new TransformerBlock<int, int>($"validator-{i}", 
                (item, ctx) => ProcessWithDelay(item, delayMs)));
        }

        var enrichers = new List<TransformerBlock<int, string>>();
        for (int i = 0; i < concurrency; i++)
        {
            enrichers.Add(new TransformerBlock<int, string>($"enricher-{i}", 
                (item, ctx) => TransformWithDelayAndRoute(item, delayMs)));
        }

        var broadcast = new BroadcastBlock<string>("broadcast");
        
        var metricsCollector = new ProcessorBlock<string>("metrics", async (item, ctx) =>
        {
            await Task.CompletedTask;
        });

        // Batch path
        var router = new RouterBlock<string>("router", item => item.StartsWith("even") ? "even" : "odd");
        var evenFilter = new RouteFilterBlock<string>("even-filter", "even");
        
        // KEY DIFFERENCE: Add BatchBlock
        var batcher = new BatchBlock<string>("batcher", batchSize, TimeSpan.FromMilliseconds(50));
        
        var aggregator = new TransformerBlock<string[], string>("aggregator", 
            (batch, ctx) => AggregateSimple(batch, delayMs));
        
        var writer = new ProcessorBlock<string>("writer", async (item, ctx) =>
        {
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("level6-test");
        builder.AddBlock(producer);
        foreach (var v in validators) builder.AddBlock(v);
        foreach (var e in enrichers) builder.AddBlock(e);
        builder.AddBlock(broadcast);
        builder.AddBlock(metricsCollector);
        builder.AddBlock(router);
        builder.AddBlock(evenFilter);
        builder.AddBlock(batcher);
        builder.AddBlock(aggregator);
        builder.AddBlock(writer);

        builder.AddEdge(new Edge(producer, validators.Cast<IBlock>().ToList(), 
            new CompetingEdgeStrategy(BufferMode.Bounded, 10)));

        foreach (var validator in validators)
        {
            builder.AddEdge(new Edge(validator, enrichers.Cast<IBlock>().ToList(), 
                new CompetingEdgeStrategy(BufferMode.Bounded, 10)));
        }

        foreach (var enricher in enrichers)
        {
            builder.Connect(enricher, broadcast);
        }

        builder.AddEdge(new Edge(broadcast, metricsCollector, BufferMode.Bounded, 10));
        builder.AddEdge(new Edge(broadcast, router, BufferMode.Bounded, 10));
        builder.Connect(router, evenFilter);
        builder.Connect(evenFilter, batcher);
        builder.Connect(batcher, aggregator);
        builder.Connect(aggregator, writer);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        var sw = Stopwatch.StartNew();
        await graph.ExecuteAsync(context);
        sw.Stop();

        var sequentialEstimate = itemCount * delayMs * 3; // 3 delay stages
        
        _output.WriteLine($"Level 6: With BatchBlock");
        _output.WriteLine($"  Total time: {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Expected sequential: {sequentialEstimate}ms");
        _output.WriteLine($"  Items: {itemCount}, BatchSize: {batchSize}");

        // With batching, still should see some speedup but may be less than before
        ((double)sw.ElapsedMilliseconds).ShouldBeLessThan(sequentialEstimate * 0.7, 
            "Pipeline with BatchBlock should still show concurrency benefit");
    }

    #endregion

    #region Level 7: Scale to 10K Items

    [Fact]
    public async Task Level7_With10KItems_Should_Scale()
    {
        // Scale up to 10K items like the benchmark
        // Keep complexity similar to Level 5 but with 10K items
        
        const int itemCount = 10000;
        const int concurrency = 4;
        const int delayMs = 1; // Match benchmark delay
        
        var services = new ServiceCollection().BuildServiceProvider();
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(itemCount));

        var validators = new List<TransformerBlock<int, int>>();
        for (int i = 0; i < concurrency; i++)
        {
            validators.Add(new TransformerBlock<int, int>($"validator-{i}", 
                (item, ctx) => ProcessWithDelay(item, delayMs)));
        }

        var enrichers = new List<TransformerBlock<int, string>>();
        for (int i = 0; i < concurrency; i++)
        {
            enrichers.Add(new TransformerBlock<int, string>($"enricher-{i}", 
                (item, ctx) => TransformWithDelay(item, delayMs)));
        }

        var collector = new ProcessorBlock<string>("collector", async (item, ctx) =>
        {
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("level7-test");
        builder.AddBlock(producer);
        foreach (var v in validators) builder.AddBlock(v);
        foreach (var e in enrichers) builder.AddBlock(e);
        builder.AddBlock(collector);

        builder.AddEdge(new Edge(producer, validators.Cast<IBlock>().ToList(), 
            new CompetingEdgeStrategy(BufferMode.Bounded, 100))); // Larger buffer for volume

        foreach (var validator in validators)
        {
            builder.AddEdge(new Edge(validator, enrichers.Cast<IBlock>().ToList(), 
                new CompetingEdgeStrategy(BufferMode.Bounded, 100)));
        }

        foreach (var enricher in enrichers)
        {
            builder.Connect(enricher, collector);
        }

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        var sw = Stopwatch.StartNew();
        await graph.ExecuteAsync(context);
        sw.Stop();

        var sequentialEstimate = itemCount * delayMs * 2;
        
        _output.WriteLine($"Level 7: With 10K Items");
        _output.WriteLine($"  Total time: {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Expected sequential: {sequentialEstimate}ms");
        _output.WriteLine($"  Items: {itemCount}");

        // Should still scale with high volume
        ((double)sw.ElapsedMilliseconds).ShouldBeLessThan(sequentialEstimate * 0.6, 
            "High-volume pipeline should scale with concurrency");
    }

    #endregion

    #region Level 8: Exact ComplexEtlPOC Match

    [Fact]
    public async Task Level8_ExactComplexEtlPOCMatch_Should_Scale()
    {
        // Exact match to ComplexEtlPOC benchmark:
        // - 10K items
        // - 1ms delays
        // - BatchBlock with batchSize=100
        // - Full complexity: validators → enrichers → broadcast → [metrics, router]
        // - Router → filters → [processors (competing), batcher→aggregator, direct writer]
        
        const int itemCount = 10000;
        const int concurrency = 4;
        const int delayMs = 1;
        const int batchSize = 100;
        
        var services = new ServiceCollection().BuildServiceProvider();
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(itemCount));

        // Stage 1: Validators
        var validators = new List<TransformerBlock<int, int>>();
        for (int i = 0; i < concurrency; i++)
        {
            validators.Add(new TransformerBlock<int, int>($"validator-{i}", 
                (item, ctx) => ProcessWithDelay(item, delayMs)));
        }

        // Stage 2: Enrichers  
        var enrichers = new List<TransformerBlock<int, string>>();
        for (int i = 0; i < concurrency; i++)
        {
            enrichers.Add(new TransformerBlock<int, string>($"enricher-{i}", 
                (item, ctx) => EnrichForRouting(item, delayMs)));
        }

        var broadcast = new BroadcastBlock<string>("broadcast");
        
        var metricsCollector = new ProcessorBlock<string>("metrics", async (item, ctx) =>
        {
            await Task.CompletedTask;
        });

        var router = new RouterBlock<string>("router", item =>
        {
            if (item.Contains("TypeA")) return "TypeA";
            if (item.Contains("TypeB")) return "TypeB";
            return "TypeC";
        });
        
        // TypeA path: filter → processors (competing)
        var typeAFilter = new RouteFilterBlock<string>("typeA-filter", "TypeA");
        var typeAProcessors = new List<TransformerBlock<string, string>>();
        for (int i = 0; i < concurrency; i++)
        {
            typeAProcessors.Add(new TransformerBlock<string, string>($"typeA-proc-{i}",
                (item, ctx) => ProcessTypeA(item, delayMs)));
        }
        var typeAWriters = new List<ProcessorBlock<string>>();
        for (int i = 0; i < concurrency; i++)
        {
            typeAWriters.Add(new ProcessorBlock<string>($"typeA-writer-{i}", async (item, ctx) =>
            {
                await Task.CompletedTask;
            }));
        }

        // TypeB path: filter → BATCHER → aggregator → writer
        var typeBFilter = new RouteFilterBlock<string>("typeB-filter", "TypeB");
        var typeBBatcher = new BatchBlock<string>("typeB-batcher", batchSize, TimeSpan.FromMilliseconds(100));
        var typeBAggregator = new TransformerBlock<string[], string>("typeB-aggregator",
            (batch, ctx) => AggregateSimple(batch, delayMs));
        var typeBWriter = new ProcessorBlock<string>("typeB-writer", async (item, ctx) =>
        {
            await Task.CompletedTask;
        });

        // TypeC path: filter → writer
        var typeCFilter = new RouteFilterBlock<string>("typeC-filter", "TypeC");
        var typeCWriter = new ProcessorBlock<string>("typeC-writer", async (item, ctx) =>
        {
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("level8-exact-match");
        builder.AddBlock(producer);
        foreach (var v in validators) builder.AddBlock(v);
        foreach (var e in enrichers) builder.AddBlock(e);
        builder.AddBlock(broadcast);
        builder.AddBlock(metricsCollector);
        builder.AddBlock(router);
        builder.AddBlock(typeAFilter);
        foreach (var p in typeAProcessors) builder.AddBlock(p);
        foreach (var w in typeAWriters) builder.AddBlock(w);
        builder.AddBlock(typeBFilter);
        builder.AddBlock(typeBBatcher);
        builder.AddBlock(typeBAggregator);
        builder.AddBlock(typeBWriter);
        builder.AddBlock(typeCFilter);
        builder.AddBlock(typeCWriter);

        // Producer → Validators (competing)
        builder.AddEdge(new Edge(producer, validators.Cast<IBlock>().ToList(), 
            new CompetingEdgeStrategy(BufferMode.Bounded, 100)));

        // Validators → Enrichers (competing)
        foreach (var validator in validators)
        {
            builder.AddEdge(new Edge(validator, enrichers.Cast<IBlock>().ToList(), 
                new CompetingEdgeStrategy(BufferMode.Bounded, 100)));
        }

        // Enrichers → Broadcast
        foreach (var enricher in enrichers)
        {
            builder.Connect(enricher, broadcast);
        }

        // Broadcast → Metrics & Router
        builder.AddEdge(new Edge(broadcast, metricsCollector, BufferMode.Bounded, 100));
        builder.AddEdge(new Edge(broadcast, router, BufferMode.Bounded, 100));

        // Router → Filters
        builder.Connect(router, typeAFilter);
        builder.Connect(router, typeBFilter);
        builder.Connect(router, typeCFilter);

        // TypeA: Filter → Processors (competing) → Writers (competing)
        builder.AddEdge(new Edge(typeAFilter, typeAProcessors.Cast<IBlock>().ToList(),
            new CompetingEdgeStrategy(BufferMode.Bounded, 100)));
        
        foreach (var processor in typeAProcessors)
        {
            builder.AddEdge(new Edge(processor, typeAWriters.Cast<IBlock>().ToList(),
                new CompetingEdgeStrategy(BufferMode.Bounded, 50)));
        }

        // TypeB: Filter → Batcher → Aggregator → Writer
        builder.Connect(typeBFilter, typeBBatcher);
        builder.Connect(typeBBatcher, typeBAggregator);
        builder.Connect(typeBAggregator, typeBWriter);

        // TypeC: Filter → Writer
        builder.Connect(typeCFilter, typeCWriter);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        var sw = Stopwatch.StartNew();
        await graph.ExecuteAsync(context);
        sw.Stop();

        var sequentialEstimate = itemCount * delayMs * 3;
        
        _output.WriteLine($"Level 8: Exact ComplexEtlPOC Match");
        _output.WriteLine($"  Total time: {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"  Expected sequential: {sequentialEstimate}ms");
        _output.WriteLine($"  Items: {itemCount}, Concurrency: {concurrency}, BatchSize: {batchSize}");
        _output.WriteLine($"  This should match the actual benchmark behavior");

        // THIS is the critical test - if this fails to scale, we've reproduced the issue
        ((double)sw.ElapsedMilliseconds).ShouldBeLessThan(sequentialEstimate * 0.6, 
            "Exact ComplexEtlPOC match should scale with concurrency");
    }

    #endregion

    #region Helper Methods (continued)

    private static async IAsyncEnumerable<string> EnrichForRouting(int item, int delayMs)
    {
        await Task.Delay(delayMs);
        // Distribute across types for routing
        var type = item % 3 == 0 ? "TypeA" : (item % 3 == 1 ? "TypeB" : "TypeC");
        yield return $"{type}-{item}";
    }

    private static async IAsyncEnumerable<string> ProcessTypeA(string item, int delayMs)
    {
        await Task.Delay(delayMs);
        yield return $"processed-{item}";
    }

    private static async IAsyncEnumerable<string> AggregateSimple(string[] batch, int delayMs)
    {
        await Task.Delay(delayMs);
        yield return $"batch-{batch.Length}";
    }

    #endregion
}
