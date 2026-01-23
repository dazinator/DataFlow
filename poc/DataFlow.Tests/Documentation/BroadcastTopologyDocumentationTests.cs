namespace DataFlow.POC.Tests.Documentation;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using Xunit.Categories;
using DataFlow.POC.Registry;

/// <summary>
/// Documentation tests for the Broadcast Topology guide.
/// These tests verify that all code examples in topology-broadcast.md are correct and functional.
/// </summary>
[Documentation]
public class BroadcastTopologyDocumentationTests
{
    [Fact]
    public async Task BroadcastTopology_BasicBroadcast_ShouldSendToAllTargets()
    {
        // This test verifies the basic broadcast pattern from the guide
        
        // Arrange
        var loggerItems = new List<int>();
        var metricsItems = new List<int>();
        
        var producer = BlockHelpers.CreateProducer("source", new[] { 1, 2, 3, 4, 5 });
        
        var logger = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "logger",
            new CollectorActor<int>(loggerItems));
        
        var metrics = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "metrics",
            new CollectorActor<int>(metricsItems));
        
        // Create broadcast edge
        var broadcastEdge = new Edge(
            producer,
            new[] { logger, metrics },
            new BroadcastEdgeStrategy(BufferMode.Bounded, 100));
        
        var graph = new DataFlowGraphBuilder("broadcast-example")
            .AddBlock(producer)
            .AddBlock(logger)
            .AddBlock(metrics)
            .AddEdge(broadcastEdge)
            .Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);
        
        // Act
        await graph.ExecuteAsync(context);
        
        // Assert - Both targets should receive all items
        loggerItems.Count.ShouldBe(5);
        metricsItems.Count.ShouldBe(5);
        loggerItems.ShouldBe(new[] { 1, 2, 3, 4, 5 });
        metricsItems.ShouldBe(new[] { 1, 2, 3, 4, 5 });
    }
    
    [Fact]
    public async Task BroadcastTopology_ThreeTargets_ShouldSendToAll()
    {
        // This test verifies broadcast to three targets
        
        // Arrange
        var target1Items = new List<int>();
        var target2Items = new List<int>();
        var target3Items = new List<int>();
        
        var producer = BlockHelpers.CreateProducer("source", new[] { 1, 2, 3 });
        
        var target1 = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "target1",
            new CollectorActor<int>(target1Items));
        
        var target2 = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "target2",
            new CollectorActor<int>(target2Items));
        
        var target3 = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "target3",
            new CollectorActor<int>(target3Items));
        
        var broadcastEdge = new Edge(
            producer,
            new[] { target1, target2, target3 },
            new BroadcastEdgeStrategy(BufferMode.Bounded, 50));
        
        var graph = new DataFlowGraphBuilder("three-target-broadcast")
            .AddBlock(producer)
            .AddBlock(target1)
            .AddBlock(target2)
            .AddBlock(target3)
            .AddEdge(broadcastEdge)
            .Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);
        
        // Act
        await graph.ExecuteAsync(context);
        
        // Assert - All three targets should receive all items
        target1Items.Count.ShouldBe(3);
        target2Items.Count.ShouldBe(3);
        target3Items.Count.ShouldBe(3);
        
        target1Items.ShouldBe(new[] { 1, 2, 3 });
        target2Items.ShouldBe(new[] { 1, 2, 3 });
        target3Items.ShouldBe(new[] { 1, 2, 3 });
    }
}
