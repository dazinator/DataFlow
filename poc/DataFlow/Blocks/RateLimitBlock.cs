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
/// The <see cref="RateLimiter"/> is disposed when the block is disposed.
/// </para>
/// </remarks>
/// <typeparam name="T">The data item type flowing through the pipeline.</typeparam>
public sealed class RateLimitBlock<T> : BlockBase<IEpochStream<T>, IEpochStream<T>>, IDisposable
{
    private readonly RateLimiter _rateLimiter;

    /// <summary>
    /// Initializes a new <see cref="RateLimitBlock{T}"/> with an explicit
    /// <see cref="RateLimiter"/> instance.
    /// </summary>
    /// <param name="context">Block context providing name and lifecycle information.</param>
    /// <param name="rateLimiter">The rate limiter that controls permit acquisition.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="context"/> or <paramref name="rateLimiter"/> is <see langword="null"/>.
    /// </exception>
    public RateLimitBlock(IBlockContext context, RateLimiter rateLimiter)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(rateLimiter);
        _rateLimiter = rateLimiter;
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
            using var lease = await _rateLimiter.AcquireAsync(permitCount: 1, context.CancellationToken);

            if (lease.IsAcquired)
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
    /// Disposes the underlying <see cref="RateLimiter"/>.
    /// </summary>
    public void Dispose()
    {
        _rateLimiter.Dispose();
    }
}
