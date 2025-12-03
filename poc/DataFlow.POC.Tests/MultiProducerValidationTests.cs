namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Shouldly;
using Xunit;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

/// <summary>
/// Tests documenting that multiple producers connecting to a single consumer
/// IS a supported pattern (merge/join topology). Items from all producers
/// are merged through the target block's input channel.
/// </summary>
public class MultiProducerMergePatternTests
{
    /// <summary>
    /// Thread-safe collector actor for merge tests.
    /// </summary>
    private class ThreadSafeIntCollectorActor : IStreamActor<int, object>
    {
        private readonly ConcurrentBag<int> _collected;

        public ThreadSafeIntCollectorActor(ConcurrentBag<int> collected)
        {
            _collected = collected;
        }

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                _collected.Add(item);
            }
            yield break;
        }
    }

    [Fact]
    public async Task Connect_MultipleProducersToSingleConsumer_MergesResults()
    {
        // Arrange
        var processedItems = new ConcurrentBag<int>();
        var processorServices = new ServiceCollection();
        processorServices.AddScoped(_ => new ThreadSafeIntCollectorActor(processedItems));
        var processorSP = processorServices.BuildServiceProvider();
        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer1 = BlockHelpers.CreateProducer<int>("producer1", ctx => ProduceIntegers(ctx, 1, 5));
        var producer2 = BlockHelpers.CreateProducer<int>("producer2", ctx => ProduceIntegers(ctx, 100, 5));
        var processor = BlockHelpers.CreateActor<int, object, ThreadSafeIntCollectorActor>("processor", processorSP.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("multi-producer-merge-test");
        builder.AddBlock(producer1)
            .AddBlock(producer2)
            .AddBlock(processor)
            .Connect(producer1, processor)
            .Connect(producer2, processor);

        // Act
        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None);
        await graph.ExecuteAsync(context);

        // Assert - Both producers' items should be collected
        processedItems.Count.ShouldBe(10);
        var itemsFromProducer1 = processedItems.Where(x => x < 100).OrderBy(x => x).ToList();
        var itemsFromProducer2 = processedItems.Where(x => x >= 100).OrderBy(x => x).ToList();
        
        itemsFromProducer1.ShouldBe(Enumerable.Range(1, 5));
        itemsFromProducer2.ShouldBe(Enumerable.Range(100, 5));
    }

    [Fact]
    public async Task Connect_ByName_MultipleProducersToSingleConsumer_MergesResults()
    {
        // Arrange
        var processedItems = new ConcurrentBag<int>();
        var processorServices = new ServiceCollection();
        processorServices.AddScoped(_ => new ThreadSafeIntCollectorActor(processedItems));
        var processorSP = processorServices.BuildServiceProvider();
        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer1 = BlockHelpers.CreateProducer<int>("producer1", ctx => ProduceIntegers(ctx, 1, 5));
        var producer2 = BlockHelpers.CreateProducer<int>("producer2", ctx => ProduceIntegers(ctx, 100, 5));
        var processor = BlockHelpers.CreateActor<int, object, ThreadSafeIntCollectorActor>("processor", processorSP.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("multi-producer-merge-test");
        builder.AddBlock(producer1)
            .AddBlock(producer2)
            .AddBlock(processor)
            .Connect("producer1", "processor")
            .Connect("producer2", "processor");

        // Act
        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None);
        await graph.ExecuteAsync(context);

        // Assert - Both producers' items should be collected
        processedItems.Count.ShouldBe(10);
        var itemsFromProducer1 = processedItems.Where(x => x < 100).OrderBy(x => x).ToList();
        var itemsFromProducer2 = processedItems.Where(x => x >= 100).OrderBy(x => x).ToList();
        
        itemsFromProducer1.ShouldBe(Enumerable.Range(1, 5));
        itemsFromProducer2.ShouldBe(Enumerable.Range(100, 5));
    }

    [Fact]
    public async Task AddEdge_MultipleProducersToSingleConsumer_MergesResults()
    {
        // Arrange
        var processedItems = new ConcurrentBag<int>();
        var processorServices = new ServiceCollection();
        processorServices.AddScoped(_ => new ThreadSafeIntCollectorActor(processedItems));
        var processorSP = processorServices.BuildServiceProvider();
        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer1 = BlockHelpers.CreateProducer<int>("producer1", ctx => ProduceIntegers(ctx, 1, 5));
        var producer2 = BlockHelpers.CreateProducer<int>("producer2", ctx => ProduceIntegers(ctx, 100, 5));
        var processor = BlockHelpers.CreateActor<int, object, ThreadSafeIntCollectorActor>("processor", processorSP.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("multi-producer-merge-test");
        builder.AddBlock(producer1)
            .AddBlock(producer2)
            .AddBlock(processor);

        var edge1 = new Edge(producer1, processor);
        builder.AddEdge(edge1);

        var edge2 = new Edge(producer2, processor);
        builder.AddEdge(edge2);

        // Act
        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None);
        await graph.ExecuteAsync(context);

        // Assert - Both producers' items should be collected
        processedItems.Count.ShouldBe(10);
        var itemsFromProducer1 = processedItems.Where(x => x < 100).OrderBy(x => x).ToList();
        var itemsFromProducer2 = processedItems.Where(x => x >= 100).OrderBy(x => x).ToList();
        
        itemsFromProducer1.ShouldBe(Enumerable.Range(1, 5));
        itemsFromProducer2.ShouldBe(Enumerable.Range(100, 5));
    }



    [Fact]
    public async Task Connect_SingleProducerToMultipleConsumers_Allowed()
    {
        // Arrange - this is the broadcast pattern (one-to-many)
        var processor1Items = new ConcurrentBag<int>();
        var processor2Items = new ConcurrentBag<int>();
        
        var processor1Services = new ServiceCollection();
        processor1Services.AddScoped(_ => new ThreadSafeIntCollectorActor(processor1Items));
        var processor1SP = processor1Services.BuildServiceProvider();

        var processor2Services = new ServiceCollection();
        processor2Services.AddScoped(_ => new ThreadSafeIntCollectorActor(processor2Items));
        var processor2SP = processor2Services.BuildServiceProvider();
        
        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = BlockHelpers.CreateProducer<int>("producer", ctx => ProduceIntegers(ctx, 1, 10));
        var processor1 = BlockHelpers.CreateActor<int, object, ThreadSafeIntCollectorActor>("processor1", processor1SP.GetRequiredService<IServiceScopeFactory>());
        var processor2 = BlockHelpers.CreateActor<int, object, ThreadSafeIntCollectorActor>("processor2", processor2SP.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("broadcast-test");
        
        // Act - One producer to many consumers is allowed (broadcast)
        builder.AddBlock(producer)
            .AddBlock(processor1)
            .AddBlock(processor2)
            .Connect(producer, processor1)
            .Connect(producer, processor2);

        // Assert
        var graph = Should.NotThrow(() => builder.Build());
        graph.ShouldNotBeNull();
        
        var context = new ExecutionContext(commonServices, CancellationToken.None);
        await graph.ExecuteAsync(context);
        
        // Both processors should receive all items (broadcast)
        processor1Items.OrderBy(x => x).ShouldBe(Enumerable.Range(1, 10));
        processor2Items.OrderBy(x => x).ShouldBe(Enumerable.Range(1, 10));
    }

    private static async IAsyncEnumerable<int> ProduceIntegers(IExecutionContext context, int start, int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return start + i;
        }
    }
}
