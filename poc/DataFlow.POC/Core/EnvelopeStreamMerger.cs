namespace DataFlow.POC.Core;

using System.Runtime.CompilerServices;
using System.Threading.Channels;

/// <summary>
/// Utility for merging multiple envelope streams into a single stream.
/// Useful for merging data and control signal streams in competing envelope edges.
/// </summary>
public static class EnvelopeStreamMerger
{
    /// <summary>
    /// Merges two envelope streams into one, interleaving items as they become available.
    /// Order between streams is not guaranteed, but order within each stream is preserved.
    /// </summary>
    public static async IAsyncEnumerable<IDataEnvelope> Merge(
        IAsyncEnumerable<IDataEnvelope> stream1,
        IAsyncEnumerable<IDataEnvelope> stream2,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<IDataEnvelope>();
        var writer = channel.Writer;

        // Start tasks to read from both streams
        var task1 = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in stream1.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    await writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Cancellation is expected
            }
        }, cancellationToken);

        var task2 = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in stream2.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    await writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Cancellation is expected
            }
        }, cancellationToken);

        // Complete writer when both streams are done
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.WhenAll(task1, task2).ConfigureAwait(false);
            }
            finally
            {
                writer.Complete();
            }
        }, cancellationToken);

        // Read from the channel
        await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    /// <summary>
    /// Merges multiple envelope streams into one, interleaving items as they become available.
    /// Order between streams is not guaranteed, but order within each stream is preserved.
    /// </summary>
    public static async IAsyncEnumerable<IDataEnvelope> MergeMany(
        IEnumerable<IAsyncEnumerable<IDataEnvelope>> streams,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var streamList = streams.ToList();
        if (streamList.Count == 0)
        {
            yield break;
        }

        if (streamList.Count == 1)
        {
            await foreach (var item in streamList[0].WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                yield return item;
            }
            yield break;
        }

        var channel = Channel.CreateUnbounded<IDataEnvelope>();
        var writer = channel.Writer;

        // Start a task for each stream
        var tasks = streamList.Select(stream => Task.Run(async () =>
        {
            try
            {
                await foreach (var item in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    await writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Cancellation is expected
            }
        }, cancellationToken)).ToList();

        // Complete writer when all streams are done
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            finally
            {
                writer.Complete();
            }
        }, cancellationToken);

        // Read from the channel
        await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }
}
