namespace DataFlow.POC.Tests.TestHelpers;

using DataFlow.POC.Core;
using System.Runtime.CompilerServices;

/// <summary>
/// Helper methods for creating test data streams.
/// Reduces boilerplate for common test data generation scenarios.
/// </summary>
public static class TestStreams
{
    /// <summary>
    /// Creates an async enumerable from an array of items.
    /// 
    /// Usage:
    /// var stream = TestStreams.FromArray(1, 2, 3, 4, 5);
    /// </summary>
    public static async IAsyncEnumerable<T> FromArray<T>(
        params T[] items)
    {
        foreach (var item in items)
        {
            yield return item;
        }
    }

    /// <summary>
    /// Creates an async enumerable from a list.
    /// </summary>
    public static async IAsyncEnumerable<T> FromList<T>(
        List<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
        }
    }

    /// <summary>
    /// Creates an async enumerable of integers from 1 to count.
    /// 
    /// Usage:
    /// var stream = TestStreams.Integers(10); // 1, 2, 3, ..., 10
    /// </summary>
    public static async IAsyncEnumerable<int> Integers(
        int count,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (int i = 1; i <= count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return i;
        }
    }

    /// <summary>
    /// Creates an async enumerable of integers within a range.
    /// 
    /// Usage:
    /// var stream = TestStreams.Range(10, 20); // 10, 11, 12, ..., 20
    /// </summary>
    public static async IAsyncEnumerable<int> Range(
        int start,
        int end,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (int i = start; i <= end; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return i;
        }
    }

    /// <summary>
    /// Creates an empty async enumerable.
    /// </summary>
    public static async IAsyncEnumerable<T> Empty<T>()
    {
        yield break;
    }

    /// <summary>
    /// Collects all items from an async enumerable into a list.
    /// 
    /// Usage:
    /// var results = await TestStreams.CollectAsync(stream);
    /// </summary>
    public static async Task<List<T>> CollectAsync<T>(
        IAsyncEnumerable<T> stream,
        CancellationToken cancellationToken = default)
    {
        var results = new List<T>();
        await foreach (var item in stream.WithCancellation(cancellationToken))
        {
            results.Add(item);
        }
        return results;
    }
}
