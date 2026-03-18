namespace DataFlow.POC.Blocks;

using DataFlow.POC.Core;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

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

    /// <summary>
    /// Constructor with IBlockContext for proper dependency injection.
    /// All dependencies are passed via constructor.
    /// </summary>
    public EpochBatchBlock(IBlockContext context, int maxBatchSize, TimeSpan? windowPeriod = null)
        : base(context)
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
        if (!_windowPeriod.HasValue)
        {
            // Simple path: no timer, accumulate and yield when full or at end of epoch
            var simpleBatch = new List<T>(_maxBatchSize);
            await foreach (var item in epochStream.Items.WithCancellation(context.CancellationToken))
            {
                simpleBatch.Add(item);
                if (simpleBatch.Count >= _maxBatchSize)
                {
                    yield return simpleBatch.ToArray();
                    simpleBatch.Clear();
                }
            }
            if (simpleBatch.Count > 0)
                yield return simpleBatch.ToArray();
            yield break;
        }

        // Windowed path: use a channel so the background timer can proactively flush
        // accumulated items without waiting for the next input item to arrive.
        var batchChannel = Channel.CreateUnbounded<T[]>(new UnboundedChannelOptions { SingleReader = true });
        var feedingTask = FeedBatchChannelAsync(epochStream, context, batchChannel.Writer);

        try
        {
            await foreach (var batch in batchChannel.Reader.ReadAllAsync(context.CancellationToken))
            {
                yield return batch;
            }
        }
        finally
        {
            // Await the feeding task to propagate any exception that occurred during input processing
            await feedingTask;
        }
    }

    /// <summary>
    /// Reads items from the epoch stream, accumulates them into batches, and writes completed batches
    /// to <paramref name="writer"/>.  A background timer task runs concurrently and flushes any
    /// partially-accumulated batch when the window period elapses, ensuring proactive emission even
    /// when item arrival is infrequent.
    ///
    /// A plain <c>lock</c> guards the shared <paramref name="batch"/> list because the critical
    /// section is purely synchronous (no awaits inside), making a synchronous mutex the correct
    /// — and lower-overhead — primitive compared to <see cref="SemaphoreSlim"/>.
    /// </summary>
    private async Task FeedBatchChannelAsync(
        IEpochStream<T> epochStream,
        IExecutionContext context,
        ChannelWriter<T[]> writer)
    {
        var batch = new List<T>(_maxBatchSize);
        var batchLock = new object();
        using var timerCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        var timerTask = RunWindowTimerAsync(batch, batchLock, writer, timerCts.Token);

        try
        {
            await foreach (var item in epochStream.Items.WithCancellation(context.CancellationToken))
            {
                T[]? toFlush = null;

                lock (batchLock)
                {
                    batch.Add(item);
                    if (batch.Count >= _maxBatchSize)
                    {
                        toFlush = batch.ToArray();
                        batch.Clear();
                    }
                }

                if (toFlush != null)
                {
                    await writer.WriteAsync(toFlush, context.CancellationToken);
                }
            }

            // Input stream exhausted: stop the timer and flush any remaining items
            await timerCts.CancelAsync();
            try { await timerTask; } catch (OperationCanceledException) { }

            T[]? remainder = null;
            lock (batchLock)
            {
                if (batch.Count > 0)
                {
                    remainder = batch.ToArray();
                    batch.Clear();
                }
            }

            if (remainder != null)
                await writer.WriteAsync(remainder, CancellationToken.None);

            writer.Complete();
        }
        catch (Exception ex)
        {
            timerCts.Cancel();
            try { await timerTask; } catch (OperationCanceledException) { }
            writer.TryComplete(ex);
        }
    }

    /// <summary>
    /// Background timer loop that wakes every <see cref="_windowPeriod"/> and flushes the current
    /// partial batch to <paramref name="writer"/> if any items have accumulated.
    /// </summary>
    private async Task RunWindowTimerAsync(
        List<T> batch,
        object batchLock,
        ChannelWriter<T[]> writer,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_windowPeriod!.Value, cancellationToken);

                T[]? toFlush = null;

                lock (batchLock)
                {
                    if (batch.Count > 0)
                    {
                        toFlush = batch.ToArray();
                        batch.Clear();
                    }
                }

                if (toFlush != null)
                {
                    await writer.WriteAsync(toFlush, CancellationToken.None);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private static IEpochStream<T[]> CreateEpochStream(EpochVector epoch, IAsyncEnumerable<T[]> items)
    {
        return new EpochStream<T[]>(epoch, items);
    }
}
