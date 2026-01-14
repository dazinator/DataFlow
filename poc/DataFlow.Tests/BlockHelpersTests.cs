namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

/// <summary>
/// Unit tests for BlockHelpers test utility.
/// Validates that all helper methods create blocks correctly.
/// </summary>
public class BlockHelpersTests
{
    [Fact]
    public void CreateProducer_WithEnumerable_ShouldCreateProducerBlock()
    {
        // Act
        var producer = BlockHelpers.CreateProducer("test", new[] { 1, 2, 3 });

        // Assert
        producer.ShouldNotBeNull();
        producer.Name.ShouldBe("test");
    }

    [Fact]
    public void CreateProducer_WithAsyncEnumerable_ShouldCreateProducerBlock()
    {
        // Act
        var producer = BlockHelpers.CreateProducer("test", TestStreams.Integers(5));

        // Assert
        producer.ShouldNotBeNull();
        producer.Name.ShouldBe("test");
    }

    [Fact]
    public void CreateProducer_WithFunction_ShouldCreateProducerBlock()
    {
        // Act
        var producer = BlockHelpers.CreateProducer("test", ctx => TestStreams.Integers(5));

        // Assert
        producer.ShouldNotBeNull();
        producer.Name.ShouldBe("test");
    }

    [Fact]
    public void CreateActor_WithScopeFactory_ShouldCreateActorBlock()
    {
        // Arrange
        var scopeFactory = TestServiceBuilder.Create()
            .WithScoped(new TransformActor<int, string>(i => i.ToString()))
            .BuildScopeFactory();

        // Act
        var actor = BlockHelpers.CreateActor<int, string, TransformActor<int, string>>(
            "test",
            scopeFactory);

        // Assert
        actor.ShouldNotBeNull();
        actor.Name.ShouldBe("test");
    }

    [Fact]
    public void CreateActor_WithActorInstance_ShouldCreateActorBlock()
    {
        // Arrange
        var actorInstance = new TransformActor<int, string>(i => i.ToString());

        // Act
        var actor = BlockHelpers.CreateActor<int, string, TransformActor<int, string>>("test", actorInstance);

        // Assert
        actor.ShouldNotBeNull();
        actor.Name.ShouldBe("test");
    }

    [Fact]
    public void CreateBatch_WithSizeOnly_ShouldCreateBatchBlock()
    {
        // Act
        var batcher = BlockHelpers.CreateBatch<int>("test", 100);

        // Assert
        batcher.ShouldNotBeNull();
        batcher.Name.ShouldBe("test");
    }

    [Fact]
    public void CreateBatch_WithSizeAndWindow_ShouldCreateBatchBlock()
    {
        // Act
        var batcher = BlockHelpers.CreateBatch<int>("test", 100, TimeSpan.FromSeconds(5));

        // Assert
        batcher.ShouldNotBeNull();
        batcher.Name.ShouldBe("test");
    }

    [Fact]
    public async Task CreateProducer_Integration_ShouldProduceItems()
    {
        // Arrange
        var collected = new List<int>();
        var producer = BlockHelpers.CreateProducer("producer", TestStreams.Integers(5));
        var collector = BlockHelpers.CreateActor<int, object, CollectorActor<int>>("collector", new CollectorActor<int>(collected));

        var builder = GraphHelpers.CreateGraphBuilder("test-flow");
        builder.AddBlock(producer)
            .AddBlock(collector)
            .Connect(producer, collector);

        var graph = builder.Build();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        collected.ShouldBe(new[] { 1, 2, 3, 4, 5 });
    }

    [Fact]
    public async Task CreateBatch_Integration_ShouldBatchItems()
    {
        // Arrange
        var batches = new List<int[]>();
        var producer = BlockHelpers.CreateProducer("producer", TestStreams.Integers(10));
        var batcher = BlockHelpers.CreateBatch<int>("batcher", 3);
        var collector = BlockHelpers.CreateActor<int[], object, CollectorActor<int[]>>("collector", new CollectorActor<int[]>(batches));

        var builder = GraphHelpers.CreateGraphBuilder("test-flow");
        builder.AddBlock(producer)
            .AddBlock(batcher)
            .AddBlock(collector)
            .Connect(producer, batcher)
            .Connect(batcher, collector);

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
}
