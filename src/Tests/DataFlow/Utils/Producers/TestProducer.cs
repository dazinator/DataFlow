namespace Tests.DataFlow.Utils.Producers;

using System.Runtime.CompilerServices;

// Generic test producer classes (can go in a shared Utils/TestHelpers folder)
public class TestProducer<T> : IStreamProducer<T>
{
    private readonly IEnumerable<T> _items;
    private readonly Action<T>? _onItemProduced;
    private readonly TimeSpan _delay;

    public TestProducer(IEnumerable<T> items, Action<T>? onItemProduced = null, TimeSpan? delay = null)
    {
        _items = items;
        _onItemProduced = onItemProduced;
        _delay = delay ?? TimeSpan.FromMilliseconds(10);
    }

    public async IAsyncEnumerable<T> ProduceAsync(
        [EnumeratorCancellation] CancellationToken cancellation)
    {
        foreach (var item in _items)
        {
            cancellation.ThrowIfCancellationRequested();
            _onItemProduced?.Invoke(item);
            yield return item;
            await Task.Delay(_delay, cancellation);
        }
    }
}
