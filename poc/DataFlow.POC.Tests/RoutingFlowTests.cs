namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public class RoutingFlowTests
{
    /// <summary>
    /// Simple collector actor for integers.
    /// </summary>
    private class IntCollectorActor : IStreamActor<int, object>
    {
        private readonly List<int> _collected;

        public IntCollectorActor(List<int> collected)
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

    private static async IAsyncEnumerable<int> ProduceIntegers(IExecutionContext ctx, int count)
    {
        for (int i = 1; i <= count; i++)
        {
            yield return i;
        }
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Router_Should_Route_Items_To_Correct_Targets()
    {
        // Arrange
        var evenItems = new List<int>();
        var oddItems = new List<int>();
        
        // Create separate service providers for isolated collectors
        var evenServices = new ServiceCollection();
        evenServices.AddScoped(_ => new IntCollectorActor(evenItems));
        var evenServiceProvider = evenServices.BuildServiceProvider();

        var oddServices = new ServiceCollection();
        oddServices.AddScoped(_ => new IntCollectorActor(oddItems));
        var oddServiceProvider = oddServices.BuildServiceProvider();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(ctx, 10));

        var router = new RouterBlock<int>("router", i => i % 2 == 0 ? "even" : "odd");

        var evenFilter = new RouteFilterBlock<int>("even-filter", "even");
        var oddFilter = new RouteFilterBlock<int>("odd-filter", "odd");

        var evenProcessor = new ActorBlock<int, object, IntCollectorActor>(
            "even-processor",
            evenServiceProvider.GetRequiredService<IServiceScopeFactory>());

        var oddProcessor = new ActorBlock<int, object, IntCollectorActor>(
            "odd-processor",
            oddServiceProvider.GetRequiredService<IServiceScopeFactory>());

        var builder = new DataFlowGraphBuilder("routing-flow");
        builder.AddBlock(producer)
            .AddBlock(router)
            .AutoConnect()
            .AddBlock(evenFilter)
            .AddBlock(oddFilter)
            .AddBlock(evenProcessor)
            .AddBlock(oddProcessor)
            .ConnectMany(router, evenFilter, oddFilter)
            .Connect(evenFilter, evenProcessor)
            .Connect(oddFilter, oddProcessor);

        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        evenItems.Count.ShouldBe(5);
        oddItems.Count.ShouldBe(5);
        evenItems.ShouldBe(new[] { 2, 4, 6, 8, 10 });
        oddItems.ShouldBe(new[] { 1, 3, 5, 7, 9 });
    }

    [Fact]
    public async Task Router_With_Three_Routes_Should_Distribute_Correctly()
    {
        // Arrange
        var lowItems = new List<int>();
        var mediumItems = new List<int>();
        var highItems = new List<int>();
        
        // Create separate service providers for isolated collectors
        var lowServices = new ServiceCollection();
        lowServices.AddScoped(_ => new IntCollectorActor(lowItems));
        var lowServiceProvider = lowServices.BuildServiceProvider();

        var mediumServices = new ServiceCollection();
        mediumServices.AddScoped(_ => new IntCollectorActor(mediumItems));
        var mediumServiceProvider = mediumServices.BuildServiceProvider();

        var highServices = new ServiceCollection();
        highServices.AddScoped(_ => new IntCollectorActor(highItems));
        var highServiceProvider = highServices.BuildServiceProvider();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(ctx, 15));

        var router = new RouterBlock<int>("router", i =>
            i <= 5 ? "low" :
            i <= 10 ? "medium" :
            "high");

        var lowFilter = new RouteFilterBlock<int>("low-filter", "low");
        var mediumFilter = new RouteFilterBlock<int>("medium-filter", "medium");
        var highFilter = new RouteFilterBlock<int>("high-filter", "high");

        var lowProcessor = new ActorBlock<int, object, IntCollectorActor>(
            "low-processor",
            lowServiceProvider.GetRequiredService<IServiceScopeFactory>());

        var mediumProcessor = new ActorBlock<int, object, IntCollectorActor>(
            "medium-processor",
            mediumServiceProvider.GetRequiredService<IServiceScopeFactory>());

        var highProcessor = new ActorBlock<int, object, IntCollectorActor>(
            "high-processor",
            highServiceProvider.GetRequiredService<IServiceScopeFactory>());

        var builder = new DataFlowGraphBuilder("three-route-flow");
        builder.AddBlock(producer)
            .AddBlock(router)
            .AutoConnect()
            .AddBlock(lowFilter)
            .AddBlock(mediumFilter)
            .AddBlock(highFilter)
            .AddBlock(lowProcessor)
            .AddBlock(mediumProcessor)
            .AddBlock(highProcessor)
            .ConnectMany(router, lowFilter, mediumFilter, highFilter)
            .Connect(lowFilter, lowProcessor)
            .Connect(mediumFilter, mediumProcessor)
            .Connect(highFilter, highProcessor);

        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        lowItems.Count.ShouldBe(5);
        mediumItems.Count.ShouldBe(5);
        highItems.Count.ShouldBe(5);
        lowItems.ShouldBe(new[] { 1, 2, 3, 4, 5 });
        mediumItems.ShouldBe(new[] { 6, 7, 8, 9, 10 });
        highItems.ShouldBe(new[] { 11, 12, 13, 14, 15 });
    }
}
