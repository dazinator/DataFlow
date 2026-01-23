namespace DataFlow.POC.Tests.TestHelpers;

using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using DataFlow.POC.Registry;

/// <summary>
/// Generic transform actor for common test transformations.
/// </summary>
public class TransformActor<TIn, TOut> : IStreamActor<TIn, TOut>
{
    private readonly Func<TIn, TOut> _transform;

    public TransformActor(Func<TIn, TOut> transform)
    {
        _transform = transform ?? throw new ArgumentNullException(nameof(transform));
    }

    public async IAsyncEnumerable<TOut> RunAsync(
        IAsyncEnumerable<TIn> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            yield return _transform(item);
        }
    }
}

/// <summary>
/// Generic filter actor for test filtering scenarios.
/// </summary>
public class FilterActor<T> : IStreamActor<T, T>
{
    private readonly Func<T, bool> _predicate;

    public FilterActor(Func<T, bool> predicate)
    {
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    public async IAsyncEnumerable<T> RunAsync(
        IAsyncEnumerable<T> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            if (_predicate(item))
            {
                yield return item;
            }
        }
    }
}
