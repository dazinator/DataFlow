namespace DataFlow.POC.Tests;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using DataFlow.POC.Tests.TestHelpers;
using DataFlow.POC.Registry;

/// <summary>
/// Tests for Out-of-Band Epoch Control Plane implementation.
/// Validates epoch tracking, propagation, and acknowledgment without hot-path overhead.
/// </summary>
public class EpochControlPlaneTests
{
    /// <summary>
    /// Simple actor that collects integers.
    /// </summary>
    private class SimpleIntCollectorActor : IStreamActor<int, object>
    {
        private readonly List<int> _collected;

        public SimpleIntCollectorActor(List<int> collected)
        {
            _collected = collected;
        }

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                _collected.Add(item);
            }
            yield break;
        }
    }
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
        var receivedItems = new List<int>();
        var receivedItemsServices = new ServiceCollection();
        receivedItemsServices.AddScoped(_ => new SimpleIntCollectorActor(receivedItems));
        var receivedItemsServiceProvider = receivedItemsServices.BuildServiceProvider();
        
        var services = new ServiceCollection().BuildServiceProvider();
        var epochManager = new EpochManager();

        var producer = BlockHelpers.CreateProducer<int>("producer", ctx => ProduceIntegers(ctx));
        var consumer = BlockHelpers.CreateActor<int, object, SimpleIntCollectorActor>("consumer", receivedItemsServiceProvider.GetRequiredService<IServiceScopeFactory>());

        var builder = GraphHelpers.CreateGraphBuilder("epoch-control-plane-flow");
        builder.AddBlock(producer)
            .AddBlock(consumer);

        // Use epoch control plane strategy
        var strategy = EpochControlPlaneFactory.CreateBroadcast(epochManager);
        builder.AddEdge(new Edge(producer, new[] { consumer }, strategy));

        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
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

// ============================================================================
// Epoch Control Plane - Experimental Out-of-Band Implementation
// ============================================================================
// NOTE: This is experimental code from earlier investigations.
// The current chosen mechanism uses IEpochStream (in-band).
// This code is preserved here for reference and testing purposes only.
// ============================================================================

/// <summary>
/// Out-of-Band Epoch Control Plane - Option 1 from investigation phase.
/// Provides near-zero hot-path overhead by tracking control signals via metadata rather than in-band channels.
/// 
/// Architecture:
/// - Sources assign monotonic sequence numbers to control signals
/// - EpochManager broadcasts {sourceId → seqE} metadata to all blocks
/// - Blocks track lastSeen[sourceId] and acknowledge epochs once all inputs ≥ seqE
/// - Data flows normally through channels without control signal interference
/// </summary>

/// <summary>
/// Represents an epoch marker with source and sequence information.
/// </summary>
public sealed record EpochMarker(string SourceId, long Sequence, DateTime Timestamp)
{
    /// <summary>
    /// Optional metadata associated with this epoch (e.g., checkpoint ID, barrier type).
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Tracks the progress of epoch markers across the dataflow graph.
/// Blocks use this to track which epochs they have seen from each source.
/// </summary>
public class EpochProgress
{
    private readonly ConcurrentDictionary<string, long> _lastSeenSequence = new();

    /// <summary>
    /// Updates the last seen sequence for a source.
    /// Returns true if this is a new highest sequence for this source.
    /// </summary>
    public bool UpdateLastSeen(string sourceId, long sequence)
    {
        return _lastSeenSequence.AddOrUpdate(
            sourceId,
            sequence,
            (_, current) => Math.Max(current, sequence)) == sequence;
    }

    /// <summary>
    /// Gets the last seen sequence for a source, or -1 if not yet seen.
    /// </summary>
    public long GetLastSeen(string sourceId)
    {
        return _lastSeenSequence.TryGetValue(sourceId, out var seq) ? seq : -1;
    }

    /// <summary>
    /// Checks if this block has seen at least the specified sequence from all specified sources.
    /// Used to determine if an epoch can be acknowledged.
    /// </summary>
    public bool HasSeenAllSources(IEnumerable<string> sourceIds, long targetSequence)
    {
        foreach (var sourceId in sourceIds)
        {
            var lastSeen = GetLastSeen(sourceId);
            if (lastSeen < targetSequence)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Gets a snapshot of all last seen sequences.
    /// </summary>
    public Dictionary<string, long> GetSnapshot()
    {
        return new Dictionary<string, long>(_lastSeenSequence);
    }
}

/// <summary>
/// Event arguments for epoch notifications.
/// </summary>
public class EpochEventArgs : EventArgs
{
    public EpochMarker Marker { get; }

    public EpochEventArgs(EpochMarker marker)
    {
        Marker = marker ?? throw new ArgumentNullException(nameof(marker));
    }
}

/// <summary>
/// Event arguments for epoch acknowledgment.
/// </summary>
public class EpochAcknowledgmentEventArgs : EventArgs
{
    public string BlockName { get; }
    public EpochMarker Marker { get; }
    public TimeSpan ProcessingTime { get; }

    public EpochAcknowledgmentEventArgs(string blockName, EpochMarker marker, TimeSpan processingTime)
    {
        BlockName = blockName ?? throw new ArgumentNullException(nameof(blockName));
        Marker = marker ?? throw new ArgumentNullException(nameof(marker));
        ProcessingTime = processingTime;
    }
}

/// <summary>
/// Publisher interface for blocks that emit epochs.
/// Typically source blocks that generate control signals.
/// </summary>
public interface IEpochPublisher
{
    /// <summary>
    /// Event raised when a new epoch is emitted.
    /// </summary>
    event EventHandler<EpochEventArgs>? EpochEmitted;
}

/// <summary>
/// Subscriber interface for blocks that handle epochs.
/// All blocks in the dataflow graph should implement this to participate in epoch coordination.
/// </summary>
public interface IEpochSubscriber
{
    /// <summary>
    /// Notifies the block of a new epoch from a source.
    /// Returns true if the block has now seen this epoch from all its upstream sources.
    /// </summary>
    ValueTask<bool> NotifyEpochAsync(EpochMarker marker, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the current epoch progress for this block.
    /// </summary>
    EpochProgress GetEpochProgress();
}

/// <summary>
/// Manages epoch propagation across the dataflow graph.
/// Provides out-of-band control plane for coordinated checkpointing and alignment.
/// </summary>
public class EpochManager : IDisposable
{
    private readonly ConcurrentDictionary<string, IEpochPublisher> _publishers = new();
    private readonly ConcurrentDictionary<string, ConcurrentBag<IEpochSubscriber>> _subscribers = new();
    private readonly ConcurrentDictionary<string, long> _nextSequence = new();
    private bool _disposed;

    /// <summary>
    /// Event raised when any block acknowledges an epoch.
    /// Useful for monitoring and debugging epoch propagation.
    /// </summary>
    public event EventHandler<EpochAcknowledgmentEventArgs>? EpochAcknowledged;

    /// <summary>
    /// Registers a publisher block that can emit epochs.
    /// </summary>
    public void RegisterPublisher(string sourceId, IEpochPublisher publisher)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(EpochManager));
        ArgumentNullException.ThrowIfNull(sourceId);
        ArgumentNullException.ThrowIfNull(publisher);

        _publishers[sourceId] = publisher;
        _nextSequence.TryAdd(sourceId, 0);

        // Subscribe to publisher's events
        publisher.EpochEmitted += OnEpochEmitted;
    }

    /// <summary>
    /// Registers a subscriber block that handles epochs.
    /// </summary>
    public void RegisterSubscriber(string blockName, IEpochSubscriber subscriber)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(EpochManager));
        ArgumentNullException.ThrowIfNull(blockName);
        ArgumentNullException.ThrowIfNull(subscriber);

        _subscribers.AddOrUpdate(
            blockName,
            _ => new ConcurrentBag<IEpochSubscriber> { subscriber },
            (_, bag) =>
            {
                bag.Add(subscriber);
                return bag;
            });
    }

    /// <summary>
    /// Unregisters a subscriber.
    /// Note: Removing from ConcurrentBag is not supported, so this tracks removal intent only.
    /// In practice, subscribers should remain registered for the lifetime of the graph.
    /// </summary>
    public void UnregisterSubscriber(string blockName, IEpochSubscriber subscriber)
    {
        // Note: ConcurrentBag doesn't support removal
        // In practice, subscribers should remain registered for graph lifetime
        // If unregistration is critical, consider using ImmutableList or other collection
    }

    /// <summary>
    /// Broadcasts an epoch marker to all registered subscribers.
    /// This is the core of the out-of-band control plane - epochs flow via events, not channels.
    /// </summary>
    public async ValueTask BroadcastEpochAsync(EpochMarker marker, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(EpochManager));
        ArgumentNullException.ThrowIfNull(marker);

        var tasks = new List<Task>();

        // Get snapshot of subscribers (ConcurrentBag iteration is thread-safe)
        foreach (var (blockName, subscribers) in _subscribers)
        {
            foreach (var subscriber in subscribers)
            {
                tasks.Add(NotifyAndAcknowledgeAsync(blockName, subscriber, marker, cancellationToken));
            }
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }

    private async Task NotifyAndAcknowledgeAsync(
        string blockName,
        IEpochSubscriber subscriber,
        EpochMarker marker,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var acknowledged = await subscriber.NotifyEpochAsync(marker, cancellationToken).ConfigureAwait(false);
            
            if (acknowledged)
            {
                stopwatch.Stop();
                EpochAcknowledged?.Invoke(this, new EpochAcknowledgmentEventArgs(
                    blockName,
                    marker,
                    stopwatch.Elapsed));
            }
        }
        catch (Exception)
        {
            // Log error but don't fail the entire broadcast
            // In production, this should use proper logging
            throw;
        }
    }

    /// <summary>
    /// Creates a new epoch marker with a monotonically increasing sequence number for the source.
    /// </summary>
    public EpochMarker CreateEpochMarker(string sourceId, Dictionary<string, object>? metadata = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(EpochManager));
        ArgumentNullException.ThrowIfNull(sourceId);

        var sequence = _nextSequence.AddOrUpdate(sourceId, 1, (_, current) => current + 1);
        
        return new EpochMarker(sourceId, sequence, DateTime.UtcNow)
        {
            Metadata = metadata
        };
    }

    /// <summary>
    /// Event handler for epoch emissions from publishers.
    /// Automatically broadcasts to all subscribers using a background task with proper error handling.
    /// </summary>
    private void OnEpochEmitted(object? sender, EpochEventArgs e)
    {
        // Use Task.Run with proper error handling to avoid unobserved exceptions
        _ = Task.Run(async () =>
        {
            try
            {
                await BroadcastEpochAsync(e.Marker, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // In production, log this error using ILogger
                // For POC, write to debug output
                System.Diagnostics.Debug.WriteLine($"Error broadcasting epoch: {ex.Message}");
                
                // Optionally: raise an event for error handling
                // EpochBroadcastError?.Invoke(this, new EpochErrorEventArgs(e.Marker, ex));
            }
        }, CancellationToken.None);
    }

    /// <summary>
    /// Gets statistics about epoch progression for debugging and monitoring.
    /// </summary>
    public EpochStatistics GetStatistics()
    {
        var sourceSequences = new Dictionary<string, long>(_nextSequence);
        var blockProgress = new Dictionary<string, Dictionary<string, long>>();

        // ConcurrentBag iteration is thread-safe
        foreach (var (blockName, subscribers) in _subscribers)
        {
            // Get progress from first subscriber (all should be similar)
            var firstSubscriber = subscribers.FirstOrDefault();
            if (firstSubscriber != null)
            {
                var progress = firstSubscriber.GetEpochProgress().GetSnapshot();
                blockProgress[blockName] = progress;
            }
        }

        return new EpochStatistics
        {
            SourceSequences = sourceSequences,
            BlockProgress = blockProgress,
            PublisherCount = _publishers.Count,
            SubscriberCount = _subscribers.Count
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Unsubscribe from all publishers
        foreach (var publisher in _publishers.Values)
        {
            publisher.EpochEmitted -= OnEpochEmitted;
        }

        _publishers.Clear();
        _subscribers.Clear();
        _nextSequence.Clear();
    }
}

/// <summary>
/// Statistics about epoch progression for monitoring and debugging.
/// </summary>
public class EpochStatistics
{
    /// <summary>
    /// Current sequence numbers for each source.
    /// </summary>
    public Dictionary<string, long> SourceSequences { get; init; } = new();

    /// <summary>
    /// Last seen sequences for each block from each source.
    /// </summary>
    public Dictionary<string, Dictionary<string, long>> BlockProgress { get; init; } = new();

    /// <summary>
    /// Number of registered publishers.
    /// </summary>
    public int PublisherCount { get; init; }

    /// <summary>
    /// Number of registered subscribers.
    /// </summary>
    public int SubscriberCount { get; init; }

    /// <summary>
    /// Calculates the minimum epoch progress (slowest block for each source).
    /// </summary>
    public Dictionary<string, long> GetMinimumProgress()
    {
        var result = new Dictionary<string, long>();

        foreach (var (source, sequence) in SourceSequences)
        {
            long minProgress = sequence;
            
            foreach (var blockProgress in BlockProgress.Values)
            {
                if (blockProgress.TryGetValue(source, out var progress))
                {
                    minProgress = Math.Min(minProgress, progress);
                }
            }
            
            result[source] = minProgress;
        }

        return result;
    }
}

/// <summary>
/// Edge strategy that uses out-of-band epoch control plane.
/// Data flows through channels normally, epochs propagate via EpochManager.
/// This provides zero hot-path overhead on data operations.
/// </summary>
public class EpochControlPlaneEdgeStrategy : EdgeStrategy
{
    private readonly EpochManager _epochManager;

    public EpochControlPlaneEdgeStrategy(
        EpochManager epochManager,
        EdgeType edgeType = EdgeType.Broadcast,
        BufferMode bufferMode = BufferMode.Bounded,
        int bufferCapacity = 100)
        : base(edgeType, bufferMode, bufferCapacity)
    {
        _epochManager = epochManager ?? throw new ArgumentNullException(nameof(epochManager));
    }

    public override (Dictionary<IBlock, object> writers, Dictionary<IBlock, object> readers) CreateTypedChannels(
        Type dataType,
        IBlock sourceBlock,
        IReadOnlyList<IBlock> targetBlocks)
    {
        // Register epoch wiring
        if (sourceBlock is IEpochPublisher publisher)
        {
            _epochManager.RegisterPublisher(sourceBlock.Name, publisher);
        }

        foreach (var target in targetBlocks)
        {
            if (target is IEpochSubscriber subscriber)
            {
                _epochManager.RegisterSubscriber(target.Name, subscriber);
            }
        }

        // Create normal data channels (epochs bypass these entirely)
        var writers = new Dictionary<IBlock, object>();
        var readers = new Dictionary<IBlock, object>();

        if (EdgeType == EdgeType.Broadcast)
        {
            // Each target gets its own channel
            foreach (var target in targetBlocks)
            {
                var (writer, reader) = TypedChannelFactory.CreateTypedChannel(
                    dataType,
                    BufferMode,
                    BufferCapacity,
                    singleReader: true,
                    singleWriter: false);

                writers[target] = writer;
                readers[target] = reader;
            }
        }
        else if (EdgeType == EdgeType.Competing)
        {
            // All targets share a single channel
            var (sharedWriter, sharedReader) = TypedChannelFactory.CreateTypedChannel(
                dataType,
                BufferMode,
                BufferCapacity,
                singleReader: false,
                singleWriter: false);

            foreach (var target in targetBlocks)
            {
                writers[target] = sharedWriter;
                readers[target] = sharedReader;
            }
        }
        else
        {
            throw new NotSupportedException($"EdgeType {EdgeType} not supported in EpochControlPlaneEdgeStrategy");
        }

        return (writers, readers);
    }

    public override async Task RouteTypedItemAsync<T>(
        T item,
        Dictionary<IBlock, ChannelWriter<T>> typedWriters,
        CancellationToken cancellationToken)
    {
        // All data items route normally through channels
        // Epochs are handled via EpochManager, completely bypassing this path
        switch (EdgeType)
        {
            case EdgeType.Broadcast:
                await BroadcastItemAsync(item, typedWriters, cancellationToken).ConfigureAwait(false);
                break;
            case EdgeType.Competing:
                await CompeteItemAsync(item, typedWriters, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new NotSupportedException($"EdgeType {EdgeType} not supported");
        }
    }

    private static async Task BroadcastItemAsync<T>(
        T item,
        Dictionary<IBlock, ChannelWriter<T>> typedWriters,
        CancellationToken cancellationToken)
    {
        var tasks = typedWriters.Values
            .Select(w => w.WriteAsync(item, cancellationToken).AsTask())
            .ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static async Task CompeteItemAsync<T>(
        T item,
        Dictionary<IBlock, ChannelWriter<T>> typedWriters,
        CancellationToken cancellationToken)
    {
        // Get the first writer (all point to same channel in competing)
        var writer = typedWriters.Values.First();
        await writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Helper factory for creating epoch control plane strategies.
/// </summary>
public static class EpochControlPlaneFactory
{
    public static EpochControlPlaneEdgeStrategy CreateBroadcast(
        EpochManager epochManager,
        BufferMode bufferMode = BufferMode.Bounded,
        int bufferCapacity = 100)
    {
        return new EpochControlPlaneEdgeStrategy(
            epochManager,
            EdgeType.Broadcast,
            bufferMode,
            bufferCapacity);
    }

    public static EpochControlPlaneEdgeStrategy CreateCompeting(
        EpochManager epochManager,
        BufferMode bufferMode = BufferMode.Bounded,
        int bufferCapacity = 100)
    {
        return new EpochControlPlaneEdgeStrategy(
            epochManager,
            EdgeType.Competing,
            bufferMode,
            bufferCapacity);
    }
}
