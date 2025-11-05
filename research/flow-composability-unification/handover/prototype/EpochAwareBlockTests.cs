namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Runtime.CompilerServices;
using Xunit;

/// <summary>
/// Tests for epoch-aware blocks that enable composability between plain and epoch streams.
/// These blocks use the ActorBlock pattern to provide DI scope safety by default.
/// </summary>
public class EpochAwareBlockTests
{
    [Fact]
    public async Task EpochActorBlock_Should_TransformItemsWithinEpochs()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<SimpleNumberProducer>();
        services.AddTransient<NumberToStringActor>();
        var provider = services.BuildServiceProvider();

        // Pipeline: PlainSource → Segmenter → EpochActorBlock
        var sourceBlock = new PlainSourceBlock<int, SimpleNumberProducer>(
            "plain-source",
            provider.GetRequiredService<IServiceScopeFactory>());
            
        var segmenterBlock = new EpochSegmenterBlock<int>(
            "segmenter",
            EpochSegmentationPolicy.ByCount(3, "test-source"));
            
        var actorBlock = new EpochActorBlock<int, string, NumberToStringActor>(
            "actor",
            provider.GetRequiredService<IServiceScopeFactory>());

        var context = new TestExecutionContext();
        
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Act
        var plainItems = sourceBlock.ExecuteAsync(EmptyInput(), context);
        var epochStreams = segmenterBlock.ExecuteAsync(plainItems, context);
        var transformedEpochs = actorBlock.ExecuteAsync(epochStreams, context);
        
        var epochs = new List<(EpochVector epoch, List<string> items)>();
        await foreach (var epochStream in transformedEpochs)
        {
            var items = new List<string>();
            await foreach (var item in epochStream.Items)
            {
                items.Add(item);
            }
            epochs.Add((epochStream.Epoch, items));
        }

        // Assert
        epochs.Count.ShouldBe(4); // 10 items / 3 per epoch = 4 epochs
        
        epochs[0].items.ShouldBe(new[] { "Item-0", "Item-1", "Item-2" });
        epochs[1].items.ShouldBe(new[] { "Item-3", "Item-4", "Item-5" });
        epochs[2].items.ShouldBe(new[] { "Item-6", "Item-7", "Item-8" });
        epochs[3].items.ShouldBe(new[] { "Item-9" });
    }

    [Fact]
    public async Task SimpleEpochTransformerBlock_Should_TransformItemsWithinEpochs()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<SimpleNumberProducer>();
        var provider = services.BuildServiceProvider();

        // Pipeline: PlainSource → Segmenter → SimpleEpochTransformer
        var sourceBlock = new PlainSourceBlock<int, SimpleNumberProducer>(
            "plain-source",
            provider.GetRequiredService<IServiceScopeFactory>());
            
        var segmenterBlock = new EpochSegmenterBlock<int>(
            "segmenter",
            EpochSegmentationPolicy.ByCount(5, "test-source"));
            
        var transformerBlock = new SimpleEpochTransformerBlock<int, int>(
            "transformer",
            item => item * 10);

        var context = new TestExecutionContext();
        
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Act
        var plainItems = sourceBlock.ExecuteAsync(EmptyInput(), context);
        var epochStreams = segmenterBlock.ExecuteAsync(plainItems, context);
        var transformedEpochs = transformerBlock.ExecuteAsync(epochStreams, context);
        
        var epochs = new List<(EpochVector epoch, List<int> items)>();
        await foreach (var epochStream in transformedEpochs)
        {
            var items = new List<int>();
            await foreach (var item in epochStream.Items)
            {
                items.Add(item);
            }
            epochs.Add((epochStream.Epoch, items));
        }

        // Assert
        epochs.Count.ShouldBe(2); // 10 items / 5 per epoch = 2 epochs
        
        epochs[0].items.ShouldBe(new[] { 0, 10, 20, 30, 40 });
        epochs[1].items.ShouldBe(new[] { 50, 60, 70, 80, 90 });
    }

    [Fact]
    public async Task EpochProcessorBlock_Should_ProcessItemsWithinEpochs()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<SimpleNumberProducer>();
        var provider = services.BuildServiceProvider();

        var processedItems = new List<int>();
        var processedEpochs = new List<EpochVector>();

        // Pipeline: PlainSource → Segmenter → EpochProcessor
        var sourceBlock = new PlainSourceBlock<int, SimpleNumberProducer>(
            "plain-source",
            provider.GetRequiredService<IServiceScopeFactory>());
            
        var segmenterBlock = new EpochSegmenterBlock<int>(
            "segmenter",
            EpochSegmentationPolicy.ByCount(4, "test-source"));
            
        var processorBlock = new EpochProcessorBlock<int>(
            "processor",
            async (item, ctx) =>
            {
                processedItems.Add(item);
                await Task.CompletedTask;
            });

        var context = new TestExecutionContext();
        
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Act
        var plainItems = sourceBlock.ExecuteAsync(EmptyInput(), context);
        var epochStreams = segmenterBlock.ExecuteAsync(plainItems, context);
        
        // Track epochs being processed
        var trackedEpochs = TrackEpochs(epochStreams, processedEpochs);
        
        var processorOutput = processorBlock.ExecuteAsync(trackedEpochs, context);
        
        // Consume the output (terminal block produces no items)
        await foreach (var _ in processorOutput)
        {
            // Should not produce any output
        }

        // Assert
        processedItems.Count.ShouldBe(10);
        processedItems.ShouldBe(Enumerable.Range(0, 10));
        processedEpochs.Count.ShouldBe(3); // 10 items / 4 per epoch = 3 epochs (4+4+2)
    }

    [Fact]
    public async Task EpochBatchBlock_Should_BatchItemsWithinEpochs()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<SimpleNumberProducer>();
        var provider = services.BuildServiceProvider();

        // Pipeline: PlainSource → Segmenter → EpochBatchBlock
        var sourceBlock = new PlainSourceBlock<int, SimpleNumberProducer>(
            "plain-source",
            provider.GetRequiredService<IServiceScopeFactory>());
            
        var segmenterBlock = new EpochSegmenterBlock<int>(
            "segmenter",
            EpochSegmentationPolicy.ByCount(5, "test-source"));
            
        var batchBlock = new EpochBatchBlock<int>(
            "batcher",
            maxBatchSize: 2);

        var context = new TestExecutionContext();
        
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Act
        var plainItems = sourceBlock.ExecuteAsync(EmptyInput(), context);
        var epochStreams = segmenterBlock.ExecuteAsync(plainItems, context);
        var batchedEpochs = batchBlock.ExecuteAsync(epochStreams, context);
        
        var epochs = new List<(EpochVector epoch, List<int[]> batches)>();
        await foreach (var epochStream in batchedEpochs)
        {
            var batches = new List<int[]>();
            await foreach (var batch in epochStream.Items)
            {
                batches.Add(batch);
            }
            epochs.Add((epochStream.Epoch, batches));
        }

        // Assert
        epochs.Count.ShouldBe(2); // 10 items / 5 per epoch = 2 epochs
        
        // Epoch 1: items [0,1,2,3,4] → batches [[0,1], [2,3], [4]]
        epochs[0].batches.Count.ShouldBe(3);
        epochs[0].batches[0].ShouldBe(new[] { 0, 1 });
        epochs[0].batches[1].ShouldBe(new[] { 2, 3 });
        epochs[0].batches[2].ShouldBe(new[] { 4 });
        
        // Epoch 2: items [5,6,7,8,9] → batches [[5,6], [7,8], [9]]
        epochs[1].batches.Count.ShouldBe(3);
        epochs[1].batches[0].ShouldBe(new[] { 5, 6 });
        epochs[1].batches[1].ShouldBe(new[] { 7, 8 });
        epochs[1].batches[2].ShouldBe(new[] { 9 });
    }

    [Fact]
    public async Task EpochBatchBlock_Should_NotSpanBatchesAcrossEpochs()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<SimpleNumberProducer>();
        var provider = services.BuildServiceProvider();

        // Pipeline: PlainSource → Segmenter → EpochBatchBlock
        var sourceBlock = new PlainSourceBlock<int, SimpleNumberProducer>(
            "plain-source",
            provider.GetRequiredService<IServiceScopeFactory>());
            
        var segmenterBlock = new EpochSegmenterBlock<int>(
            "segmenter",
            EpochSegmentationPolicy.ByCount(3, "test-source"));
            
        var batchBlock = new EpochBatchBlock<int>(
            "batcher",
            maxBatchSize: 10); // Large batch size - should still break at epoch boundaries

        var context = new TestExecutionContext();
        
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Act
        var plainItems = sourceBlock.ExecuteAsync(EmptyInput(), context);
        var epochStreams = segmenterBlock.ExecuteAsync(plainItems, context);
        var batchedEpochs = batchBlock.ExecuteAsync(epochStreams, context);
        
        var epochs = new List<(EpochVector epoch, List<int[]> batches)>();
        await foreach (var epochStream in batchedEpochs)
        {
            var batches = new List<int[]>();
            await foreach (var batch in epochStream.Items)
            {
                batches.Add(batch);
            }
            epochs.Add((epochStream.Epoch, batches));
        }

        // Assert
        epochs.Count.ShouldBe(4); // 10 items / 3 per epoch = 4 epochs
        
        // Each epoch should have exactly one batch (not spanning across epochs)
        epochs[0].batches.Count.ShouldBe(1);
        epochs[0].batches[0].ShouldBe(new[] { 0, 1, 2 });
        
        epochs[1].batches.Count.ShouldBe(1);
        epochs[1].batches[0].ShouldBe(new[] { 3, 4, 5 });
        
        epochs[2].batches.Count.ShouldBe(1);
        epochs[2].batches[0].ShouldBe(new[] { 6, 7, 8 });
        
        epochs[3].batches.Count.ShouldBe(1);
        epochs[3].batches[0].ShouldBe(new[] { 9 });
    }

    [Fact]
    public async Task ComposedPipeline_Should_WorkWithMixOfEpochAwareBlocks()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<SimpleNumberProducer>();
        var provider = services.BuildServiceProvider();

        // Complex pipeline: PlainSource → Segmenter → Transform → Batch → Transform
        var sourceBlock = new PlainSourceBlock<int, SimpleNumberProducer>(
            "plain-source",
            provider.GetRequiredService<IServiceScopeFactory>());
            
        var segmenterBlock = new EpochSegmenterBlock<int>(
            "segmenter",
            EpochSegmentationPolicy.ByCount(4, "test-source"));
            
        var transformerBlock1 = new SimpleEpochTransformerBlock<int, int>(
            "transformer1",
            item => item * 2);
            
        var batchBlock = new EpochBatchBlock<int>(
            "batcher",
            maxBatchSize: 2);
            
        var transformerBlock2 = new EpochTransformerBlock<int[], string>(
            "transformer2",
            (batch, ctx) => TransformBatchToString(batch));

        var context = new TestExecutionContext();
        
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Act - Chain the pipeline
        var plainItems = sourceBlock.ExecuteAsync(EmptyInput(), context);
        var epochStreams = segmenterBlock.ExecuteAsync(plainItems, context);
        var transformedEpochs1 = transformerBlock1.ExecuteAsync(epochStreams, context);
        var batchedEpochs = batchBlock.ExecuteAsync(transformedEpochs1, context);
        var transformedEpochs2 = transformerBlock2.ExecuteAsync(batchedEpochs, context);
        
        var allResults = new List<string>();
        await foreach (var epochStream in transformedEpochs2)
        {
            await foreach (var item in epochStream.Items)
            {
                allResults.Add(item);
            }
        }

        // Assert
        // Input: [0,1,2,3] [4,5,6,7] [8,9]
        // After transform1 (*2): [0,2,4,6] [8,10,12,14] [16,18]
        // After batch (size 2): [[0,2],[4,6]] [[8,10],[12,14]] [[16,18]]
        // After transform2: "Batch[0,2]", "Batch[4,6]", "Batch[8,10]", "Batch[12,14]", "Batch[16,18]"
        allResults.Count.ShouldBe(5);
        allResults.ShouldContain("Batch[0,2]");
        allResults.ShouldContain("Batch[4,6]");
        allResults.ShouldContain("Batch[8,10]");
        allResults.ShouldContain("Batch[12,14]");
        allResults.ShouldContain("Batch[16,18]");
    }

    // Helper method to track epochs as they flow through
    private static async IAsyncEnumerable<IEpochStream<T>> TrackEpochs<T>(
        IAsyncEnumerable<IEpochStream<T>> input,
        List<EpochVector> tracker)
    {
        await foreach (var epochStream in input)
        {
            tracker.Add(epochStream.Epoch);
            yield return epochStream;
        }
    }

    // Helper method to transform an item to a string
    private static async IAsyncEnumerable<string> TransformToString(int item)
    {
        yield return $"Item-{item}";
        await Task.CompletedTask;
    }

    // Helper method to transform a batch to a string
    private static async IAsyncEnumerable<string> TransformBatchToString(int[] batch)
    {
        yield return $"Batch[{string.Join(",", batch)}]";
        await Task.CompletedTask;
    }
}

/// <summary>
/// Simple producer for testing that yields numbers 0-9.
/// </summary>
internal class SimpleNumberProducer : PlainSourceActorBase<int>
{
    public override async IAsyncEnumerable<int> ProduceAsync(
        [EnumeratorCancellation] IActorExecutionContext context)
    {
        for (int i = 0; i < 10; i++)
        {
            yield return i;
        }
        await Task.CompletedTask;
    }
}

/// <summary>
/// Test execution context for testing.
/// </summary>
internal class TestExecutionContext : IExecutionContext
{
    public CancellationToken CancellationToken { get; } = CancellationToken.None;
    public IServiceProvider ServiceProvider { get; } = new ServiceCollection().BuildServiceProvider();
    public Guid InvocationId { get; } = Guid.NewGuid();
}
