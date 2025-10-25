namespace DataFlow.POC.Blocks;

using DataFlow.POC.Core;

/// <summary>
/// Batch block that accumulates items into batches based on size and/or time window.
/// </summary>
public class BatchBlock<T> : BlockBase<T, T[]>
{
    private readonly int _maxBatchSize;
    private readonly TimeSpan? _windowPeriod;

    public BatchBlock(string name, int maxBatchSize, TimeSpan? windowPeriod = null)
        : base(name)
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
        var batchStartTime = DateTimeOffset.UtcNow;
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
                            // Safe to ignore: cts may have already been disposed if the block is shutting down
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

            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                // Start timer on first item in batch
                if (batch.Count == 0 && _windowPeriod.HasValue && windowTimer != null)
                {
                    batchStartTime = DateTimeOffset.UtcNow;
                    windowTimer.Change(_windowPeriod.Value, Timeout.InfiniteTimeSpan);
                }

                batch.Add(item);

                // Check if batch is full or window expired
                var batchIsFull = batch.Count >= _maxBatchSize;

                if (batchIsFull || (cts.IsCancellationRequested && !context.CancellationToken.IsCancellationRequested))
                {
                    // Emit batch
                    yield return batch.ToArray();
                    batch.Clear();

                    // Stop timer
                    windowTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

                    // Reset cancellation if it was due to timer
                    if (cts.IsCancellationRequested && !context.CancellationToken.IsCancellationRequested)
                    {
                        cts.Dispose();
                        cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
                    }
                }
            }

            // Emit final batch if any items remain
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
