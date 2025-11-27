namespace DataFlow.POC.Core;

using System.Threading.Channels;

/// <summary>
/// Epoch stream backed by a channel for edge routing.
/// This enables edges to unwrap epoch streams, route individual items,
/// and re-wrap them into new epoch streams for each downstream consumer.
/// </summary>
internal sealed class ChannelBackedEpochStream<T> : IEpochStream<T>
{
    private readonly Channel<T> _channel;
    
    public EpochVector Epoch { get; }
    public IAsyncEnumerable<T> Items => _channel.Reader.ReadAllAsync();
    public IEpoch? EpochScope { get; }
    
    /// <summary>
    /// Creates a new channel-backed epoch stream.
    /// </summary>
    /// <param name="epochVector">The epoch vector for this stream</param>
    /// <param name="epochScope">Optional epoch scope with DI container</param>
    /// <param name="channel">The backing channel</param>
    public ChannelBackedEpochStream(
        EpochVector epochVector,
        IEpoch? epochScope,
        Channel<T> channel)
    {
        Epoch = epochVector ?? throw new ArgumentNullException(nameof(epochVector));
        EpochScope = epochScope;
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
    }
    
    /// <summary>
    /// Gets the channel writer for routing items into this epoch stream.
    /// Internal use only - edges use this to populate the stream.
    /// </summary>
    internal ChannelWriter<T> GetWriter() => _channel.Writer;
    
    /// <summary>
    /// Completes writing to this epoch stream.
    /// Called by edges when all items have been routed.
    /// </summary>
    internal void CompleteWriting(Exception? exception = null)
    {
        _channel.Writer.Complete(exception);
    }
    
    public ValueTask DisposeAsync()
    {
        // Don't dispose epoch scope here - coordinator manages epoch disposal
        return ValueTask.CompletedTask;
    }
}
