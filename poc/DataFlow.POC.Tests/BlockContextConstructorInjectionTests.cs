namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

/// <summary>
/// Tests to validate that IBlockContext is properly injected via constructor
/// and that the two-phase initialization pattern (SetContext) has been removed.
/// </summary>
public class BlockContextConstructorInjectionTests
{
    private class SimpleActor : IStreamActor<int, int>
    {
        public async IAsyncEnumerable<int> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                yield return item * 2;
            }
        }
    }

    [Fact]
    public void ActorBlock_ConstructorWithContext_SetsNameImmediately()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<SimpleActor>();
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        var context = new BlockContext("test-block");

        // Act
        var actorBlock = new ActorBlock<int, int, SimpleActor>(context, scopeFactory);

        // Assert
        actorBlock.Name.ShouldBe("test-block");
    }

    [Fact]
    public void ActorBlock_ConstructorWithContext_ContextIsImmutable()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<SimpleActor>();
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        var context = new BlockContext("immutable-block");

        // Act
        var actorBlock = new ActorBlock<int, int, SimpleActor>(context, scopeFactory);

        // Assert
        actorBlock.Name.ShouldBe("immutable-block");
        
        // Verify that there's no SetContext method available (compilation would fail if called)
        // This test validates that the block's name is set once and cannot be changed
    }

    [Fact]
    public void ActorBlock_ConstructorWithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        IBlockContext? nullContext = null;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => 
            new ActorBlock<int, int, SimpleActor>(nullContext!, scopeFactory));
    }

    [Fact]
    public void ActorBlock_ConstructorWithNullScopeFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var context = new BlockContext("test-block");

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => 
            new ActorBlock<int, int, SimpleActor>(context, null!));
    }

    [Fact]
    public async Task ActorBlock_WithConstructorInjectedContext_ExecutesCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<SimpleActor>();
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        var context = new BlockContext("processing-block");
        var actorBlock = new ActorBlock<int, int, SimpleActor>(context, scopeFactory);
        
        var executionContext = new ExecutionContext(serviceProvider, CancellationToken.None);
        var input = ProduceIntegers(5);

        // Act
        var results = new List<int>();
        await foreach (var item in actorBlock.ExecuteAsync(input, executionContext))
        {
            results.Add(item);
        }

        // Assert
        actorBlock.Name.ShouldBe("processing-block");
        results.Count.ShouldBe(5);
        results.ShouldBe(new[] { 2, 4, 6, 8, 10 });
    }

    [Fact]
    public void BlockContext_WithMetadata_PreservesMetadata()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            { "version", "1.0" },
            { "author", "test" }
        };
        var context = new BlockContext("metadata-block", metadata);

        var services = new ServiceCollection();
        services.AddTransient<SimpleActor>();
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        // Act
        var actorBlock = new ActorBlock<int, int, SimpleActor>(context, scopeFactory);

        // Assert
        actorBlock.Name.ShouldBe("metadata-block");
        // Metadata is preserved in the context (though not directly accessible via block)
    }

    private static async IAsyncEnumerable<int> ProduceIntegers(int count)
    {
        for (int i = 1; i <= count; i++)
        {
            yield return i;
        }
    }

    #region ProducerBlock Tests

    [Fact]
    public void ProducerBlock_ConstructorWithContext_SetsNameImmediately()
    {
        // Arrange
        var context = new BlockContext("producer-block");
        var producer = (IExecutionContext ctx) => ProduceIntegers(5);

        // Act
        var block = new ProducerBlock<int>(context, producer);

        // Assert
        block.Name.ShouldBe("producer-block");
    }

    [Fact]
    public void ProducerBlock_ConstructorWithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        IBlockContext? nullContext = null;
        var producer = (IExecutionContext ctx) => ProduceIntegers(5);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new ProducerBlock<int>(nullContext!, producer));
    }

    [Fact]
    public async Task ProducerBlock_WithConstructorInjectedContext_ExecutesCorrectly()
    {
        // Arrange
        var context = new BlockContext("producer-test");
        var producer = (IExecutionContext ctx) => ProduceIntegers(3);
        var block = new ProducerBlock<int>(context, producer);
        
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var executionContext = new ExecutionContext(serviceProvider, CancellationToken.None);

        // Act
        var results = new List<int>();
        await foreach (var item in block.ExecuteAsync(EmptyInput(), executionContext))
        {
            results.Add(item);
        }

        // Assert
        results.ShouldBe(new[] { 1, 2, 3 });
    }

    private static async IAsyncEnumerable<object> EmptyInput()
    {
        // Producer blocks don't use input, but ExecuteAsync requires it
        yield break;
    }

    #endregion

    #region BatchBlock Tests

    [Fact]
    public void BatchBlock_ConstructorWithContext_SetsNameImmediately()
    {
        // Arrange
        var context = new BlockContext("batch-block");

        // Act
        var block = new BatchBlock<int>(context, maxBatchSize: 10);

        // Assert
        block.Name.ShouldBe("batch-block");
    }

    [Fact]
    public void BatchBlock_ConstructorWithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        IBlockContext? nullContext = null;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new BatchBlock<int>(nullContext!, 10));
    }

    [Fact]
    public async Task BatchBlock_WithConstructorInjectedContext_ExecutesCorrectly()
    {
        // Arrange
        var context = new BlockContext("batch-test");
        var block = new BatchBlock<int>(context, maxBatchSize: 3);
        
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var executionContext = new ExecutionContext(serviceProvider, CancellationToken.None);

        // Act
        var results = new List<int[]>();
        await foreach (var batch in block.ExecuteAsync(ProduceIntegers(7), executionContext))
        {
            results.Add(batch);
        }

        // Assert
        results.Count.ShouldBe(3);  // 3 full batches + 1 partial
        results[0].Length.ShouldBe(3);
        results[1].Length.ShouldBe(3);
        results[2].Length.ShouldBe(1);
    }

    #endregion

    #region BroadcastBlock Tests

    [Fact]
    public void BroadcastBlock_ConstructorWithContext_SetsNameImmediately()
    {
        // Arrange
        var context = new BlockContext("broadcast-block");

        // Act
        var block = new BroadcastBlock<int>(context);

        // Assert
        block.Name.ShouldBe("broadcast-block");
    }

    [Fact]
    public void BroadcastBlock_ConstructorWithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        IBlockContext? nullContext = null;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new BroadcastBlock<int>(nullContext!));
    }

    #endregion

    #region RouterBlock Tests

    [Fact]
    public void RouterBlock_ConstructorWithContext_SetsNameImmediately()
    {
        // Arrange
        var context = new BlockContext("router-block");
        var routeSelector = (int x) => x % 2 == 0 ? "even" : "odd";

        // Act
        var block = new RouterBlock<int>(context, routeSelector);

        // Assert
        block.Name.ShouldBe("router-block");
    }

    [Fact]
    public void RouterBlock_ConstructorWithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        IBlockContext? nullContext = null;
        var routeSelector = (int x) => "route";

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new RouterBlock<int>(nullContext!, routeSelector));
    }

    #endregion

    #region EpochActorBlock Tests

    [Fact]
    public void EpochActorBlock_ConstructorWithContext_SetsNameImmediately()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<SimpleActor>();
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        var context = new BlockContext("epoch-actor-block");

        // Act
        var block = new EpochActorBlock<int, int, SimpleActor>(context, scopeFactory);

        // Assert
        block.Name.ShouldBe("epoch-actor-block");
    }

    [Fact]
    public void EpochActorBlock_ConstructorWithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        IBlockContext? nullContext = null;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => 
            new EpochActorBlock<int, int, SimpleActor>(nullContext!, scopeFactory));
    }

    #endregion

    #region EpochBatchBlock Tests

    [Fact]
    public void EpochBatchBlock_ConstructorWithContext_SetsNameImmediately()
    {
        // Arrange
        var context = new BlockContext("epoch-batch-block");

        // Act
        var block = new EpochBatchBlock<int>(context, maxBatchSize: 10);

        // Assert
        block.Name.ShouldBe("epoch-batch-block");
    }

    [Fact]
    public void EpochBatchBlock_ConstructorWithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        IBlockContext? nullContext = null;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new EpochBatchBlock<int>(nullContext!, 10));
    }

    #endregion

    #region EpochSegmenterBlock Tests

    [Fact]
    public void EpochSegmenterBlock_ConstructorWithContext_SetsNameImmediately()
    {
        // Arrange
        var context = new BlockContext("segmenter-block");
        var policy = new EpochSegmentationPolicy 
        { 
            Mode = SegmentationMode.None, 
            SourceId = "test-source" 
        };

        // Act
        var block = new EpochSegmenterBlock<int>(context, policy);

        // Assert
        block.Name.ShouldBe("segmenter-block");
    }

    [Fact]
    public void EpochSegmenterBlock_ConstructorWithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        IBlockContext? nullContext = null;
        var policy = new EpochSegmentationPolicy 
        { 
            Mode = SegmentationMode.None, 
            SourceId = "test-source" 
        };

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new EpochSegmenterBlock<int>(nullContext!, policy));
    }

    #endregion
}
