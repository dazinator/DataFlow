// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Blocks;

/// <summary>
/// Represents a source that can provide data as an async enumerable.
/// This abstraction allows blocks to connect without requiring channels.
/// </summary>
/// <typeparam name="T">The type of items produced</typeparam>
public interface IAsyncEnumerableSource<T>
{
    /// <summary>
    /// Gets an async enumerable that produces items from this source.
    /// This method may be called multiple times by different downstream blocks.
    /// </summary>
    /// <param name="target">The target block that will consume the items</param>
    /// <param name="cancellationToken">Cancellation token for the enumeration</param>
    /// <returns>An async enumerable of items</returns>
    IAsyncEnumerable<T> GetAsyncEnumerable(ITargetBlock<T> target, CancellationToken cancellationToken);
}
