namespace DataFlow.POC.Tests;

using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using DataFlow.POC.Registry;
using DataFlow.POC.Visualization;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
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
            .UseFileName("SimpleFlow_Graphviz");
    }

    [Fact]
    public Task Should_RenderFlowWithBufferNode_AsMermaid()
    {
        // Arrange
        var graph = CreateFlowWithBufferNode();

        // Act
        var mermaid = graph.ToMermaidDiagram();

        // Assert
        return Verify(mermaid)
            .UseFileName("BufferNodeFlow_Mermaid");
    }

    [Fact]
    public Task Should_RenderFlowWithBufferNode_AsGraphviz()
    {
        // Arrange
        var graph = CreateFlowWithBufferNode();

        // Act
        var dot = graph.ToGraphviz();

        // Assert
        return Verify(dot)
            .UseFileName("BufferNodeFlow_Graphviz");
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
            .UseFileName("NoTypeInfo_Mermaid");
    }

    [Fact]
    public Task Should_HideBufferNodes_WhenOptionSet()
    {
        // Arrange
        var graph = CreateFlowWithBufferNode();
        var options = new GraphRenderOptions
        {
            ShowBufferNodes = false
        };

        // Act
        var mermaid = graph.ToMermaidDiagram(options: options);

        // Assert
        return Verify(mermaid)
            .UseFileName("HiddenBufferNodes_Mermaid");
    }

    [Fact]
    public void Should_GenerateTextSummary()
    {
        // Arrange
        var graph = CreateSimpleFlowGraph();

        // Act
        var summary = graph.ToTextSummary();

        // Assert
        Assert.Contains("DataFlow:", summary);
        Assert.Contains("Blocks: 3", summary);
        Assert.Contains("Edges: 2", summary);
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
            .UseFileName("RoutingFlow_Mermaid");
    }

    [Fact]
    public Task Should_RenderRoutingFlow_AsGraphviz()
    {
        // Arrange
        var graph = CreateRoutingFlowGraph();

        // Act
        var dot = graph.ToGraphviz();

        // Assert
        return Verify(dot)
            .UseFileName("RoutingFlow_Graphviz");
    }

    #region Test Graph Builders

    private static DataFlowGraph CreateSimpleFlowGraph()
    {
        var builder = new DataFlowGraphBuilder("SimpleFlow", null!);
        
        // Create blocks using the test helpers
        var source = BlockHelpers.CreateProducer("source", Enumerable.Range(1, 10));
        var transform = BlockHelpers.CreateActor<int, string, TransformActor<int, string>>("transform", 
            new TransformActor<int, string>(i => $"Item-{i}"));
        var target = BlockHelpers.CreateActor<string, object, CollectorActor<string>>("target", 
            new CollectorActor<string>(new List<string>()));

        builder.AddBlock(source)
            .AddBlock(transform)
            .AddBlock(target)
            .Connect(source, transform)
            .Connect(transform, target);

        return builder.Build();
    }

    private static DataFlowGraph CreateFlowWithBufferNode()
    {
        var builder = new DataFlowGraphBuilder("BufferNodeFlow", null!);
        var buffer = builder.Buffer<int>(capacity: 50, name: "SharedBuffer");

        var source = BlockHelpers.CreateProducer("source", Enumerable.Range(1, 10));
        var target1 = BlockHelpers.CreateActor<int, object, CollectorActor<int>>("target1", 
            new CollectorActor<int>(new List<int>()));
        var target2 = BlockHelpers.CreateActor<int, object, CollectorActor<int>>("target2", 
            new CollectorActor<int>(new List<int>()));

        builder.AddBlock(source)
            .AddBlock(target1)
            .AddBlock(target2)
            .Connect(source, buffer)
            .Connect(buffer, target1)
            .Connect(buffer, target2);

        return builder.Build();
    }

    private static DataFlowGraph CreateBroadcastFlowGraph()
    {
        var builder = new DataFlowGraphBuilder("BroadcastFlow", null!);
        
        var source = BlockHelpers.CreateProducer("source", Enumerable.Range(1, 10));
        var target1 = BlockHelpers.CreateActor<int, object, CollectorActor<int>>("target1", 
            new CollectorActor<int>(new List<int>()));
        var target2 = BlockHelpers.CreateActor<int, object, CollectorActor<int>>("target2", 
            new CollectorActor<int>(new List<int>()));
        var target3 = BlockHelpers.CreateActor<int, object, CollectorActor<int>>("target3", 
            new CollectorActor<int>(new List<int>()));

        builder.AddBlock(source)
            .AddBlock(target1)
            .AddBlock(target2)
            .AddBlock(target3)
            .ConnectMany(source, 100, target1, target2, target3);

        return builder.Build();
    }

    private static DataFlowGraph CreateComplexFlowGraph()
    {
        var builder = new DataFlowGraphBuilder("ComplexFlow", null!);
        var buffer = builder.Buffer<int>(capacity: 100, name: "MergeBuffer");

        var source1 = BlockHelpers.CreateProducer("source1", Enumerable.Range(1, 5));
        var source2 = BlockHelpers.CreateProducer("source2", Enumerable.Range(6, 5));
        var transform = BlockHelpers.CreateActor<int, string, TransformActor<int, string>>("transform", 
            new TransformActor<int, string>(i => $"Item-{i}"));
        var target1 = BlockHelpers.CreateActor<string, object, CollectorActor<string>>("target1", 
            new CollectorActor<string>(new List<string>()));
        var target2 = BlockHelpers.CreateActor<string, object, CollectorActor<string>>("target2", 
            new CollectorActor<string>(new List<string>()));

        builder.AddBlock(source1)
            .AddBlock(source2)
            .AddBlock(transform)
            .AddBlock(target1)
            .AddBlock(target2)
            .Connect(source1, buffer)
            .Connect(source2, buffer)
            .Connect(buffer, transform)
            .ConnectMany(transform, 50, target1, target2);

        return builder.Build();
    }

    private static DataFlowGraph CreateRoutingFlowGraph()
    {
        var builder = new DataFlowGraphBuilder("RoutingFlow", null!);

        var source = BlockHelpers.CreateProducer("source", Enumerable.Range(1, 10));
        var evenProcessor = BlockHelpers.CreateActor<int, object, CollectorActor<int>>("even-processor", 
            new CollectorActor<int>(new List<int>()));
        var oddProcessor = BlockHelpers.CreateActor<int, object, CollectorActor<int>>("odd-processor", 
            new CollectorActor<int>(new List<int>()));

        // Create selective routing edge strategy
        var routeMapping = new Dictionary<string, IBlock>
        {
            ["even"] = evenProcessor,
            ["odd"] = oddProcessor
        };

        var strategy = new SelectiveRoutingEdgeStrategy<int>(
            routeKeyToBlock: routeMapping,
            routeSelector: i => i % 2 == 0 ? "even" : "odd");

        // Create edge with selective routing strategy
        var edge = new Edge(
            source,
            new[] { evenProcessor, oddProcessor },
            strategy);

        builder.AddBlock(source)
            .AddBlock(evenProcessor)
            .AddBlock(oddProcessor)
            .AddEdge(edge);

        return builder.Build();
    }

    #endregion
}
