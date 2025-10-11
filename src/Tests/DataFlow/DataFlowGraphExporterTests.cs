namespace Tests.DataFlow;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Uniun.DataFlow;
using Uniun.DataFlow.Builder.Graph;
using Xunit.Abstractions;

/// <summary>
/// Tests for graph export and visualization utilities using snapshot testing.
/// </summary>
[UnitTest]
public class DataFlowGraphExporterTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly ServiceProvider _serviceProvider;

    public DataFlowGraphExporterTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(_testOutputHelper));
        services.AddDataFlows();
        services.AddDataFlowMetrics();
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public Task Should_GenerateMermaidDiagram_ForSimpleFlow()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "SimpleFlow");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddProcessor("processor", sp => new TestProcessor<int>());

        // Act
        var mermaid = builder.Graph.ToMermaidDiagram();

        // Assert - use snapshot testing
        return Verify(mermaid).UseFileName("SimpleFlow_Mermaid");
    }

    [Fact]
    public Task Should_GenerateMermaidDiagram_WithTransformBlock()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TransformFlow");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddTransform("transform", sp => new NumberTransformer("num"))
            .AddProcessor("processor", sp => new TestProcessor<string>());

        // Act
        var mermaid = builder.Graph.ToMermaidDiagram();

        // Assert - use snapshot testing
        return Verify(mermaid).UseFileName("TransformFlow_Mermaid");
    }

    [Fact]
    public Task Should_GenerateMermaidDiagram_WithBatchBlock()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "BatchFlow");
        var items = new[] { 1, 2, 3, 4, 5 };

        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddBatch("batcher", maxBatchSize: 2, windowPeriod: TimeSpan.FromSeconds(1))
            .AddProcessor("processor", sp => new TestProcessor<int[]>());

        // Act
        var mermaid = builder.Graph.ToMermaidDiagram();

        // Assert - use snapshot testing
        return Verify(mermaid).UseFileName("BatchFlow_Mermaid");
    }

    [Fact]
    public Task Should_GenerateTextDiagram_ForFlow()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddTransform("transform", sp => new NumberTransformer("num"))
            .AddProcessor("processor", sp => new TestProcessor<string>());

        // Act
        var textDiagram = builder.Graph.ToTextDiagram();

        // Assert - use snapshot testing
        return Verify(textDiagram).UseFileName("TestFlow_Text");
    }

    [Fact]
    public void Should_GenerateSummary_ForFlow()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddTransform("transform", sp => new NumberTransformer("num"))
            .AddProcessor("processor", sp => new TestProcessor<string>());

        // Act
        var summary = builder.Graph.GetSummary();

        // Assert
        summary.Name.ShouldBe("TestFlow");
        summary.BlockCount.ShouldBe(3);
        summary.ConnectionCount.ShouldBe(2);
        // GetSourceBlocks now returns ALL source blocks (producer + transform = 2)
        summary.SourceBlockCount.ShouldBe(2);
        // GetTargetBlocks now returns ALL target blocks (transform + processor = 2)
        summary.TargetBlockCount.ShouldBe(2);
        summary.BlockTypes.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public Task Should_UseDifferentShapes_ForDifferentBlockTypes()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "ShapeTestFlow");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddTransform("transform", sp => new NumberTransformer("num"))
            .AddProcessor("processor", sp => new TestProcessor<string>());

        // Act
        var mermaid = builder.Graph.ToMermaidDiagram();

        // Assert - verify shapes via snapshot
        return Verify(mermaid).UseFileName("ShapeTest_Mermaid");
    }

    [Fact]
    public Task Should_SupportDifferentDirections_InMermaidDiagram()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "DirectionTestFlow");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddProcessor("processor", sp => new TestProcessor<int>());

        // Act
        var diagrams = new
        {
            LR = builder.Graph.ToMermaidDiagram("LR"),
            TB = builder.Graph.ToMermaidDiagram("TB"),
            RL = builder.Graph.ToMermaidDiagram("RL"),
            BT = builder.Graph.ToMermaidDiagram("BT")
        };

        // Assert - verify all directions via snapshot
        return Verify(diagrams).UseFileName("DirectionTest_Mermaid");
    }

    [Fact]
    public Task Should_IncludeTypeInformation_InMermaidLabels()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TypeInfoFlow");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddTransform("transform", sp => new NumberTransformer("num"))
            .AddProcessor("processor", sp => new TestProcessor<string>());

        // Act
        var mermaid = builder.Graph.ToMermaidDiagram();

        // Assert - verify type info via snapshot
        return Verify(mermaid).UseFileName("TypeInfo_Mermaid");
    }

    [Fact]
    public Task Should_GenerateMermaidDiagram_WithBroadcastBlock()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "BroadcastFlow");
        var items = new[] { 1, 2, 3 };

        // Create a broadcast flow with multiple targets
        builder.AddProducer("source", sp => new TestProducer<int>(items));
        
        builder.AddBroadcast<int>("broadcast", null)
            .WithTarget("validator", null)
            .WithTarget("archiver", x => x)
            .ReceiveFrom("source");

        builder.AddProcessor("validator", sp => new TestProcessor<int>())
            .ReceiveFrom("broadcast");
        
        builder.AddProcessor("archiver", sp => new TestProcessor<int>())
            .ReceiveFrom("broadcast");
        
        builder.AddProcessor("notifier", sp => new TestProcessor<int>())
            .ReceiveFrom("broadcast");

        // Act
        var mermaid = builder.Graph.ToMermaidDiagram();

        // Assert - use snapshot testing
        return Verify(mermaid).UseFileName("BroadcastFlow_Mermaid");
    }

    [Fact]
    public Task Should_GenerateTextDiagram_WithBroadcastBlock()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "BroadcastFlow");
        var items = new[] { 1, 2, 3 };

        // Create a broadcast flow with multiple targets
        builder.AddProducer("source", sp => new TestProducer<int>(items));
        
        builder.AddBroadcast<int>("broadcast", null)
            .ReceiveFrom("source");

        builder.AddProcessor("validator", sp => new TestProcessor<int>())
            .ReceiveFrom("broadcast");
        
        builder.AddProcessor("archiver", sp => new TestProcessor<int>())
            .ReceiveFrom("broadcast");
        
        builder.AddProcessor("notifier", sp => new TestProcessor<int>())
            .ReceiveFrom("broadcast");

        // Act
        var textDiagram = builder.Graph.ToTextDiagram();

        // Assert - use snapshot testing
        return Verify(textDiagram).UseFileName("BroadcastFlow_Text");
    }
}
