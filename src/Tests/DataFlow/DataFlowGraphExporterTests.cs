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

    [SnapshotTest]
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

    [SnapshotTest]
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

    [SnapshotTest]
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

    [SnapshotTest]
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

    [SnapshotTest]
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

    [SnapshotTest]
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

    [SnapshotTest]
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

    [SnapshotTest]
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

    [SnapshotTest]
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

    [SnapshotTest]
    [Fact]
    public Task Should_GenerateMermaidDiagram_WithRoutingBlock()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "RoutingFlow");
        var items = new[] { 1, 2, 3, 4, 5, 6 };

        // Build a routing flow similar to the issue example
        builder.AddProducer("source", sp => new TestProducer<int>(items));

        builder.AddRouter<int>("router", item => item % 2 == 0 ? "even" : "odd")
            .RegisterRoute("even", context =>
            {
                var routeBuilder = context.RouteBuilder;
                routeBuilder.AddProcessor("even-processor", sp => new TestProcessor<int>())
                    .AsEntry();
                return routeBuilder;
            })
            .RegisterRoute("odd", context =>
            {
                var routeBuilder = context.RouteBuilder;
                routeBuilder.AddProcessor("odd-processor", sp => new TestProcessor<int>())
                    .AsEntry();
                return routeBuilder;
            })
            .ReceiveFrom("source");

        // Act - pass service provider to render route details
        var mermaid = builder.Graph.ToMermaidDiagram(serviceProvider: _serviceProvider);

        // Assert - use snapshot testing
        return Verify(mermaid).UseFileName("RoutingFlow_Mermaid");
    }

    [SnapshotTest]
    [Fact]
    public Task Should_GenerateMermaidDiagram_WithComplexRoutingBlock()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "ComplexRoutingFlow");
        var items = new[] { 1, 2, 3, 4, 5, 6 };

        builder.AddProducer("data-source", sp => new TestProducer<int>(items));

        builder.AddTransform("enricher", sp => new NumberTransformer("item"))
            .ReceiveFrom("data-source");

        builder.AddRouter<string>("router", item =>
        {
            var num = int.Parse(item.Replace("item", ""));
            return num % 3 == 0 ? "TypeA" : (num % 3 == 1 ? "TypeB" : "TypeC");
        })
            .RegisterRoute("TypeA", context =>
            {
                var routeBuilder = context.RouteBuilder;
                routeBuilder.AddTransform("processor", sp => new PassthroughTransformer<string>())
                    .AsEntry()
                    .AddProcessor("record-writer", sp => new TestProcessor<string>());
                return routeBuilder;
            })
            .RegisterRoute("TypeB", context =>
            {
                var routeBuilder = context.RouteBuilder;
                routeBuilder.AddBatch<string>("batcher", maxBatchSize: 2, windowPeriod: TimeSpan.FromSeconds(1))
                    .AsEntry()
                    .AddProcessor("aggregation-writer", sp => new TestProcessor<string[]>());
                return routeBuilder;
            })
            .RegisterRoute("TypeC", context =>
            {
                var routeBuilder = context.RouteBuilder;
                routeBuilder.AddProcessor("category-writer", sp => new TestProcessor<string>())
                    .AsEntry();
                return routeBuilder;
            })
            .ReceiveFrom("enricher");

        // Act - pass service provider to render route details
        var mermaid = builder.Graph.ToMermaidDiagram(serviceProvider: _serviceProvider);

        // Assert - use snapshot testing
        return Verify(mermaid).UseFileName("ComplexRoutingFlow_Mermaid");
    }

    #region Branch Diagram Tests

    [Fact]
    public Task Should_GenerateMermaidDiagram_WithBranches_NotCollapsed()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "BranchFlow");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items));
        builder.AddBroadcast<int>("fanout").ReceiveFrom("source");

        // Add 3 branches
        for (var i = 0; i < 3; i++)
        {
            var branch = builder.AddBranch($"branch-{i}");
            branch.AddProcessor<int>($"processor-{i}", sp => new TestProcessor<int>())
                .ReceiveFrom("fanout");
        }

        // Act - Render without collapse
        var mermaid = builder.Graph.ToMermaidDiagram(options: new DiagramRenderOptions
        {
            CollapseConcurrentBranches = false
        });

        // Assert - use snapshot testing
        return Verify(mermaid).UseFileName("BranchFlow_NotCollapsed_Mermaid");
    }

    [Fact]
    public Task Should_GenerateMermaidDiagram_WithBranches_Collapsed()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "BranchFlowCollapsed");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items));
        builder.AddBroadcast<int>("fanout").ReceiveFrom("source");

        // Add 10 branches - exceeds threshold
        for (var i = 0; i < 10; i++)
        {
            var branch = builder.AddBranch($"branch-{i}");
            branch.AddProcessor<int>($"processor-{i}", sp => new TestProcessor<int>())
                .ReceiveFrom("fanout");
        }

        // Act - Render with collapse enabled (threshold = 5)
        var mermaid = builder.Graph.ToMermaidDiagram(options: new DiagramRenderOptions
        {
            CollapseConcurrentBranches = true,
            MaxBranchesToShowIndividually = 5
        });

        // Assert - use snapshot testing - shows collapsed notation [×10]
        return Verify(mermaid).UseFileName("BranchFlow_Collapsed_Mermaid");
    }

    [Fact]
    public Task Should_GenerateMermaidDiagram_WithComplexBranches()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "ComplexBranchFlow");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items));
        builder.AddBroadcast<int>("fanout").ReceiveFrom("source");

        // Add 3 branches with multi-stage pipelines
        for (var i = 0; i < 3; i++)
        {
            var branch = builder.AddBranch($"branch-{i}");
            branch.AddTransform<int, string>($"transform-{i}",
                sp => new NumberTransformer($"Item"))
                .ReceiveFrom("fanout");
            branch.AddProcessor<string>($"processor-{i}",
                sp => new TestProcessor<string>())
                .ReceiveFrom($"transform-{i}");
        }

        // Act - Render without collapse
        var mermaid = builder.Graph.ToMermaidDiagram(options: new DiagramRenderOptions
        {
            CollapseConcurrentBranches = false
        });

        // Assert - use snapshot testing - shows all branch blocks with metadata
        return Verify(mermaid).UseFileName("ComplexBranchFlow_Mermaid");
    }

    [Fact]
    public Task Should_GenerateTextDiagram_WithBranches()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "BranchTextFlow");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items));
        builder.AddBroadcast<int>("fanout").ReceiveFrom("source");

        for (var i = 0; i < 2; i++)
        {
            var branch = builder.AddBranch($"branch-{i}");
            branch.AddProcessor<int>($"processor-{i}", sp => new TestProcessor<int>())
                .ReceiveFrom("fanout");
        }

        // Act
        var textDiagram = builder.Graph.ToTextDiagram();

        // Assert - use snapshot testing
        return Verify(textDiagram).UseFileName("BranchFlow_Text");
    }

    [Fact]
    public Task Should_ShowBranchMetadata_InBlockDefinitions()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "BranchMetadataFlow");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items));
        builder.AddBroadcast<int>("fanout").ReceiveFrom("source");

        // Add branches
        for (var i = 0; i < 2; i++)
        {
            var branch = builder.AddBranch($"branch-{i}");
            branch.AddProcessor<int>($"processor-{i}", sp => new TestProcessor<int>())
                .ReceiveFrom("fanout");
        }

        // Act - Check that blocks have branch metadata
        var blockMetadata = builder.Graph.BlockDefinitions.Values
            .Select(b => new
            {
                b.Name,
                BranchName = b.Metadata.ContainsKey("BranchName") ? b.Metadata["BranchName"] : null
            })
            .OrderBy(b => b.Name)
            .ToList();

        // Assert - use snapshot testing to show metadata
        return Verify(blockMetadata).UseFileName("BranchMetadata");
    }

    [SnapshotTest]
    [Fact]
    public Task Should_GenerateMermaidDiagram_WithNestedBranchFamilies()
    {
        // Arrange - Create a flow with multiple branch families at different levels
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "NestedBranchFamiliesFlow");
        var items = new[] { 1, 2, 3 };

        // Root source
        builder.AddProducer("source", sp => new TestProducer<int>(items));

        // First branch family: Split from source via broadcast
        builder.AddBroadcast<int>("fanout1").ReceiveFrom("source");

        // Branch family 1 - Branch A: Simple processor
        var branchA = builder.AddBranch("family1-branchA");
        branchA.AddProcessor<int>("processor-A", sp => new TestProcessor<int>())
            .ReceiveFrom("fanout1");

        // Branch family 1 - Branch B: Has a nested broadcast creating another branch family
        var branchB = builder.AddBranch("family1-branchB");
        branchB.AddTransform<int, string>("transform-B", sp => new NumberTransformer("Item"))
            .ReceiveFrom("fanout1");

        // Second branch family: Nested within branchB - broadcast from transform-B
        branchB.AddBroadcast<string>("fanout2")
            .ReceiveFrom("transform-B");

        // Branch family 2 - Branch B1: First sub-branch
        var branchB1 = builder.AddBranch("family2-branchB1");
        branchB1.AddProcessor<string>("processor-B1", sp => new TestProcessor<string>())
            .ReceiveFrom("fanout2");

        // Branch family 2 - Branch B2: Second sub-branch
        var branchB2 = builder.AddBranch("family2-branchB2");
        branchB2.AddProcessor<string>("processor-B2", sp => new TestProcessor<string>())
            .ReceiveFrom("fanout2");

        // Act - Render the nested structure
        var mermaid = builder.Graph.ToMermaidDiagram(options: new DiagramRenderOptions
        {
            CollapseConcurrentBranches = false,
            GroupBranchesInSubgraphs = true
        });

        // Assert - use snapshot testing to verify nested subgraphs render correctly
        return Verify(mermaid).UseFileName("NestedBranchFamilies_Mermaid");
    }

    #endregion
}
