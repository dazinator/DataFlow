// ReSharper disable once CheckNamespace

namespace Uniun.DataFlow;

// Helper extension to wrap IAsyncEnumerable<T> and call an action for each item
public static class AsyncEnumerableExtensions
{
    public static async IAsyncEnumerable<T> DecorateWithCallbackAfterEachItem<T>(
        this IAsyncEnumerable<T> source,
        Action onItem,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
            onItem();
        }
    }
}
