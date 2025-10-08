namespace Tests.DataFlow;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Uniun.DataFlow;
using Uniun.DataFlow.Builder.Graph;
using Xunit.Abstractions;

/// <summary>
/// Tests for the structured dataflow builder that builds graph metadata before instantiating blocks.
/// </summary>
[UnitTest]
public class StructuredDataFlowBuilderTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly ServiceProvider _serviceProvider;

    public StructuredDataFlowBuilderTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(_testOutputHelper));
        services.AddDataFlows();
        services.AddDataFlowMetrics();
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void Should_BuildGraphMetadata_Before_BuildingBlocks()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var items = new[] { 1, 2, 3 };

        // Act - Build the graph metadata
        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddProcessor("processor", sp => new TestProcessor<int>());

        // Assert - Graph should have metadata before Build() is called
        builder.Graph.BlockDefinitions.Count.ShouldBe(2);
        builder.Graph.BlockDefinitions.Keys.ShouldContain("source");
        builder.Graph.BlockDefinitions.Keys.ShouldContain("processor");
        builder.Graph.Connections.Count.ShouldBe(1);
        builder.Graph.Connections[0].SourceBlockName.ShouldBe("source");
        builder.Graph.Connections[0].TargetBlockName.ShouldBe("processor");
    }

    [Fact]
    public void Should_CreateBlockInstances_WhenBuildIsCalled()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddProcessor("processor", sp => new TestProcessor<int>());

        // Act - Build the actual dataflow
        var dataflow = builder.Build();

        // Assert
        dataflow.ShouldNotBeNull();
        dataflow.Name.ShouldBe("TestFlow");
        builder.State.Blocks.Count.ShouldBe(2);
    }

    [Fact]
    public void Should_SupportChainedBuilder_WithReceiveFrom()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var items = new[] { 1, 2, 3 };

        // Act - Use fluent chaining with explicit ReceiveFrom
        builder.AddProducer("source", sp => new TestProducer<int>(items));
        builder.AddProcessor("processor", sp => new TestProcessor<int>())
            .ReceiveFrom("source");

        // Assert
        builder.Graph.BlockDefinitions.Count.ShouldBe(2);
        builder.Graph.Connections.Count.ShouldBe(1);
        builder.Graph.Connections[0].SourceBlockName.ShouldBe("source");
        builder.Graph.Connections[0].TargetBlockName.ShouldBe("processor");
    }

    [Fact]
    public void Should_SupportTransformBlocks_InGraph()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var items = new[] { 1, 2, 3 };

        // Act
        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddTransform("transform", sp => new NumberTransformer("num"))
            .AddProcessor("processor", sp => new TestProcessor<string>());

        // Assert
        builder.Graph.BlockDefinitions.Count.ShouldBe(3);
        builder.Graph.Connections.Count.ShouldBe(2);
        
        var sourceDef = builder.Graph.GetBlockDefinition("source");
        sourceDef.OutputType.ShouldBe(typeof(int));
        sourceDef.InputType.ShouldBeNull();

        var transformDef = builder.Graph.GetBlockDefinition("transform");
        transformDef.InputType.ShouldBe(typeof(int));
        transformDef.OutputType.ShouldBe(typeof(string));

        var processorDef = builder.Graph.GetBlockDefinition("processor");
        processorDef.InputType.ShouldBe(typeof(string));
        processorDef.OutputType.ShouldBeNull();
    }

    [Fact]
    public void Should_SupportBatchBlocks_InGraph()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var items = new[] { 1, 2, 3, 4, 5 };

        // Act
        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddBatch("batcher", maxBatchSize: 2, windowPeriod: TimeSpan.FromSeconds(1))
            .AddProcessor("processor", sp => new TestProcessor<int[]>());

        // Assert
        builder.Graph.BlockDefinitions.Count.ShouldBe(3);
        
        var batchDef = builder.Graph.GetBlockDefinition("batcher");
        batchDef.InputType.ShouldBe(typeof(int));
        batchDef.OutputType.ShouldBe(typeof(int[]));
    }

    [Fact]
    public void Should_GetSourceBlocks_FromGraph()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddProcessor("processor", sp => new TestProcessor<int>());

        // Act
        var sourceBlocks = builder.Graph.GetSourceBlocks().ToList();

        // Assert
        sourceBlocks.Count.ShouldBe(1);
        sourceBlocks[0].Name.ShouldBe("source");
    }

    [Fact]
    public void Should_GetTargetBlocks_FromGraph()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddProcessor("processor", sp => new TestProcessor<int>());

        // Act
        var targetBlocks = builder.Graph.GetTargetBlocks().ToList();

        // Assert
        targetBlocks.Count.ShouldBe(1);
        targetBlocks[0].Name.ShouldBe("processor");
    }

    [Fact]
    public void Should_ValidateGraph_BeforeBuild()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddProcessor("processor", sp => new TestProcessor<int>());

        // Act & Assert - Should not throw
        Should.NotThrow(() => builder.Build());
    }

    [Fact]
    public async Task Should_ExecuteDataFlow_FromStructuredBuilder()
    {
        // Arrange
        var processedItems = new List<int>();
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddProcessor("processor", sp => new TestProcessor<int>(
                onProcessItem: item => processedItems.Add(item)));

        var dataflow = builder.Build();
        var context = DataFlowContextTestUtils.GetContext("test", Guid.NewGuid(), _serviceProvider);

        // Act
        await dataflow.ExecuteAsync(context);

        // Assert
        processedItems.Count.ShouldBe(3);
        processedItems.ShouldBe(new[] { 1, 2, 3 });
    }

    [Fact]
    public void Should_ThrowException_WhenBlockNameAlreadyExists()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items));

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            builder.AddProducer("source", sp => new TestProducer<int>(items)));
    }

    [Fact]
    public void Should_GetIncomingConnections_ForBlock()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source1", sp => new TestProducer<int>(items))
            .AddTransform("transform", sp => new NumberTransformer("num"));
        
        builder.AddProducer("source2", sp => new TestProducer<int>(items))
            .LinkTo("transform");

        // Act
        var incoming = builder.Graph.GetIncomingConnections("transform").ToList();

        // Assert
        incoming.Count.ShouldBe(2);
        incoming.Select(c => c.SourceBlockName).ShouldContain("source1");
        incoming.Select(c => c.SourceBlockName).ShouldContain("source2");
    }

    [Fact]
    public void Should_GetOutgoingConnections_ForBlock()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddTransform("transform1", sp => new NumberTransformer("num1"));

        builder.AddTransform("transform2", sp => new NumberTransformer("num2"))
            .ReceiveFrom("source");

        // Act
        var outgoing = builder.Graph.GetOutgoingConnections("source").ToList();

        // Assert
        outgoing.Count.ShouldBe(2);
        outgoing.Select(c => c.TargetBlockName).ShouldContain("transform1");
        outgoing.Select(c => c.TargetBlockName).ShouldContain("transform2");
    }

    [Fact]
    public void Should_ThrowException_WhenMultipleSourcesConnectToSingleTarget()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "MultiSourceFlow");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source1", sp => new TestProducer<int>(items));
        builder.AddProducer("source2", sp => new TestProducer<int>(items));
        
        builder.AddProcessor("processor", sp => new TestProcessor<int>());
        
        // Connect both sources to the same processor
        builder.AddConnection("source1", "processor", typeof(int));
        builder.AddConnection("source2", "processor", typeof(int));

        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() => builder.Build());
        ex.Message.ShouldContain("multiple incoming connections");
        ex.Message.ShouldContain("processor");
    }

    [Fact]
    public void Should_GetRootBlocks_RegardlessOfType()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "RootTest");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddProcessor("processor", sp => new TestProcessor<int>());

        // Act
        var rootBlocks = builder.Graph.GetRootBlocks().ToList();

        // Assert
        rootBlocks.Count.ShouldBe(1);
        rootBlocks[0].Name.ShouldBe("source");
    }

    [Fact]
    public void Should_GetLeafBlocks_RegardlessOfType()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "LeafTest");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddProcessor("processor", sp => new TestProcessor<int>());

        // Act
        var leafBlocks = builder.Graph.GetLeafBlocks().ToList();

        // Assert
        leafBlocks.Count.ShouldBe(1);
        leafBlocks[0].Name.ShouldBe("processor");
    }

    [Fact]
    public void Should_OnlyReturnSourceBlocks_ThatImplementISourceBlock()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "SourceTypeTest");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddProcessor("processor", sp => new TestProcessor<int>());

        // Act
        var sourceBlocks = builder.Graph.GetSourceBlocks().ToList();

        // Assert
        sourceBlocks.Count.ShouldBe(1);
        sourceBlocks[0].Name.ShouldBe("source");
        // Verify it actually implements ISourceBlock
        sourceBlocks[0].BlockType.GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISourceBlock<>))
            .ShouldBeTrue();
    }

    [Fact]
    public void Should_OnlyReturnTargetBlocks_ThatImplementITargetBlock()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TargetTypeTest");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddProcessor("processor", sp => new TestProcessor<int>());

        // Act
        var targetBlocks = builder.Graph.GetTargetBlocks().ToList();

        // Assert
        targetBlocks.Count.ShouldBe(1);
        targetBlocks[0].Name.ShouldBe("processor");
        // Verify it actually implements ITargetBlock
        targetBlocks[0].BlockType.GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITargetBlock<>))
            .ShouldBeTrue();
    }
}
