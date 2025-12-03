namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using DataFlow.POC.Registry;
using DataFlow.POC.Visualization;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// Snapshot tests for graph visualization and export functionality.
/// These tests validate that diagrams are generated correctly by capturing
/// the output and comparing it to verified snapshots.
/// </summary>
public class GraphVisualizationTests
{
    [Fact]
    public Task Should_RenderSimpleFlow_AsMermaid()
    {
        // Arrange
        var graph = CreateSimpleFlowGraph();

        // Act
        var mermaid = graph.ToMermaidDiagram();

        // Assert
        return Verify(mermaid)
            .UseDirectory("Snapshots/GraphVisualizationTests")
            .UseFileName("SimpleFlow_Mermaid");
    }

    [Fact]
    public Task Should_RenderSimpleFlow_AsGraphviz()
    {
        // Arrange
        var graph = CreateSimpleFlowGraph();

        // Act
        var dot = graph.ToGraphviz();

        // Assert
        return Verify(dot)
            .UseDirectory("Snapshots/GraphVisualizationTests")
            .UseFileName("SimpleFlow_Graphviz");
    }



    [Fact]
    public Task Should_RenderBroadcastFlow_AsMermaid()
    {
        // Arrange
        var graph = CreateBroadcastFlowGraph();

        // Act
        var mermaid = graph.ToMermaidDiagram();

        // Assert
        return Verify(mermaid)
            .UseDirectory("Snapshots/GraphVisualizationTests")
            .UseFileName("BroadcastFlow_Mermaid");
    }

    [Fact]
    public Task Should_RenderBroadcastFlow_AsGraphviz()
    {
        // Arrange
        var graph = CreateBroadcastFlowGraph();

        // Act
        var dot = graph.ToGraphviz();

        // Assert
        return Verify(dot)
            .UseDirectory("Snapshots/GraphVisualizationTests")
            .UseFileName("BroadcastFlow_Graphviz");
    }

    [Fact]
    public Task Should_RenderComplexFlow_AsMermaid()
    {
        // Arrange
        var graph = CreateComplexFlowGraph();

        // Act
        var mermaid = graph.ToMermaidDiagram();

        // Assert
        return Verify(mermaid)
            .UseDirectory("Snapshots/GraphVisualizationTests")
            .UseFileName("ComplexFlow_Mermaid");
    }

    [Fact]
    public Task Should_SupportDifferentDirections_InMermaid()
    {
        // Arrange
        var graph = CreateSimpleFlowGraph();

        // Act
        var diagrams = new
        {
            LR = graph.ToMermaidDiagram("LR"),
            TB = graph.ToMermaidDiagram("TB"),
            RL = graph.ToMermaidDiagram("RL"),
            BT = graph.ToMermaidDiagram("BT")
        };

        // Assert
        return Verify(diagrams)
            .UseDirectory("Snapshots/GraphVisualizationTests")
            .UseFileName("DirectionTest_Mermaid");
    }

    [Fact]
    public Task Should_RenderWithoutTypeInfo_WhenOptionSet()
    {
        // Arrange
        var graph = CreateSimpleFlowGraph();
        var options = new GraphRenderOptions
        {
            IncludeTypeInfo = false
        };

        // Act
        var mermaid = graph.ToMermaidDiagram(options: options);

        // Assert
        return Verify(mermaid)
            .UseDirectory("Snapshots/GraphVisualizationTests")
            .UseFileName("NoTypeInfo_Mermaid");
    }



    [Fact]
    public Task Should_GenerateTextSummary()
    {
        // Arrange
        var graph = CreateSimpleFlowGraph();

        // Act
        var summary = graph.ToTextSummary();

        // Assert
        return Verify(summary)
            .UseDirectory("Snapshots/GraphVisualizationTests")
            .UseFileName("SimpleFlow_TextSummary");
    }

    [Fact]
    public Task Should_RenderCompetingConsumersFlow_AsMermaid()
    {
        // Arrange
        var graph = CreateCompetingConsumersFlowGraph();

        // Act
        var mermaid = graph.ToMermaidDiagram();

        // Assert
        return Verify(mermaid)
            .UseDirectory("Snapshots/GraphVisualizationTests")
            .UseFileName("CompetingConsumersFlow_Mermaid");
    }

    [Fact]
    public Task Should_RenderRoutingFlow_AsMermaid()
    {
        // Arrange
        var graph = CreateRoutingFlowGraph();

        // Act
        var mermaid = graph.ToMermaidDiagram();

        // Assert
        return Verify(mermaid)
            .UseDirectory("Snapshots/GraphVisualizationTests")
            .UseFileName("RoutingFlow_Mermaid");
    }

    // Helper methods to create test graphs

    private DataFlowGraph CreateSimpleFlowGraph()
    {
        var producer = BlockHelpers.CreateProducer("source", TestStreams.Integers(5));
        var transformer = BlockHelpers.CreateActor<int, string, TransformActor<int, string>>(
            "transform",
            new TransformActor<int, string>(i => $"Item-{i}"));
        var processor = BlockHelpers.CreateActor<string, object, CollectorActor<string>>(
            "target",
            new CollectorActor<string>(new List<string>()));

        var builder = GraphHelpers.CreateGraphBuilder("SimpleFlow");
        builder.AddBlock(producer)
            .AddBlock(transformer)
            .Connect(producer, transformer)
            .AddBlock(processor)
            .Connect(transformer, processor);

        return builder.Build();
    }



    private DataFlowGraph CreateBroadcastFlowGraph()
    {
        var producer = BlockHelpers.CreateProducer("source", TestStreams.Integers(5));
        var processor1 = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "target1",
            new CollectorActor<int>(new List<int>()));
        var processor2 = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "target2",
            new CollectorActor<int>(new List<int>()));

        var builder = GraphHelpers.CreateGraphBuilder("BroadcastFlow");
        builder.AddBlock(producer)
            .AddBlock(processor1)
            .AddBlock(processor2)
            .Connect(producer, processor1)
            .Connect(producer, processor2);

        return builder.Build();
    }

    private DataFlowGraph CreateComplexFlowGraph()
    {
        // Create a complex graph with multiple sources, transformations, and targets
        var producer1 = BlockHelpers.CreateProducer("source1", TestStreams.Integers(3));
        var producer2 = BlockHelpers.CreateProducer("source2", TestStreams.Integers(3));
        
        var transformer = BlockHelpers.CreateActor<int, string, TransformActor<int, string>>(
            "transform",
            new TransformActor<int, string>(i => $"Item-{i}"));
        
        var processor1 = BlockHelpers.CreateActor<string, object, CollectorActor<string>>(
            "target1",
            new CollectorActor<string>(new List<string>()));
        var processor2 = BlockHelpers.CreateActor<string, object, CollectorActor<string>>(
            "target2",
            new CollectorActor<string>(new List<string>()));

        var builder = GraphHelpers.CreateGraphBuilder("ComplexFlow");
        
        // Connect multiple producers to single transformer (competing consumers pattern)
        // Note: Without BufferNode, we create separate competing edges from each producer
        builder.AddBlock(producer1)
            .AddBlock(producer2)
            .AddBlock(transformer)
            .AddBlock(processor1)
            .AddBlock(processor2);
        
        // Add competing edges from producers to transformer
        builder.AddEdge(new Edge(producer1, transformer, new CompetingEdgeStrategy(BufferMode.Bounded, 100)));
        builder.AddEdge(new Edge(producer2, transformer, new CompetingEdgeStrategy(BufferMode.Bounded, 100)));
        
        // Connect transformer to processors (broadcast)
        builder.Connect(transformer, processor1)
            .Connect(transformer, processor2);

        return builder.Build();
    }

    private DataFlowGraph CreateCompetingConsumersFlowGraph()
    {
        var producer = BlockHelpers.CreateProducer("source", TestStreams.Integers(10));
        var processor1 = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "worker1",
            new CollectorActor<int>(new List<int>()));
        var processor2 = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "worker2",
            new CollectorActor<int>(new List<int>()));

        var builder = GraphHelpers.CreateGraphBuilder("CompetingConsumersFlow");
        
        // Use CompetingEdgeStrategy to create competing consumers
        var competingEdge = new Edge(
            producer,
            new[] { processor1, processor2 },
            new CompetingEdgeStrategy(BufferMode.Bounded, 100));
        
        builder.AddBlock(producer)
            .AddBlock(processor1)
            .AddBlock(processor2)
            .AddEdge(competingEdge);

        return builder.Build();
    }

    private DataFlowGraph CreateRoutingFlowGraph()
    {
        var producer = BlockHelpers.CreateProducer("source", TestStreams.Integers(12));
        
        var evenProcessor = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "even-handler",
            new CollectorActor<int>(new List<int>()));
        var oddProcessor = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "odd-handler",
            new CollectorActor<int>(new List<int>()));

        var builder = GraphHelpers.CreateGraphBuilder("RoutingFlow");
        
        // Use SelectiveRoutingEdgeStrategy to route items by even/odd
        var routeKeyToBlock = new Dictionary<string, IBlock>
        {
            ["even"] = evenProcessor,
            ["odd"] = oddProcessor
        };
        
        var routingEdge = new Edge(
            producer,
            new[] { evenProcessor, oddProcessor },
            new SelectiveRoutingEdgeStrategy<int>(
                routeKeyToBlock,
                item => item % 2 == 0 ? "even" : "odd",
                BufferMode.Bounded,
                100));
        
        builder.AddBlock(producer)
            .AddBlock(evenProcessor)
            .AddBlock(oddProcessor)
            .AddEdge(routingEdge);

        return builder.Build();
    }
}
