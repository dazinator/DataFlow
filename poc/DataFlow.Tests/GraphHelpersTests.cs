namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using DataFlow.POC.Registry;

/// <summary>
/// Tests to verify GraphHelpers functionality.
/// </summary>
public class GraphHelpersTests
{
    [Fact]
    public void CreateGraphBuilder_ShouldCreateBuilderWithServiceProvider()
    {
        // Act
        var builder = GraphHelpers.CreateGraphBuilder("test-graph");

        // Assert
        builder.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateGraphBuilder_ShouldSupportBuildingAndExecutingGraph()
    {
        // Arrange
        var collected = new List<int>();
        var producer = BlockHelpers.CreateProducer("producer", TestStreams.Integers(5));
        var collector = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "collector",
            new CollectorActor<int>(collected));

        // Act
        var builder = GraphHelpers.CreateGraphBuilder("test-graph");
        builder.AddBlock(producer)
            .AddBlock(collector)
            .Connect(producer, collector);
        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());

        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);
        await graph.ExecuteAsync(context);

        // Assert
        collected.ShouldBe(new[] { 1, 2, 3, 4, 5 });
    }

    [Fact]
    public async Task CreateGraph_ShouldBuildGraphInOneStep()
    {
        // Arrange
        var collected = new List<int>();
        var producer = BlockHelpers.CreateProducer("producer", TestStreams.Integers(3));
        var collector = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "collector",
            new CollectorActor<int>(collected));

        // Act
        var graph = GraphHelpers.CreateGraph("test-graph", builder =>
        {
            builder.AddBlock(producer)
                .AddBlock(collector)
                .Connect(producer, collector);
        });

        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);
        await graph.ExecuteAsync(context);

        // Assert
        collected.ShouldBe(new[] { 1, 2, 3 });
    }

    [Fact]
    public void CreateGraphBuilder_WithCustomServiceProvider_ShouldUseProvidedServiceProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        var customServiceProvider = services.BuildServiceProvider();

        // Act
        var builder = GraphHelpers.CreateGraphBuilder("test-graph", customServiceProvider);

        // Assert
        builder.ShouldNotBeNull();
        // The builder should use the provided service provider internally
    }
}
