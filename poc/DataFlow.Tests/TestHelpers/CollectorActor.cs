namespace DataFlow.POC.Tests.TestHelpers;

using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using DataFlow.POC.Registry;

/// <summary>
/// Generic collector actor that accumulates items into a provided list.
/// Eliminates the need for 15+ specialized collector implementations across tests.
/// 
/// Usage:
/// var collected = new List<int>();
/// var collector = new CollectorActor<int>(collected);
/// 
/// // With callback (e.g. for cancellation or side effects):
/// var collector = new CollectorActor<int>(collected, onCollect: item => { ... });
/// </summary>
public class CollectorActor<T> : IStreamActor<T, object>
{
    private readonly List<T> _collected;
    private readonly Action<T>? _onCollect;

    public CollectorActor(List<T> collected, Action<T>? onCollect = null)
    {
        _collected = collected ?? throw new ArgumentNullException(nameof(collected));
        _onCollect = onCollect;
    }

    public async IAsyncEnumerable<object> RunAsync(
        IAsyncEnumerable<T> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            _collected.Add(item);
            _onCollect?.Invoke(item);
        }
        yield break;
    }
}
