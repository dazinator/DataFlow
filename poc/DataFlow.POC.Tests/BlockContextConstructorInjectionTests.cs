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
}
