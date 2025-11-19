namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using DataFlow.POC.Tests.TestHelpers;

/// <summary>
/// Demonstrates the BufferNode feature with practical examples.
/// </summary>
public class BufferNodeDemonstrationTests
{
    private readonly ITestOutputHelper _output;

    public BufferNodeDemonstrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Actor that collects integers with source tracking.
    /// </summary>
    private class SourceTrackingCollectorActor : IStreamActor<int, object>
    {
        private readonly List<(int value, string source)> _collected;
        private readonly ITestOutputHelper _output;

        public SourceTrackingCollectorActor(List<(int value, string source)> collected, ITestOutputHelper output)
        {
            _collected = collected;
            _output = output;
        }

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                var source = item < 100 ? "A" : (item < 200 ? "B" : "C");
                lock (_collected)
                {
                    _collected.Add((item, source));
                    _output.WriteLine($"  Processed: {item} from Producer-{source}");
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Actor that collects integers with worker name tracking.
    /// </summary>
    private class WorkerCollectorActor : IStreamActor<int, object>
    {
        private readonly List<(int item, string worker)> _collected;
        private readonly string _workerName;
        private readonly ITestOutputHelper _output;

        public WorkerCollectorActor(List<(int item, string worker)> collected, string workerName, ITestOutputHelper output)
        {
            _collected = collected;
            _workerName = workerName;
            _output = output;
        }

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                lock (_collected)
                {
                    _collected.Add((item, _workerName));
                    _output.WriteLine($"  Worker {_workerName}: Processed item {item}");
                }
                await Task.Delay(10, context.CancellationToken); // Simulate work
            }
            yield break;
        }
    }

    /// <summary>
    /// Actor that collects integers with work simulation.
    /// </summary>
    private class DelayedIntCollectorActor : IStreamActor<int, object>
    {
        private readonly List<int> _collected;
        private readonly string _workerName;
        private readonly ITestOutputHelper _output;

        public DelayedIntCollectorActor(List<int> collected, string workerName, ITestOutputHelper output)
        {
            _collected = collected;
            _workerName = workerName;
            _output = output;
        }

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                lock (_collected)
                {
                    _collected.Add(item);
                    _output.WriteLine($"  {_workerName} processed: {item}");
                }
                await Task.Delay(10, context.CancellationToken); // Simulate work
            }
            yield break;
        }
    }

    /// <summary>
    /// Actor that collects strings with logging.
    /// </summary>
    private class StringCollectorActor : IStreamActor<string, object>
    {
        private readonly List<string> _collected;
        private readonly ITestOutputHelper _output;

        public StringCollectorActor(List<string> collected, ITestOutputHelper output)
        {
            _collected = collected;
            _output = output;
        }

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<string> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                lock (_collected)
                {
                    _collected.Add(item);
                    _output.WriteLine($"  Final output: {item}");
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Actor that transforms integers to formatted strings.
    /// </summary>
    private class IntToStringTransformerActor : IStreamActor<int, string>
    {
        private readonly string _prefix;
        private readonly ITestOutputHelper _output;

        public IntToStringTransformerActor(string prefix, ITestOutputHelper output)
        {
            _prefix = prefix;
            _output = output;
        }

        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                var result = $"{_prefix}-{item}";
                _output.WriteLine($"  {_prefix}: {item} → '{result}'");
                yield return result;
            }
        }
    }

    private static async IAsyncEnumerable<int> ProduceIntegers(IExecutionContext ctx, int start, int count)
    {
        for (int i = start; i < start + count; i++)
        {
            yield return i;
        }
        await Task.CompletedTask;
    }

    [Fact]
    [Trait("Category", "Documentation")]
    public async Task BufferNode_Demonstration_FanIn_Scenario()
    {
        // Scenario: Multiple data sources (producers) feed into a single shared buffer,
        // which is then consumed by a single processor.
        // This is useful for consolidating data from multiple sources before processing.

        _output.WriteLine("=== Fan-In Scenario: Multiple Producers → Single Buffer → Single Consumer ===");

        var processedItems = new List<(int value, string source)>();
        
        var services = new ServiceCollection();
        services.AddScoped(_ => new SourceTrackingCollectorActor(processedItems, _output));
        var serviceProvider = services.BuildServiceProvider();
        var commonServices = new ServiceCollection().BuildServiceProvider();

        // Create three producers that generate different ranges of numbers
        var producer1 = BlockHelpers.CreateProducer<int>("producer-A", ctx => ProduceIntegers(ctx, 1, 3));
        var producer2 = BlockHelpers.CreateProducer<int>("producer-B", ctx => ProduceIntegers(ctx, 100, 3));
        var producer3 = BlockHelpers.CreateProducer<int>("producer-C", ctx => ProduceIntegers(ctx, 200, 3));

        // Create a processor that tracks which items it receives
        var processor = BlockHelpers.CreateActor<int, object, SourceTrackingCollectorActor>("processor", serviceProvider.GetRequiredService<IServiceScopeFactory>());

        // Build the graph with a shared buffer
        var builder = new DataFlowGraphBuilder("fan-in-demo");
        var sharedBuffer = builder.Buffer<int>(capacity: 10);

        builder.AddBlock(producer1)
            .AddBlock(producer2)
            .AddBlock(producer3)
            .AddBlock(processor)
            .Connect(producer1, sharedBuffer)
            .Connect(producer2, sharedBuffer)
            .Connect(producer3, sharedBuffer)
            .Connect(sharedBuffer, processor);

        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Execute
        await graph.ExecuteAsync(context);

        // Verify
        _output.WriteLine($"\nTotal items processed: {processedItems.Count}");
        processedItems.Count.ShouldBe(9); // 3 items from each producer
        
        var fromA = processedItems.Where(x => x.source == "A").Select(x => x.value).OrderBy(x => x).ToList();
        var fromB = processedItems.Where(x => x.source == "B").Select(x => x.value).OrderBy(x => x).ToList();
        var fromC = processedItems.Where(x => x.source == "C").Select(x => x.value).OrderBy(x => x).ToList();
        
        fromA.ShouldBe(new[] { 1, 2, 3 });
        fromB.ShouldBe(new[] { 100, 101, 102 });
        fromC.ShouldBe(new[] { 200, 201, 202 });
    }

    [Fact]
    [Trait("Category", "Documentation")]
    public async Task BufferNode_Demonstration_FanOut_Scenario()
    {
        // Scenario: Single producer feeds a buffer, which distributes work to multiple
        // competing consumers (workers) for parallel processing.
        // This is useful for load balancing and parallel processing.

        _output.WriteLine("=== Fan-Out Scenario: Single Producer → Buffer → Multiple Competing Consumers ===");

        var worker1Items = new List<int>();
        var worker2Items = new List<int>();
        var worker3Items = new List<int>();

        // Create separate service providers for each worker
        var services1 = new ServiceCollection();
        services1.AddScoped(_ => new DelayedIntCollectorActor(worker1Items, "Worker-1", _output));
        var serviceProvider1 = services1.BuildServiceProvider();

        var services2 = new ServiceCollection();
        services2.AddScoped(_ => new DelayedIntCollectorActor(worker2Items, "Worker-2", _output));
        var serviceProvider2 = services2.BuildServiceProvider();

        var services3 = new ServiceCollection();
        services3.AddScoped(_ => new DelayedIntCollectorActor(worker3Items, "Worker-3", _output));
        var serviceProvider3 = services3.BuildServiceProvider();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        // Create a single producer
        var producer = BlockHelpers.CreateProducer<int>("producer", ctx => ProduceIntegers(ctx, 1, 15));

        // Create three workers that compete for items
        var worker1 = BlockHelpers.CreateActor<int, object, DelayedIntCollectorActor>("worker-1", serviceProvider1.GetRequiredService<IServiceScopeFactory>());

        var worker2 = BlockHelpers.CreateActor<int, object, DelayedIntCollectorActor>("worker-2", serviceProvider2.GetRequiredService<IServiceScopeFactory>());

        var worker3 = BlockHelpers.CreateActor<int, object, DelayedIntCollectorActor>("worker-3", serviceProvider3.GetRequiredService<IServiceScopeFactory>());

        // Build the graph with a shared buffer
        var builder = new DataFlowGraphBuilder("fan-out-demo");
        var workQueue = builder.Buffer<int>(capacity: 5);

        builder.AddBlock(producer)
            .AddBlock(worker1)
            .AddBlock(worker2)
            .AddBlock(worker3)
            .Connect(producer, workQueue)
            .Connect(workQueue, worker1)
            .Connect(workQueue, worker2)
            .Connect(workQueue, worker3);

        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Execute
        await graph.ExecuteAsync(context);

        // Verify
        _output.WriteLine($"\nWork distribution:");
        _output.WriteLine($"  Worker-1: {worker1Items.Count} items");
        _output.WriteLine($"  Worker-2: {worker2Items.Count} items");
        _output.WriteLine($"  Worker-3: {worker3Items.Count} items");

        // Each worker should have processed some items
        worker1Items.Count.ShouldBeGreaterThan(0);
        worker2Items.Count.ShouldBeGreaterThan(0);
        worker3Items.Count.ShouldBeGreaterThan(0);

        // Total should be 15
        (worker1Items.Count + worker2Items.Count + worker3Items.Count).ShouldBe(15);

        // No duplicates - each item processed exactly once
        var allItems = worker1Items.Concat(worker2Items).Concat(worker3Items).OrderBy(x => x).ToList();
        allItems.ShouldBe(Enumerable.Range(1, 15));
    }

    [Fact]
    [Trait("Category", "Documentation")]
    public async Task BufferNode_Demonstration_Complex_Pipeline()
    {
        // Scenario: Multiple producers → Buffer → Multiple workers → Another buffer → Final consumer
        // This demonstrates chaining buffer nodes in a complex pipeline.

        _output.WriteLine("=== Complex Pipeline: Multi-stage with Buffer Nodes ===");

        var finalResults = new List<string>();
        
        var finalServices = new ServiceCollection();
        finalServices.AddScoped(_ => new StringCollectorActor(finalResults, _output));
        var finalServiceProvider = finalServices.BuildServiceProvider();

        // Create separate service providers for each transformer
        var transformer1Services = new ServiceCollection();
        transformer1Services.AddScoped(_ => new IntToStringTransformerActor("T1", _output));
        var transformer1ServiceProvider = transformer1Services.BuildServiceProvider();

        var transformer2Services = new ServiceCollection();
        transformer2Services.AddScoped(_ => new IntToStringTransformerActor("T2", _output));
        var transformer2ServiceProvider = transformer2Services.BuildServiceProvider();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        // Stage 1: Two producers generate numbers
        var producer1 = BlockHelpers.CreateProducer<int>("producer-1", ctx => ProduceIntegers(ctx, 1, 5));
        var producer2 = BlockHelpers.CreateProducer<int>("producer-2", ctx => ProduceIntegers(ctx, 100, 5));

        // Stage 2: Two workers transform numbers to strings
        var transformer1 = BlockHelpers.CreateActor<int, string, IntToStringTransformerActor>("transformer-1", transformer1ServiceProvider.GetRequiredService<IServiceScopeFactory>());
        var transformer2 = BlockHelpers.CreateActor<int, string, IntToStringTransformerActor>("transformer-2", transformer2ServiceProvider.GetRequiredService<IServiceScopeFactory>());

        // Stage 3: Final processor
        var finalProcessor = BlockHelpers.CreateActor<string, object, StringCollectorActor>("final-processor", finalServiceProvider.GetRequiredService<IServiceScopeFactory>());

        // Build the graph with two buffer nodes
        var builder = new DataFlowGraphBuilder("complex-pipeline-demo");
        var inputBuffer = builder.Buffer<int>(capacity: 10);
        var outputBuffer = builder.Buffer<string>(capacity: 10);

        builder.AddBlock(producer1)
            .AddBlock(producer2)
            .AddBlock(transformer1)
            .AddBlock(transformer2)
            .AddBlock(finalProcessor)
            // Stage 1: Producers → Input Buffer
            .Connect(producer1, inputBuffer)
            .Connect(producer2, inputBuffer)
            // Stage 2: Input Buffer → Transformers → Output Buffer
            .Connect(inputBuffer, transformer1)
            .Connect(inputBuffer, transformer2)
            .Connect(transformer1, outputBuffer)
            .Connect(transformer2, outputBuffer)
            // Stage 3: Output Buffer → Final Processor
            .Connect(outputBuffer, finalProcessor);

        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Execute
        _output.WriteLine("\nExecuting pipeline...\n");
        await graph.ExecuteAsync(context);

        // Verify
        _output.WriteLine($"\nTotal results: {finalResults.Count}");
        finalResults.Count.ShouldBe(10); // 5 from each producer
    }
}
