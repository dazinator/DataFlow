namespace DataFlow.Blazor.Services;

using System.Runtime.CompilerServices;
using DataFlow.Blazor.Events;

/// <summary>
/// Mock event source demonstrating branching topology with routing blocks.
/// Shows fan-out pattern: producer -> router -> [processor-high, processor-low]
/// </summary>
public class BranchingMockEventSource : IEventSource
{
    private readonly Random _random = Random.Shared;

    public async IAsyncEnumerable<IDataFlowEvent> GetEventsAsync(
        Guid invocationId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Flow started
        yield return new FlowStartedEvent(
            invocationId,
            "Branching Pipeline (Fan-Out via Router)",
            DateTime.UtcNow
        );

        await Task.Delay(100, cancellationToken);

        // Producer block starts
        yield return new BlockStartedEvent("producer", "ProducerBlock", DateTime.UtcNow);
        
        await Task.Delay(100, cancellationToken);

        // Router block starts (fan-out point)
        yield return new BlockStartedEvent("router", "RoutingBlock", DateTime.UtcNow);

        await Task.Delay(100, cancellationToken);

        // Two processor blocks for different priorities
        yield return new BlockStartedEvent("processor-high", "ProcessorBlock", DateTime.UtcNow);
        yield return new BlockStartedEvent("processor-low", "ProcessorBlock", DateTime.UtcNow);

        // Simulate progress over time
        for (int i = 1; i <= 20; i++)
        {
            await Task.Delay(200, cancellationToken);

            // Producer progress (source: consumed=0)
            yield return new BlockMetricsEvent("producer", ItemsConsumed: 0, ItemsOutput: i * 50, DateTime.UtcNow);
            yield return new ChannelStatsEvent("producer", "router", 100, _random.Next(20, 80), DateTime.UtcNow);

            // Router progress (1:1 pass-through; routes all items to two downstream processors)
            if (i > 1)
            {
                yield return new BlockMetricsEvent("router", ItemsConsumed: (i - 1) * 50, ItemsOutput: (i - 1) * 50, DateTime.UtcNow);
                yield return new ChannelStatsEvent("router", "processor-high", 100, _random.Next(5, 40), DateTime.UtcNow);
                yield return new ChannelStatsEvent("router", "processor-low", 100, _random.Next(5, 40), DateTime.UtcNow);
            }

            // High priority processor (terminal sink: ~70% of items)
            if (i > 2)
            {
                yield return new BlockMetricsEvent("processor-high", ItemsConsumed: (int)((i - 2) * 35), ItemsOutput: 0, DateTime.UtcNow);
            }

            // Low priority processor (terminal sink: ~30% of items)
            if (i > 2)
            {
                yield return new BlockMetricsEvent("processor-low", ItemsConsumed: (int)((i - 2) * 15), ItemsOutput: 0, DateTime.UtcNow);
            }
        }

        await Task.Delay(500, cancellationToken);

        // All blocks complete
        yield return new BlockCompletedEvent("producer", true, DateTime.UtcNow);
        
        await Task.Delay(200, cancellationToken);
        yield return new BlockCompletedEvent("router", true, DateTime.UtcNow);
        
        await Task.Delay(200, cancellationToken);
        yield return new BlockCompletedEvent("processor-high", true, DateTime.UtcNow);
        yield return new BlockCompletedEvent("processor-low", true, DateTime.UtcNow);

        await Task.Delay(100, cancellationToken);

        // Flow completed
        yield return new FlowCompletedEvent(invocationId, true, DateTime.UtcNow);
    }

    public Task<FlowSnapshot?> GetSnapshotAsync(Guid invocationId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<FlowSnapshot?>(null);
    }
}
