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

    public async IAsyncEnumerable<object> GetEventsAsync(
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

            // Producer A progress (faster)
            yield return new BlockProgressEvent("producer-a", i * 60, DateTime.UtcNow);
            yield return new ChannelStatsEvent("producer-a", 50, _random.Next(10, 40), DateTime.UtcNow);

            // Producer B progress (slower)
            yield return new BlockProgressEvent("producer-b", i * 40, DateTime.UtcNow);
            yield return new ChannelStatsEvent("producer-b", 50, _random.Next(10, 40), DateTime.UtcNow);

            // Buffer progress (combines both producers)
            if (i > 1)
            {
                yield return new BlockProgressEvent("buffer", (i - 1) * 100, DateTime.UtcNow);
                yield return new ChannelStatsEvent("buffer", 200, _random.Next(50, 150), DateTime.UtcNow);
            }

            // Batch progress
            if (i > 2)
            {
                yield return new BlockProgressEvent("batch", (i - 2) * 20, DateTime.UtcNow);
                yield return new ChannelStatsEvent("batch", 100, _random.Next(20, 80), DateTime.UtcNow);
            }

            // Processor progress
            if (i > 3)
            {
                yield return new BlockProgressEvent("processor", (i - 3) * 20, DateTime.UtcNow);
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
