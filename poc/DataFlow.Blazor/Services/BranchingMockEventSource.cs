namespace DataFlow.Blazor.Services;

using System.Runtime.CompilerServices;
using DataFlow.Blazor.Events;

/// <summary>
/// Mock event source demonstrating branching topology with routing blocks.
/// Shows fan-out pattern: producer -> router -> [processor-high, processor-low]
/// </summary>
public class BranchingMockEventSource : IEventSource
{
    private readonly Random _random = new();

    public async IAsyncEnumerable<object> GetEventsAsync(
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

            // Producer progress
            yield return new BlockProgressEvent("producer", i * 50, DateTime.UtcNow);
            yield return new ChannelStatsEvent("producer", 100, _random.Next(20, 80), DateTime.UtcNow);

            // Router progress
            if (i > 1)
            {
                yield return new BlockProgressEvent("router", (i - 1) * 50, DateTime.UtcNow);
                yield return new ChannelStatsEvent("router", 100, _random.Next(15, 70), DateTime.UtcNow);
            }

            // High priority processor (processes ~70% of items)
            if (i > 2)
            {
                yield return new BlockProgressEvent("processor-high", (int)((i - 2) * 35), DateTime.UtcNow);
            }

            // Low priority processor (processes ~30% of items)
            if (i > 2)
            {
                yield return new BlockProgressEvent("processor-low", (int)((i - 2) * 15), DateTime.UtcNow);
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
