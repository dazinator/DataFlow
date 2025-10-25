namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public class BasicFlowTests
{
    private static async IAsyncEnumerable<int> ProduceIntegers(IExecutionContext ctx, int count)
    {
        for (int i = 1; i <= count; i++)
        {
            yield return i;
        }
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Producer_To_Processor_Flow_Should_Process_All_Items()
    {
        // Arrange
        var processedItems = new List<int>();
        var services = new ServiceCollection().BuildServiceProvider();

        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(ctx, 10));

        var processor = new ProcessorBlock<int>("processor", async (item, ctx) =>
        {
            processedItems.Add(item);
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("basic-flow");
        builder.AddBlock(producer)
            .AddBlock(processor)
            .Connect(producer, processor);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

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
        var services = new ServiceCollection().BuildServiceProvider();

        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(ctx, 5));

        var transformer = new SimpleTransformerBlock<int, string>("transformer", i => $"Item-{i}");

        var processor = new ProcessorBlock<string>("processor", async (item, ctx) =>
        {
            processedItems.Add(item);
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("transform-flow");
        builder.AddBlock(producer)
            .AddBlock(transformer)
            .AutoConnect()  // Connects transformer to producer
            .AddBlock(processor)
            .AutoConnect(); // Connects processor to transformer

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

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
        var services = new ServiceCollection().BuildServiceProvider();

        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(ctx, 5));

        var processor = new ProcessorBlock<int>("processor", async (item, ctx) =>
        {
            processedItems.Add(item);
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("unbuffered-flow");
        builder.AddBlock(producer)
            .AddBlock(processor)
            .Connect(producer, processor, bufferCapacity: 1); // Small buffer

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        processedItems.Count.ShouldBe(5);
        processedItems.ShouldBe(Enumerable.Range(1, 5));
    }
}
