namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public class ComplexFlowTests
{
    /// <summary>
    /// Actor that transforms integers to formatted strings.
    /// </summary>
    private class IntToFormattedStringActor : IStreamActor<int, string>
    {
        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                yield return $"Item-{item:D2}";
            }
        }
    }

    /// <summary>
    /// Actor that transforms integers to prefixed strings.
    /// </summary>
    private class PrefixTransformActor : IStreamActor<int, string>
    {
        private readonly string _prefix;

        public PrefixTransformActor(string prefix)
        {
            _prefix = prefix;
        }

        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                yield return $"{_prefix}-{item}";
            }
        }
    }

    /// <summary>
    /// Collector actor for string arrays.
    /// </summary>
    private class StringArrayCollectorActor : IStreamActor<string[], object>
    {
        private readonly List<string[]> _collected;

        public StringArrayCollectorActor(List<string[]> collected)
        {
            _collected = collected;
        }

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<string[]> input,
            IActorExecutionContext context)
        {
            await foreach (var batch in input.WithCancellation(context.CancellationToken))
            {
                _collected.Add(batch);
            }
            yield break;
        }
    }

    /// <summary>
    /// Collector actor for strings.
    /// </summary>
    private class StringCollectorActor : IStreamActor<string, object>
    {
        private readonly List<string> _collected;

        public StringCollectorActor(List<string> collected)
        {
            _collected = collected;
        }

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<string> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                _collected.Add(item);
            }
            yield break;
        }
    }

    private static async IAsyncEnumerable<int> ProduceIntegers(IExecutionContext ctx, int count)
    {
        for (int i = 1; i <= count; i++)
        {
            yield return i;
        }
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Complex_Flow_With_Transform_Batch_And_Routing_Should_Work()
    {
        // Arrange
        var smallBatches = new List<string[]>();
        var largeBatches = new List<string[]>();
        
        // Create separate service providers for isolated collectors
        var smallServices = new ServiceCollection();
        smallServices.AddScoped(_ => new StringArrayCollectorActor(smallBatches));
        var smallServiceProvider = smallServices.BuildServiceProvider();

        var largeServices = new ServiceCollection();
        largeServices.AddScoped(_ => new StringArrayCollectorActor(largeBatches));
        var largeServiceProvider = largeServices.BuildServiceProvider();

        var transformServices = new ServiceCollection();
        transformServices.AddScoped<IntToFormattedStringActor>();
        var transformServiceProvider = transformServices.BuildServiceProvider();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        // Producer generates numbers 1-20
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(ctx, 20));

        // Transform to strings
        var transformer = new ActorBlock<int, string, IntToFormattedStringActor>(
            "transformer",
            transformServiceProvider.GetRequiredService<IServiceScopeFactory>());

        // Batch into groups of 5
        var batcher = new BatchBlock<string>("batcher", maxBatchSize: 5, windowPeriod: null);

        // Route batches by size
        var router = new RouterBlock<string[]>("router", batch => batch.Length < 5 ? "small" : "large");

        var smallFilter = new RouteFilterBlock<string[]>("small-filter", "small");
        var largeFilter = new RouteFilterBlock<string[]>("large-filter", "large");

        var smallProcessor = new ActorBlock<string[], object, StringArrayCollectorActor>(
            "small-processor",
            smallServiceProvider.GetRequiredService<IServiceScopeFactory>());

        var largeProcessor = new ActorBlock<string[], object, StringArrayCollectorActor>(
            "large-processor",
            largeServiceProvider.GetRequiredService<IServiceScopeFactory>());

        var builder = new DataFlowGraphBuilder("complex-flow");
        builder.AddBlock(producer)
            .AddBlock(transformer)
            .AddBlock(batcher)
            .AddBlock(router)
            .AddBlock(smallFilter)
            .AddBlock(largeFilter)
            .AddBlock(smallProcessor)
            .AddBlock(largeProcessor)
            .Connect(producer, transformer)
            .Connect(transformer, batcher)
            .Connect(batcher, router)
            .Connect(router, smallFilter)
            .Connect(router, largeFilter)
            .Connect(smallFilter, smallProcessor)
            .Connect(largeFilter, largeProcessor);

        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        largeBatches.Count.ShouldBe(4); // 4 full batches
        smallBatches.Count.ShouldBe(0); // No partial batches (20 items / 5 = 4 even)

        largeBatches[0].ShouldBe(new[] { "Item-01", "Item-02", "Item-03", "Item-04", "Item-05" });
        largeBatches[1].ShouldBe(new[] { "Item-06", "Item-07", "Item-08", "Item-09", "Item-10" });
        largeBatches[2].ShouldBe(new[] { "Item-11", "Item-12", "Item-13", "Item-14", "Item-15" });
        largeBatches[3].ShouldBe(new[] { "Item-16", "Item-17", "Item-18", "Item-19", "Item-20" });
    }

    [Fact]
    public async Task Diamond_Topology_Should_Merge_Results()
    {
        // Arrange
        var finalResults = new List<string>();
        
        // Create separate service providers for each transform path
        var transform1Services = new ServiceCollection();
        transform1Services.AddScoped(_ => new PrefixTransformActor("T1"));
        var transform1ServiceProvider = transform1Services.BuildServiceProvider();

        var transform2Services = new ServiceCollection();
        transform2Services.AddScoped(_ => new PrefixTransformActor("T2"));
        var transform2ServiceProvider = transform2Services.BuildServiceProvider();

        var processorServices = new ServiceCollection();
        processorServices.AddScoped(_ => new StringCollectorActor(finalResults));
        var processorServiceProvider = processorServices.BuildServiceProvider();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        // Single producer
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(ctx, 6));

        // Split into two transformers
        var transformer1 = new ActorBlock<int, string, PrefixTransformActor>(
            "transform1",
            transform1ServiceProvider.GetRequiredService<IServiceScopeFactory>());
        
        var transformer2 = new ActorBlock<int, string, PrefixTransformActor>(
            "transform2",
            transform2ServiceProvider.GetRequiredService<IServiceScopeFactory>());

        // Merge back to single processor
        var processor = new ActorBlock<string, object, StringCollectorActor>(
            "processor",
            processorServiceProvider.GetRequiredService<IServiceScopeFactory>());

        var builder = new DataFlowGraphBuilder("diamond-flow");
        builder.AddBlock(producer)
            .AddBlock(transformer1)
            .AddBlock(transformer2)
            .AddBlock(processor)
            .ConnectMany(producer, transformer1, transformer2) // Split
            .ConnectMany(transformer1, processor) // Merge
            .Connect(transformer2, processor); // Merge

        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        finalResults.Count.ShouldBe(12); // 6 items * 2 paths = 12
        finalResults.Where(r => r.StartsWith("T1-")).Count().ShouldBe(6);
        finalResults.Where(r => r.StartsWith("T2-")).Count().ShouldBe(6);
    }
}
