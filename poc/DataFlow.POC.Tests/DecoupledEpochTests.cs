namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Checkpointing;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Runtime.CompilerServices;
using Xunit;
using DataFlow.POC.Tests.TestHelpers;

/// <summary>
/// Tests for decoupled epoch segmentation design.
/// This validates that sources can emit plain data streams and have epochs
/// applied externally via EpochSegmenterBlock.
/// </summary>
public class DecoupledEpochTests
{
    [Fact]
    public async Task PlainSource_WithoutSegmenter_ProducesContinuousStream()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<SimpleNumberProducer>();
        var provider = services.BuildServiceProvider();

        var sourceBlock = BlockHelpers.CreatePlainSource<int, SimpleNumberProducer>("plain-source", provider.GetRequiredService<IServiceScopeFactory>());

        var context = new TestExecutionContext();
        
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Act
        var items = new List<int>();
        await foreach (var item in sourceBlock.ExecuteAsync(EmptyInput(), context))
        {
            items.Add(item);
        }

        // Assert
        items.Count.ShouldBe(10);
        items.ShouldBe(Enumerable.Range(0, 10));
    }

    [Fact]
    public async Task PlainSource_WithCountSegmenter_ProducesEpochs()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<SimpleNumberProducer>();
        var provider = services.BuildServiceProvider();

        var sourceBlock = BlockHelpers.CreatePlainSource<int, SimpleNumberProducer>("plain-source", provider.GetRequiredService<IServiceScopeFactory>());
            
        var segmenterBlock = BlockHelpers.CreateEpochSegmenter<int>("segmenter", EpochSegmentationPolicy.ByCount(3, "test-source"));

        var context = new TestExecutionContext();
        
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Act
        var epochs = new List<(EpochVector epoch, List<int> items)>();
        
        // Pipeline: PlainSource → Segmenter
        var plainItems = sourceBlock.ExecuteAsync(EmptyInput(), context);
        var epochStreams = segmenterBlock.ExecuteAsync(plainItems, context);
        
        await foreach (var epochStream in epochStreams)
        {
            var items = new List<int>();
            await foreach (var item in epochStream.Items)
            {
                items.Add(item);
            }
            epochs.Add((epochStream.Epoch, items));
        }

        // Assert
        epochs.Count.ShouldBe(4); // 10 items / 3 per epoch = 4 epochs (3+3+3+1)
        
        epochs[0].epoch.GetSequence("test-source").ShouldBe(1);
        epochs[0].items.ShouldBe(new[] { 0, 1, 2 });
        
        epochs[1].epoch.GetSequence("test-source").ShouldBe(2);
        epochs[1].items.ShouldBe(new[] { 3, 4, 5 });
        
        epochs[2].epoch.GetSequence("test-source").ShouldBe(3);
        epochs[2].items.ShouldBe(new[] { 6, 7, 8 });
        
        epochs[3].epoch.GetSequence("test-source").ShouldBe(4);
        epochs[3].items.ShouldBe(new[] { 9 });
    }

    [Fact]
    public async Task PlainSource_WithKeySegmenter_SegmentsByKey()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<GroupedDataProducer>();
        var provider = services.BuildServiceProvider();

        var sourceBlock = BlockHelpers.CreatePlainSource<(string group, int value), GroupedDataProducer>("plain-source", provider.GetRequiredService<IServiceScopeFactory>());
            
        var segmenterBlock = BlockHelpers.CreateEpochSegmenter<(string group, int value)>("segmenter", EpochSegmentationPolicy.ByKey<(string group, int value), string>(
                item => item.group,
                "test-source"));

        var context = new TestExecutionContext();
        
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Act
        var epochs = new List<(EpochVector epoch, List<(string, int)> items)>();
        
        var plainItems = sourceBlock.ExecuteAsync(EmptyInput(), context);
        var epochStreams = segmenterBlock.ExecuteAsync(plainItems, context);
        
        await foreach (var epochStream in epochStreams)
        {
            var items = new List<(string, int)>();
            await foreach (var item in epochStream.Items)
            {
                items.Add(item);
            }
            epochs.Add((epochStream.Epoch, items));
        }

        // Assert
        epochs.Count.ShouldBe(3); // A, B, A groups
        
        epochs[0].items.All(i => i.Item1 == "A").ShouldBeTrue();
        epochs[1].items.All(i => i.Item1 == "B").ShouldBeTrue();
        epochs[2].items.All(i => i.Item1 == "A").ShouldBeTrue();
    }

    [Fact]
    public async Task PlainSource_WithNonePolicy_CreatesSingleEpoch()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<SimpleNumberProducer>();
        var provider = services.BuildServiceProvider();

        var sourceBlock = BlockHelpers.CreatePlainSource<int, SimpleNumberProducer>("plain-source", provider.GetRequiredService<IServiceScopeFactory>());
            
        var segmenterBlock = BlockHelpers.CreateEpochSegmenter<int>("segmenter", EpochSegmentationPolicy.None);

        var context = new TestExecutionContext();
        
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Act
        var epochs = new List<(EpochVector epoch, List<int> items)>();
        
        var plainItems = sourceBlock.ExecuteAsync(EmptyInput(), context);
        var epochStreams = segmenterBlock.ExecuteAsync(plainItems, context);
        
        await foreach (var epochStream in epochStreams)
        {
            var items = new List<int>();
            await foreach (var item in epochStream.Items)
            {
                items.Add(item);
            }
            epochs.Add((epochStream.Epoch, items));
        }

        // Assert
        epochs.Count.ShouldBe(1);
        epochs[0].items.Count.ShouldBe(10);
        epochs[0].items.ShouldBe(Enumerable.Range(0, 10));
    }

    [Fact]
    public async Task PlainSource_WithEmptyInput_ProducesNoEpochs()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<EmptyProducer>();
        var provider = services.BuildServiceProvider();

        var sourceBlock = BlockHelpers.CreatePlainSource<int, EmptyProducer>("plain-source", provider.GetRequiredService<IServiceScopeFactory>());
            
        var segmenterBlock = BlockHelpers.CreateEpochSegmenter<int>("segmenter", EpochSegmentationPolicy.ByCount(3, "test-source"));

        var context = new TestExecutionContext();
        
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Act
        var epochs = new List<EpochVector>();
        
        var plainItems = sourceBlock.ExecuteAsync(EmptyInput(), context);
        var epochStreams = segmenterBlock.ExecuteAsync(plainItems, context);
        
        await foreach (var epochStream in epochStreams)
        {
            epochs.Add(epochStream.Epoch);
        }

        // Assert
        epochs.Count.ShouldBe(0);
    }

    [Fact]
    public async Task DecoupledDesign_ProducesIdenticalResults_ToSourceCentric()
    {
        // This test validates functional equivalence between the two approaches
        
        // Arrange - Source-centric approach
        var services1 = new ServiceCollection();
        services1.AddSingleton<IEpochCoordinator>(sp => 
            new EpochCoordinator(sp.GetRequiredService<IServiceScopeFactory>()));
        services1.AddTransient(sp => new SourceCentricNumberProducer(
            sp.GetRequiredService<IEpochCoordinator>(),
            "test-source"));
        var provider1 = services1.BuildServiceProvider();

        var sourceCentricBlock = new EpochSourceBlock<int, SourceCentricNumberProducer>(
            new BlockContext("source-centric"),
            provider1.GetRequiredService<IServiceScopeFactory>());

        // Arrange - Decoupled approach
        var services2 = new ServiceCollection();
        services2.AddTransient<SimpleNumberProducer>();
        var provider2 = services2.BuildServiceProvider();

        var plainSourceBlock = BlockHelpers.CreatePlainSource<int, SimpleNumberProducer>("plain-source", provider2.GetRequiredService<IServiceScopeFactory>());
            
        var segmenterBlock = BlockHelpers.CreateEpochSegmenter<int>("segmenter", EpochSegmentationPolicy.ByCount(3, "test-source"));

        var context1 = new TestExecutionContext();
        var context2 = new TestExecutionContext();
        
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Act - Source-centric
        var sourceCentricEpochs = new List<(EpochVector epoch, List<int> items)>();
        await foreach (var epochStream in sourceCentricBlock.ExecuteAsync(EmptyInput(), context1))
        {
            var items = new List<int>();
            await foreach (var item in epochStream.Items)
            {
                items.Add(item);
            }
            sourceCentricEpochs.Add((epochStream.Epoch, items));
        }

        // Act - Decoupled
        var decoupledEpochs = new List<(EpochVector epoch, List<int> items)>();
        var plainItems = plainSourceBlock.ExecuteAsync(EmptyInput(), context2);
        var epochStreams = segmenterBlock.ExecuteAsync(plainItems, context2);
        
        await foreach (var epochStream in epochStreams)
        {
            var items = new List<int>();
            await foreach (var item in epochStream.Items)
            {
                items.Add(item);
            }
            decoupledEpochs.Add((epochStream.Epoch, items));
        }

        // Assert - Same number of epochs
        decoupledEpochs.Count.ShouldBe(sourceCentricEpochs.Count);
        
        // Assert - Same epoch sequences
        for (int i = 0; i < decoupledEpochs.Count; i++)
        {
            decoupledEpochs[i].epoch.GetSequence("test-source")
                .ShouldBe(sourceCentricEpochs[i].epoch.GetSequence("test-source"));
        }
        
        // Assert - Same items in each epoch
        for (int i = 0; i < decoupledEpochs.Count; i++)
        {
            decoupledEpochs[i].items.ShouldBe(sourceCentricEpochs[i].items);
        }
        
        // Cleanup
        await provider1.DisposeAsync();
        await provider2.DisposeAsync();
    }

    // Test helper classes

    private class SimpleNumberProducer : PlainSourceActorBase<int>
    {
        public override async IAsyncEnumerable<int> ProduceAsync(
            [EnumeratorCancellation] IActorExecutionContext context)
        {
            for (int i = 0; i < 10; i++)
            {
                yield return i;
            }
        }
    }

    private class EmptyProducer : PlainSourceActorBase<int>
    {
        public override async IAsyncEnumerable<int> ProduceAsync(
            [EnumeratorCancellation] IActorExecutionContext context)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private class GroupedDataProducer : PlainSourceActorBase<(string group, int value)>
    {
        public override async IAsyncEnumerable<(string group, int value)> ProduceAsync(
            [EnumeratorCancellation] IActorExecutionContext context)
        {
            // Produce data: A, A, B, B, A
            yield return ("A", 1);
            yield return ("A", 2);
            yield return ("B", 3);
            yield return ("B", 4);
            yield return ("A", 5);
        }
    }

    private class SourceCentricNumberProducer : SourceActorBase<int>
    {
        public SourceCentricNumberProducer(IEpochCoordinator coordinator, string sourceId)
            : base(coordinator, sourceId)
        {
        }

        public override async IAsyncEnumerable<IEpochStream<int>> ProduceEpochsAsync(
            [EnumeratorCancellation] IActorExecutionContext context)
        {
            // Mimic count-based segmentation with 3 items per epoch
            var data = Enumerable.Range(0, 10).ToList();
            
            for (int epochIndex = 0; epochIndex * 3 < data.Count; epochIndex++)
            {
                if (epochIndex > 0)
                {
                    SignalReadyForNext(epochIndex, epochIndex + 1);
                }
                
                var epochData = data.Skip(epochIndex * 3).Take(3).ToList();
                yield return await CreateEpochStreamAsync(
                    epochIndex + 1,
                    epochData.ToAsyncEnumerable(),
                    context.CancellationToken);
            }
        }
    }

    private class TestExecutionContext : IExecutionContext
    {
        public CancellationToken CancellationToken { get; } = CancellationToken.None;
        public IServiceProvider ServiceProvider { get; } = new ServiceCollection().BuildServiceProvider();
        public Guid InvocationId { get; } = Guid.NewGuid();
        public ICheckpoint? RecoveryCheckpoint { get; } = null;
    }
}
