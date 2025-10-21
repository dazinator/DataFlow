namespace Tests.DataFlow;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Uniun.DataFlow;
using Uniun.DataFlow.Builder.Graph;
using Xunit.Abstractions;

/// <summary>
/// Tests for runtime block access and initialization features.
/// </summary>
[UnitTest]
public class RuntimeBlockAccessTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly ServiceProvider _serviceProvider;

    public RuntimeBlockAccessTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(_testOutputHelper));
        services.AddDataFlows();
        services.AddDataFlowMetrics();
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void Should_CreateRuntimeGraph_WithBlockInstances()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var items = new[] { 1, 2, 3 };
        var testBlock = new InitializableTestBlock<int>("test");

        builder.AddProducer("source", sp => new TestProducer<int>(items));
        builder.AddBlockDefinition("test", sp => testBlock);
        
        builder.AddConnection("source", "test");

        // Act
        var dataflow = builder.Build();

        // Assert - runtime graph should be accessible via the initializable block
        var runtimeGraph = testBlock.RuntimeGraphReceived;
        runtimeGraph.ShouldNotBeNull();
        runtimeGraph!.BlockInstances.Count.ShouldBe(2);
        runtimeGraph.BlockInstances.Keys.ShouldContain("source");
        runtimeGraph.BlockInstances.Keys.ShouldContain("test");
    }

    [Fact]
    public void Should_CallInitialization_OnInitializableBlocks()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var initializableBlock = new InitializableTestBlock<int>("initializable");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items));
        builder.AddBlockDefinition("initializable", sp => initializableBlock);

        builder.AddConnection("source", "initializable");

        // Act
        var dataflow = builder.Build();

        // Assert
        initializableBlock.WasInitialized.ShouldBeTrue();
        initializableBlock.RuntimeGraphReceived.ShouldNotBeNull();
        initializableBlock.RuntimeGraphReceived!.Name.ShouldBe("TestFlow");
    }

    [Fact]
    public void Should_InitializeBlocksInTopologicalOrder()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var initOrder = new List<string>();

        var block1 = new OrderTrackingProducerBlock("block1", initOrder);
        var block2 = new OrderTrackingTransformBlock("block2", initOrder);
        var block3 = new OrderTrackingProcessorBlock("block3", initOrder);

        builder.AddBlockDefinition("block1", sp => block1);
        builder.AddBlockDefinition("block2", sp => block2);
        builder.AddBlockDefinition("block3", sp => block3);

        // Create connections: block1 -> block2 -> block3
        builder.AddConnection("block1", "block2");
        builder.AddConnection("block2", "block3");

        // Act
        var dataflow = builder.Build();

        // Assert - blocks should be initialized in topological order
        initOrder.Count.ShouldBe(3);
        initOrder[0].ShouldBe("block1"); // Root block first
        initOrder[1].ShouldBe("block2"); // Then its dependent
        initOrder[2].ShouldBe("block3"); // Then the final dependent
    }

    [Fact]
    public void Should_PassRuntimeGraph_WithAllBlockInstances()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var initializableBlock = new InitializableTestBlock<string>("initializable");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items));
        builder.AddTransform("transform", sp => new NumberTransformer("num"));
        builder.AddBlockDefinition("initializable", sp => initializableBlock);

        builder.AddConnection("transform", "initializable");

        // Act
        var dataflow = builder.Build();

        // Assert - runtime graph should contain all blocks
        var runtimeGraph = initializableBlock.RuntimeGraphReceived;
        runtimeGraph.ShouldNotBeNull();
        runtimeGraph!.BlockInstances.Count.ShouldBe(3);
        runtimeGraph.BlockInstances.Keys.ShouldContain("source");
        runtimeGraph.BlockInstances.Keys.ShouldContain("transform");
        runtimeGraph.BlockInstances.Keys.ShouldContain("initializable");
    }

    [Fact]
    public void Should_AllowBlocksToAccessOtherBlocks()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var items = new[] { 1, 2, 3 };
        var initializableBlock = new BlockThatAccessesOtherBlocks<string>("accessor");

        builder.AddProducer("source", sp => new TestProducer<int>(items));
        builder.AddTransform("transform", sp => new NumberTransformer("num"));
        builder.AddBlockDefinition("accessor", sp => initializableBlock);

        builder.AddConnection("transform", "accessor");

        // Act
        var dataflow = builder.Build();

        // Assert - the initializable block should have accessed the transform block
        initializableBlock.AccessedBlock.ShouldNotBeNull();
        initializableBlock.AccessedBlock!.Name.ShouldBe("transform");
    }

    [Fact]
    public void Should_RespectCancellationToken_DuringInitialization()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var cancellableBlock = new CancellableInitBlock<int>("cancellable");
        var items = new[] { 1, 2, 3 };

        builder.AddProducer("source", sp => new TestProducer<int>(items));
        builder.AddBlockDefinition("cancellable", sp => cancellableBlock);

        builder.AddConnection("source", "cancellable");

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        Should.Throw<OperationCanceledException>(() =>
        {
            builder.Build(cts.Token);
        });
    }

    [Fact]
    public void Should_SupportGetBlockInstance_ByName()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var items = new[] { 1, 2, 3 };
        var testBlock = new InitializableTestBlock<string>("test");

        builder.AddProducer("source", sp => new TestProducer<int>(items));
        builder.AddTransform("transform", sp => new NumberTransformer("num"));
        builder.AddBlockDefinition("test", sp => testBlock);

        builder.AddConnection("transform", "test");

        var dataflow = builder.Build();

        // Assert
        var runtimeGraph = testBlock.RuntimeGraphReceived;
        runtimeGraph.ShouldNotBeNull();

        var retrievedBlock = runtimeGraph!.GetBlockInstance("transform");
        retrievedBlock.ShouldNotBeNull();
        retrievedBlock.Name.ShouldBe("transform");
    }

    [Fact]
    public void Should_SupportGetBlockInstance_WithStrongTyping()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var items = new[] { 1, 2, 3 };
        var testBlock = new InitializableTestBlock<string>("test");

        builder.AddProducer("source", sp => new TestProducer<int>(items));
        builder.AddTransform("transform", sp => new NumberTransformer("num"));
        builder.AddBlockDefinition("test", sp => testBlock);

        builder.AddConnection("transform", "test");

        var dataflow = builder.Build();

        // Assert
        var runtimeGraph = testBlock.RuntimeGraphReceived;
        runtimeGraph.ShouldNotBeNull();

        // Get the TransformBlock instance
        var retrievedTransform = runtimeGraph!.GetBlockInstance<Uniun.DataFlow.Blocks.Transform.TransformBlock<int, string>>("transform");
        retrievedTransform.ShouldNotBeNull();
        retrievedTransform.Name.ShouldBe("transform");
    }

    [Fact]
    public void Should_ThrowException_WhenBlockNotFound()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var items = new[] { 1, 2, 3 };
        var testBlock = new InitializableTestBlock<int>("test");

        builder.AddProducer("source", sp => new TestProducer<int>(items));
        builder.AddBlockDefinition("test", sp => testBlock);

        builder.AddConnection("source", "test");

        var dataflow = builder.Build();

        // Assert
        var runtimeGraph = testBlock.RuntimeGraphReceived;
        runtimeGraph.ShouldNotBeNull();

        Should.Throw<InvalidOperationException>(() =>
        {
            runtimeGraph!.GetBlockInstance("nonexistent");
        });
    }

    [Fact]
    public void Should_TryGetBlockInstance_ReturnFalse_WhenBlockNotFound()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var items = new[] { 1, 2, 3 };
        var testBlock = new InitializableTestBlock<int>("test");

        builder.AddProducer("source", sp => new TestProducer<int>(items));
        builder.AddBlockDefinition("test", sp => testBlock);

        builder.AddConnection("source", "test");

        var dataflow = builder.Build();

        // Assert
        var runtimeGraph = testBlock.RuntimeGraphReceived;
        runtimeGraph.ShouldNotBeNull();

        var found = runtimeGraph!.TryGetBlockInstance("nonexistent", out var block);
        found.ShouldBeFalse();
        block.ShouldBeNull();
    }

    [Fact]
    public void Should_SupportBuild_WithoutExplicitCancellation()
    {
        // Arrange
        var builder = new StructuredDataFlowBuilder(_serviceProvider, "TestFlow");
        var items = new[] { 1, 2, 3 };
        var initializableBlock = new InitializableTestBlock<int>("initializable");

        builder.AddProducer("source", sp => new TestProducer<int>(items));
        builder.AddBlockDefinition("initializable", sp => initializableBlock);

        builder.AddConnection("source", "initializable");

        // Act - Build() method with default cancellation token
        var dataflow = builder.Build();

        // Assert
        initializableBlock.WasInitialized.ShouldBeTrue();
        dataflow.ShouldNotBeNull();
    }
}

/// <summary>
/// Test block that implements IDataFlowInitializable for testing.
/// </summary>
internal class InitializableTestBlock<T> : Uniun.DataFlow.Blocks.BlockBase, Uniun.DataFlow.Blocks.ITargetBlock<T>
{
    private Uniun.DataFlow.Blocks.ISourceBlock<T>? _source;

    public InitializableTestBlock(string name) 
        : base(name, new Uniun.DataFlow.Blocks.BlockOptions(), new Microsoft.Extensions.Logging.Abstractions.NullLogger<InitializableTestBlock<T>>())
    {
    }

    public bool WasInitialized { get; private set; }
    public IDataFlowRuntimeGraph? RuntimeGraphReceived { get; private set; }

    public void SetSource(Uniun.DataFlow.Blocks.ISourceBlock<T> source)
    {
        _source = source;
    }

    public override void OnDataFlowInitialized(IDataFlowRuntimeGraph runtimeGraph, CancellationToken cancellationToken)
    {
        WasInitialized = true;
        RuntimeGraphReceived = runtimeGraph;
    }

    protected override async Task CoreExecuteAsync(IDataFlowContext context)
    {
        if (_source == null)
        {
            throw new InvalidOperationException("No source block configured");
        }

        await foreach (var item in _source.GetAsyncEnumerable(this, context.CancellationToken))
        {
            // Just consume items
        }
    }
}

/// <summary>
/// Test block that tracks initialization order.
/// </summary>
internal class OrderTrackingBlock : Uniun.DataFlow.Blocks.BlockBase, Uniun.DataFlow.Blocks.ISourceBlock<int>
{
    private readonly List<string> _initOrder;

    public OrderTrackingBlock(string name, List<string> initOrder)
        : base(name, new Uniun.DataFlow.Blocks.BlockOptions(), new Microsoft.Extensions.Logging.Abstractions.NullLogger<OrderTrackingBlock>())
    {
        _initOrder = initOrder;
    }

    public override void OnDataFlowInitialized(IDataFlowRuntimeGraph runtimeGraph, CancellationToken cancellationToken)
    {
        _initOrder.Add(Name);
    }

    public async IAsyncEnumerable<int> GetAsyncEnumerable(Uniun.DataFlow.Blocks.ITargetBlock<int> target, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Return empty enumerable
        yield break;
    }

    protected override Task CoreExecuteAsync(IDataFlowContext context)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// Producer block that tracks initialization order.
/// </summary>
internal class OrderTrackingProducerBlock : Uniun.DataFlow.Blocks.BlockBase, Uniun.DataFlow.Blocks.ISourceBlock<int>
{
    private readonly List<string> _initOrder;

    public OrderTrackingProducerBlock(string name, List<string> initOrder)
        : base(name, new Uniun.DataFlow.Blocks.BlockOptions(), new Microsoft.Extensions.Logging.Abstractions.NullLogger<OrderTrackingProducerBlock>())
    {
        _initOrder = initOrder;
    }

    public override void OnDataFlowInitialized(IDataFlowRuntimeGraph runtimeGraph, CancellationToken cancellationToken)
    {
        _initOrder.Add(Name);
    }

    public async IAsyncEnumerable<int> GetAsyncEnumerable(Uniun.DataFlow.Blocks.ITargetBlock<int> target, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield break;
    }

    protected override Task CoreExecuteAsync(IDataFlowContext context)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// Transform block that tracks initialization order.
/// </summary>
internal class OrderTrackingTransformBlock : Uniun.DataFlow.Blocks.BlockBase, Uniun.DataFlow.Blocks.IPropagatorBlock<int, int>
{
    private readonly List<string> _initOrder;
    private Uniun.DataFlow.Blocks.ISourceBlock<int>? _source;

    public OrderTrackingTransformBlock(string name, List<string> initOrder)
        : base(name, new Uniun.DataFlow.Blocks.BlockOptions(), new Microsoft.Extensions.Logging.Abstractions.NullLogger<OrderTrackingTransformBlock>())
    {
        _initOrder = initOrder;
    }

    public void SetSource(Uniun.DataFlow.Blocks.ISourceBlock<int> source)
    {
        _source = source;
    }

    public override void OnDataFlowInitialized(IDataFlowRuntimeGraph runtimeGraph, CancellationToken cancellationToken)
    {
        _initOrder.Add(Name);
    }

    public async IAsyncEnumerable<int> GetAsyncEnumerable(Uniun.DataFlow.Blocks.ITargetBlock<int> target, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_source != null)
        {
            await foreach (var item in _source.GetAsyncEnumerable(this, cancellationToken))
            {
                yield return item;
            }
        }
    }

    protected override Task CoreExecuteAsync(IDataFlowContext context)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// Processor block that tracks initialization order.
/// </summary>
internal class OrderTrackingProcessorBlock : Uniun.DataFlow.Blocks.BlockBase, Uniun.DataFlow.Blocks.ITargetBlock<int>
{
    private readonly List<string> _initOrder;
    private Uniun.DataFlow.Blocks.ISourceBlock<int>? _source;

    public OrderTrackingProcessorBlock(string name, List<string> initOrder)
        : base(name, new Uniun.DataFlow.Blocks.BlockOptions(), new Microsoft.Extensions.Logging.Abstractions.NullLogger<OrderTrackingProcessorBlock>())
    {
        _initOrder = initOrder;
    }

    public void SetSource(Uniun.DataFlow.Blocks.ISourceBlock<int> source)
    {
        _source = source;
    }

    public override void OnDataFlowInitialized(IDataFlowRuntimeGraph runtimeGraph, CancellationToken cancellationToken)
    {
        _initOrder.Add(Name);
    }

    protected override async Task CoreExecuteAsync(IDataFlowContext context)
    {
        if (_source != null)
        {
            await foreach (var item in _source.GetAsyncEnumerable(this, context.CancellationToken))
            {
                // Just consume items
            }
        }
    }
}

/// <summary>
/// Test block that accesses other blocks during initialization.
/// </summary>
internal class BlockThatAccessesOtherBlocks<T> : Uniun.DataFlow.Blocks.BlockBase, Uniun.DataFlow.Blocks.ITargetBlock<T>
{
    private Uniun.DataFlow.Blocks.ISourceBlock<T>? _source;

    public BlockThatAccessesOtherBlocks(string name)
        : base(name, new Uniun.DataFlow.Blocks.BlockOptions(), new Microsoft.Extensions.Logging.Abstractions.NullLogger<BlockThatAccessesOtherBlocks<T>>())
    {
    }

    public Uniun.DataFlow.Blocks.IBlock? AccessedBlock { get; private set; }

    public void SetSource(Uniun.DataFlow.Blocks.ISourceBlock<T> source)
    {
        _source = source;
    }

    public override void OnDataFlowInitialized(IDataFlowRuntimeGraph runtimeGraph, CancellationToken cancellationToken)
    {
        // Try to access the transform block
        if (runtimeGraph.TryGetBlockInstance("transform", out var block))
        {
            AccessedBlock = block;
        }
    }

    protected override async Task CoreExecuteAsync(IDataFlowContext context)
    {
        if (_source == null)
        {
            throw new InvalidOperationException("No source block configured");
        }

        await foreach (var item in _source.GetAsyncEnumerable(this, context.CancellationToken))
        {
            // Just consume items
        }
    }
}

/// <summary>
/// Test block that checks cancellation during initialization.
/// </summary>
internal class CancellableInitBlock<T> : Uniun.DataFlow.Blocks.BlockBase, Uniun.DataFlow.Blocks.ITargetBlock<T>
{
    private Uniun.DataFlow.Blocks.ISourceBlock<T>? _source;

    public CancellableInitBlock(string name)
        : base(name, new Uniun.DataFlow.Blocks.BlockOptions(), new Microsoft.Extensions.Logging.Abstractions.NullLogger<CancellableInitBlock<T>>())
    {
    }

    public void SetSource(Uniun.DataFlow.Blocks.ISourceBlock<T> source)
    {
        _source = source;
    }

    public override void OnDataFlowInitialized(IDataFlowRuntimeGraph runtimeGraph, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
    }

    protected override async Task CoreExecuteAsync(IDataFlowContext context)
    {
        if (_source == null)
        {
            throw new InvalidOperationException("No source block configured");
        }

        await foreach (var item in _source.GetAsyncEnumerable(this, context.CancellationToken))
        {
            // Just consume items
        }
    }
}
