namespace DataFlow.POC.Core;

using System.Threading.Channels;

/// <summary>
/// Node that manages epoch stream channel and publishes epochs for processing.
/// Acts as the source in the epoch processing pipeline, providing a channel
/// for epoch publishing that processor nodes can consume from.
/// </summary>
/// <remarks>
/// This node is purely a channel wrapper for epoch streaming. It does not create
/// or manage epochs - that responsibility belongs to source actors via IEpochCoordinator.
/// The coordinator is owned by the DataFlowGraph and injected into source actors.
/// </remarks>
public sealed class EpochSourceNode
{
    private readonly Channel<IEpoch> _epochStream;

    /// <summary>
    /// Initializes a new instance of the <see cref="EpochSourceNode"/> class.
    /// </summary>
    public EpochSourceNode()
    {
        _epochStream = Channel.CreateUnbounded<IEpoch>(new UnboundedChannelOptions
        {
            SingleReader = false, // Multiple processors can read
            SingleWriter = true,  // Single coordinator writes
            AllowSynchronousContinuations = false
        });
    }

    /// <summary>
    /// Gets the channel reader for consuming epochs.
    /// Multiple processor nodes can read from this stream.
    /// </summary>
    public ChannelReader<IEpoch> EpochReader => _epochStream.Reader;

    /// <summary>
    /// Publishes an epoch to the stream for processing.
    /// This is typically called after creating an epoch via the coordinator.
    /// </summary>
    /// <param name="epoch">The epoch to publish.</param>
    /// <returns>A task that completes when the epoch is published.</returns>
    public async Task PublishEpochAsync(IEpoch epoch)
    {
        ArgumentNullException.ThrowIfNull(epoch);
        await _epochStream.Writer.WriteAsync(epoch).ConfigureAwait(false);
    }

    /// <summary>
    /// Signals that no more epochs will be published.
    /// This completes the epoch stream, allowing processor nodes to complete their work.
    /// </summary>
    public void SignalCompletion()
    {
        _epochStream.Writer.Complete();
    }
}
