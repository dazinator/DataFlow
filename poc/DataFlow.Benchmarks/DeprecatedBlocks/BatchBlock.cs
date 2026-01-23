namespace DataFlow.POC.Benchmarks.DeprecatedBlocks;

using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using DataFlow.POC.Registry;

/// <summary>
/// ⚠️ DEPRECATED - Benchmark-only. Use EpochBatchBlock for new code.
/// 
/// Plain (non-epoch) batch block for backward compatibility with benchmarks.
/// This block is retained solely for benchmark comparisons between plain and epoch-based batching.
/// New code should use EpochBatchBlock instead.
/// </summary>
/// <typeparam name="T">Input item type</typeparam>
/// <remarks>
/// This block was part of the pre-epoch architecture and has been deprecated in favor of
/// EpochBatchBlock. It is maintained only for benchmark compatibility.
/// </remarks>
[Obsolete("BatchBlock is deprecated. Use EpochBatchBlock for new code.")]
public sealed class BatchBlock<T> : BlockBase<T, T[]>
{
    private readonly int _maxBatchSize;
    private readonly TimeSpan? _windowPeriod;

    /// <summary>
    /// Constructor for backward compatibility with benchmarks.
    /// </summary>
    /// <param name="name">Block name</param>
    /// <param name="maxBatchSize">Maximum number of items per batch</param>
    /// <param name="windowPeriod">Optional time-based batching window</param>
    public BatchBlock(string name, int maxBatchSize, TimeSpan? windowPeriod = null)
        : base(new BlockContext(name))
    {
        if (maxBatchSize <= 0)
        {
            throw new ArgumentException("Max batch size must be greater than 0", nameof(maxBatchSize));
        }

        _maxBatchSize = maxBatchSize;
        _windowPeriod = windowPeriod;
    }

    /// <summary>
    /// Constructor with IBlockContext for proper dependency injection.
    /// </summary>
    public BatchBlock(IBlockContext context, int maxBatchSize, TimeSpan? windowPeriod = null)
        : base(context)
    {
        if (maxBatchSize <= 0)
        {
            throw new ArgumentException("Max batch size must be greater than 0", nameof(maxBatchSize));
        }

        _maxBatchSize = maxBatchSize;
        _windowPeriod = windowPeriod;
    }

    public override async IAsyncEnumerable<T[]> ExecuteAsync(
        IAsyncEnumerable<T> input,
        IExecutionContext context)
    {
        var batch = new List<T>();
        CancellationTokenSource? cts = null;
        System.Threading.Timer? windowTimer = null;

        try
        {
            if (_windowPeriod.HasValue)
            {
                cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
                windowTimer = new System.Threading.Timer(
                    _ => cts?.Cancel(),
                    null,
                    _windowPeriod.Value,
                    Timeout.InfiniteTimeSpan);
            }

            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                batch.Add(item);

                // Yield batch if max size reached
                if (batch.Count >= _maxBatchSize)
                {
                    yield return batch.ToArray();
                    batch.Clear();

                    // Reset window timer
                    if (windowTimer != null && cts != null)
                    {
                        windowTimer.Change(_windowPeriod!.Value, Timeout.InfiniteTimeSpan);
                        cts.Dispose();
                        cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
                    }
                }

                // Check for window timeout
                if (cts?.IsCancellationRequested == true && batch.Count > 0)
                {
                    yield return batch.ToArray();
                    batch.Clear();
                    
                    // Reset for next window
                    cts.Dispose();
                    cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
                    windowTimer?.Change(_windowPeriod!.Value, Timeout.InfiniteTimeSpan);
                }
            }

            // Yield final partial batch if any
            if (batch.Count > 0)
            {
                yield return batch.ToArray();
            }
        }
        finally
        {
            windowTimer?.Dispose();
            cts?.Dispose();
        }
    }
}
