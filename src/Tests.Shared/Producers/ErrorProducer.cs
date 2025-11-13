namespace Tests.DataFlow.Utils.Producers;

using System.Runtime.CompilerServices;
using Uniun.DataFlow;

public class ErrorProducer<T> : IStreamProducer<T>
{
    private readonly IEnumerable<T> _items;
    private readonly Action<T>? _onItemProduced;
    private readonly Func<T, bool> _shouldError;
    private readonly string _errorMessage;

    public ErrorProducer(
        IEnumerable<T> items,
        Func<T, bool> shouldError,
        Action<T>? onItemProduced = null,
        string? errorMessage = null)
    {
        _items = items;
        _shouldError = shouldError;
        _onItemProduced = onItemProduced;
        _errorMessage = errorMessage ?? "Simulated error in producer";
    }

    // Async iterator without await - synchronous enumeration wrapped in async enumerable interface
#pragma warning disable CS1998
    public async IAsyncEnumerable<T> ProduceAsync(IDataFlowContext context,
        [EnumeratorCancellation] CancellationToken cancellation)
    {
        foreach (var item in _items)
        {
            cancellation.ThrowIfCancellationRequested();
            _onItemProduced?.Invoke(item);
            yield return item;

            if (_shouldError(item))
            {
                throw new InvalidOperationException(_errorMessage);
            }
        }
    }
#pragma warning restore CS1998
}
