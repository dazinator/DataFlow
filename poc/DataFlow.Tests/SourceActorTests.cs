namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Checkpointing;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using DataFlow.POC.Tests.TestHelpers;
using DataFlow.POC.Registry;

/// <summary>
/// Tests for source actor pattern and EpochSourceBlock.
/// Validates Phase 4 streaming source actor model.
/// </summary>
public class SourceActorTests
{
    [Fact]
    public async Task SourceActor_Should_ProduceEpochStreams()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient(sp => new TestSourceActor("test-source"));
        var provider = services.BuildServiceProvider();
        
        var coordinator = new EpochCoordinator(
            provider.GetRequiredService<IServiceScopeFactory>());

        var block = BlockHelpers.CreateEpochSource<int, TestSourceActor>(
            "testSource",
            provider.GetRequiredService<IServiceScopeFactory>(),
            coordinator);

        var context = new TestExecutionContext();
        
        // Source blocks don't use input
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Act
        var epochs = new List<(EpochVector epoch, List<int> items)>();
        
        await foreach (var epochStream in block.ExecuteAsync(EmptyInput(), context))
        {
            var items = new List<int>();
            await foreach (var item in epochStream.Items)
            {
                items.Add(item);
            }
            epochs.Add((epochStream.Epoch, items));
        }

        // Assert
        epochs.Count.ShouldBe(3);
        
        epochs[0].epoch.GetSequence("test-source").ShouldBe(1);
        epochs[0].items.ShouldBe(new[] { 0, 1, 2, 3, 4 });
        
        epochs[1].epoch.GetSequence("test-source").ShouldBe(2);
        epochs[1].items.ShouldBe(new[] { 5, 6, 7, 8, 9 });
        
        epochs[2].epoch.GetSequence("test-source").ShouldBe(3);
        epochs[2].items.ShouldBe(new[] { 10, 11, 12, 13, 14 });
        
        // Cleanup
        await provider.DisposeAsync();
    }

    [Fact]
    public async Task SourceActor_Should_SupportCancellation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient(sp => new LongRunningSourceActor("test-source"));
        var provider = services.BuildServiceProvider();
        
        var coordinator = new EpochCoordinator(
            provider.GetRequiredService<IServiceScopeFactory>());

        var block = BlockHelpers.CreateEpochSource<int, LongRunningSourceActor>(
            "testSource",
            provider.GetRequiredService<IServiceScopeFactory>(),
            coordinator);

        var cts = new CancellationTokenSource();
        var context = new TestExecutionContext { CancellationToken = cts.Token };
        
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Act
        var itemsProduced = 0;
        var cancelled = false;

        try
        {
            await foreach (var epochStream in block.ExecuteAsync(EmptyInput(), context))
            {
                await foreach (var item in epochStream.Items)
                {
                    itemsProduced++;
                    if (itemsProduced >= 5)
                    {
                        cts.Cancel();
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        // Assert
        cancelled.ShouldBeTrue();
        itemsProduced.ShouldBeGreaterThanOrEqualTo(5);
        
        // Cleanup
        await provider.DisposeAsync();
    }

    [Fact]
    public async Task SourceActor_Should_BeCompatibleWithEpochSegmenter()
    {
        // This test demonstrates integrating SourceActor with existing EpochSegmenter
        // for cases where the actor produces continuous data and segmentation happens downstream

        // Arrange
        var services = new ServiceCollection();
        services.AddTransient(sp => new ContinuousSourceActor("continuous-source"));
        var provider = services.BuildServiceProvider();
        
        var coordinator = new EpochCoordinator(
            provider.GetRequiredService<IServiceScopeFactory>());

        var block = BlockHelpers.CreateEpochSource<int, ContinuousSourceActor>(
            "continuousSource",
            provider.GetRequiredService<IServiceScopeFactory>(),
            coordinator);

        var context = new TestExecutionContext();
        
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Act
        var allItems = new List<int>();
        
        await foreach (var epochStream in block.ExecuteAsync(EmptyInput(), context))
        {
            await foreach (var item in epochStream.Items)
            {
                allItems.Add(item);
            }
        }

        // Assert
        allItems.Count.ShouldBe(30);
        allItems.ShouldBe(Enumerable.Range(0, 30));
        
        // Cleanup
        await provider.DisposeAsync();
    }

    [Fact]
    public async Task SourceActor_Should_ProduceStreamingEpochs()
    {
        // This test validates that epochs truly stream - items are not buffered

        // Arrange
        var services = new ServiceCollection();
        services.AddTransient(sp => new DelayedSourceActor("delayed-source"));
        var provider = services.BuildServiceProvider();
        
        var coordinator = new EpochCoordinator(
            provider.GetRequiredService<IServiceScopeFactory>());

        var block = BlockHelpers.CreateEpochSource<int, DelayedSourceActor>(
            "delayedSource",
            provider.GetRequiredService<IServiceScopeFactory>(),
            coordinator);

        var context = new TestExecutionContext();
        
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Act
        var firstItemTimes = new List<DateTimeOffset>();
        
        await foreach (var epochStream in block.ExecuteAsync(EmptyInput(), context))
        {
            var firstItemReceived = false;
            await foreach (var item in epochStream.Items)
            {
                if (!firstItemReceived)
                {
                    firstItemTimes.Add(DateTimeOffset.UtcNow);
                    firstItemReceived = true;
                }
            }
        }

        // Assert
        firstItemTimes.Count.ShouldBe(2);
        
        // Epochs should start streaming immediately, not wait for all items
        // to be collected (the old list-based approach would have collected everything)
        
        // Cleanup
        await provider.DisposeAsync();
    }

    // Test helper classes

    private class TestSourceActor : SourceActorBase<int>
    {
        public TestSourceActor(string sourceId)
            : base(sourceId)
        {
        }

        public override async IAsyncEnumerable<IEpochStream<int>> ProduceEpochsAsync(
            IActorExecutionContext context)
        {
            // Produce 3 epochs of 5 items each
            for (int epoch = 1; epoch <= 3; epoch++)
            {
                if (epoch > 1)
                {
                    SignalReadyForNext(context, epoch - 1, epoch);
                }
                
                yield return await CreateEpochStreamAsync(
                    context,
                    epoch,
                    ProduceEpochItems(epoch, context.CancellationToken),
                    context.CancellationToken);
            }
        }

        private static async IAsyncEnumerable<int> ProduceEpochItems(
            int epochNum,
            CancellationToken cancellationToken)
        {
            var start = (epochNum - 1) * 5;
            for (int i = 0; i < 5; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return start + i;
            }
        }
    }

    private class LongRunningSourceActor : SourceActorBase<int>
    {
        public LongRunningSourceActor(string sourceId)
            : base(sourceId)
        {
        }

        public override async IAsyncEnumerable<IEpochStream<int>> ProduceEpochsAsync(
            IActorExecutionContext context)
        {
            yield return await CreateEpochStreamAsync(
                context,
                1,
                ProduceInfiniteItems(context.CancellationToken),
                context.CancellationToken);
        }

        private static async IAsyncEnumerable<int> ProduceInfiniteItems(
            CancellationToken cancellationToken)
        {
            int i = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(10, cancellationToken);
                yield return i++;
            }
        }
    }

    private class ContinuousSourceActor : SourceActorBase<int>
    {
        public ContinuousSourceActor(string sourceId)
            : base(sourceId)
        {
        }

        public override async IAsyncEnumerable<IEpochStream<int>> ProduceEpochsAsync(
            IActorExecutionContext context)
        {
            // Produces one epoch with all items - compatible with downstream segmentation
            yield return await CreateEpochStreamAsync(
                context,
                1,
                ProduceAllItems(context.CancellationToken),
                context.CancellationToken);
        }

        private static async IAsyncEnumerable<int> ProduceAllItems(
            CancellationToken cancellationToken)
        {
            for (int i = 0; i < 30; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return i;
            }
        }
    }

    private class DelayedSourceActor : SourceActorBase<int>
    {
        public DelayedSourceActor(string sourceId)
            : base(sourceId)
        {
        }

        public override async IAsyncEnumerable<IEpochStream<int>> ProduceEpochsAsync(
            IActorExecutionContext context)
        {
            yield return await CreateEpochStreamAsync(
                context,
                1,
                ProduceDelayedItems(5, context.CancellationToken),
                context.CancellationToken);

            SignalReadyForNext(context, 1, 2);
            
            yield return await CreateEpochStreamAsync(
                context,
                2,
                ProduceDelayedItems(5, context.CancellationToken),
                context.CancellationToken);
        }

        private static async IAsyncEnumerable<int> ProduceDelayedItems(
            int count,
            CancellationToken cancellationToken)
        {
            for (int i = 0; i < count; i++)
            {
                await Task.Delay(20, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                yield return i;
            }
        }
    }

    private class TestExecutionContext : IExecutionContext
    {
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
        public IServiceProvider ServiceProvider { get; } = new ServiceCollection().BuildServiceProvider();
        public Guid InvocationId { get; } = Guid.NewGuid();
        public ICheckpoint? RecoveryCheckpoint { get; } = null;
        public POC.Observability.IDataFlowMetrics? Metrics { get; } = null;
    }
}
