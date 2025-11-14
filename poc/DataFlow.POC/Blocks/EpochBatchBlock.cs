namespace DataFlow.POC.Blocks;

using DataFlow.POC.Core;
using System.Runtime.CompilerServices;

/// <summary>
/// Epoch-aware batch block that accumulates items into batches within epoch boundaries.
/// This block receives epoch streams and batches items within each epoch independently.
/// Batches do not span across epoch boundaries.
/// </summary>
/// <typeparam name="T">Input item type</typeparam>
public sealed class EpochBatchBlock<T> : BlockBase<IEpochStream<T>, IEpochStream<T[]>>
{
    private readonly int _maxBatchSize;
    private readonly TimeSpan? _windowPeriod;

    public EpochBatchBlock(string name, int maxBatchSize, TimeSpan? windowPeriod = null)
        : base(name)
    {
        if (maxBatchSize <= 0)
        {
            throw new ArgumentException("Max batch size must be greater than 0", nameof(maxBatchSize));
        }

        _maxBatchSize = maxBatchSize;
        _windowPeriod = windowPeriod;
    }

    public override async IAsyncEnumerable<IEpochStream<T[]>> ExecuteAsync(
        IAsyncEnumerable<IEpochStream<T>> input,
        IExecutionContext context)
    {
        await foreach (var epochStream in input.WithCancellation(context.CancellationToken))
        {
            yield return CreateEpochStream(epochStream.Epoch, BatchEpochItems(epochStream, context));
        }
    }

    private async IAsyncEnumerable<T[]> BatchEpochItems(
        IEpochStream<T> epochStream,
        IExecutionContext context)
    {
        var batch = new List<T>();
        CancellationTokenSource? cts = null;
        var windowTimer = _windowPeriod.HasValue
            ? new System.Threading.Timer(
                _ =>
                {
                    if (cts != null && !cts.IsCancellationRequested)
                    {
                        try
                        {
                            cts.Cancel();
                        }
                        catch (ObjectDisposedException)
                        {
                            // Safe to ignore: CancellationTokenSource may have already been disposed if the block is shutting down
                        }
                    }
                },
                null,
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan)
            : null;

        try
        {
            cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);

            await foreach (var item in epochStream.Items.WithCancellation(context.CancellationToken))
            {
                // Start timer on first item in batch
                if (batch.Count == 0 && _windowPeriod.HasValue && windowTimer != null)
                {
                    windowTimer.Change(_windowPeriod.Value, Timeout.InfiniteTimeSpan);
                }

                batch.Add(item);

                // Check if batch is full or window expired
                var batchIsFull = batch.Count >= _maxBatchSize;
                var windowExpired = cts.IsCancellationRequested && !context.CancellationToken.IsCancellationRequested;

                if (batchIsFull || windowExpired)
                {
                    // Emit batch
                    yield return batch.ToArray();
                    batch.Clear();

                    // Stop timer
                    windowTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

                    // Reset cancellation if it was due to timer
                    if (windowExpired)
                    {
                        cts.Dispose();
                        cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
                    }
                }
            }

            // Emit final batch if any items remain (within the epoch)
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

    private static IEpochStream<T[]> CreateEpochStream(EpochVector epoch, IAsyncEnumerable<T[]> items)
    {
        return new EpochStream<T[]>(epoch, items);
    }
}
