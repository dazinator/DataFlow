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
    // Refactored to use test helpers - removed duplicate implementations
    // - IntCollectorActor → using TestHelpers.CollectorActor<int>
    // - ProduceIntegers → using TestStreams.Integers()

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
        var producer = new ProducerBlock<int>("producer", _ => TestStreams.Integers(5));

        var broadcast = new BroadcastBlock<int>("broadcast");

        var processor1 = new ActorBlock<int, object, CollectorActor<int>>(
            "processor1",
            scopeFactory1);

        var processor2 = new ActorBlock<int, object, CollectorActor<int>>(
            "processor2",
            scopeFactory2);

        var builder = new DataFlowGraphBuilder("broadcast-flow");
        builder.AddBlock(producer)
            .AddBlock(broadcast)
            .AddBlock(processor1)
            .AddBlock(processor2)
            .Connect(producer, broadcast)
            .Connect(broadcast, processor1) // Broadcast to processor1
            .Connect(broadcast, processor2); // Broadcast to processor2

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
