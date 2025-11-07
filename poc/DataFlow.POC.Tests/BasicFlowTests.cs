namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public class BasicFlowTests
{
    // Refactored to use test helpers - removed duplicate actor implementations
    // - CollectorActor<T> → using TestHelpers.CollectorActor<T>
    // - IntToStringActor → using TestHelpers.TransformActor<int, string>
    // - ProduceIntegers → using TestStreams.Integers()

    [Fact]
    public async Task Producer_To_Processor_Flow_Should_Process_All_Items()
    {
        // Arrange
        var processedItems = new List<int>();
        
        // Using TestServiceBuilder instead of manual ServiceCollection setup
        var scopeFactory = TestServiceBuilder.Create()
            .WithScoped(new CollectorActor<int>(processedItems))
            .BuildScopeFactory();

        // Using TestStreams.Integers() instead of custom ProduceIntegers function
        var producer = new ProducerBlock<int>("producer", _ => TestStreams.Integers(10));

        var processor = new ActorBlock<int, object, CollectorActor<int>>(
            "processor",
            scopeFactory);

        var builder = new DataFlowGraphBuilder("basic-flow");
        builder.AddBlock(producer)
            .AddBlock(processor)
            .Connect(producer, processor);

        var graph = builder.Build();
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
        
        // Using TestServiceBuilder with TransformActor instead of custom IntToStringActor
        var scopeFactory = TestServiceBuilder.Create()
            .WithScoped(new TransformActor<int, string>(i => $"Item-{i}"))
            .WithScoped(new CollectorActor<string>(processedItems))
            .BuildScopeFactory();

        // Using TestStreams.Integers() instead of custom ProduceIntegers function
        var producer = new ProducerBlock<int>("producer", _ => TestStreams.Integers(5));

        var transformer = new ActorBlock<int, string, TransformActor<int, string>>(
            "transformer",
            scopeFactory);

        var processor = new ActorBlock<string, object, CollectorActor<string>>(
            "processor",
            scopeFactory);

        var builder = new DataFlowGraphBuilder("transform-flow");
        builder.AddBlock(producer)
            .AddBlock(transformer)
            .AutoConnect()  // Connects transformer to producer
            .AddBlock(processor)
            .AutoConnect(); // Connects processor to transformer

        var graph = builder.Build();
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
        
        // Using TestServiceBuilder instead of manual ServiceCollection setup
        var scopeFactory = TestServiceBuilder.Create()
            .WithScoped(new CollectorActor<int>(processedItems))
            .BuildScopeFactory();

        // Using TestStreams.Integers() instead of custom ProduceIntegers function
        var producer = new ProducerBlock<int>("producer", _ => TestStreams.Integers(5));

        var processor = new ActorBlock<int, object, CollectorActor<int>>(
            "processor",
            scopeFactory);

        var builder = new DataFlowGraphBuilder("unbuffered-flow");
        builder.AddBlock(producer)
            .AddBlock(processor)
            .Connect(producer, processor, bufferCapacity: 1); // Small buffer

        var graph = builder.Build();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        processedItems.Count.ShouldBe(5);
        processedItems.ShouldBe(Enumerable.Range(1, 5));
    }
}
