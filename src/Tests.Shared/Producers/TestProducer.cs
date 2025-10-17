namespace Tests.DataFlow.Utils.Producers;

using System.Runtime.CompilerServices;
using Uniun.DataFlow;

// Generic test producer classes (can go in a shared Utils/TestHelpers folder)
public class TestProducer<T> : IStreamProducer<T>
{
    private readonly IEnumerable<T> _items;
    private readonly Action<T>? _onItemProduced;
    private readonly TimeSpan? _delay;

    public TestProducer(IEnumerable<T> items, Action<T>? onItemProduced = null, TimeSpan? delay = null)
    {
        _items = items;
        _onItemProduced = onItemProduced;
        _delay = delay ?? TimeSpan.FromMilliseconds(10);
    }

    public async IAsyncEnumerable<T> ProduceAsync(IDataFlowContext context,
        [EnumeratorCancellation] CancellationToken cancellation)
    {
        if (_delay is null)
        {
            foreach (var item in _items)
            {
                cancellation.ThrowIfCancellationRequested();
                _onItemProduced?.Invoke(item);
                yield return item;
            }
        }
        else
        {
            foreach (var item in _items)
            {
                cancellation.ThrowIfCancellationRequested();
                await Task.Delay(_delay.Value, cancellation);
                _onItemProduced?.Invoke(item);
                yield return item;
            }
        }

    }


}
