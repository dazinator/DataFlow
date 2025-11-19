namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public class BatchFlowTests
{
    // Refactored to use BlockHelpers for consistent block instantiation patterns.
    private static async IAsyncEnumerable<int> ProduceIntegersWithDelay(IExecutionContext ctx, int count, int delayMs)
    {
        for (int i = 1; i <= count; i++)
        {
            yield return i;
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, ctx.CancellationToken);
            }
        }
    }

    [Fact]
    public async Task Batch_Block_Should_Batch_Items_By_Size()
    {
        // Arrange
        var batches = new List<int[]>();

        var producer = BlockHelpers.CreateProducer("producer", TestStreams.Integers(10));
        var batcher = BlockHelpers.CreateBatch<int>("batcher", maxBatchSize: 3);
        var processor = BlockHelpers.CreateActor<int[], object, CollectorActor<int[]>>(
            "processor",
            new CollectorActor<int[]>(batches));

        var builder = new DataFlowGraphBuilder("batch-flow");
        builder.AddBlock(producer)
            .AddBlock(batcher)
            .AddBlock(processor)
            .Connect(producer, batcher)
            .Connect(batcher, processor);

        var graph = builder.Build();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        batches.Count.ShouldBe(4); // 3 full batches + 1 partial
        batches[0].ShouldBe(new[] { 1, 2, 3 });
        batches[1].ShouldBe(new[] { 4, 5, 6 });
        batches[2].ShouldBe(new[] { 7, 8, 9 });
        batches[3].ShouldBe(new[] { 10 }); // Final partial batch
    }

    [Fact]
    public async Task Batch_Block_Should_Batch_Items_By_Time_Window()
    {
        // Arrange
        var batches = new List<int[]>();

        var producer = BlockHelpers.CreateProducer("producer", ctx => ProduceIntegersWithDelay(ctx, 5, delayMs: 50));
        var batcher = BlockHelpers.CreateBatch<int>("batcher", maxBatchSize: 100, windowPeriod: TimeSpan.FromMilliseconds(120));
        var processor = BlockHelpers.CreateActor<int[], object, CollectorActor<int[]>>(
            "processor",
            new CollectorActor<int[]>(batches));

        var builder = new DataFlowGraphBuilder("batch-window-flow");
        builder.AddBlock(producer)
            .AddBlock(batcher)
            .AddBlock(processor)
            .Connect(producer, batcher)
            .Connect(batcher, processor);

        var graph = builder.Build();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        batches.Count.ShouldBeGreaterThan(1); // Should have multiple batches due to time window
        batches.SelectMany(b => b).Count().ShouldBe(5); // All items should be present
    }
}
