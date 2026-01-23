namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using DataFlow.POC.Registry;

public class BasicFlowTests
{
    // Refactored to use BlockHelpers for consistent block instantiation patterns.

    [Fact]
    public async Task Producer_To_Processor_Flow_Should_Process_All_Items()
    {
        // Arrange
        var processedItems = new List<int>();

        var producer = BlockHelpers.CreateProducer("producer", TestStreams.Integers(10));
        var processor = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "processor",
            new CollectorActor<int>(processedItems));

        var builder = GraphHelpers.CreateGraphBuilder("basic-flow");
        builder.AddBlock(producer)
            .AddBlock(processor)
            .Connect(producer, processor);

        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        processedItems.Count.ShouldBe(10);
        processedItems.ShouldBe(Enumerable.Range(1, 10));
    }

    [Fact]
    public async Task Producer_Transform_Processor_Flow_Should_Transform_Items()
    {
        // Arrange
        var processedItems = new List<string>();
        
        var scopeFactory = TestServiceBuilder.Create()
            .WithScoped(new TransformActor<int, string>(i => $"Item-{i}"))
            .WithScoped(new CollectorActor<string>(processedItems))
            .BuildScopeFactory();

        var producer = BlockHelpers.CreateProducer("producer", TestStreams.Integers(5));
        var transformer = BlockHelpers.CreateActor<int, string, TransformActor<int, string>>(
            "transformer",
            scopeFactory);
        var processor = BlockHelpers.CreateActor<string, object, CollectorActor<string>>(
            "processor",
            scopeFactory);

        var builder = GraphHelpers.CreateGraphBuilder("transform-flow");
        builder.AddBlock(producer)
            .AddBlock(transformer)
            .AutoConnect()  // Connects transformer to producer
            .AddBlock(processor)
            .AutoConnect(); // Connects processor to transformer

        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        processedItems.Count.ShouldBe(5);
        processedItems.ShouldBe(new[] { "Item-1", "Item-2", "Item-3", "Item-4", "Item-5" });
    }

    [Fact]
    public async Task Unbuffered_Edge_Should_Work_For_Simple_Flows()
    {
        // Note: In this POC, "unbuffered" edges still use a small buffer for coordination
        // A production implementation could optimize this further
        
        // Arrange
        var processedItems = new List<int>();

        var producer = BlockHelpers.CreateProducer("producer", TestStreams.Integers(5));
        var processor = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "processor",
            new CollectorActor<int>(processedItems));

        var builder = GraphHelpers.CreateGraphBuilder("unbuffered-flow");
        builder.AddBlock(producer)
            .AddBlock(processor)
            .Connect(producer, processor, bufferCapacity: 1); // Small buffer

        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        processedItems.Count.ShouldBe(5);
        processedItems.ShouldBe(Enumerable.Range(1, 5));
    }
}
