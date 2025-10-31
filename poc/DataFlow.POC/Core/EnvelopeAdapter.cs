namespace DataFlow.POC.Core;

using System.Runtime.CompilerServices;

/// <summary>
/// Provides utilities for adapting between plain data streams and envelope streams.
/// Enables backward compatibility with existing blocks that work with plain T.
/// </summary>
public static class EnvelopeAdapter
{
    /// <summary>
    /// Wraps a plain data stream into an envelope stream.
    /// Each item of type T is wrapped in a DataItem&lt;T&gt; envelope.
    /// </summary>
    /// <typeparam name="T">The type of data items</typeparam>
    /// <param name="source">The source stream of plain data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A stream of envelopes</returns>
    public static async IAsyncEnumerable<IDataEnvelope> WrapInEnvelopes<T>(
        IAsyncEnumerable<T> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return new DataItem<T>(item);
        }
    }

    /// <summary>
    /// Unwraps an envelope stream back to a plain data stream.
    /// Extracts values from DataItem&lt;T&gt; envelopes and ignores control signals.
    /// </summary>
    /// <typeparam name="T">The type of data items</typeparam>
    /// <param name="source">The source stream of envelopes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A stream of plain data</returns>
    public static async IAsyncEnumerable<T> UnwrapEnvelopes<T>(
        IAsyncEnumerable<IDataEnvelope> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var envelope in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (envelope.TryGetValue<T>(out var value) && value is not null)
            {
                yield return value;
            }
            // Control signals are silently ignored when unwrapping
        }
    }

    /// <summary>
    /// Filters an envelope stream to only include data items.
    /// Control signals are filtered out.
    /// </summary>
    /// <param name="source">The source stream of envelopes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A stream of data item envelopes only</returns>
    public static async IAsyncEnumerable<IDataEnvelope> FilterDataItems(
        IAsyncEnumerable<IDataEnvelope> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var envelope in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (envelope.IsDataItem())
            {
                yield return envelope;
            }
        }
    }

    /// <summary>
    /// Filters an envelope stream to only include control signals.
    /// Data items are filtered out.
    /// </summary>
    /// <param name="source">The source stream of envelopes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A stream of control signal envelopes only</returns>
    public static async IAsyncEnumerable<IDataEnvelope> FilterControlSignals(
        IAsyncEnumerable<IDataEnvelope> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var envelope in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (envelope.IsControlSignal())
            {
                yield return envelope;
            }
        }
    }
}
