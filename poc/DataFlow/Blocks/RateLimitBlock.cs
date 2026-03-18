namespace DataFlow.POC.Blocks;

using DataFlow.POC.Core;
using System.Threading.RateLimiting;

/// <summary>
/// Rate-limiting pass-through block that throttles the flow of items using a
/// <see cref="RateLimiter"/> from <c>System.Threading.RateLimiting</c>.
/// Each item acquires a single permit before being forwarded downstream, so
/// throughput is bounded by the configured rate-limiter policy.
/// </summary>
/// <remarks>
/// <para>
/// This block is a pure pass-through: input type <typeparamref name="T"/> equals
/// output type <typeparamref name="T"/>. It only throttles throughput — no data
/// transformation is applied.
/// </para>
/// <para>
/// The permit lease is acquired and immediately released before the item is yielded.
/// This means the block is suitable for window-based and token-bucket limiters where
/// only the permit acquisition matters. If you need concurrency-based limiting
/// (holding a permit while downstream processes), use a custom actor instead.
/// </para>
/// <para>
/// When <paramref name="ownsLimiter"/> is <see langword="true"/> (the default for
/// internally-created limiters), the <see cref="RateLimiter"/> is disposed when the
/// block is disposed. When <see langword="false"/> (externally-supplied limiters), the
/// caller retains ownership and is responsible for disposal — this prevents the block
/// from disposing a shared limiter that is reused across multiple graph executions.
/// </para>
/// </remarks>
/// <typeparam name="T">The data item type flowing through the pipeline.</typeparam>
public sealed class RateLimitBlock<T> : BlockBase<IEpochStream<T>, IEpochStream<T>>, IDisposable
{
    private readonly RateLimiter _rateLimiter;
    private readonly bool _ownsLimiter;

    /// <summary>
    /// Initializes a new <see cref="RateLimitBlock{T}"/> with an explicit
    /// <see cref="RateLimiter"/> instance.
    /// </summary>
    /// <param name="context">Block context providing name and lifecycle information.</param>
    /// <param name="rateLimiter">The rate limiter that controls permit acquisition.</param>
    /// <param name="ownsLimiter">
    /// When <see langword="true"/> (default), the block disposes <paramref name="rateLimiter"/> when
    /// the block itself is disposed. Set to <see langword="false"/> when the limiter is externally owned
    /// and shared across multiple block instances or graph executions.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="context"/> or <paramref name="rateLimiter"/> is <see langword="null"/>.
    /// </exception>
    public RateLimitBlock(IBlockContext context, RateLimiter rateLimiter, bool ownsLimiter = true)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(rateLimiter);
        _rateLimiter = rateLimiter;
        _ownsLimiter = ownsLimiter;
    }

    /// <summary>
    /// Processes the incoming epoch streams, applying rate limiting to each item
    /// before forwarding it downstream within the same epoch boundary.
    /// </summary>
    /// <param name="input">The stream of epoch streams to process.</param>
    /// <param name="context">Execution context providing cancellation support.</param>
    /// <returns>An async enumerable of epoch streams with rate-limited throughput.</returns>
    public override async IAsyncEnumerable<IEpochStream<T>> ExecuteAsync(
        IAsyncEnumerable<IEpochStream<T>> input,
        IExecutionContext context)
    {
        await foreach (var epochStream in input.WithCancellation(context.CancellationToken))
        {
            yield return new EpochStream<T>(epochStream.Epoch, RateLimitEpochItems(epochStream, context));
        }
    }

    private async IAsyncEnumerable<T> RateLimitEpochItems(
        IEpochStream<T> epochStream,
        IExecutionContext context)
    {
        await foreach (var item in epochStream.Items.WithCancellation(context.CancellationToken))
        {
            // Acquire and immediately dispose the lease before yielding.
            // This ensures the permit is released as soon as the item is cleared for forwarding,
            // which is correct for window/token-bucket limiters.
            // (Holding the lease across the yield would pin the permit while the downstream
            // consumer is processing, which is unintended for throughput-gating.)
            bool acquired;
            using (var lease = await _rateLimiter.AcquireAsync(permitCount: 1, context.CancellationToken))
            {
                acquired = lease.IsAcquired;
            }

            if (acquired)
            {
                yield return item;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Rate limiter rejected a permit for block '{Name}'. " +
                    "This may indicate the queue limit has been exceeded or the rate limiter has been disposed.");
            }
        }
    }

    /// <summary>
    /// Disposes the underlying <see cref="RateLimiter"/> if this block owns it
    /// (i.e., <c>ownsLimiter</c> was <see langword="true"/> at construction).
    /// </summary>
    public void Dispose()
    {
        if (_ownsLimiter)
        {
            _rateLimiter.Dispose();
        }
    }
}
