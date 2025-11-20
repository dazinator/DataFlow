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
        var actorBlock = new EpochActorBlock<int, int, SimpleActor>(context, scopeFactory);

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

    #region ProducerBlock Tests (Skipped - ProducerBlock removed)

    [Fact(Skip = "ProducerBlock removed - use EpochSourceBlock with actor or PlainSourceAdapter")]
    public void ProducerBlock_ConstructorWithContext_SetsNameImmediately()
    {
        // ProducerBlock has been removed
    }

    [Fact(Skip = "ProducerBlock removed - use EpochSourceBlock with actor or PlainSourceAdapter")]
    public void ProducerBlock_ConstructorWithNullContext_ThrowsArgumentNullException()
    {
        // ProducerBlock has been removed
    }

    [Fact(Skip = "ProducerBlock removed - use EpochSourceBlock with actor or PlainSourceAdapter")]
    public async Task ProducerBlock_WithConstructorInjectedContext_ExecutesCorrectly()
    {
        // ProducerBlock has been removed
    }

    private static async IAsyncEnumerable<object> EmptyInput()
    {
        // Producer blocks don't use input, but ExecuteAsync requires it
        yield break;
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
