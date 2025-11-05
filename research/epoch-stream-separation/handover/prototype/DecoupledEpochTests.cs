namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

/// <summary>
/// PROTOTYPE: Tests for decoupled epoch segmentation design.
/// This explores separating epoch concerns from source blocks.
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

        var sourceBlock = new PlainSourceBlock<int, SimpleNumberProducer>(
            "plain-source",
            provider.GetRequiredService<IServiceScopeFactory>());

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

        var sourceBlock = new PlainSourceBlock<int, SimpleNumberProducer>(
            "plain-source",
            provider.GetRequiredService<IServiceScopeFactory>());
            
        var segmenterBlock = new EpochSegmenterBlock<int>(
            "segmenter",
            EpochSegmentationPolicy.ByCount(3, "test-source"));

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
    public async Task PlainSource_WithNoSegmentation_ProducesSingleEpoch()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<SimpleNumberProducer>();
        var provider = services.BuildServiceProvider();

        var sourceBlock = new PlainSourceBlock<int, SimpleNumberProducer>(
            "plain-source",
            provider.GetRequiredService<IServiceScopeFactory>());
            
        var segmenterBlock = new EpochSegmenterBlock<int>(
            "segmenter",
            EpochSegmentationPolicy.None);

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
        epochs.Count.ShouldBe(1); // All items in one epoch
        epochs[0].epoch.GetSequence("default-source").ShouldBe(1);
        epochs[0].items.Count.ShouldBe(10);
        epochs[0].items.ShouldBe(Enumerable.Range(0, 10));
    }

    [Fact]
    public async Task PlainSource_WithKeySegmenter_GroupsByKey()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<AlternatingTypeProducer>();
        var provider = services.BuildServiceProvider();

        var sourceBlock = new PlainSourceBlock<(string type, int value), AlternatingTypeProducer>(
            "plain-source",
            provider.GetRequiredService<IServiceScopeFactory>());
            
        var segmenterBlock = new EpochSegmenterBlock<(string type, int value)>(
            "segmenter",
            EpochSegmentationPolicy.ByKey<(string type, int value), string>(
                item => item.type,
                "test-source"));

        var context = new TestExecutionContext();
        
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Act
        var epochs = new List<(EpochVector epoch, List<(string type, int value)> items)>();
        
        var plainItems = sourceBlock.ExecuteAsync(EmptyInput(), context);
        var epochStreams = segmenterBlock.ExecuteAsync(plainItems, context);
        
        await foreach (var epochStream in epochStreams)
        {
            var items = new List<(string type, int value)>();
            await foreach (var item in epochStream.Items)
            {
                items.Add(item);
            }
            epochs.Add((epochStream.Epoch, items));
        }

        // Assert
        epochs.Count.ShouldBeGreaterThan(1); // Should have multiple epochs based on type changes
        
        // Each epoch should have items of the same type
        foreach (var (epoch, items) in epochs)
        {
            var distinctTypes = items.Select(i => i.type).Distinct().ToList();
            distinctTypes.Count.ShouldBe(1, $"Epoch {epoch} should have items of only one type");
        }
    }

    [Fact]
    public async Task PlainSource_WithClockSegmenter_SegmentsByClockChanges()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ClockAwareProducer>();
        var provider = services.BuildServiceProvider();
        
        var clock = new ManualEpochClock();
        clock.SetEpoch(EpochVector.FromSingleSource("clock-source", 1));

        var sourceBlock = new PlainSourceBlock<int, ClockAwareProducer>(
            "plain-source",
            provider.GetRequiredService<IServiceScopeFactory>());
            
        var segmenterBlock = new EpochSegmenterBlock<int>(
            "segmenter",
            EpochSegmentationPolicy.ByClock(clock, "clock-source"));

        var context = new TestExecutionContext();
        
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Act
        var epochs = new List<(EpochVector epoch, List<int> items)>();
        
        var plainItems = sourceBlock.ExecuteAsync(EmptyInput(), context);
        var epochStreams = segmenterBlock.ExecuteAsync(plainItems, context);
        
        // Note: This is a simplified test - in reality, clock would be advanced
        // by external trigger, not controlled by the producer
        await foreach (var epochStream in epochStreams)
        {
            var items = new List<int>();
            await foreach (var item in epochStream.Items)
            {
                items.Add(item);
                
                // Simulate clock advance after some items
                if (item == 2 || item == 5)
                {
                    clock.AdvanceEpoch("clock-source");
                }
            }
            epochs.Add((epochStream.Epoch, items));
        }

        // Assert
        epochs.Count.ShouldBeGreaterThan(1);
    }

    [Fact]
    public async Task CompareSourceCentricVsDecoupled_SameResults()
    {
        // This test demonstrates that both approaches produce identical results
        
        // Arrange - Source-Centric Approach
        var services1 = new ServiceCollection();
        services1.AddTransient<SourceCentricEpochProducer>();
        var provider1 = services1.BuildServiceProvider();
        
        var sourceCentricBlock = new EpochSourceBlock<int, SourceCentricEpochProducer>(
            "source-centric",
            provider1.GetRequiredService<IServiceScopeFactory>());

        // Arrange - Decoupled Approach
        var services2 = new ServiceCollection();
        services2.AddTransient<SimpleNumberProducer>();
        var provider2 = services2.BuildServiceProvider();
        
        var plainSourceBlock = new PlainSourceBlock<int, SimpleNumberProducer>(
            "plain-source",
            provider2.GetRequiredService<IServiceScopeFactory>());
            
        var segmenterBlock = new EpochSegmenterBlock<int>(
            "segmenter",
            EpochSegmentationPolicy.ByCount(3, "test-source"));

        var context = new TestExecutionContext();
        
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Act - Source-Centric
        var sourceCentricEpochs = new List<(EpochVector epoch, List<int> items)>();
        await foreach (var epochStream in sourceCentricBlock.ExecuteAsync(EmptyInput(), context))
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
        var plainItems = plainSourceBlock.ExecuteAsync(EmptyInput(), context);
        var epochStreams = segmenterBlock.ExecuteAsync(plainItems, context);
        await foreach (var epochStream in epochStreams)
        {
            var items = new List<int>();
            await foreach (var item in epochStream.Items)
            {
                items.Add(item);
            }
            decoupledEpochs.Add((epochStream.Epoch, items));
        }

        // Assert - Both produce same epoch structure and data
        sourceCentricEpochs.Count.ShouldBe(decoupledEpochs.Count);
        
        for (int i = 0; i < sourceCentricEpochs.Count; i++)
        {
            sourceCentricEpochs[i].epoch.GetSequence("test-source")
                .ShouldBe(decoupledEpochs[i].epoch.GetSequence("test-source"));
            sourceCentricEpochs[i].items.ShouldBe(decoupledEpochs[i].items);
        }
    }

    // Test Helper Classes

    private class TestExecutionContext : IExecutionContext
    {
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
        public IServiceProvider ServiceProvider { get; } = new ServiceCollection().BuildServiceProvider();
        public Guid InvocationId { get; } = Guid.NewGuid();
    }

    private class SimpleNumberProducer : PlainSourceActorBase<int>
    {
        public override async IAsyncEnumerable<int> ProduceAsync(IActorExecutionContext context)
        {
            for (int i = 0; i < 10; i++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                yield return i;
            }
        }
    }

    private class AlternatingTypeProducer : PlainSourceActorBase<(string type, int value)>
    {
        public override async IAsyncEnumerable<(string type, int value)> ProduceAsync(IActorExecutionContext context)
        {
            // Produces: A, A, A, B, B, B, A, A, C, C
            var pattern = new[] { "A", "A", "A", "B", "B", "B", "A", "A", "C", "C" };
            for (int i = 0; i < pattern.Length; i++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                yield return (pattern[i], i);
            }
        }
    }

    private class ClockAwareProducer : PlainSourceActorBase<int>
    {
        public override async IAsyncEnumerable<int> ProduceAsync(IActorExecutionContext context)
        {
            for (int i = 0; i < 10; i++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(1, context.CancellationToken); // Small delay to allow clock changes
                yield return i;
            }
        }
    }

    private class SourceCentricEpochProducer : SourceActorBase<int>
    {
        public override async IAsyncEnumerable<IEpochStream<int>> ProduceEpochsAsync(IActorExecutionContext context)
        {
            // Mimics count-based segmentation (3 items per epoch)
            long sequence = 1;
            var allItems = Enumerable.Range(0, 10).ToList();
            
            for (int start = 0; start < allItems.Count; start += 3)
            {
                var epochItems = allItems.Skip(start).Take(3).ToList();
                yield return CreateEpochStream(
                    CreateEpoch("test-source", sequence++),
                    epochItems.ToAsyncEnumerable());
            }
        }
    }
}
