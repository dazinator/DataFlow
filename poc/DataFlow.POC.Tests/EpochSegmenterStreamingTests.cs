namespace DataFlow.POC.Tests;

using DataFlow.POC.Core;
using Shouldly;
using Xunit;

/// <summary>
/// Tests to validate Phase 4 streaming segmentation with no internal buffering.
/// These tests ensure that items flow directly from source to consumer without
/// intermediate list collection.
/// </summary>
public class EpochSegmenterStreamingTests
{
    [Fact]
    public async Task SegmentByKey_Should_StreamItemsWithoutBuffering()
    {
        // This test validates that items stream through immediately without being collected into lists
        
        // Arrange
        var itemProducedTimes = new List<DateTimeOffset>();
        var itemConsumedTimes = new List<DateTimeOffset>();
        
        async IAsyncEnumerable<int> ProduceWithTimestamps()
        {
            for (int i = 0; i < 20; i++)
            {
                itemProducedTimes.Add(DateTimeOffset.UtcNow);
                await Task.Delay(10); // Small delay between items
                yield return i;
            }
        }

        // Act
        await foreach (var epochStream in EpochSegmenter.SegmentByKey(
            ProduceWithTimestamps(),
            n => n / 10, // Two epochs: 0-9, 10-19
            "source1"))
        {
            await foreach (var item in epochStream.Items)
            {
                itemConsumedTimes.Add(DateTimeOffset.UtcNow);
            }
        }

        // Assert
        itemProducedTimes.Count.ShouldBe(20);
        itemConsumedTimes.Count.ShouldBe(20);
        
        // With true streaming, consumption should happen concurrently with production
        // The old list-based approach would complete all production before consumption
        // With streaming, first item should be consumed very close to when it's produced
        var firstProducedTime = itemProducedTimes[0];
        var firstConsumedTime = itemConsumedTimes[0];
        var timeDifference = (firstConsumedTime - firstProducedTime).TotalMilliseconds;
        
        // Should be consumed almost immediately (within 100ms is generous)
        timeDifference.ShouldBeLessThan(100);
    }

    [Fact]
    public async Task SegmentByKey_Should_NotCollectAllItemsBeforeYielding()
    {
        // Validates that the first epoch stream is yielded before all items are produced
        
        // Arrange
        var productionCompleted = false;
        var firstEpochStarted = false;
        
        async IAsyncEnumerable<int> SlowProducer()
        {
            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(5);
                yield return i;
            }
            productionCompleted = true;
        }

        // Act
        await foreach (var epochStream in EpochSegmenter.SegmentByKey(
            SlowProducer(),
            n => n / 10, // Three epochs
            "source1"))
        {
            if (!firstEpochStarted)
            {
                firstEpochStarted = true;
                
                // First epoch should start before all production completes
                productionCompleted.ShouldBeFalse();
            }

            await foreach (var item in epochStream.Items)
            {
                // Consume items
            }
        }

        // Assert
        firstEpochStarted.ShouldBeTrue();
        productionCompleted.ShouldBeTrue(); // Eventually completes
    }

    [Fact]
    public async Task SegmentByKey_Should_PreserveItemOrder()
    {
        // Streaming must preserve order even without buffering
        
        // Arrange
        async IAsyncEnumerable<int> GenerateSequence()
        {
            for (int i = 0; i < 100; i++)
            {
                yield return i;
            }
        }

        // Act
        var allItems = new List<int>();
        
        await foreach (var epochStream in EpochSegmenter.SegmentByKey(
            GenerateSequence(),
            n => n / 20, // 5 epochs of 20 items
            "source1"))
        {
            await foreach (var item in epochStream.Items)
            {
                allItems.Add(item);
            }
        }

        // Assert
        allItems.Count.ShouldBe(100);
        allItems.ShouldBe(Enumerable.Range(0, 100));
    }

    [Fact]
    public async Task SegmentByEpoch_Should_StreamItemsWithoutBuffering()
    {
        // Clock-based segmentation should also stream
        
        // Arrange
        var clock = new ManualEpochClock();
        clock.SetEpoch(EpochVector.FromSingleSource("source1", 1));
        
        var firstItemConsumedBeforeEpochChange = false;
        var epochChangedTime = default(DateTimeOffset);
        var firstItemConsumedTime = default(DateTimeOffset);
        
        async IAsyncEnumerable<int> GenerateWithClockChange()
        {
            for (int i = 0; i < 10; i++)
            {
                yield return i;
                
                if (i == 4)
                {
                    clock.AdvanceEpoch("source1");
                    epochChangedTime = DateTimeOffset.UtcNow;
                    await Task.Delay(50); // Delay after epoch change
                }
            }
        }

        // Act
        var epochCount = 0;
        
        await foreach (var epochStream in EpochSegmenter.SegmentByEpoch(
            GenerateWithClockChange(),
            clock))
        {
            epochCount++;
            
            await foreach (var item in epochStream.Items)
            {
                if (epochCount == 1 && firstItemConsumedTime == default)
                {
                    firstItemConsumedTime = DateTimeOffset.UtcNow;
                    firstItemConsumedBeforeEpochChange = epochChangedTime == default;
                }
            }
        }

        // Assert
        epochCount.ShouldBeGreaterThanOrEqualTo(2);
        firstItemConsumedBeforeEpochChange.ShouldBeTrue();
    }

    [Fact]
    public async Task SegmentByKey_Should_HandleSingleItemEpochs()
    {
        // Edge case: epochs with single items should still stream
        
        // Arrange
        async IAsyncEnumerable<int> GenerateSingleItemEpochs()
        {
            // Each item is its own epoch
            for (int i = 0; i < 10; i++)
            {
                yield return i;
            }
        }

        // Act
        var epochs = new List<(EpochVector epoch, List<int> items)>();
        
        await foreach (var epochStream in EpochSegmenter.SegmentByKey(
            GenerateSingleItemEpochs(),
            n => n, // Each item has unique key
            "source1"))
        {
            var items = new List<int>();
            await foreach (var item in epochStream.Items)
            {
                items.Add(item);
            }
            epochs.Add((epochStream.Epoch, items));
        }

        // Assert
        epochs.Count.ShouldBe(10);
        for (int i = 0; i < 10; i++)
        {
            epochs[i].epoch.GetSequence("source1").ShouldBe(i + 1);
            epochs[i].items.Count.ShouldBe(1);
            epochs[i].items[0].ShouldBe(i);
        }
    }

    [Fact]
    public async Task SegmentByKey_Should_HandleEmptyEpochs()
    {
        // Epochs can't be truly empty since they're triggered by items,
        // but this tests the boundary case
        
        // Arrange
        async IAsyncEnumerable<int> GenerateSequence()
        {
            for (int i = 0; i < 10; i++)
            {
                yield return i;
            }
        }

        // Act
        var epochCount = 0;
        var totalItems = 0;
        
        await foreach (var epochStream in EpochSegmenter.SegmentByKey(
            GenerateSequence(),
            n => n / 5, // Two epochs
            "source1"))
        {
            epochCount++;
            
            await foreach (var item in epochStream.Items)
            {
                totalItems++;
            }
        }

        // Assert
        epochCount.ShouldBe(2);
        totalItems.ShouldBe(10);
    }

    [Fact]
    public async Task SegmentByKey_Should_SupportBackpressure()
    {
        // Slow consumer should naturally apply backpressure to producer
        
        // Arrange
        var maxBufferedItems = 0;
        var currentBufferedItems = 0;
        var lockObj = new object();
        
        async IAsyncEnumerable<int> FastProducer()
        {
            for (int i = 0; i < 50; i++)
            {
                lock (lockObj)
                {
                    currentBufferedItems++;
                    maxBufferedItems = Math.Max(maxBufferedItems, currentBufferedItems);
                }
                
                yield return i;
            }
        }

        // Act
        await foreach (var epochStream in EpochSegmenter.SegmentByKey(
            FastProducer(),
            n => n / 10,
            "source1"))
        {
            await foreach (var item in epochStream.Items)
            {
                // Slow consumer
                await Task.Delay(5);
                
                lock (lockObj)
                {
                    currentBufferedItems--;
                }
            }
        }

        // Assert
        // With streaming and natural backpressure, buffering should be minimal
        // (This is a behavior test - exact numbers may vary)
        maxBufferedItems.ShouldBeLessThan(50); // Much less than total items
    }

    [Fact]
    public async Task SegmentByKey_Should_CompleteEpochsInOrder()
    {
        // Even with streaming, epoch completion order must be preserved
        
        // Arrange
        var completionOrder = new List<long>();
        var progress = new CompletionBasedEpochProgress();
        
        async IAsyncEnumerable<int> GenerateSequence()
        {
            for (int i = 0; i < 30; i++)
            {
                yield return i;
            }
        }

        // Act
        await foreach (var epochStream in EpochSegmenter.SegmentByKey(
            GenerateSequence(),
            n => n / 10,
            "source1"))
        {
            progress.RegisterEpochStarted(epochStream.Epoch);
            
            await foreach (var item in epochStream.Items)
            {
                // Process item
            }
            
            progress.RegisterEpochCompleted(epochStream.Epoch);
            completionOrder.Add(epochStream.Epoch.GetSequence("source1"));
        }

        // Assert
        completionOrder.ShouldBe(new long[] { 1, 2, 3 });
        
        // All epochs should be completed
        progress.GetCompletedEpochs().Count.ShouldBe(3);
    }
}
