namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public class BroadcastFlowTests
{
    // Refactored to use BlockHelpers for consistent block instantiation patterns.
    // Also using TestHelpers.CollectorActor<int> and TestStreams.Integers()

    [Fact]
    public async Task Broadcast_Should_Send_Items_To_Multiple_Processors()
    {
        // Arrange
        var processor1Items = new List<int>();
        var processor2Items = new List<int>();
        
        // Using TestServiceBuilder to create separate scope factories for isolated collectors
        var scopeFactory1 = TestServiceBuilder.Create()
            .WithScoped(new CollectorActor<int>(processor1Items))
            .BuildScopeFactory();

        var scopeFactory2 = TestServiceBuilder.Create()
            .WithScoped(new CollectorActor<int>(processor2Items))
            .BuildScopeFactory();

        // Use a common service provider for the execution context
        var commonServices = new ServiceCollection().BuildServiceProvider();

        // Using TestStreams.Integers() instead of custom ProduceIntegers function
        var producer = BlockHelpers.CreateProducer("producer", TestStreams.Integers(5));

        var processor1 = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "processor1",
            scopeFactory1);

        var processor2 = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "processor2",
            scopeFactory2);

        var builder = GraphHelpers.CreateGraphBuilder("broadcast-flow");
        builder.AddBlock(producer)
            .AddBlock(processor1)
            .AddBlock(processor2)
            .Connect(producer, processor1) // Producer broadcasts to processor1 (edge layer handles broadcasting)
            .Connect(producer, processor2); // Producer broadcasts to processor2

        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        processor1Items.Count.ShouldBe(5);
        processor2Items.Count.ShouldBe(5);
        processor1Items.ShouldBe(Enumerable.Range(1, 5));
        processor2Items.ShouldBe(Enumerable.Range(1, 5));
    }
}
