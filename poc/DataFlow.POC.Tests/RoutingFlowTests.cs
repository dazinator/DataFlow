namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public class RoutingFlowTests
{
    // Refactored to use test helpers - removed duplicate implementations
    // - IntCollectorActor → using TestHelpers.CollectorActor<int>
    // - ProduceIntegers → using TestStreams.Integers()

    [Fact]
    public async Task Router_Should_Route_Items_To_Correct_Targets()
    {
        // Arrange
        var evenItems = new List<int>();
        var oddItems = new List<int>();
        
        // Using TestServiceBuilder to create isolated scope factories
        var evenScopeFactory = TestServiceBuilder.Create()
            .WithScoped(new CollectorActor<int>(evenItems))
            .BuildScopeFactory();

        var oddScopeFactory = TestServiceBuilder.Create()
            .WithScoped(new CollectorActor<int>(oddItems))
            .BuildScopeFactory();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        // Using TestStreams.Integers() instead of custom ProduceIntegers function
        var producer = new ProducerBlock<int>("producer", _ => TestStreams.Integers(10));

        var router = new RouterBlock<int>("router", i => i % 2 == 0 ? "even" : "odd");

        var evenFilter = new RouteFilterBlock<int>("even-filter", "even");
        var oddFilter = new RouteFilterBlock<int>("odd-filter", "odd");

        var evenProcessor = new ActorBlock<int, object, CollectorActor<int>>(
            "even-processor",
            evenScopeFactory);

        var oddProcessor = new ActorBlock<int, object, CollectorActor<int>>(
            "odd-processor",
            oddScopeFactory);

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
        
        // Using TestServiceBuilder to create isolated scope factories
        var lowScopeFactory = TestServiceBuilder.Create()
            .WithScoped(new CollectorActor<int>(lowItems))
            .BuildScopeFactory();

        var mediumScopeFactory = TestServiceBuilder.Create()
            .WithScoped(new CollectorActor<int>(mediumItems))
            .BuildScopeFactory();

        var highScopeFactory = TestServiceBuilder.Create()
            .WithScoped(new CollectorActor<int>(highItems))
            .BuildScopeFactory();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        // Using TestStreams.Integers() instead of custom ProduceIntegers function
        var producer = new ProducerBlock<int>("producer", _ => TestStreams.Integers(15));

        var router = new RouterBlock<int>("router", i =>
            i <= 5 ? "low" :
            i <= 10 ? "medium" :
            "high");

        var lowFilter = new RouteFilterBlock<int>("low-filter", "low");
        var mediumFilter = new RouteFilterBlock<int>("medium-filter", "medium");
        var highFilter = new RouteFilterBlock<int>("high-filter", "high");

        var lowProcessor = new ActorBlock<int, object, CollectorActor<int>>(
            "low-processor",
            lowScopeFactory);

        var mediumProcessor = new ActorBlock<int, object, CollectorActor<int>>(
            "medium-processor",
            mediumScopeFactory);

        var highProcessor = new ActorBlock<int, object, CollectorActor<int>>(
            "high-processor",
            highScopeFactory);

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
