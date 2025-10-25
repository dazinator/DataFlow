namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public class BroadcastFlowTests
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
    public async Task Broadcast_Should_Send_Items_To_Multiple_Processors()
    {
        // Arrange
        var processor1Items = new List<int>();
        var processor2Items = new List<int>();
        var services = new ServiceCollection().BuildServiceProvider();

        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(ctx, 5));

        var broadcast = new BroadcastBlock<int>("broadcast");

        var processor1 = new ProcessorBlock<int>("processor1", async (item, ctx) =>
        {
            processor1Items.Add(item);
            await Task.CompletedTask;
        });

        var processor2 = new ProcessorBlock<int>("processor2", async (item, ctx) =>
        {
            processor2Items.Add(item);
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("broadcast-flow");
        builder.AddBlock(producer)
            .AddBlock(broadcast)
            .AddBlock(processor1)
            .AddBlock(processor2)
            .Connect(producer, broadcast)
            .Connect(broadcast, processor1) // Broadcast to processor1
            .Connect(broadcast, processor2); // Broadcast to processor2

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        processor1Items.Count.ShouldBe(5);
        processor2Items.Count.ShouldBe(5);
        processor1Items.ShouldBe(Enumerable.Range(1, 5));
        processor2Items.ShouldBe(Enumerable.Range(1, 5));
    }
}
