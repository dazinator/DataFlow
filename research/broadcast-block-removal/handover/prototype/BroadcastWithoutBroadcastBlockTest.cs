namespace DataFlow.POC.Tests.Research;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

/// <summary>
/// Prototype test demonstrating that broadcast functionality works WITHOUT BroadcastBlock.
/// 
/// This validates the hypothesis that any block can broadcast to multiple downstream
/// blocks via edge connections, making BroadcastBlock redundant in POC.
/// </summary>
public class BroadcastWithoutBroadcastBlockTest
{
    [Fact]
    public async Task Producer_Can_Broadcast_Directly_To_Multiple_Processors()
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

        // Create a producer that will broadcast to multiple consumers
        var producer = BlockHelpers.CreateProducer("producer", TestStreams.Integers(5));

        var processor1 = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "processor1",
            scopeFactory1);

        var processor2 = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "processor2",
            scopeFactory2);

        var builder = GraphHelpers.CreateGraphBuilder("direct-broadcast-flow");
        
        // KEY: Connect producer DIRECTLY to both processors
        // No BroadcastBlock needed - edge layer handles the broadcasting
        builder.AddBlock(producer)
            .AddBlock(processor1)
            .AddBlock(processor2)
            .Connect(producer, processor1)  // Producer broadcasts to processor1
            .Connect(producer, processor2); // Producer broadcasts to processor2

        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert - Both processors should receive ALL items (broadcast semantics)
        processor1Items.Count.ShouldBe(5, "Processor1 should receive all items");
        processor2Items.Count.ShouldBe(5, "Processor2 should receive all items");
        processor1Items.ShouldBe(Enumerable.Range(1, 5), "Processor1 should have items 1-5");
        processor2Items.ShouldBe(Enumerable.Range(1, 5), "Processor2 should have items 1-5");
    }

    [Fact]
    public async Task EpochActorBlock_Can_Broadcast_To_Multiple_Processors()
    {
        // Arrange
        var processor1Items = new List<int>();
        var processor2Items = new List<int>();
        
        var scopeFactory1 = TestServiceBuilder.Create()
            .WithScoped(new CollectorActor<int>(processor1Items))
            .BuildScopeFactory();

        var scopeFactory2 = TestServiceBuilder.Create()
            .WithScoped(new CollectorActor<int>(processor2Items))
            .BuildScopeFactory();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        // Create a producer and a transformer that will broadcast
        var producer = BlockHelpers.CreateProducer("producer", TestStreams.Integers(3));

        // Create a transformer actor that doubles values
        var transformScopeFactory = TestServiceBuilder.Create()
            .WithScoped(new DoublerActor())
            .BuildScopeFactory();
        
        var transformer = BlockHelpers.CreateActor<int, int, DoublerActor>(
            "transformer",
            transformScopeFactory);

        var processor1 = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "processor1",
            scopeFactory1);

        var processor2 = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "processor2",
            scopeFactory2);

        var builder = GraphHelpers.CreateGraphBuilder("transformer-broadcast-flow");
        
        // Connect transformer output to BOTH processors
        builder.AddBlock(producer)
            .AddBlock(transformer)
            .AddBlock(processor1)
            .AddBlock(processor2)
            .Connect(producer, transformer)
            .Connect(transformer, processor1)  // Transformer broadcasts to processor1
            .Connect(transformer, processor2); // Transformer broadcasts to processor2

        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert - Both processors should receive all transformed items
        processor1Items.Count.ShouldBe(3, "Processor1 should receive all items");
        processor2Items.Count.ShouldBe(3, "Processor2 should receive all items");
        
        var expectedDoubled = new[] { 2, 4, 6 }; // 1*2, 2*2, 3*2
        processor1Items.ShouldBe(expectedDoubled, "Processor1 should have doubled values");
        processor2Items.ShouldBe(expectedDoubled, "Processor2 should have doubled values");
    }

    /// <summary>
    /// Simple actor that doubles input values
    /// </summary>
    private class DoublerActor : IStreamActor<int, int>
    {
        public async IAsyncEnumerable<int> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                yield return item * 2;
            }
        }
    }
}
