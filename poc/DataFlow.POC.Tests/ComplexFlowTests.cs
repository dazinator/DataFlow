namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public class ComplexFlowTests
{
    // Refactored to use BlockHelpers for consistent block instantiation patterns.

    [Fact(Skip = "Uses obsolete RouterBlock - needs migration to SelectiveRoutingEdgeStrategy")]
    public async Task Complex_Flow_With_Transform_Batch_And_Routing_Should_Work()
    {
        // Arrange
        var smallBatches = new List<string[]>();
        var largeBatches = new List<string[]>();
        
        var transformScopeFactory = TestServiceBuilder.Create()
            .WithScoped(new TransformActor<int, string>(i => $"Item-{i:D2}"))
            .BuildScopeFactory();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = BlockHelpers.CreateProducer("producer", TestStreams.Integers(20));
        var transformer = BlockHelpers.CreateActor<int, string, TransformActor<int, string>>(
            "transformer",
            transformScopeFactory);
        var batcher = BlockHelpers.CreateBatch<string>("batcher", maxBatchSize: 5);
        var router = BlockHelpers.CreateRouter<string[]>("router", batch => batch.Length < 5 ? "small" : "large");
        var smallFilter = BlockHelpers.CreateRouteFilter<string[]>("small-filter", "small");
        var largeFilter = BlockHelpers.CreateRouteFilter<string[]>("large-filter", "large");
        var smallProcessor = BlockHelpers.CreateActor<string[], object, CollectorActor<string[]>>(
            "small-processor",
            new CollectorActor<string[]>(smallBatches));
        var largeProcessor = BlockHelpers.CreateActor<string[], object, CollectorActor<string[]>>(
            "large-processor",
            new CollectorActor<string[]>(largeBatches));

        var builder = GraphHelpers.CreateGraphBuilder("complex-flow");
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
        var context = new ExecutionContext(commonServices, CancellationToken.None);

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
        
        var transform1ScopeFactory = TestServiceBuilder.Create()
            .WithScoped(new TransformActor<int, string>(i => $"T1-{i}"))
            .BuildScopeFactory();

        var transform2ScopeFactory = TestServiceBuilder.Create()
            .WithScoped(new TransformActor<int, string>(i => $"T2-{i}"))
            .BuildScopeFactory();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = BlockHelpers.CreateProducer("producer", TestStreams.Integers(6));
        var transformer1 = BlockHelpers.CreateActor<int, string, TransformActor<int, string>>(
            "transform1",
            transform1ScopeFactory);
        var transformer2 = BlockHelpers.CreateActor<int, string, TransformActor<int, string>>(
            "transform2",
            transform2ScopeFactory);
        var processor = BlockHelpers.CreateActor<string, object, CollectorActor<string>>(
            "processor",
            new CollectorActor<string>(finalResults));

        var builder = GraphHelpers.CreateGraphBuilder("diamond-flow");
        builder.AddBlock(producer)
            .AddBlock(transformer1)
            .AddBlock(transformer2)
            .AddBlock(processor)
            .ConnectMany(producer, transformer1, transformer2) // Split
            .ConnectMany(transformer1, processor) // Merge
            .Connect(transformer2, processor); // Merge

        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        finalResults.Count.ShouldBe(12); // 6 items * 2 paths = 12
        finalResults.Where(r => r.StartsWith("T1-")).Count().ShouldBe(6);
        finalResults.Where(r => r.StartsWith("T2-")).Count().ShouldBe(6);
    }
}
