namespace DataFlow.POC.Core;

using System.Threading.Channels;

/// <summary>
/// PROTOTYPE: Epoch-aware buffer block that preserves epoch boundaries.
/// This is a research prototype to validate the design approach.
/// </summary>
/// <typeparam name="T">The type of items being buffered</typeparam>
public class EpochBufferBlock<T> : BlockBase<IEpochStream<T>, IEpochStream<T>>
{
    private readonly int _capacity;

    public EpochBufferBlock(IBlockContext context, IBufferConfiguration bufferConfig)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(bufferConfig);
        _capacity = bufferConfig.Capacity;
        
        if (_capacity <= 0)
        {
            throw new ArgumentException("Buffer capacity must be greater than 0", nameof(bufferConfig));
        }
    }

    /// <summary>
    /// Process epoch streams by buffering items within each epoch.
    /// Each input epoch stream gets its own channel, preserving epoch boundaries.
    /// </summary>
    public override async IAsyncEnumerable<IEpochStream<T>> ExecuteAsync(
        IAsyncEnumerable<IEpochStream<T>> input,
        IExecutionContext context)
    {
        await foreach (var epochStream in input.WithCancellation(context.CancellationToken))
        {
            // Create a bounded channel for this epoch's items
            var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(_capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false, // Multiple downstream consumers may read
                SingleWriter = true   // Single epoch stream writes items to this channel
            });

            // Start background task to write epoch items to channel (unwrap)
            var writerTask = WriteEpochItemsToChannelAsync(
                epochStream,
                channel.Writer,
                context.CancellationToken);

            // Create output epoch stream backed by channel (re-wrap)
            var outputStream = new ChannelBackedEpochStream<T>(
                epochStream.Epoch,
                epochStream.EpochScope,
                channel);

            yield return outputStream;

            // Wait for writer to complete before processing next epoch
            // This ensures epoch boundaries are strictly preserved
            await writerTask;
        }
    }

    /// <summary>
    /// Writes all items from an epoch stream into the channel.
    /// Completes the channel when all items are written or on error.
    /// </summary>
    private async Task WriteEpochItemsToChannelAsync(
        IEpochStream<T> epochStream,
        ChannelWriter<T> writer,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var item in epochStream.Items.WithCancellation(cancellationToken))
            {
                // Write item to channel (may block if channel is full - backpressure)
                await writer.WriteAsync(item, cancellationToken);
            }
        }
        catch (Exception)
        {
            // On error, propagate exception and complete channel with error state
            // The exception will surface when downstream consumers read from the channel
            throw;
        }
        finally
        {
            // Always complete the channel writer when done
            writer.Complete();
        }
    }
}

/// <summary>
/// Configuration for buffer blocks.
/// </summary>
public interface IBufferConfiguration
{
    /// <summary>
    /// Maximum number of items that can be buffered.
    /// When buffer is full, upstream writes will block (backpressure).
    /// </summary>
    int Capacity { get; }
}

/// <summary>
/// Default implementation of buffer configuration.
/// </summary>
public class BufferConfiguration : IBufferConfiguration
{
    public BufferConfiguration(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentException("Capacity must be greater than 0", nameof(capacity));
        }
        
        Capacity = capacity;
    }

    public int Capacity { get; }
}
