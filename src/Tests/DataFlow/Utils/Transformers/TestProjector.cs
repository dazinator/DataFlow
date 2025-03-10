namespace Tests.DataFlow.Utils.Transformers;
public class TestProjector<TIn, TOut> : IStreamTransformer<TIn, TOut>
{
    private readonly Func<TIn, IEnumerable<TOut>> _projection;
    private readonly Action<TIn>? _onProjectStarted;
    private readonly Action<TOut>? _onItemProjected;
    private readonly TimeSpan? _delay;
    private readonly IConcurrencyTracker? _tracker;

    public TestProjector(
        Func<TIn, IEnumerable<TOut>> projection,
        Action<TIn>? onProjectStarted = null,
        Action<TOut>? onItemProjected = null,
        TimeSpan? delay = null,
        IConcurrencyTracker? tracker = null)
    {
        _projection = projection;
        _onProjectStarted = onProjectStarted;
        _onItemProjected = onItemProjected;
        _delay = delay;
        _tracker = tracker;
    }

    public async IAsyncEnumerable<TOut> TransformAsync(
        IAsyncEnumerable<TIn> input,
        CancellationToken cancellationToken)
    {
        _tracker?.Enter();
        try
        {
            await foreach (var inputItem in input.WithCancellation(cancellationToken))
            {
                _onProjectStarted?.Invoke(inputItem);

                foreach (var outputItem in _projection(inputItem))
                {
                    if (_delay.HasValue)
                    {
                        await Task.Delay(_delay.Value, cancellationToken);
                    }

                    _onItemProjected?.Invoke(outputItem);
                    yield return outputItem;
                }
            }
        }
        finally
        {
            _tracker?.Exit();
        }
    }
}
