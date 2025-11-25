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
/// Tests for epoch-aware blocks that enable composability between plain and epoch streams.
/// These blocks use the ActorBlock pattern to provide DI scope safety by default.
/// </summary>
public class EpochAwareBlockTests
{
    /// <summary>
    /// Helper method to produce plain test items (replaces SimpleNumberProducer for tests).
    /// </summary>
    private static async IAsyncEnumerable<int> ProducePlainItems(int count = 10)
    {
        for (int i = 0; i < count; i++)
        {
            yield return i;
        }
        await Task.CompletedTask;
    }

    [Fact]
    public async Task EpochActorBlock_Should_TransformItemsWithinEpochs()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<NumberToStringActor>();
        var provider = services.BuildServiceProvider();

        var segmenterBlock = BlockHelpers.CreateEpochSegmenter<int>("segmenter", EpochSegmentationPolicy.ByCount(3, "test-source"));

        var actorBlock = BlockHelpers.CreateEpochActor<int, string, NumberToStringActor>("actor", provider.GetRequiredService<IServiceScopeFactory>());

        var context = new TestExecutionContext();

        // Act
        var plainItems = ProducePlainItems();
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
    public async Task EpochActorBlock_Should_PreserveEpochBoundaries()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<IdentityActor>();
        var provider = services.BuildServiceProvider();

        var segmenterBlock = BlockHelpers.CreateEpochSegmenter<int>("segmenter", EpochSegmentationPolicy.ByCount(2, "test-source"));

        var actorBlock = BlockHelpers.CreateEpochActor<int, int, IdentityActor>("actor", provider.GetRequiredService<IServiceScopeFactory>());

        var context = new TestExecutionContext();

        // Act
        var plainItems = ProducePlainItems();
        var epochStreams = segmenterBlock.ExecuteAsync(plainItems, context);
        var transformedEpochs = actorBlock.ExecuteAsync(epochStreams, context);

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

        // Assert - Verify epoch metadata preserved
        epochs.Count.ShouldBe(5); // 10 items / 2 per epoch = 5 epochs

        for (int i = 0; i < epochs.Count; i++)
        {
            var (epoch, items) = epochs[i];

            // Verify epoch vector consistent
            epoch.Sequences.ShouldContainKey("test-source");
            epoch.Sequences["test-source"].ShouldBe(i + 1);

            // Verify item count per epoch
            if (i < 4)
            {
                items.Count.ShouldBe(2);
            }
        }
    }

    [Fact]
    public async Task EpochActorBlock_Should_Support1ToManyTransformation()
    {
        // Arrange
        var services = new ServiceCollection();
        // Removed SimpleNumberProducer - using ProducePlainItems() helper instead
        services.AddTransient<OneToManyActor>();
        var provider = services.BuildServiceProvider();

        var segmenterBlock = BlockHelpers.CreateEpochSegmenter<int>("segmenter", EpochSegmentationPolicy.ByCount(5, "test-source"));

        var actorBlock = BlockHelpers.CreateEpochActor<int, int, OneToManyActor>("actor", provider.GetRequiredService<IServiceScopeFactory>());

        var context = new TestExecutionContext();
        // Act
        var plainItems = ProducePlainItems();
        var epochStreams = segmenterBlock.ExecuteAsync(plainItems, context);
        var transformedEpochs = actorBlock.ExecuteAsync(epochStreams, context);

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

        // Assert - Each input item produces 2 output items (item, item*2)
        epochs.Count.ShouldBe(2); // 10 items / 5 per epoch = 2 epochs

        // Epoch 1: items [0,1,2,3,4] → [0,0, 1,2, 2,4, 3,6, 4,8] (10 outputs)
        epochs[0].items.Count.ShouldBe(10);
        epochs[0].items.ShouldBe(new[] { 0, 0, 1, 2, 2, 4, 3, 6, 4, 8 });

        // Epoch 2: items [5,6,7,8,9] → [5,10, 6,12, 7,14, 8,16, 9,18] (10 outputs)
        epochs[1].items.Count.ShouldBe(10);
        epochs[1].items.ShouldBe(new[] { 5, 10, 6, 12, 7, 14, 8, 16, 9, 18 });
    }

    [Fact]
    public async Task EpochActorBlock_Should_SupportFiltering()
    {
        // Arrange
        var services = new ServiceCollection();
        // Removed SimpleNumberProducer - using ProducePlainItems() helper instead
        services.AddTransient<FilterEvenActor>();
        var provider = services.BuildServiceProvider();

        var segmenterBlock = BlockHelpers.CreateEpochSegmenter<int>("segmenter", EpochSegmentationPolicy.ByCount(5, "test-source"));

        var actorBlock = BlockHelpers.CreateEpochActor<int, int, FilterEvenActor>("actor", provider.GetRequiredService<IServiceScopeFactory>());

        var context = new TestExecutionContext();
        // Act
        var plainItems = ProducePlainItems();
        var epochStreams = segmenterBlock.ExecuteAsync(plainItems, context);
        var transformedEpochs = actorBlock.ExecuteAsync(epochStreams, context);

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

        // Assert - Only even numbers pass through
        epochs.Count.ShouldBe(2); // 10 items / 5 per epoch = 2 epochs

        // Epoch 1: items [0,1,2,3,4] → [0,2,4] (only evens)
        epochs[0].items.ShouldBe(new[] { 0, 2, 4 });

        // Epoch 2: items [5,6,7,8,9] → [6,8] (only evens)
        epochs[1].items.ShouldBe(new[] { 6, 8 });
    }

    [Fact]
    public async Task EpochBatchBlock_Should_BatchItemsWithinEpochs()
    {
        // Arrange
        var services = new ServiceCollection();
        // Removed SimpleNumberProducer - using ProducePlainItems() helper instead
        var provider = services.BuildServiceProvider();

        // Pipeline: PlainSource → Segmenter → EpochBatchBlock
        var segmenterBlock = BlockHelpers.CreateEpochSegmenter<int>("segmenter", EpochSegmentationPolicy.ByCount(5, "test-source"));

        var batchBlock = new EpochBatchBlock<int>(
            new BlockContext("batcher"),
            maxBatchSize: 2);

        var context = new TestExecutionContext();
        // Act
        var plainItems = ProducePlainItems();
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
        // Removed SimpleNumberProducer - using ProducePlainItems() helper instead
        var provider = services.BuildServiceProvider();

        // Pipeline: PlainSource → Segmenter → EpochBatchBlock
        var segmenterBlock = BlockHelpers.CreateEpochSegmenter<int>("segmenter", EpochSegmentationPolicy.ByCount(3, "test-source"));

        var batchBlock = new EpochBatchBlock<int>(
            new BlockContext("batcher"),
            maxBatchSize: 10); // Large batch size - should still break at epoch boundaries

        var context = new TestExecutionContext();
        // Act
        var plainItems = ProducePlainItems();
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
    public async Task EpochBatchBlock_Should_EmitUnderfilledBatches()
    {
        // Arrange
        var services = new ServiceCollection();
        // Removed SimpleNumberProducer - using ProducePlainItems() helper instead
        var provider = services.BuildServiceProvider();

        var segmenterBlock = BlockHelpers.CreateEpochSegmenter<int>("segmenter", EpochSegmentationPolicy.ByCount(5, "test-source"));

        var batchBlock = new EpochBatchBlock<int>(
            new BlockContext("batcher"),
            maxBatchSize: 3);

        var context = new TestExecutionContext();
        // Act
        var plainItems = ProducePlainItems();
        var epochStreams = segmenterBlock.ExecuteAsync(plainItems, context);
        var batchedEpochs = batchBlock.ExecuteAsync(epochStreams, context);

        var allBatches = new List<int[]>();
        await foreach (var epochStream in batchedEpochs)
        {
            await foreach (var batch in epochStream.Items)
            {
                allBatches.Add(batch);
            }
        }

        // Assert - Last batch of each epoch can be underfilled
        allBatches.Count.ShouldBe(4);
        allBatches[0].ShouldBe(new[] { 0, 1, 2 });
        allBatches[1].ShouldBe(new[] { 3, 4 }); // Underfilled due to epoch boundary
        allBatches[2].ShouldBe(new[] { 5, 6, 7 });
        allBatches[3].ShouldBe(new[] { 8, 9 }); // Underfilled due to epoch boundary
    }

    [Fact]
    public async Task ComposedPipeline_Should_WorkWithMixOfEpochAwareBlocks()
    {
        // Arrange
        var services = new ServiceCollection();
        // Removed SimpleNumberProducer - using ProducePlainItems() helper instead
        services.AddTransient<DoubleActor>();
        services.AddTransient<BatchToStringActor>();
        var provider = services.BuildServiceProvider();

        // Complex pipeline: PlainSource → Segmenter → Transform → Batch → Transform
        var segmenterBlock = BlockHelpers.CreateEpochSegmenter<int>("segmenter", EpochSegmentationPolicy.ByCount(4, "test-source"));

        var transformerBlock1 = BlockHelpers.CreateEpochActor<int, int, DoubleActor>("transformer1", provider.GetRequiredService<IServiceScopeFactory>());

        var batchBlock = new EpochBatchBlock<int>(
            new BlockContext("batcher"),
            maxBatchSize: 2);

        var transformerBlock2 = BlockHelpers.CreateEpochActor<int[], string, BatchToStringActor>("transformer2", provider.GetRequiredService<IServiceScopeFactory>());

        var context = new TestExecutionContext();
        // Act - Chain the pipeline
        var plainItems = ProducePlainItems();
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
/// Actor that transforms numbers to strings.
/// </summary>
internal class NumberToStringActor : IStreamActor<int, string>
{
    public async IAsyncEnumerable<string> RunAsync(
        IAsyncEnumerable<int> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            yield return $"Item-{item}";
        }
    }
}

/// <summary>
/// Actor that passes through items unchanged.
/// </summary>
internal class IdentityActor : IStreamActor<int, int>
{
    public async IAsyncEnumerable<int> RunAsync(
        IAsyncEnumerable<int> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            yield return item;
        }
    }
}

/// <summary>
/// Actor that transforms one input to multiple outputs (1-to-many).
/// </summary>
internal class OneToManyActor : IStreamActor<int, int>
{
    public async IAsyncEnumerable<int> RunAsync(
        IAsyncEnumerable<int> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            yield return item;
            yield return item * 2;
        }
    }
}

/// <summary>
/// Actor that filters out odd numbers (only passes even numbers).
/// </summary>
internal class FilterEvenActor : IStreamActor<int, int>
{
    public async IAsyncEnumerable<int> RunAsync(
        IAsyncEnumerable<int> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            if (item % 2 == 0)
            {
                yield return item;
            }
        }
    }
}

/// <summary>
/// Actor that doubles numbers.
/// </summary>
internal class DoubleActor : IStreamActor<int, int>
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

/// <summary>
/// Actor that transforms a batch to a string representation.
/// </summary>
internal class BatchToStringActor : IStreamActor<int[], string>
{
    public async IAsyncEnumerable<string> RunAsync(
        IAsyncEnumerable<int[]> input,
        IActorExecutionContext context)
    {
        await foreach (var batch in input.WithCancellation(context.CancellationToken))
        {
            yield return $"Batch[{string.Join(",", batch)}]";
        }
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
    public ICheckpoint? RecoveryCheckpoint { get; } = null;
    public POC.Observability.IDataFlowMetrics? Metrics { get; } = null;
}
