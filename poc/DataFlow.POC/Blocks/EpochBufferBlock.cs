namespace DataFlow.POC.Blocks;

using DataFlow.POC.Core;
using System.Threading.Channels;

/// <summary>
/// Epoch-aware buffer block that preserves epoch boundaries while buffering items.
/// </summary>
/// <typeparam name="T">The type of items being buffered</typeparam>
/// <remarks>
/// <para>
/// This block buffers items within each epoch independently, preserving epoch boundaries
/// and metadata. Each input epoch stream gets its own bounded channel for buffering.
/// </para>
/// <para>
/// <strong>Use Case:</strong> Use this block when you need to buffer epoch streams
/// while maintaining epoch boundaries. For example, when connecting multiple producers
/// to a single consumer, or when you need to smooth out processing rate variations.
/// </para>
/// <para>
/// <strong>Backpressure:</strong> When the buffer for an epoch fills up, upstream
/// producers will block until space becomes available, providing natural backpressure.
/// </para>
/// <para>
/// <strong>Capacity:</strong> The capacity is per-epoch. Each epoch can buffer up to
/// the specified number of items.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var builder = new DataFlowGraphBuilder("my-graph", serviceProvider, registry);
/// builder.AddEpochBuffer&lt;int&gt;("buffer", capacity: 100);
/// </code>
/// </example>
public sealed class EpochBufferBlock<T> : BlockBase<IEpochStream<T>, IEpochStream<T>>
{
    private readonly int _capacity;

    /// <summary>
    /// Initializes a new instance of the <see cref="EpochBufferBlock{T}"/> class.
    /// </summary>
    /// <param name="context">The block context for dependency injection and configuration.</param>
    /// <param name="bufferConfig">The buffer configuration specifying capacity.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bufferConfig"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when buffer capacity is less than or equal to 0.</exception>
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
    /// Processes epoch streams by buffering items within each epoch.
    /// Each input epoch stream gets its own bounded channel, preserving epoch boundaries.
    /// </summary>
    /// <param name="input">The input stream of epoch streams.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>An async enumerable of buffered epoch streams.</returns>
    /// <remarks>
    /// This method processes epochs sequentially to ensure strict epoch boundary preservation.
    /// Each epoch is fully buffered before the next epoch is processed.
    /// </remarks>
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
            await writerTask.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Writes all items from an epoch stream into the channel.
    /// Completes the channel when all items are written or on error.
    /// </summary>
    /// <param name="epochStream">The epoch stream to read items from.</param>
    /// <param name="writer">The channel writer to write items to.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task that completes when all items are written or on error.</returns>
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
                await writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
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
/// Configuration interface for buffer blocks.
/// </summary>
public interface IBufferConfiguration
{
    /// <summary>
    /// Gets the maximum number of items that can be buffered per epoch.
    /// When the buffer is full, upstream writes will block, providing backpressure.
    /// </summary>
    int Capacity { get; }
}

/// <summary>
/// Default implementation of buffer configuration.
/// </summary>
public class BufferConfiguration : IBufferConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BufferConfiguration"/> class.
    /// </summary>
    /// <param name="capacity">The buffer capacity (must be greater than 0).</param>
    /// <exception cref="ArgumentException">Thrown when capacity is less than or equal to 0.</exception>
    public BufferConfiguration(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentException("Capacity must be greater than 0", nameof(capacity));
        }
        
        Capacity = capacity;
    }

    /// <inheritdoc/>
    public int Capacity { get; }
}
