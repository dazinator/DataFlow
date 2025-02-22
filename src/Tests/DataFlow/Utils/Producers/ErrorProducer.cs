namespace Tests.DataFlow.Utils.Producers;

using System.Runtime.CompilerServices;

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

    public async IAsyncEnumerable<T> ProduceAsync(
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
}
