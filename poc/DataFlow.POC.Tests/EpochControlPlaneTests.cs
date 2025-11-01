namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

/// <summary>
/// Tests for Out-of-Band Epoch Control Plane implementation.
/// Validates epoch tracking, propagation, and acknowledgment without hot-path overhead.
/// </summary>
public class EpochControlPlaneTests
{
    [Fact]
    public async Task EpochManager_Should_BroadcastEpoch_To_All_Subscribers()
    {
        // Arrange
        var epochManager = new EpochManager();
        var subscriber1Epochs = new List<EpochMarker>();
        var subscriber2Epochs = new List<EpochMarker>();

        var subscriber1 = new TestEpochSubscriber("subscriber1", subscriber1Epochs);
        var subscriber2 = new TestEpochSubscriber("subscriber2", subscriber2Epochs);

        epochManager.RegisterSubscriber("subscriber1", subscriber1);
        epochManager.RegisterSubscriber("subscriber2", subscriber2);

        var marker = new EpochMarker("source1", 1, DateTime.UtcNow);

        // Act
        await epochManager.BroadcastEpochAsync(marker, CancellationToken.None);

        // Assert
        subscriber1Epochs.ShouldContain(marker);
        subscriber2Epochs.ShouldContain(marker);
    }

    [Fact]
    public async Task EpochManager_Should_Track_MonotonicSequences()
    {
        // Arrange
        var epochManager = new EpochManager();
        
        // Act
        var epoch1 = epochManager.CreateEpochMarker("source1");
        var epoch2 = epochManager.CreateEpochMarker("source1");
        var epoch3 = epochManager.CreateEpochMarker("source2");

        // Assert
        epoch1.Sequence.ShouldBe(1);
        epoch2.Sequence.ShouldBe(2);
        epoch3.Sequence.ShouldBe(1);
        epoch1.SourceId.ShouldBe("source1");
        epoch2.SourceId.ShouldBe("source1");
        epoch3.SourceId.ShouldBe("source2");
    }

    [Fact]
    public void EpochProgress_Should_Track_LastSeenSequences()
    {
        // Arrange
        var progress = new EpochProgress();

        // Act
        progress.UpdateLastSeen("source1", 5);
        progress.UpdateLastSeen("source2", 3);
        progress.UpdateLastSeen("source1", 7); // Update to higher sequence

        // Assert
        progress.GetLastSeen("source1").ShouldBe(7);
        progress.GetLastSeen("source2").ShouldBe(3);
        progress.GetLastSeen("unknown").ShouldBe(-1);
    }

    [Fact]
    public void EpochProgress_Should_DetectAlignment_WhenAllSourcesReached()
    {
        // Arrange
        var progress = new EpochProgress();
        progress.UpdateLastSeen("source1", 10);
        progress.UpdateLastSeen("source2", 8);
        progress.UpdateLastSeen("source3", 12);

        // Act & Assert
        progress.HasSeenAllSources(new[] { "source1", "source2", "source3" }, 8).ShouldBeTrue();
        progress.HasSeenAllSources(new[] { "source1", "source2", "source3" }, 9).ShouldBeFalse();
        progress.HasSeenAllSources(new[] { "source1", "source3" }, 10).ShouldBeTrue(); // source1=10, source3=12, both >= 10
        progress.HasSeenAllSources(new[] { "source1", "source2" }, 10).ShouldBeFalse(); // source2=8 < 10
    }

    [Fact]
    public async Task EpochManager_Should_RaiseAcknowledgment_WhenSubscriberAligned()
    {
        // Arrange
        var epochManager = new EpochManager();
        var acknowledgedEpochs = new List<EpochMarker>();
        
        epochManager.EpochAcknowledged += (sender, args) =>
        {
            acknowledgedEpochs.Add(args.Marker);
        };

        var subscriber = new TestEpochSubscriber("subscriber1", new List<EpochMarker>(), acknowledgeImmediately: true);
        epochManager.RegisterSubscriber("subscriber1", subscriber);

        var marker = new EpochMarker("source1", 1, DateTime.UtcNow);

        // Act
        await epochManager.BroadcastEpochAsync(marker, CancellationToken.None);
        await Task.Delay(50); // Give time for acknowledgment

        // Assert
        acknowledgedEpochs.ShouldContain(marker);
    }

    [Fact]
    public async Task EpochControlPlaneEdgeStrategy_Should_PropagateData_Without_TypeChecks()
    {
        // Arrange
        var services = new ServiceCollection().BuildServiceProvider();
        var epochManager = new EpochManager();
        var receivedItems = new List<int>();

        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(ctx));
        var consumer = new ProcessorBlock<int>("consumer", async (value, ctx) =>
        {
            receivedItems.Add(value);
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("epoch-control-plane-flow");
        builder.AddBlock(producer)
            .AddBlock(consumer);

        // Use epoch control plane strategy
        var strategy = EpochControlPlaneFactory.CreateBroadcast(epochManager);
        builder.AddEdge(new Edge(producer, new[] { consumer }, strategy));

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert - Data flows normally without control signal overhead
        receivedItems.ShouldBe(new[] { 1, 2, 3, 4, 5 });
    }

    [Fact]
    public void EpochStatistics_Should_CalculateMinimumProgress()
    {
        // Arrange
        var stats = new EpochStatistics
        {
            SourceSequences = new Dictionary<string, long>
            {
                ["source1"] = 10,
                ["source2"] = 15
            },
            BlockProgress = new Dictionary<string, Dictionary<string, long>>
            {
                ["block1"] = new Dictionary<string, long> { ["source1"] = 8, ["source2"] = 14 },
                ["block2"] = new Dictionary<string, long> { ["source1"] = 10, ["source2"] = 12 },
                ["block3"] = new Dictionary<string, long> { ["source1"] = 9, ["source2"] = 15 }
            }
        };

        // Act
        var minProgress = stats.GetMinimumProgress();

        // Assert
        minProgress["source1"].ShouldBe(8); // Slowest block for source1
        minProgress["source2"].ShouldBe(12); // Slowest block for source2
    }

    [Fact]
    public async Task EpochManager_With_Metadata_Should_PreserveMetadata()
    {
        // Arrange
        var epochManager = new EpochManager();
        var receivedEpochs = new List<EpochMarker>();
        var subscriber = new TestEpochSubscriber("subscriber1", receivedEpochs);
        
        epochManager.RegisterSubscriber("subscriber1", subscriber);

        var metadata = new Dictionary<string, object>
        {
            ["checkpointId"] = Guid.NewGuid().ToString(),
            ["type"] = "checkpoint"
        };

        var marker = epochManager.CreateEpochMarker("source1", metadata);

        // Act
        await epochManager.BroadcastEpochAsync(marker, CancellationToken.None);

        // Assert
        receivedEpochs.Count.ShouldBe(1);
        receivedEpochs[0].Metadata.ShouldNotBeNull();
        receivedEpochs[0].Metadata!["type"].ShouldBe("checkpoint");
    }

    /// <summary>
    /// KNOWN BUG: This test demonstrates premature epoch alignment when data is still in-flight.
    /// 
    /// The issue: NotifyEpochAsync() updates progress immediately upon receiving the broadcast,
    /// without waiting for all data items from that epoch to be fully processed. This creates
    /// a race condition where:
    /// 
    /// 1. Source emits data items for Epoch N
    /// 2. Source broadcasts Epoch N+1 (while N's data is still in channels)
    /// 3. Downstream blocks receive Epoch N+1 broadcast and update their progress
    /// 4. Alignment check passes, indicating "Epoch N complete"
    /// 5. But Epoch N's data items are still being processed!
    /// 
    /// This can lead to:
    /// - Premature checkpoint acknowledgments
    /// - False completion signals before pipeline is fully drained
    /// - Incorrect state persistence during recovery scenarios
    /// 
    /// Expected behavior: Epoch alignment should only be acknowledged AFTER all data items
    /// associated with that epoch have been fully processed by the block.
    /// 
    /// See: EPOCH_CONTROL_PLANE_DESIGN.md - "Known Limitations" section for remediation plan.
    /// Follow-up issue: To be created for Phase 3 investigation of correct alignment semantics.
    /// </summary>
    [Fact]
    public async Task KNOWN_BUG_EpochAlignment_Can_Occur_Before_Data_Processing_Complete()
    {
        // Arrange
        var epochManager = new EpochManager();
        var processedItems = new List<int>();
        var epochAcknowledgments = new List<(long sequence, int itemsProcessedAtAck)>();
        
        // Create a subscriber that tracks when epochs are acknowledged vs when data is processed
        var subscriber = new SlowProcessingSubscriber(
            "slowProcessor",
            processedItems,
            epochAcknowledgments,
            processingDelayMs: 50); // Simulate slow processing
        
        epochManager.RegisterSubscriber("slowProcessor", subscriber);
        
        // Simulate a source that emits data followed by epoch markers
        var dataItemsPerEpoch = 10;
        
        // Act: Emit data for Epoch 1, then immediately broadcast Epoch 2
        // This simulates the race condition where Epoch 2 broadcast arrives
        // before all Epoch 1 data has been processed
        
        // Start processing data for Epoch 1 (in background)
        var processingTask = Task.Run(async () =>
        {
            for (int i = 0; i < dataItemsPerEpoch; i++)
            {
                await subscriber.ProcessDataItemAsync(i, CancellationToken.None);
            }
        });
        
        // Immediately broadcast Epoch 1 completion (before data processing finishes)
        await Task.Delay(10); // Small delay to let processing start but not finish
        var epoch1 = new EpochMarker("source1", 1, DateTime.UtcNow);
        await epochManager.BroadcastEpochAsync(epoch1, CancellationToken.None);
        
        // Wait for all data processing to complete
        await processingTask;
        await Task.Delay(100); // Ensure all async operations complete
        
        // Assert: This demonstrates the bug
        // The epoch was acknowledged before all data items were processed
        epochAcknowledgments.Count.ShouldBe(1, "Epoch should have been acknowledged");
        var (sequence, itemsAtAck) = epochAcknowledgments[0];
        
        sequence.ShouldBe(1, "Should be Epoch 1");
        
        // BUG: Items processed at acknowledgment time is less than total items
        // This proves that the epoch was acknowledged before data processing completed
        itemsAtAck.ShouldBeLessThan(dataItemsPerEpoch, 
            $"BUG DEMONSTRATED: Epoch acknowledged after only {itemsAtAck}/{dataItemsPerEpoch} items processed. " +
            $"Expected acknowledgment should wait until all {dataItemsPerEpoch} items are done.");
        
        // Eventually all items do get processed (but too late)
        processedItems.Count.ShouldBe(dataItemsPerEpoch, 
            "All items eventually processed, but epoch was already acknowledged");
    }

    private static async IAsyncEnumerable<int> ProduceIntegers(IExecutionContext ctx)
    {
        for (int i = 1; i <= 5; i++)
        {
            yield return i;
        }
    }
}

/// <summary>
/// Test subscriber that simulates slow data processing to expose the premature alignment bug.
/// This subscriber tracks both data processing and epoch acknowledgments with timestamps.
/// </summary>
internal class SlowProcessingSubscriber : IEpochSubscriber
{
    private readonly string _name;
    private readonly List<int> _processedItems;
    private readonly List<(long sequence, int itemsProcessedAtAck)> _epochAcknowledgments;
    private readonly int _processingDelayMs;
    private readonly EpochProgress _progress = new();

    public SlowProcessingSubscriber(
        string name,
        List<int> processedItems,
        List<(long sequence, int itemsProcessedAtAck)> epochAcknowledgments,
        int processingDelayMs)
    {
        _name = name;
        _processedItems = processedItems;
        _epochAcknowledgments = epochAcknowledgments;
        _processingDelayMs = processingDelayMs;
    }

    public async Task ProcessDataItemAsync(int item, CancellationToken cancellationToken)
    {
        // Simulate slow processing (e.g., database write, network call)
        await Task.Delay(_processingDelayMs, cancellationToken);
        _processedItems.Add(item);
    }

    public ValueTask<bool> NotifyEpochAsync(EpochMarker marker, CancellationToken cancellationToken)
    {
        // BUG: This updates progress immediately, without waiting for in-flight data
        _progress.UpdateLastSeen(marker.SourceId, marker.Sequence);
        
        // Record how many items have been processed at the time of acknowledgment
        // This will demonstrate that acknowledgment happens too early
        _epochAcknowledgments.Add((marker.Sequence, _processedItems.Count));
        
        // Always acknowledge immediately (simulating current behavior)
        return new ValueTask<bool>(true);
    }

    public EpochProgress GetEpochProgress()
    {
        return _progress;
    }
}

/// <summary>
/// Test implementation of IEpochSubscriber for testing epoch propagation.
/// </summary>
internal class TestEpochSubscriber : IEpochSubscriber
{
    private readonly string _name;
    private readonly List<EpochMarker> _receivedEpochs;
    private readonly bool _acknowledgeImmediately;
    private readonly EpochProgress _progress = new();

    public TestEpochSubscriber(string name, List<EpochMarker> receivedEpochs, bool acknowledgeImmediately = false)
    {
        _name = name;
        _receivedEpochs = receivedEpochs;
        _acknowledgeImmediately = acknowledgeImmediately;
    }

    public ValueTask<bool> NotifyEpochAsync(EpochMarker marker, CancellationToken cancellationToken)
    {
        _receivedEpochs.Add(marker);
        _progress.UpdateLastSeen(marker.SourceId, marker.Sequence);
        
        // For testing, optionally acknowledge immediately
        return new ValueTask<bool>(_acknowledgeImmediately);
    }

    public EpochProgress GetEpochProgress()
    {
        return _progress;
    }
}
