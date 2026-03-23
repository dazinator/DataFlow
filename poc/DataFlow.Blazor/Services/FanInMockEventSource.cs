namespace DataFlow.Blazor.Services;

using System.Runtime.CompilerServices;
using DataFlow.Blazor.Events;

/// <summary>
/// Mock event source demonstrating fan-in topology with multiple producers.
/// Shows: [producer-a, producer-b] -> buffer -> batch -> processor
/// </summary>
public class FanInMockEventSource : IEventSource
{
    private readonly Random _random = Random.Shared;

    public async IAsyncEnumerable<IDataFlowEvent> GetEventsAsync(
        Guid invocationId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Flow started
        yield return new FlowStartedEvent(
            invocationId,
            "Fan-In Pipeline (Multiple Producers)",
            DateTime.UtcNow
        );

        await Task.Delay(100, cancellationToken);

        // Two producer blocks start
        yield return new BlockStartedEvent("producer-a", "ProducerBlock", DateTime.UtcNow);
        yield return new BlockStartedEvent("producer-b", "ProducerBlock", DateTime.UtcNow);
        
        await Task.Delay(100, cancellationToken);

        // Buffer block (merge point)
        yield return new BlockStartedEvent("buffer", "BufferBlock", DateTime.UtcNow);

        await Task.Delay(100, cancellationToken);

        // Batch and processor
        yield return new BlockStartedEvent("batch", "BatchBlock", DateTime.UtcNow);
        
        await Task.Delay(100, cancellationToken);
        
        yield return new BlockStartedEvent("processor", "ProcessorBlock", DateTime.UtcNow);

        // Simulate progress over time
        for (int i = 1; i <= 20; i++)
        {
            await Task.Delay(200, cancellationToken);

            // Producer A progress (source: consumed=0, faster)
            yield return new BlockMetricsEvent("producer-a", ItemsConsumed: 0, ItemsProduced: i * 60, DateTime.UtcNow);
            yield return new ChannelStatsEvent("producer-a", "buffer", 50, _random.Next(10, 40), DateTime.UtcNow);

            // Producer B progress (source: consumed=0, slower)
            yield return new BlockMetricsEvent("producer-b", ItemsConsumed: 0, ItemsProduced: i * 40, DateTime.UtcNow);
            yield return new ChannelStatsEvent("producer-b", "buffer", 50, _random.Next(10, 40), DateTime.UtcNow);

            // Buffer progress (fan-in: consumes from both producers, 1:1 pass-through)
            if (i > 1)
            {
                yield return new BlockMetricsEvent("buffer", ItemsConsumed: (i - 1) * 100, ItemsProduced: (i - 1) * 100, DateTime.UtcNow);
                yield return new ChannelStatsEvent("buffer", "batch", 200, _random.Next(50, 150), DateTime.UtcNow);
            }

            // Batch progress (batches 100→20 items)
            if (i > 2)
            {
                yield return new BlockMetricsEvent("batch", ItemsConsumed: (i - 2) * 100, ItemsProduced: (i - 2) * 20, DateTime.UtcNow);
                yield return new ChannelStatsEvent("batch", "processor", 100, _random.Next(20, 80), DateTime.UtcNow);
            }

            // Processor progress (terminal sink: consumed=N, output=0)
            if (i > 3)
            {
                yield return new BlockMetricsEvent("processor", ItemsConsumed: (i - 3) * 20, ItemsProduced: 0, DateTime.UtcNow);
            }
        }

        await Task.Delay(500, cancellationToken);

        // All blocks complete
        yield return new BlockCompletedEvent("producer-a", true, DateTime.UtcNow);
        yield return new BlockCompletedEvent("producer-b", true, DateTime.UtcNow);
        
        await Task.Delay(200, cancellationToken);
        yield return new BlockCompletedEvent("buffer", true, DateTime.UtcNow);
        
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
        return Task.FromResult<FlowSnapshot?>(null);
    }
}
