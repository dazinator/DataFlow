namespace DataFlow.POC.Tests.TestHelpers;

using DataFlow.POC.Core;

/// <summary>
/// Generic collector actor that accumulates items into a provided list.
/// Eliminates the need for 15+ specialized collector implementations across tests.
/// 
/// Usage:
/// var collected = new List<int>();
/// var collector = new CollectorActor<int>(collected);
/// </summary>
public class CollectorActor<T> : IStreamActor<T, object>
{
    private readonly List<T> _collected;

    public CollectorActor(List<T> collected)
    {
        _collected = collected ?? throw new ArgumentNullException(nameof(collected));
    }

    public async IAsyncEnumerable<object> RunAsync(
        IAsyncEnumerable<T> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            _collected.Add(item);
        }
        yield break;
    }
}
