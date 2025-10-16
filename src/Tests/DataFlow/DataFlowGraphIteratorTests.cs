namespace Tests.DataFlow;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Uniun.DataFlow;
using Uniun.DataFlow.Builder.Graph;
using Xunit.Abstractions;

/// <summary>
/// Tests for the DAG-based graph iterator.
/// </summary>
[UnitTest]
public class DataFlowGraphIteratorTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly ServiceProvider _serviceProvider;

    public DataFlowGraphIteratorTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(_testOutputHelper));
        services.AddDataFlows();
        services.AddDataFlowMetrics();
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void IterateTopologically_Should_VisitAllBlocks()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddTransform("transform", sp => new NumberTransformer("num"))
            .AddProcessor("processor", sp => new TestProcessor<string>());

        var iterator = new DataFlowGraphIterator(builder.Graph);

        // Act
        var blocks = iterator.IterateTopologically().ToList();

        // Assert
        blocks.Count.ShouldBe(3);
        blocks.Select(b => b.Name).ShouldContain("source");
        blocks.Select(b => b.Name).ShouldContain("transform");
        blocks.Select(b => b.Name).ShouldContain("processor");
    }

    [Fact]
    public void IterateTopologically_Should_RespectDependencyOrder()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddTransform("transform", sp => new NumberTransformer("num"))
            .AddProcessor("processor", sp => new TestProcessor<string>());

        var iterator = new DataFlowGraphIterator(builder.Graph);

        // Act
        var blocks = iterator.IterateTopologically().ToList();

        // Assert
        var sourceIdx = blocks.FindIndex(b => b.Name == "source");
        var transformIdx = blocks.FindIndex(b => b.Name == "transform");
        var processorIdx = blocks.FindIndex(b => b.Name == "processor");

        // Source should come before transform
        sourceIdx.ShouldBeLessThan(transformIdx);
        // Transform should come before processor
        transformIdx.ShouldBeLessThan(processorIdx);
    }

    [Fact]
    public void GetChildren_Should_ReturnDownstreamBlocks()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddTransform("transform", sp => new NumberTransformer("num"))
            .AddProcessor("processor", sp => new TestProcessor<string>());

        var iterator = new DataFlowGraphIterator(builder.Graph);
        var sourceBlock = builder.Graph.GetBlockDefinition("source");

        // Act
        var children = iterator.GetChildren(sourceBlock).ToList();

        // Assert
        children.Count.ShouldBe(1);
        children[0].Name.ShouldBe("transform");
    }

    [Fact]
    public void GetParents_Should_ReturnUpstreamBlocks()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddTransform("transform", sp => new NumberTransformer("num"))
            .AddProcessor("processor", sp => new TestProcessor<string>());

        var iterator = new DataFlowGraphIterator(builder.Graph);
        var processorBlock = builder.Graph.GetBlockDefinition("processor");

        // Act
        var parents = iterator.GetParents(processorBlock).ToList();

        // Assert
        parents.Count.ShouldBe(1);
        parents[0].Name.ShouldBe("transform");
    }

    [Fact]
    public void IsBranchingPoint_Should_DetectMultipleChildren()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items));
        builder.AddBroadcast<int>("fanout").ReceiveFrom("source");
        builder.AddProcessor("processor1", sp => new TestProcessor<int>()).ReceiveFrom("fanout");
        builder.AddProcessor("processor2", sp => new TestProcessor<int>()).ReceiveFrom("fanout");

        var iterator = new DataFlowGraphIterator(builder.Graph);
        var fanoutBlock = builder.Graph.GetBlockDefinition("fanout");
        var sourceBlock = builder.Graph.GetBlockDefinition("source");

        // Act
        var fanoutIsBranching = iterator.IsBranchingPoint(fanoutBlock);
        var sourceIsBranching = iterator.IsBranchingPoint(sourceBlock);

        // Assert
        fanoutIsBranching.ShouldBeTrue();
        sourceIsBranching.ShouldBeFalse(); // Source only has one child (fanout)
    }

    [Fact]
    public void GroupByBranch_Should_SeparateBranchedAndUnbranchedBlocks()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items));
        builder.AddBroadcast<int>("fanout").ReceiveFrom("source");

        // Add 2 branches
        for (int i = 0; i < 2; i++)
        {
            var branch = builder.AddBranch($"branch-{i}");
            branch.AddProcessor<int>($"processor-{i}", sp => new TestProcessor<int>())
                .ReceiveFrom("fanout");
        }

        var iterator = new DataFlowGraphIterator(builder.Graph);

        // Act
        var (branchGroups, unbranchedBlocks) = iterator.GroupByBranch();

        // Assert
        branchGroups.Count.ShouldBe(2);
        branchGroups.ShouldContainKey("branch-0");
        branchGroups.ShouldContainKey("branch-1");
        
        unbranchedBlocks.Count.ShouldBe(2); // source and fanout
        unbranchedBlocks.Select(b => b.Name).ShouldContain("source");
        unbranchedBlocks.Select(b => b.Name).ShouldContain("fanout");
    }

    [Fact]
    public void GroupByBranch_Should_GroupBlocksByBranchName()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items));
        builder.AddBroadcast<int>("fanout").ReceiveFrom("source");

        // Add branch with multiple blocks
        var branch = builder.AddBranch("my-branch");
        branch.AddTransform<int, string>("transform", sp => new NumberTransformer("num"))
            .ReceiveFrom("fanout");
        
        // Add processor within the same branch
        branch.AddProcessor<string>("processor", sp => new TestProcessor<string>())
            .ReceiveFrom("transform");

        var iterator = new DataFlowGraphIterator(builder.Graph);

        // Act
        var (branchGroups, _) = iterator.GroupByBranch();

        // Assert
        branchGroups.ShouldContainKey("my-branch");
        branchGroups["my-branch"].Count.ShouldBe(2); // transform and processor
        branchGroups["my-branch"].Select(b => b.Name).ShouldContain("transform");
        branchGroups["my-branch"].Select(b => b.Name).ShouldContain("processor");
    }
}
