namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public class ComplexFlowTests
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
    public async Task Complex_Flow_With_Transform_Batch_And_Routing_Should_Work()
    {
        // Arrange
        var smallBatches = new List<string[]>();
        var largeBatches = new List<string[]>();
        var services = new ServiceCollection().BuildServiceProvider();

        // Producer generates numbers 1-20
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(ctx, 20));

        // Transform to strings
        var transformer = new SimpleTransformerBlock<int, string>("transformer", i => $"Item-{i:D2}");

        // Batch into groups of 5
        var batcher = new BatchBlock<string>("batcher", maxBatchSize: 5, windowPeriod: null);

        // Route batches by size
        var router = new RouterBlock<string[]>("router", batch => batch.Length < 5 ? "small" : "large");

        var smallFilter = new RouteFilterBlock<string[]>("small-filter", "small");
        var largeFilter = new RouteFilterBlock<string[]>("large-filter", "large");

        var smallProcessor = new ProcessorBlock<string[]>("small-processor", async (batch, ctx) =>
        {
            smallBatches.Add(batch);
            await Task.CompletedTask;
        });

        var largeProcessor = new ProcessorBlock<string[]>("large-processor", async (batch, ctx) =>
        {
            largeBatches.Add(batch);
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("complex-flow");
        builder.AddBlock(producer)
            .AddBlock(transformer)
            .AddBlock(batcher)
            .AddBlock(router)
            .AddBlock(smallFilter)
            .AddBlock(largeFilter)
            .AddBlock(smallProcessor)
            .AddBlock(largeProcessor)
            .Connect(producer, transformer)
            .Connect(transformer, batcher)
            .Connect(batcher, router)
            .Connect(router, smallFilter)
            .Connect(router, largeFilter)
            .Connect(smallFilter, smallProcessor)
            .Connect(largeFilter, largeProcessor);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        largeBatches.Count.ShouldBe(4); // 4 full batches
        smallBatches.Count.ShouldBe(0); // No partial batches (20 items / 5 = 4 even)

        largeBatches[0].ShouldBe(new[] { "Item-01", "Item-02", "Item-03", "Item-04", "Item-05" });
        largeBatches[1].ShouldBe(new[] { "Item-06", "Item-07", "Item-08", "Item-09", "Item-10" });
        largeBatches[2].ShouldBe(new[] { "Item-11", "Item-12", "Item-13", "Item-14", "Item-15" });
        largeBatches[3].ShouldBe(new[] { "Item-16", "Item-17", "Item-18", "Item-19", "Item-20" });
    }

    [Fact]
    public async Task Diamond_Topology_Should_Merge_Results()
    {
        // Arrange
        var finalResults = new List<string>();
        var services = new ServiceCollection().BuildServiceProvider();

        // Single producer
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(ctx, 6));

        // Split into two transformers
        var transformer1 = new SimpleTransformerBlock<int, string>("transform1", i => $"T1-{i}");
        var transformer2 = new SimpleTransformerBlock<int, string>("transform2", i => $"T2-{i}");

        // Merge back to single processor
        var processor = new ProcessorBlock<string>("processor", async (item, ctx) =>
        {
            finalResults.Add(item);
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("diamond-flow");
        builder.AddBlock(producer)
            .AddBlock(transformer1)
            .AddBlock(transformer2)
            .AddBlock(processor)
            .ConnectMany(producer, transformer1, transformer2) // Split
            .ConnectMany(transformer1, processor) // Merge
            .Connect(transformer2, processor); // Merge

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        finalResults.Count.ShouldBe(12); // 6 items * 2 paths = 12
        finalResults.Where(r => r.StartsWith("T1-")).Count().ShouldBe(6);
        finalResults.Where(r => r.StartsWith("T2-")).Count().ShouldBe(6);
    }
}
