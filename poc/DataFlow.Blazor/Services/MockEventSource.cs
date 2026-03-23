namespace DataFlow.Blazor.Services;

using System.Runtime.CompilerServices;
using DataFlow.Blazor.Events;

/// <summary>
/// Mock event source that generates sample events for testing the UI.
/// Simulates a real-time DataFlow execution with various blocks and channels.
/// </summary>
public class MockEventSource : IEventSource
{
    private readonly Random _random = Random.Shared;

    public async IAsyncEnumerable<IDataFlowEvent> GetEventsAsync(
        Guid invocationId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Flow started
        yield return new FlowStartedEvent(
            invocationId,
            "Sample DataFlow Pipeline",
            DateTime.UtcNow
        );

        await Task.Delay(100, cancellationToken);

        // Producer block starts
        yield return new BlockStartedEvent("producer", "ProducerBlock", DateTime.UtcNow);
        
        await Task.Delay(100, cancellationToken);

        // Transform block starts
        yield return new BlockStartedEvent("transform", "TransformBlock", DateTime.UtcNow);

        await Task.Delay(100, cancellationToken);

        // Batch block starts
        yield return new BlockStartedEvent("batch", "BatchBlock", DateTime.UtcNow);

        await Task.Delay(100, cancellationToken);

        // Processor block starts
        yield return new BlockStartedEvent("processor", "ProcessorBlock", DateTime.UtcNow);

        // Simulate progress over time
        for (int i = 1; i <= 20; i++)
        {
            await Task.Delay(200, cancellationToken);

            // Producer progress (source: consumed=0)
            yield return new BlockMetricsEvent("producer", ItemsConsumed: 0, ItemsProduced: i * 50, DateTime.UtcNow);
            yield return new ChannelStatsEvent("producer", "transform", 100, _random.Next(20, 80), DateTime.UtcNow);

            // Transform progress (slightly behind; 1:1 transformer)
            if (i > 1)
            {
                yield return new BlockMetricsEvent("transform", ItemsConsumed: (i - 1) * 48, ItemsProduced: (i - 1) * 48, DateTime.UtcNow);
                yield return new ChannelStatsEvent("transform", "batch", 100, _random.Next(15, 70), DateTime.UtcNow);
            }

            // Batch progress (even more behind; batches 48→10 items)
            if (i > 2)
            {
                yield return new BlockMetricsEvent("batch", ItemsConsumed: (i - 2) * 48, ItemsProduced: (i - 2) * 10, DateTime.UtcNow);
                yield return new ChannelStatsEvent("batch", "processor", 50, _random.Next(5, 40), DateTime.UtcNow);
            }

            // Processor progress (terminal sink: consumed=N, output=0)
            if (i > 3)
            {
                yield return new BlockMetricsEvent("processor", ItemsConsumed: (i - 3) * 10, ItemsProduced: 0, DateTime.UtcNow);
            }
        }

        await Task.Delay(500, cancellationToken);

        // All blocks complete
        yield return new BlockCompletedEvent("producer", true, DateTime.UtcNow);
        
        await Task.Delay(200, cancellationToken);
        yield return new BlockCompletedEvent("transform", true, DateTime.UtcNow);
        
        await Task.Delay(200, cancellationToken);
        yield return new BlockCompletedEvent("batch", true, DateTime.UtcNow);
        
        await Task.Delay(200, cancellationToken);
        yield return new BlockCompletedEvent("processor", true, DateTime.UtcNow);

        await Task.Delay(100, cancellationToken);

        // Flow completed
        yield return new FlowCompletedEvent(invocationId, true, DateTime.UtcNow);
    }

    public Task<FlowSnapshot?> GetSnapshotAsync(Guid invocationId, CancellationToken cancellationToken = default)
    {
        // For this mock, we don't have a pre-built snapshot
        // In a real implementation, this would query a database or event store
        return Task.FromResult<FlowSnapshot?>(null);
    }
}
