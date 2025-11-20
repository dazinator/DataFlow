namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Core;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using System.Runtime.CompilerServices;

/// <summary>
/// Tests for PlainSourceAdapter - enables legacy plain sources in epoch-based architecture.
/// </summary>
public class PlainSourceAdapterTests
{
    /// <summary>
    /// Test that PlainSourceAdapter produces exactly one epoch stream.
    /// </summary>
    [Fact]
    public async Task PlainSourceAdapter_ShouldProduceOneEpochStream()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<SimpleSourceActor>();
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var context = new BlockContext("test-source");
        var adapter = new PlainSourceAdapter<int, SimpleSourceActor>(
            context, 
            scopeFactory, 
            "test-source");

        var executionContext = new ExecutionContext(provider, CancellationToken.None);

        // Act
        var emptyInput = EmptyAsyncEnumerable();
        var epochStreams = adapter.ExecuteAsync(emptyInput, executionContext);
        var epochs = new List<IEpochStream<int>>();
        await foreach (var epoch in epochStreams)
        {
            epochs.Add(epoch);
        }

        // Assert
        epochs.Count.ShouldBe(1, "Should produce exactly one epoch stream");
    }

    /// <summary>
    /// Test that all items from plain source are included in the epoch.
    /// </summary>
    [Fact]
    public async Task PlainSourceAdapter_ShouldContainAllSourceItems()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<SimpleSourceActor>();
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var context = new BlockContext("test-source");
        var adapter = new PlainSourceAdapter<int, SimpleSourceActor>(
            context, 
            scopeFactory, 
            "test-source");

        var executionContext = new ExecutionContext(provider, CancellationToken.None);

        // Act
        var emptyInput = EmptyAsyncEnumerable();
        var epochStreams = adapter.ExecuteAsync(emptyInput, executionContext);
        IEpochStream<int>? epoch = null;
        await foreach (var e in epochStreams)
        {
            epoch = e;
        }

        var items = new List<int>();
        await foreach (var item in epoch!.Items)
        {
            items.Add(item);
        }

        // Assert
        var expectedItems = Enumerable.Range(1, 10).ToList();
        items.ShouldBe(expectedItems, "All items from source should be in the epoch");
    }

    /// <summary>
    /// Test that the epoch vector has correct source name and sequence.
    /// </summary>
    [Fact]
    public async Task PlainSourceAdapter_ShouldHaveCorrectEpochVector()
    {
        // Arrange
        var sourceName = "my-plain-source";
        var services = new ServiceCollection();
        services.AddSingleton<SimpleSourceActor>();
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var context = new BlockContext(sourceName);
        var adapter = new PlainSourceAdapter<int, SimpleSourceActor>(
            context, 
            scopeFactory, 
            sourceName);

        var executionContext = new ExecutionContext(provider, CancellationToken.None);

        // Act
        var emptyInput = EmptyAsyncEnumerable();
        var epochStreams = adapter.ExecuteAsync(emptyInput, executionContext);
        IEpochStream<int>? epoch = null;
        await foreach (var e in epochStreams)
        {
            epoch = e;
        }

        // Assert
        epoch.ShouldNotBeNull();
        epoch.Epoch.ShouldNotBeNull();
        epoch.Epoch.GetSequence(sourceName).ShouldBe(1L, "Should have sequence number 1");
        epoch.Epoch.Sequences.Count.ShouldBe(1, "Should have exactly one source");
    }

    /// <summary>
    /// Test that actor is resolved from DI scope.
    /// </summary>
    [Fact]
    public async Task PlainSourceAdapter_ShouldResolveActorFromDI()
    {
        // Arrange
        ScopedSourceActor.InstanceCount = 0; // Reset counter
        var services = new ServiceCollection();
        services.AddScoped<ScopedSourceActor>();
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var context = new BlockContext("test-source");
        var adapter = new PlainSourceAdapter<int, ScopedSourceActor>(
            context, 
            scopeFactory, 
            "test-source");

        var executionContext = new ExecutionContext(provider, CancellationToken.None);

        // Act
        var emptyInput = EmptyAsyncEnumerable();
        var epochStreams = adapter.ExecuteAsync(emptyInput, executionContext);
        IEpochStream<int>? epoch = null;
        await foreach (var e in epochStreams)
        {
            epoch = e;
        }

        var items = new List<int>();
        await foreach (var item in epoch!.Items)
        {
            items.Add(item);
        }

        // Assert
        items.ShouldNotBeEmpty("Should successfully resolve and execute scoped actor");
        ScopedSourceActor.InstanceCount.ShouldBe(1, "Should create exactly one scoped instance");
    }

    /// <summary>
    /// Test that empty plain source produces empty epoch.
    /// </summary>
    [Fact]
    public async Task PlainSourceAdapter_EmptySource_ShouldProduceEmptyEpoch()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<EmptySourceActor>();
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var context = new BlockContext("empty-source");
        var adapter = new PlainSourceAdapter<int, EmptySourceActor>(
            context, 
            scopeFactory, 
            "empty-source");

        var executionContext = new ExecutionContext(provider, CancellationToken.None);

        // Act
        var emptyInput = EmptyAsyncEnumerable();
        var epochStreams = adapter.ExecuteAsync(emptyInput, executionContext);
        IEpochStream<int>? epoch = null;
        await foreach (var e in epochStreams)
        {
            epoch = e;
        }

        var items = new List<int>();
        await foreach (var item in epoch!.Items)
        {
            items.Add(item);
        }

        // Assert
        items.ShouldBeEmpty("Empty source should produce epoch with no items");
    }

    /// <summary>
    /// Test that null scopeFactory throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void PlainSourceAdapter_NullScopeFactory_ShouldThrow()
    {
        // Arrange
        var context = new BlockContext("test");

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
        {
            new PlainSourceAdapter<int, SimpleSourceActor>(context, null!, "test");
        });
    }

    /// <summary>
    /// Test that null source name throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void PlainSourceAdapter_NullSourceName_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var context = new BlockContext("test");

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
        {
            new PlainSourceAdapter<int, SimpleSourceActor>(context, scopeFactory, null!);
        });
    }

    // Test Actor Implementations

    private class SimpleSourceActor : IPlainSourceActor<int>
    {
        public async IAsyncEnumerable<int> ProduceAsync(
            IActorExecutionContext context)
        {
            for (int i = 1; i <= 10; i++)
            {
                yield return i;
            }
        }
    }

    private class ScopedSourceActor : IPlainSourceActor<int>
    {
        public static int InstanceCount { get; set; }

        public ScopedSourceActor()
        {
            InstanceCount++;
        }

        public async IAsyncEnumerable<int> ProduceAsync(
            IActorExecutionContext context)
        {
            for (int i = 1; i <= 5; i++)
            {
                yield return i;
            }
        }
    }

    private class InfiniteSourceActor : IPlainSourceActor<int>
    {
        public async IAsyncEnumerable<int> ProduceAsync(
            IActorExecutionContext context)
        {
            int i = 0;
            while (!context.CancellationToken.IsCancellationRequested)
            {
                yield return i++;
                await Task.Delay(1, context.CancellationToken);
            }
        }
    }

    private class EmptySourceActor : IPlainSourceActor<int>
    {
        public async IAsyncEnumerable<int> ProduceAsync(
            IActorExecutionContext context)
        {
            yield break;
        }
    }

    // Helper methods
    private static async IAsyncEnumerable<object> EmptyAsyncEnumerable()
    {
        yield break;
    }
}
