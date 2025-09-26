namespace Tests.DataFlow.Utils.Producers;

using System.Runtime.CompilerServices;

public class ConcurrencyTestProducer<T> : IStreamProducer<T>
{
    private readonly IEnumerable<T> _items;
    private readonly Action<T>? _onItemProduced;
    private readonly IConcurrencyTracker _tracker;
    private readonly TimeSpan _workDelay;

    public ConcurrencyTestProducer(
        IEnumerable<T> items,
        IConcurrencyTracker tracker,
        Action<T>? onItemProduced = null,
        TimeSpan? workDelay = null)
    {
        _items = items;
        _tracker = tracker;
        _onItemProduced = onItemProduced;
        _workDelay = workDelay ?? TimeSpan.FromMilliseconds(50);
    }

    public async IAsyncEnumerable<T> ProduceAsync(IDataFlowContext context,
        [EnumeratorCancellation] CancellationToken cancellation)
    {
        _tracker.Enter();
        try
        {
            foreach (var item in _items)
            {
                cancellation.ThrowIfCancellationRequested();
                await Task.Delay(_workDelay, cancellation);
                _onItemProduced?.Invoke(item);
                yield return item;
            }
        }
        finally
        {
            _tracker.Exit();
        }
    }
}
