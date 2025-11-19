namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using DataFlow.POC.Tests.TestHelpers;

/// <summary>
/// Tests for multi-source epoch segmentation patterns.
/// These validate the composability patterns identified in research.
/// </summary>
public class MultiSourceSegmentationTests
{
    [Fact]
    public async Task UnifiedThenSegment_Should_CreateSingleSourceEpochs()
    {
        // Arrange - Two plain sources producing different data
        var source1Items = new[] { 1, 2, 3, 4, 5 };
        var source2Items = new[] { 10, 20, 30, 40, 50 };

        var context = new TestExecutionContext();

        // Act - Simulate unified-then-segment pattern
        // Source1 → ┐
        //          ├→ Union → EpochSegmenter("unified") → Downstream
        // Source2 → ┘

        var unifiedStream = UnifyStreams(
            CreatePlainStream(source1Items),
            CreatePlainStream(source2Items));

        var segmenter = BlockHelpers.CreateEpochSegmenter<int>("unified-segmenter", EpochSegmentationPolicy.ByCount(3, sourceId: "unified"));

        var epochStreams = segmenter.ExecuteAsync(unifiedStream, context);

        // Collect epoch vectors
        var epochs = new List<(EpochVector epoch, List<int> items)>();
        await foreach (var epochStream in epochStreams)
        {
            var items = new List<int>();
            await foreach (var item in epochStream.Items)
            {
                items.Add(item);
            }
            epochs.Add((epochStream.Epoch, items));
        }

        // Assert - All epochs should be single-source
        foreach (var (epoch, items) in epochs)
        {
            // Verify single source
            epoch.Sequences.Count.ShouldBe(1);
            epoch.Sequences.ShouldContainKey("unified");
        }

        // Verify items from both sources were processed
        var allItems = epochs.SelectMany(e => e.items).OrderBy(x => x).ToList();
        var expectedItems = source1Items.Concat(source2Items).OrderBy(x => x).ToList();
        allItems.ShouldBe(expectedItems);
    }

    [Fact]
    public async Task SegmentThenMerge_Should_ProcessIndependentEpochStreams()
    {
        // Arrange - Two sources with independent segmentation
        var source1Items = new[] { 1, 2, 3, 4, 5 };
        var source2Items = new[] { 10, 20, 30 };

        var context = new TestExecutionContext();

        // Act - Simulate segment-then-merge pattern
        // Source1 → EpochSegmenter("s1") → ┐
        //                                   ├→ Merge → Downstream
        // Source2 → EpochSegmenter("s2") → ┘

        var segmenter1 = BlockHelpers.CreateEpochSegmenter<int>("seg1", EpochSegmentationPolicy.ByCount(2, sourceId: "source1"));

        var segmenter2 = BlockHelpers.CreateEpochSegmenter<int>("seg2", EpochSegmentationPolicy.ByCount(2, sourceId: "source2"));

        var epochStream1 = segmenter1.ExecuteAsync(CreatePlainStream(source1Items), context);
        var epochStream2 = segmenter2.ExecuteAsync(CreatePlainStream(source2Items), context);

        // Sequential merge (simulated - in practice would need MergeBlock)
        var mergedStreams = MergeEpochStreamsSequentially(epochStream1, epochStream2);

        // Collect merged epoch streams
        var epochs = new List<(EpochVector epoch, List<int> items)>();
        await foreach (var epochStream in mergedStreams)
        {
            var items = new List<int>();
            await foreach (var item in epochStream.Items)
            {
                items.Add(item);
            }

            epochs.Add((epochStream.Epoch, items));
        }

        // Assert - Epochs maintain their separate source identities
        var source1Epochs = epochs.Where(e => e.epoch.Sequences.ContainsKey("source1")).ToList();
        var source2Epochs = epochs.Where(e => e.epoch.Sequences.ContainsKey("source2")).ToList();

        source1Epochs.Count.ShouldBeGreaterThan(0);
        source2Epochs.Count.ShouldBeGreaterThan(0);

        // Verify each source's epochs are single-source
        foreach (var (epoch, _) in source1Epochs)
        {
            epoch.Sequences.Count.ShouldBe(1);
            epoch.Sequences.ShouldContainKey("source1");
        }

        foreach (var (epoch, _) in source2Epochs)
        {
            epoch.Sequences.Count.ShouldBe(1);
            epoch.Sequences.ShouldContainKey("source2");
        }
    }

    [Fact]
    public async Task SingleSource_EpochStreamBoundaries_AlignWithEpochs()
    {
        // Arrange
        var items = Enumerable.Range(1, 10).ToArray();
        var context = new TestExecutionContext();

        // Act - Single source with segmentation
        var segmenter = BlockHelpers.CreateEpochSegmenter<int>("seg", EpochSegmentationPolicy.ByCount(3, sourceId: "single"));

        var epochStreams = segmenter.ExecuteAsync(CreatePlainStream(items), context);

        // Track epochs
        var epochs = new List<(EpochVector epoch, int itemCount)>();

        await foreach (var epochStream in epochStreams)
        {
            var count = 0;
            await foreach (var item in epochStream.Items)
            {
                count++;
            }

            epochs.Add((epochStream.Epoch, count));
        }

        // Assert - Verify epoch boundaries are consistent
        epochs.Count.ShouldBe(4); // 10 items / 3 per epoch = 4 epochs

        epochs[0].itemCount.ShouldBe(3);
        epochs[1].itemCount.ShouldBe(3);
        epochs[2].itemCount.ShouldBe(3);
        epochs[3].itemCount.ShouldBe(1); // Last epoch has remaining items

        // Verify epoch sequence numbers
        for (int i = 0; i < epochs.Count; i++)
        {
            epochs[i].epoch.Sequences["single"].ShouldBe(i + 1);
        }
    }

    [Fact]
    public async Task ProducerGroup_WithSingleSegmenter_Should_CreateUnifiedEpochs()
    {
        // Arrange - Simulate producer group with multiple producers
        var producer1Items = new[] { 1, 2, 3 };
        var producer2Items = new[] { 4, 5, 6 };
        var producer3Items = new[] { 7, 8, 9 };
        var producer4Items = new[] { 10, 11, 12 };

        var context = new TestExecutionContext();

        // Act - Simulate producer group → single segmenter
        // ProducerGroup(4 producers, max 2 concurrent) → EpochSegmenter("group")

        // Union all producers into single stream (simulating producer group output)
        var groupStream = UnifyStreams(
            CreatePlainStream(producer1Items),
            CreatePlainStream(producer2Items),
            CreatePlainStream(producer3Items),
            CreatePlainStream(producer4Items));

        var segmenter = BlockHelpers.CreateEpochSegmenter<int>("group-segmenter", EpochSegmentationPolicy.ByCount(4, sourceId: "producer-group"));

        var epochStreams = segmenter.ExecuteAsync(groupStream, context);

        // Collect epochs
        var epochs = new List<EpochVector>();
        await foreach (var epochStream in epochStreams)
        {
            epochs.Add(epochStream.Epoch);
            await foreach (var item in epochStream.Items)
            {
                // Process items
            }
        }

        // Assert - All epochs single-source with group sourceId
        foreach (var epoch in epochs)
        {
            epoch.Sequences.Count.ShouldBe(1);
            epoch.Sequences.ShouldContainKey("producer-group");
        }

        epochs.Count.ShouldBeGreaterThan(0);
    }

    // Helper methods

    private static async IAsyncEnumerable<T> CreatePlainStream<T>(T[] items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.CompletedTask;
        }
    }

    private static async IAsyncEnumerable<T> UnifyStreams<T>(params IAsyncEnumerable<T>[] streams)
    {
        foreach (var stream in streams)
        {
            await foreach (var item in stream)
            {
                yield return item;
            }
        }
    }

    private static async IAsyncEnumerable<IEpochStream<T>> MergeEpochStreamsSequentially<T>(
        IAsyncEnumerable<IEpochStream<T>> stream1,
        IAsyncEnumerable<IEpochStream<T>> stream2)
    {
        // Simple sequential merge (in practice would interleave)
        await foreach (var epochStream in stream1)
        {
            yield return epochStream;
        }

        await foreach (var epochStream in stream2)
        {
            yield return epochStream;
        }
    }
}
