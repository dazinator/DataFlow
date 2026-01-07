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
using DataFlow.POC.Tests.TestHelpers;

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

    #region Actor Implementations

    /// <summary>
    /// Actor that transforms integers to strings with logging.
    /// </summary>
    private class TransformWithLoggingActor : IStreamActor<int, string>
    {
        private readonly string _blockName;
        private readonly ConcurrentBag<(string, int, long)> _log;
        private readonly int _delayMs;

        public TransformWithLoggingActor(string blockName, ConcurrentBag<(string, int, long)> log, int delayMs)
        {
            _blockName = blockName;
            _log = log;
            _delayMs = delayMs;
        }

        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                var timestamp = Stopwatch.GetTimestamp() / (Stopwatch.Frequency / 1000);
                _log.Add((_blockName, item, timestamp));
                
                await Task.Delay(_delayMs, context.CancellationToken);
                yield return $"{_blockName}:{item}";
            }
        }
    }

    /// <summary>
    /// Actor that processes items with timing tracking.
    /// </summary>
    private class ProcessWithTimingActor : IStreamActor<int, object>
    {
        private readonly string _processorName;
        private readonly int _delayMs;
        private readonly ConcurrentBag<(string, int, long, long)> _log;

        public ProcessWithTimingActor(
            string processorName, 
            int delayMs, 
            ConcurrentBag<(string, int, long, long)> log)
        {
            _processorName = processorName;
            _delayMs = delayMs;
            _log = log;
        }

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                var start = Stopwatch.GetTimestamp() / (Stopwatch.Frequency / 1000);
                await Task.Delay(_delayMs, context.CancellationToken);
                var end = Stopwatch.GetTimestamp() / (Stopwatch.Frequency / 1000);
                
                _log.Add((_processorName, item, start, end));
            }
            yield break;
        }
    }

    /// <summary>
    /// Actor for validation with logging.
    /// </summary>
    private class ValidateWithLoggingActor : IStreamActor<int, int>
    {
        private readonly string _name;
        private readonly ConcurrentBag<string> _log;
        private readonly int _delayMs;

        public ValidateWithLoggingActor(string name, ConcurrentBag<string> log, int delayMs)
        {
            _name = name;
            _log = log;
            _delayMs = delayMs;
        }

        public async IAsyncEnumerable<int> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                _log.Add($"{_name}:{item}");
                await Task.Delay(_delayMs, context.CancellationToken);
                yield return item;
            }
        }
    }

    /// <summary>
    /// Actor for enrichment with logging.
    /// </summary>
    private class EnrichWithLoggingActor : IStreamActor<int, string>
    {
        private readonly string _name;
        private readonly ConcurrentBag<string> _log;
        private readonly int _delayMs;

        public EnrichWithLoggingActor(string name, ConcurrentBag<string> log, int delayMs)
        {
            _name = name;
            _log = log;
            _delayMs = delayMs;
        }

        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                _log.Add($"{_name}:{item}");
                await Task.Delay(_delayMs, context.CancellationToken);
                yield return $"enriched-{item}";
            }
        }
    }

    /// <summary>
    /// Actor that transforms with delay.
    /// </summary>
    private class TransformWithDelayActor : IStreamActor<int, string>
    {
        private readonly int _delayMs;

        public TransformWithDelayActor(int delayMs)
        {
            _delayMs = delayMs;
        }

        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                await Task.Delay(_delayMs, context.CancellationToken);
                yield return $"item-{item}";
            }
        }
    }

    /// <summary>
    /// Actor that transforms with delay and routing prefix.
    /// </summary>
    private class TransformWithDelayAndRouteActor : IStreamActor<int, string>
    {
        private readonly int _delayMs;

        public TransformWithDelayAndRouteActor(int delayMs)
        {
            _delayMs = delayMs;
        }

        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                await Task.Delay(_delayMs, context.CancellationToken);
                var prefix = item % 2 == 0 ? "even" : "odd";
                yield return $"{prefix}-{item}";
            }
        }
    }

    /// <summary>
    /// Actor that processes integers with delay.
    /// </summary>
    private class ProcessWithDelayActor : IStreamActor<int, int>
    {
        private readonly int _delayMs;

        public ProcessWithDelayActor(int delayMs)
        {
            _delayMs = delayMs;
        }

        public async IAsyncEnumerable<int> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                await Task.Delay(_delayMs, context.CancellationToken);
                yield return item;
            }
        }
    }

    /// <summary>
    /// No-op processor actor for strings.
    /// </summary>
    private class NoOpStringProcessorActor : IStreamActor<string, object>
    {
        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<string> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                // No-op
            }
            yield break;
        }
    }

    /// <summary>
    /// Actor for enriching for routing.
    /// </summary>
    private class EnrichForRoutingActor : IStreamActor<int, string>
    {
        private readonly int _delayMs;

        public EnrichForRoutingActor(int delayMs)
        {
            _delayMs = delayMs;
        }

        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                await Task.Delay(_delayMs, context.CancellationToken);
                var type = item % 3 == 0 ? "TypeA" : (item % 3 == 1 ? "TypeB" : "TypeC");
                yield return $"{type}-{item}";
            }
        }
    }

    /// <summary>
    /// Actor for processing TypeA items.
    /// </summary>
    private class ProcessTypeAActor : IStreamActor<string, string>
    {
        private readonly int _delayMs;

        public ProcessTypeAActor(int delayMs)
        {
            _delayMs = delayMs;
        }

        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<string> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                await Task.Delay(_delayMs, context.CancellationToken);
                yield return $"processed-{item}";
            }
        }
    }

    /// <summary>
    /// Actor for aggregating batches.
    /// </summary>
    private class AggregateBatchActor : IStreamActor<string[], string>
    {
        private readonly int _delayMs;

        public AggregateBatchActor(int delayMs)
        {
            _delayMs = delayMs;
        }

        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<string[]> input,
            IActorExecutionContext context)
        {
            await foreach (var batch in input.WithCancellation(context.CancellationToken))
            {
                await Task.Delay(_delayMs, context.CancellationToken);
                yield return $"batch-{batch.Length}";
            }
        }
    }

    /// <summary>
    /// Actor that collects strings into a bag.
    /// </summary>
    private class StringBagCollectorActor : IStreamActor<string, object>
    {
        private readonly ConcurrentBag<string> _results;

        public StringBagCollectorActor(ConcurrentBag<string> results)
        {
            _results = results;
        }

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<string> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                _results.Add(item);
            }
            yield break;
        }
    }

    /// <summary>
    /// Processor actor with configurable delay.
    /// </summary>
    private class DelayProcessorActor : IStreamActor<string, object>
    {
        private readonly int _delayMs;

        public DelayProcessorActor(int delayMs)
        {
            _delayMs = delayMs;
        }

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<string> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                await Task.Delay(_delayMs, context.CancellationToken);
            }
            yield break;
        }
    }

    #endregion

    [Fact]
    [Trait("Category", "Performance")]
    public async Task Multiple_Transformers_With_CompetingEdge_Should_Process_Concurrently()
    {
        // Arrange
        const int itemCount = 100;
        const int concurrency = 4;
        const int processingDelayMs = 10;
        
        var services = new ServiceCollection().BuildServiceProvider();
        var producer = BlockHelpers.CreateProducer<int>("producer", ProduceIntegers(itemCount));

        // Create N transformer instances that will compete for items
        var transformers = new List<IBlock<int, string>>();
        var processingLog = new ConcurrentBag<(string BlockName, int Item, long TimestampMs)>();
        
        for (int i = 0; i < concurrency; i++)
        {
            var blockName = $"transformer-{i}";
            
            // Create separate service provider for each transformer
            var transformerServices = new ServiceCollection();
            transformerServices.AddScoped(_ => new TransformWithLoggingActor(blockName, processingLog, processingDelayMs));
            var transformerServiceProvider = transformerServices.BuildServiceProvider();
            
            var transformer = BlockHelpers.CreateActor<int, string, TransformWithLoggingActor>(
                blockName,
                transformerServiceProvider.GetRequiredService<IServiceScopeFactory>());
            transformers.Add(transformer);
        }

        // Create service provider for collector
        var collectorServices = new ServiceCollection();
        collectorServices.AddScoped<NoOpStringProcessorActor>();
        var collectorServiceProvider = collectorServices.BuildServiceProvider();

        var collector = BlockHelpers.CreateActor<string, object, NoOpStringProcessorActor>("collector", collectorServiceProvider.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("concurrency-test");
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
    [Trait("Category", "Performance")]
    public async Task Multiple_Processors_With_CompetingEdge_Should_Execute_Concurrently()
    {
        // Arrange
        const int itemCount = 50;
        const int concurrency = 4;
        const int processingDelayMs = 20;
        
        var services = new ServiceCollection().BuildServiceProvider();
        var producer = BlockHelpers.CreateProducer<int>("producer", ProduceIntegers(itemCount));

        // Track which processor handles which item and when
        var processingLog = new ConcurrentBag<(string ProcessorName, int Item, long StartMs, long EndMs)>();
        
        var processors = new List<IBlock<int, object>>();
        for (int i = 0; i < concurrency; i++)
        {
            var processorName = $"processor-{i}";
            
            // Create separate service provider for each processor
            var processorServices = new ServiceCollection();
            processorServices.AddScoped(_ => new ProcessWithTimingActor(processorName, processingDelayMs, processingLog));
            var processorServiceProvider = processorServices.BuildServiceProvider();
            
            var processor = BlockHelpers.CreateActor<int, object, ProcessWithTimingActor>(
                processorName,
                processorServiceProvider.GetRequiredService<IServiceScopeFactory>());
            processors.Add(processor);
        }

        var builder = GraphHelpers.CreateGraphBuilder("processor-concurrency-test");
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
    [Trait("Category", "Performance")]
    public async Task Chained_Competing_Stages_Should_Maintain_Concurrency()
    {
        // This test verifies that concurrency is maintained across multiple stages
        // Arrange
        const int itemCount = 40;
        const int concurrencyPerStage = 3;
        const int delayMs = 10;
        
        var services = new ServiceCollection().BuildServiceProvider();
        var producer = BlockHelpers.CreateProducer<int>("producer", ProduceIntegers(itemCount));

        // Stage 1: Validators
        var validators = new List<IBlock<int, int>>();
        var validatorLog = new ConcurrentBag<string>();
        for (int i = 0; i < concurrencyPerStage; i++)
        {
            var name = $"validator-{i}";
            
            // Create separate service provider for each validator
            var validatorServices = new ServiceCollection();
            validatorServices.AddScoped(_ => new ValidateWithLoggingActor(name, validatorLog, delayMs));
            var validatorServiceProvider = validatorServices.BuildServiceProvider();
            
            validators.Add(BlockHelpers.CreateActor<int, int, ValidateWithLoggingActor>(
                name,
                validatorServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }

        // Stage 2: Enrichers
        var enrichers = new List<IBlock<int, string>>();
        var enricherLog = new ConcurrentBag<string>();
        for (int i = 0; i < concurrencyPerStage; i++)
        {
            var name = $"enricher-{i}";
            
            // Create separate service provider for each enricher
            var enricherServices = new ServiceCollection();
            enricherServices.AddScoped(_ => new EnrichWithLoggingActor(name, enricherLog, delayMs));
            var enricherServiceProvider = enricherServices.BuildServiceProvider();
            
            enrichers.Add(BlockHelpers.CreateActor<int, string, EnrichWithLoggingActor>(
                name,
                enricherServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }

        // Collector
        var results = new ConcurrentBag<string>();
        var collectorServices = new ServiceCollection();
        collectorServices.AddScoped(_ => new StringBagCollectorActor(results));
        var collectorServiceProvider = collectorServices.BuildServiceProvider();
        
        var collector = BlockHelpers.CreateActor<string, object, StringBagCollectorActor>("collector", collectorServiceProvider.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("chained-concurrency-test");
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
    [Trait("Category", "Performance")]
    public async Task Level1_Simple_Competing_Transformers_Should_Scale()
    {
        // This is our baseline - we know this works with 3.6x speedup
        // Source → Transformers (competing) → Collector
        
        const int itemCount = 100;
        const int concurrency = 4;
        const int processingDelayMs = 10;
        
        var services = new ServiceCollection().BuildServiceProvider();
        var producer = BlockHelpers.CreateProducer<int>("producer", ProduceIntegers(itemCount));

        var transformers = new List<IBlock<int, string>>();
        var processingLog = new ConcurrentBag<(string BlockName, int Item, long TimestampMs)>();
        
        for (int i = 0; i < concurrency; i++)
        {
            var blockName = $"transformer-{i}";
            
            // Create separate service provider for each transformer
            var transformerServices = new ServiceCollection();
            transformerServices.AddScoped(_ => new TransformWithLoggingActor(blockName, processingLog, processingDelayMs));
            var transformerServiceProvider = transformerServices.BuildServiceProvider();
            
            var transformer = BlockHelpers.CreateActor<int, string, TransformWithLoggingActor>(
                blockName,
                transformerServiceProvider.GetRequiredService<IServiceScopeFactory>());
            transformers.Add(transformer);
        }

        var collectorServices = new ServiceCollection();
        collectorServices.AddScoped<NoOpStringProcessorActor>();
        var collectorServiceProvider = collectorServices.BuildServiceProvider();
        
        var collector = BlockHelpers.CreateActor<string, object, NoOpStringProcessorActor>("collector", collectorServiceProvider.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("level1-test");
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
    [Trait("Category", "Performance")]
    public async Task Level2_TwoStage_Pipeline_Should_Scale()
    {
        // Add second stage: Source → Validators (competing) → Enrichers (competing) → Collector
        // This matches the first two stages of ComplexEtlPOC
        
        const int itemCount = 100;
        const int concurrency = 4;
        const int delayMs = 10;
        
        var services = new ServiceCollection().BuildServiceProvider();
        var producer = BlockHelpers.CreateProducer<int>("producer", ProduceIntegers(itemCount));

        // Stage 1: Validators
        var validators = new List<IBlock<int, int>>();
        for (int i = 0; i < concurrency; i++)
        {
            var name = $"validator-{i}";
            
            var validatorServices = new ServiceCollection();
            validatorServices.AddScoped(_ => new ProcessWithDelayActor(delayMs));
            var validatorServiceProvider = validatorServices.BuildServiceProvider();
            
            validators.Add(BlockHelpers.CreateActor<int, int, ProcessWithDelayActor>(
                name,
                validatorServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }

        // Stage 2: Enrichers
        var enrichers = new List<IBlock<int, string>>();
        for (int i = 0; i < concurrency; i++)
        {
            var name = $"enricher-{i}";
            
            var enricherServices = new ServiceCollection();
            enricherServices.AddScoped(_ => new TransformWithDelayActor(delayMs));
            var enricherServiceProvider = enricherServices.BuildServiceProvider();
            
            enrichers.Add(BlockHelpers.CreateActor<int, string, TransformWithDelayActor>(
                name,
                enricherServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }

        var collectorServices = new ServiceCollection();
        collectorServices.AddScoped<NoOpStringProcessorActor>();
        var collectorServiceProvider = collectorServices.BuildServiceProvider();
        
        var collector = BlockHelpers.CreateActor<string, object, NoOpStringProcessorActor>("collector", collectorServiceProvider.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("level2-test");
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
    [Trait("Category", "Performance")]
    public async Task Level3_WithBroadcast_Should_Scale()
    {
        // Add broadcast: Source → Validators → Enrichers → Broadcast → [Collector1, Collector2]
        
        const int itemCount = 100;
        const int concurrency = 4;
        const int delayMs = 10;
        
        var services = new ServiceCollection().BuildServiceProvider();
        var producer = BlockHelpers.CreateProducer<int>("producer", ProduceIntegers(itemCount));

        var validators = new List<IBlock<int, int>>();
        for (int i = 0; i < concurrency; i++)
        {
            var validatorServices = new ServiceCollection();
            validatorServices.AddScoped(_ => new ProcessWithDelayActor(delayMs));
            var validatorServiceProvider = validatorServices.BuildServiceProvider();
            
            validators.Add(BlockHelpers.CreateActor<int, int, ProcessWithDelayActor>(
                $"validator-{i}",
                validatorServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }

        var enrichers = new List<IBlock<int, string>>();
        for (int i = 0; i < concurrency; i++)
        {
            var enricherServices = new ServiceCollection();
            enricherServices.AddScoped(_ => new TransformWithDelayActor(delayMs));
            var enricherServiceProvider = enricherServices.BuildServiceProvider();
            
            enrichers.Add(BlockHelpers.CreateActor<int, string, TransformWithDelayActor>(
                $"enricher-{i}",
                enricherServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }

        var broadcast = BlockHelpers.CreateBroadcast<string>("broadcast");

        var collector1Services = new ServiceCollection();
        collector1Services.AddScoped<NoOpStringProcessorActor>();
        var collector1ServiceProvider = collector1Services.BuildServiceProvider();
        
        var collector1 = BlockHelpers.CreateActor<string, object, NoOpStringProcessorActor>("collector1", collector1ServiceProvider.GetRequiredService<IServiceScopeFactory>());

        var collector2Services = new ServiceCollection();
        collector2Services.AddScoped<NoOpStringProcessorActor>();
        var collector2ServiceProvider = collector2Services.BuildServiceProvider();
        
        var collector2 = BlockHelpers.CreateActor<string, object, NoOpStringProcessorActor>("collector2", collector2ServiceProvider.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("level3-test");
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
    [Trait("Category", "Performance")]
    public async Task Level4_WithRouting_Should_Scale()
    {
        // Add routing: Source → Validators → Enrichers → SelectiveRouting → [Even, Odd]
        
        const int itemCount = 100;
        const int concurrency = 4;
        const int delayMs = 10;
        
        var services = new ServiceCollection().BuildServiceProvider();
        var producer = BlockHelpers.CreateProducer<int>("producer", ProduceIntegers(itemCount));

        var validators = new List<IBlock<int, int>>();
        for (int i = 0; i < concurrency; i++)
        {
            var validatorServices = new ServiceCollection();
            validatorServices.AddScoped(_ => new ProcessWithDelayActor(delayMs));
            var validatorServiceProvider = validatorServices.BuildServiceProvider();
            
            validators.Add(BlockHelpers.CreateActor<int, int, ProcessWithDelayActor>(
                $"validator-{i}",
                validatorServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }

        var enrichers = new List<IBlock<int, string>>();
        for (int i = 0; i < concurrency; i++)
        {
            var enricherServices = new ServiceCollection();
            enricherServices.AddScoped(_ => new TransformWithDelayAndRouteActor(delayMs));
            var enricherServiceProvider = enricherServices.BuildServiceProvider();
            
            enrichers.Add(BlockHelpers.CreateActor<int, string, TransformWithDelayAndRouteActor>(
                $"enricher-{i}",
                enricherServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }

        var evenCollectorServices = new ServiceCollection();
        evenCollectorServices.AddScoped<NoOpStringProcessorActor>();
        var evenCollectorServiceProvider = evenCollectorServices.BuildServiceProvider();
        
        var evenCollector = BlockHelpers.CreateActor<string, object, NoOpStringProcessorActor>("even-collector", evenCollectorServiceProvider.GetRequiredService<IServiceScopeFactory>());
        
        var oddCollectorServices = new ServiceCollection();
        oddCollectorServices.AddScoped<NoOpStringProcessorActor>();
        var oddCollectorServiceProvider = oddCollectorServices.BuildServiceProvider();
        
        var oddCollector = BlockHelpers.CreateActor<string, object, NoOpStringProcessorActor>("odd-collector", oddCollectorServiceProvider.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("level4-test");
        builder.AddBlock(producer);
        foreach (var v in validators) builder.AddBlock(v);
        foreach (var e in enrichers) builder.AddBlock(e);
        builder.AddBlock(evenCollector);
        builder.AddBlock(oddCollector);

        builder.AddEdge(new Edge(producer, validators.Cast<IBlock>().ToList(), 
            new CompetingEdgeStrategy(BufferMode.Bounded, 10)));

        foreach (var validator in validators)
        {
            builder.AddEdge(new Edge(validator, enrichers.Cast<IBlock>().ToList(), 
                new CompetingEdgeStrategy(BufferMode.Bounded, 10)));
        }

        // Create selective routing from enrichers to collectors
        var routeMapping = new Dictionary<string, IBlock>
        {
            ["even"] = evenCollector,
            ["odd"] = oddCollector
        };

        var routingStrategy = new SelectiveRoutingEdgeStrategy<string>(
            routeKeyToBlock: routeMapping,
            routeSelector: item => item.StartsWith("even") ? "even" : "odd");

        foreach (var enricher in enrichers)
        {
            var routingEdge = new Edge(
                enricher,
                new[] { evenCollector, oddCollector },
                routingStrategy);
            builder.AddEdge(routingEdge);
        }

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
    [Trait("Category", "Performance")]
    public async Task Level5_FullComplexity_Should_Scale()
    {
        // Full complexity: Broadcast + Routing + Multiple downstream paths with competing processors
        
        const int itemCount = 100;
        const int concurrency = 4;
        const int delayMs = 10;
        
        var services = new ServiceCollection().BuildServiceProvider();
        var producer = BlockHelpers.CreateProducer<int>("producer", ProduceIntegers(itemCount));

        // Validators
        var validators = new List<IBlock<int, int>>();
        for (int i = 0; i < concurrency; i++)
        {
            var validatorServices = new ServiceCollection();
            validatorServices.AddScoped(_ => new ProcessWithDelayActor(delayMs));
            var validatorServiceProvider = validatorServices.BuildServiceProvider();
            
            validators.Add(BlockHelpers.CreateActor<int, int, ProcessWithDelayActor>(
                $"validator-{i}",
                validatorServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }

        // Enrichers
        var enrichers = new List<IBlock<int, string>>();
        for (int i = 0; i < concurrency; i++)
        {
            var enricherServices = new ServiceCollection();
            enricherServices.AddScoped(_ => new TransformWithDelayAndRouteActor(delayMs));
            var enricherServiceProvider = enricherServices.BuildServiceProvider();
            
            enrichers.Add(BlockHelpers.CreateActor<int, string, TransformWithDelayAndRouteActor>(
                $"enricher-{i}",
                enricherServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }

        var broadcast = BlockHelpers.CreateBroadcast<string>("broadcast");
        
        // Broadcast path 1: Collector
        var metricsCollectorServices = new ServiceCollection();
        metricsCollectorServices.AddScoped<NoOpStringProcessorActor>();
        var metricsCollectorServiceProvider = metricsCollectorServices.BuildServiceProvider();
        
        var metricsCollector = BlockHelpers.CreateActor<string, object, NoOpStringProcessorActor>("metrics", metricsCollectorServiceProvider.GetRequiredService<IServiceScopeFactory>());

        // Even route: Multiple processors competing
        var evenProcessors = new List<IBlock<string, object>>();
        for (int i = 0; i < concurrency; i++)
        {
            var evenProcServices = new ServiceCollection();
            evenProcServices.AddScoped(_ => new DelayProcessorActor(delayMs / 2));
            var evenProcServiceProvider = evenProcServices.BuildServiceProvider();
            
            evenProcessors.Add(BlockHelpers.CreateActor<string, object, DelayProcessorActor>(
                $"even-proc-{i}",
                evenProcServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }
        
        // Odd route: Multiple processors competing
        var oddProcessors = new List<IBlock<string, object>>();
        for (int i = 0; i < concurrency; i++)
        {
            var oddProcServices = new ServiceCollection();
            oddProcServices.AddScoped(_ => new DelayProcessorActor(delayMs / 2));
            var oddProcServiceProvider = oddProcServices.BuildServiceProvider();
            
            oddProcessors.Add(BlockHelpers.CreateActor<string, object, DelayProcessorActor>(
                $"odd-proc-{i}",
                oddProcServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }

        var builder = GraphHelpers.CreateGraphBuilder("level5-test");
        builder.AddBlock(producer);
        foreach (var v in validators) builder.AddBlock(v);
        foreach (var e in enrichers) builder.AddBlock(e);
        builder.AddBlock(broadcast);
        builder.AddBlock(metricsCollector);
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

        // Broadcast → Metrics
        builder.AddEdge(new Edge(broadcast, metricsCollector, BufferMode.Bounded, 10));

        // Broadcast → Selective Routing to [even processors (competing), odd processors (competing)]
        // Route to all processors of each type, then they compete
        var routeMapping = new Dictionary<string, IBlock>();
        foreach (var proc in evenProcessors)
        {
            routeMapping[proc.Name] = proc;
        }
        foreach (var proc in oddProcessors)
        {
            routeMapping[proc.Name] = proc;
        }

        // Selector picks a route for even/odd and distributes among processors of that type
        int evenIndex = 0;
        int oddIndex = 0;
        var routingStrategy = new SelectiveRoutingEdgeStrategy<string>(
            routeKeyToBlock: routeMapping,
            routeSelector: item =>
            {
                if (item.StartsWith("even"))
                {
                    var idx = Interlocked.Increment(ref evenIndex) - 1;
                    return evenProcessors[idx % concurrency].Name;
                }
                else
                {
                    var idx = Interlocked.Increment(ref oddIndex) - 1;
                    return oddProcessors[idx % concurrency].Name;
                }
            });

        var allProcessors = evenProcessors.Cast<IBlock>().Concat(oddProcessors.Cast<IBlock>()).ToList();
        var routingEdge = new Edge(broadcast, allProcessors, routingStrategy);
        builder.AddEdge(routingEdge);

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
    [Trait("Category", "Performance")]
    public async Task Level6_WithBatchBlock_Should_Scale()
    {
        // Add batching: Source → Validators → Enrichers → Broadcast → [Metrics, BatchPath]
        // BatchPath: SelectiveRouting → Batch → Aggregator → Writer (only even items)
        
        const int itemCount = 1000; // Increased to make batching meaningful
        const int concurrency = 4;
        const int delayMs = 5;
        const int batchSize = 50;
        
        var services = new ServiceCollection().BuildServiceProvider();
        var producer = BlockHelpers.CreateProducer<int>("producer", ProduceIntegers(itemCount));

        var validators = new List<IBlock<int, int>>();
        for (int i = 0; i < concurrency; i++)
        {
            var validatorServices = new ServiceCollection();
            validatorServices.AddScoped(_ => new ProcessWithDelayActor(delayMs));
            var validatorServiceProvider = validatorServices.BuildServiceProvider();
            
            validators.Add(BlockHelpers.CreateActor<int, int, ProcessWithDelayActor>(
                $"validator-{i}",
                validatorServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }

        var enrichers = new List<IBlock<int, string>>();
        for (int i = 0; i < concurrency; i++)
        {
            var enricherServices = new ServiceCollection();
            enricherServices.AddScoped(_ => new TransformWithDelayAndRouteActor(delayMs));
            var enricherServiceProvider = enricherServices.BuildServiceProvider();
            
            enrichers.Add(BlockHelpers.CreateActor<int, string, TransformWithDelayAndRouteActor>(
                $"enricher-{i}",
                enricherServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }

        var broadcast = BlockHelpers.CreateBroadcast<string>("broadcast");
        
        var metricsCollectorServices = new ServiceCollection();
        metricsCollectorServices.AddScoped<NoOpStringProcessorActor>();
        var metricsCollectorServiceProvider = metricsCollectorServices.BuildServiceProvider();
        
        var metricsCollector = BlockHelpers.CreateActor<string, object, NoOpStringProcessorActor>("metrics", metricsCollectorServiceProvider.GetRequiredService<IServiceScopeFactory>());

        // Batch path - only route "even" items to the batcher, odd items to a discard sink
        var batcher = BlockHelpers.CreateBatch<string>("batcher", batchSize, TimeSpan.FromMilliseconds(50));
        
        var aggregatorServices = new ServiceCollection();
        aggregatorServices.AddScoped(_ => new AggregateBatchActor(delayMs));
        var aggregatorServiceProvider = aggregatorServices.BuildServiceProvider();
        
        var aggregator = BlockHelpers.CreateActor<string[], string, AggregateBatchActor>("aggregator", aggregatorServiceProvider.GetRequiredService<IServiceScopeFactory>());
        
        var writerServices = new ServiceCollection();
        writerServices.AddScoped<NoOpStringProcessorActor>();
        var writerServiceProvider = writerServices.BuildServiceProvider();
        
        var writer = BlockHelpers.CreateActor<string, object, NoOpStringProcessorActor>("writer", writerServiceProvider.GetRequiredService<IServiceScopeFactory>());

        // Discard sink for odd items (not batched)
        var discardServices = new ServiceCollection();
        discardServices.AddScoped<NoOpStringProcessorActor>();
        var discardServiceProvider = discardServices.BuildServiceProvider();
        
        var discardSink = BlockHelpers.CreateActor<string, object, NoOpStringProcessorActor>("discard", discardServiceProvider.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("level6-test");
        builder.AddBlock(producer);
        foreach (var v in validators) builder.AddBlock(v);
        foreach (var e in enrichers) builder.AddBlock(e);
        builder.AddBlock(broadcast);
        builder.AddBlock(metricsCollector);
        builder.AddBlock(batcher);
        builder.AddBlock(aggregator);
        builder.AddBlock(writer);
        builder.AddBlock(discardSink);

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
        
        // Selective routing from broadcast - "even" items go to batcher, "odd" to discard
        var routeMapping = new Dictionary<string, IBlock>
        {
            ["even"] = batcher,
            ["odd"] = discardSink
        };

        var routingStrategy = new SelectiveRoutingEdgeStrategy<string>(
            routeKeyToBlock: routeMapping,
            routeSelector: item => item.StartsWith("even") ? "even" : "odd");

        var routingEdge = new Edge(broadcast, new IBlock[] { batcher, discardSink }, routingStrategy);
        builder.AddEdge(routingEdge);
        
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
    [Trait("Category", "Performance")]
    public async Task Level7_With10KItems_Should_Scale()
    {
        // Scale up to 10K items like the benchmark
        // Keep complexity similar to Level 5 but with 10K items
        
        const int itemCount = 10000;
        const int concurrency = 4;
        const int delayMs = 1; // Match benchmark delay
        
        var services = new ServiceCollection().BuildServiceProvider();
        var producer = BlockHelpers.CreateProducer<int>("producer", ProduceIntegers(itemCount));

        var validators = new List<IBlock<int, int>>();
        for (int i = 0; i < concurrency; i++)
        {
            var validatorServices = new ServiceCollection();
            validatorServices.AddScoped(_ => new ProcessWithDelayActor(delayMs));
            var validatorServiceProvider = validatorServices.BuildServiceProvider();
            
            validators.Add(BlockHelpers.CreateActor<int, int, ProcessWithDelayActor>(
                $"validator-{i}",
                validatorServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }

        var enrichers = new List<IBlock<int, string>>();
        for (int i = 0; i < concurrency; i++)
        {
            var enricherServices = new ServiceCollection();
            enricherServices.AddScoped(_ => new TransformWithDelayActor(delayMs));
            var enricherServiceProvider = enricherServices.BuildServiceProvider();
            
            enrichers.Add(BlockHelpers.CreateActor<int, string, TransformWithDelayActor>(
                $"enricher-{i}",
                enricherServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }

        var collectorServices = new ServiceCollection();
        collectorServices.AddScoped<NoOpStringProcessorActor>();
        var collectorServiceProvider = collectorServices.BuildServiceProvider();
        
        var collector = BlockHelpers.CreateActor<string, object, NoOpStringProcessorActor>("collector", collectorServiceProvider.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("level7-test");
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
    [Trait("Category", "Performance")]
    public async Task Level8_ExactComplexEtlPOCMatch_Should_Scale()
    {
        // Exact match to ComplexEtlPOC benchmark:
        // - 10K items
        // - 1ms delays
        // - BatchBlock with batchSize=100
        // - Full complexity: validators → enrichers → broadcast → [metrics, selective routing]
        // - Selective routing → [processors (competing), batcher→aggregator, direct writer]
        
        const int itemCount = 10000;
        const int concurrency = 4;
        const int delayMs = 1;
        const int batchSize = 100;
        
        var services = new ServiceCollection().BuildServiceProvider();
        var producer = BlockHelpers.CreateProducer<int>("producer", ProduceIntegers(itemCount));

        // Stage 1: Validators
        var validators = new List<IBlock<int, int>>();
        for (int i = 0; i < concurrency; i++)
        {
            var validatorServices = new ServiceCollection();
            validatorServices.AddScoped(_ => new ProcessWithDelayActor(delayMs));
            var validatorServiceProvider = validatorServices.BuildServiceProvider();
            
            validators.Add(BlockHelpers.CreateActor<int, int, ProcessWithDelayActor>(
                $"validator-{i}",
                validatorServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }

        // Stage 2: Enrichers  
        var enrichers = new List<IBlock<int, string>>();
        for (int i = 0; i < concurrency; i++)
        {
            var enricherServices = new ServiceCollection();
            enricherServices.AddScoped(_ => new EnrichForRoutingActor(delayMs));
            var enricherServiceProvider = enricherServices.BuildServiceProvider();
            
            enrichers.Add(BlockHelpers.CreateActor<int, string, EnrichForRoutingActor>(
                $"enricher-{i}",
                enricherServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }

        var broadcast = BlockHelpers.CreateBroadcast<string>("broadcast");
        
        var metricsCollectorServices = new ServiceCollection();
        metricsCollectorServices.AddScoped<NoOpStringProcessorActor>();
        var metricsCollectorServiceProvider = metricsCollectorServices.BuildServiceProvider();
        
        var metricsCollector = BlockHelpers.CreateActor<string, object, NoOpStringProcessorActor>("metrics", metricsCollectorServiceProvider.GetRequiredService<IServiceScopeFactory>());

        // TypeA path: processors (competing) → writers (competing)
        var typeAProcessors = new List<IBlock<string, string>>();
        for (int i = 0; i < concurrency; i++)
        {
            var typeAProcServices = new ServiceCollection();
            typeAProcServices.AddScoped(_ => new ProcessTypeAActor(delayMs));
            var typeAProcServiceProvider = typeAProcServices.BuildServiceProvider();
            
            typeAProcessors.Add(BlockHelpers.CreateActor<string, string, ProcessTypeAActor>(
                $"typeA-proc-{i}",
                typeAProcServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }
        var typeAWriters = new List<IBlock<string, object>>();
        for (int i = 0; i < concurrency; i++)
        {
            var typeAWriterServices = new ServiceCollection();
            typeAWriterServices.AddScoped<NoOpStringProcessorActor>();
            var typeAWriterServiceProvider = typeAWriterServices.BuildServiceProvider();
            
            typeAWriters.Add(BlockHelpers.CreateActor<string, object, NoOpStringProcessorActor>(
                $"typeA-writer-{i}",
                typeAWriterServiceProvider.GetRequiredService<IServiceScopeFactory>()));
        }

        // TypeB path: BATCHER → aggregator → writer
        var typeBBatcher = BlockHelpers.CreateBatch<string>("typeB-batcher", batchSize, TimeSpan.FromMilliseconds(100));
        
        var typeBAggregatorServices = new ServiceCollection();
        typeBAggregatorServices.AddScoped(_ => new AggregateBatchActor(delayMs));
        var typeBAggregatorServiceProvider = typeBAggregatorServices.BuildServiceProvider();
        
        var typeBAggregator = BlockHelpers.CreateActor<string[], string, AggregateBatchActor>("typeB-aggregator", typeBAggregatorServiceProvider.GetRequiredService<IServiceScopeFactory>());
        
        var typeBWriterServices = new ServiceCollection();
        typeBWriterServices.AddScoped<NoOpStringProcessorActor>();
        var typeBWriterServiceProvider = typeBWriterServices.BuildServiceProvider();
        
        var typeBWriter = BlockHelpers.CreateActor<string, object, NoOpStringProcessorActor>("typeB-writer", typeBWriterServiceProvider.GetRequiredService<IServiceScopeFactory>());

        // TypeC path: direct writer
        var typeCWriterServices = new ServiceCollection();
        typeCWriterServices.AddScoped<NoOpStringProcessorActor>();
        var typeCWriterServiceProvider = typeCWriterServices.BuildServiceProvider();
        
        var typeCWriter = BlockHelpers.CreateActor<string, object, NoOpStringProcessorActor>("typeC-writer", typeCWriterServiceProvider.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("level8-exact-match");
        builder.AddBlock(producer);
        foreach (var v in validators) builder.AddBlock(v);
        foreach (var e in enrichers) builder.AddBlock(e);
        builder.AddBlock(broadcast);
        builder.AddBlock(metricsCollector);
        foreach (var p in typeAProcessors) builder.AddBlock(p);
        foreach (var w in typeAWriters) builder.AddBlock(w);
        builder.AddBlock(typeBBatcher);
        builder.AddBlock(typeBAggregator);
        builder.AddBlock(typeBWriter);
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

        // Broadcast → Metrics
        builder.AddEdge(new Edge(broadcast, metricsCollector, BufferMode.Bounded, 100));

        // Broadcast → Selective Routing to [TypeA processors, TypeB batcher, TypeC writer]
        // For TypeA, route to competing processors
        var routeMapping = new Dictionary<string, IBlock>();
        
        // TypeA routes - distribute across competing processors
        for (int i = 0; i < concurrency; i++)
        {
            routeMapping[$"TypeA-{i}"] = typeAProcessors[i];
        }
        
        // TypeB route - to batcher
        routeMapping["TypeB"] = typeBBatcher;
        
        // TypeC route - direct to writer
        routeMapping["TypeC"] = typeCWriter;

        int typeAIndex = 0;
        var routingStrategy = new SelectiveRoutingEdgeStrategy<string>(
            routeKeyToBlock: routeMapping,
            routeSelector: item =>
            {
                if (item.Contains("TypeA"))
                {
                    var idx = Interlocked.Increment(ref typeAIndex) - 1;
                    return $"TypeA-{idx % concurrency}";
                }
                if (item.Contains("TypeB")) return "TypeB";
                return "TypeC";
            });

        var allTargets = typeAProcessors.Cast<IBlock>()
            .Append(typeBBatcher)
            .Append(typeCWriter)
            .ToList();
        
        var routingEdge = new Edge(broadcast, allTargets, routingStrategy);
        builder.AddEdge(routingEdge);

        // TypeA: Processors → Writers (competing)
        foreach (var processor in typeAProcessors)
        {
            builder.AddEdge(new Edge(processor, typeAWriters.Cast<IBlock>().ToList(),
                new CompetingEdgeStrategy(BufferMode.Bounded, 50)));
        }

        // TypeB: Batcher → Aggregator → Writer
        builder.Connect(typeBBatcher, typeBAggregator);
        builder.Connect(typeBAggregator, typeBWriter);

        // TypeC: already routed to writer

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
