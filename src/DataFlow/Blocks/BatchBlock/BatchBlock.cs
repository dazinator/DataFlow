namespace Uniun.DataFlow.Blocks.BatchBlock;
using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Uniun.DataFlow;
using Uniun.DataFlow.Blocks;
using Uniun.DataFlow.Metrics;

/// <summary>
/// The block will batch incoming items and emit them as arrays either when:
/// - The batch size is reached (size-based emission)
/// - The window period has elapsed since the first item in the current batch (time-based emission)
/// - The source completes (remaining items are flushed)
/// 
/// The timer-based emission strategy has been optimized to avoid unnecessary partial batch emissions:
/// - Timer starts only when the first item arrives in a new batch
/// - Timer is cancelled if the batch is emitted due to reaching max size
/// - This prevents partial batches from being emitted unnecessarily when items are flowing quickly
/// </summary>
/// <typeparam name="T"></typeparam>
public class BatchBlock<T> : BlockBase, IPropagatorBlock<T, T[]>
{
    private readonly BatchProcessor<T> _batchProcessor;
    private readonly MonitoredChannel<T[]> _outputChannel;
    private readonly IBoundedChannelFactory _channelFactory;
    private ISourceBlock<T>? _source;

    public BatchBlock(
        string name,
        ILogger<BatchBlock<T>> logger,
        IBoundedChannelFactory channelFactory,
        BatchBlockOptions options) : base(name, options, logger)
    {
        if (options.MaxBatchSize <= 0)
        {
            throw new ArgumentException("Max batch size must be greater than 0", nameof(options.MaxBatchSize));
        }
        _channelFactory = channelFactory;
        _outputChannel = _channelFactory.CreateMonitoredChannel<T[]>(name, options.Capacity);
        _batchProcessor = new BatchProcessor<T>(options.MaxBatchSize, options.WindowPeriod, _outputChannel.Writer, this.RecordOperation);
    }

    public void SetSource(ISourceBlock<T> source)
    {
        _source = source;
        (source as IExpectsDownstreamTargets)?.RegisterExpectedTarget();
    }

    private void EnsureSource()
    {
        if (_source is null)
        {
            throw new InvalidOperationException("No source block configured");
        }
    }

    public ChannelReader<T[]> Reader => _outputChannel.Reader;


    protected override async Task CoreExecuteAsync(IDataFlowContext context)
    {
        using var monitoringLease = _outputChannel.StartMonitoring(context);

        try
        {
            EnsureSource();
            //using var monitoredChannel = this.CreateMonitoredChannel(_outputChannelOptions.Capacity, context, _outputChannel);
            await ReadAllAsync(context);
        }
        finally
        {
            try
            {
                await _batchProcessor.CompleteAsync(); // this can emit any remaining items in the current batch.
            }
            finally
            {
                _outputChannel.Writer.Complete(); // no more data to write.               
            }
        }
    }

    protected async Task ReadAllAsync(IDataFlowContext context)
    {
        await foreach (var item in _source!.GetAsyncEnumerable(this, context.CancellationToken))
        {
            await _batchProcessor.AddAsync(item, context.CancellationToken);
        }
    }

    public ChannelReader<T[]> GetReader(ITargetBlock<T[]> target)
    {
        return Reader;
    }

    public async IAsyncEnumerable<T[]> GetAsyncEnumerable(
        ITargetBlock<T[]> target,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var reader = Reader;
        await foreach (var item in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    private class BatchProcessor<TItem>
    {
        private readonly int _maxBatchSize;
        private readonly TimeSpan _windowPeriod;
        private readonly ChannelWriter<TItem[]> _outputChannel;
        private readonly Action _onBatchEmitted;
        private volatile List<TItem> _currentBatch;
        private readonly CancellationTokenSource _timerCts;
        private readonly Task _timerTask;
        private readonly ObjectPool<List<TItem>> _listPool;

        // Lightweight signaling: 0 = no batch, 1 = batch waiting
        private int _hasBatch;
        private volatile bool _isFirstItem = true;

        public BatchProcessor(
            int maxBatchSize,
            TimeSpan windowPeriod,
            ChannelWriter<TItem[]> outputChannel,
            Action onBatchEmitted)
        {
            _maxBatchSize = maxBatchSize;
            _windowPeriod = windowPeriod;
            _outputChannel = outputChannel;
            _onBatchEmitted = onBatchEmitted;
            var poolPolicy = new ListPoolPolicy<TItem>(maxBatchSize);
            _listPool = new DefaultObjectPool<List<TItem>>(poolPolicy);

            _currentBatch = _listPool.Get();
            _timerCts = new CancellationTokenSource();

            // Start single long-running timer task (like old implementation)
            _timerTask = RunTimerAsync(_timerCts.Token);
        }

        private class ListPoolPolicy<TMember> : IPooledObjectPolicy<List<TMember>>
        {
            private readonly int _maxBatchSize;

            public ListPoolPolicy(int maxBatchSize)
            {
                _maxBatchSize = maxBatchSize;
            }

            public List<TMember> Create()
            {
                return new List<TMember>(_maxBatchSize);
            }

            public bool Return(List<TMember> obj)
            {
                obj.Clear();
                return true;
            }
        }

        public async ValueTask AddAsync(TItem item, CancellationToken cancellationToken = default)
        {
            var isFirst = _isFirstItem;
            _currentBatch.Add(item);
            var count = _currentBatch.Count;

            if (count >= _maxBatchSize)
            {
                // Size-based emission
                await EmitBatchIfNotEmptyAsync(cancellationToken);
            }
            else if (isFirst && count == 1)
            {
                // Signal timer that we have a batch waiting (no lock needed!)
                _isFirstItem = false;
                Interlocked.Exchange(ref _hasBatch, 1);
            }
        }

        private async Task RunTimerAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_windowPeriod, cancellationToken);

                    // Only emit if we have a batch waiting (lightweight check)
                    if (Interlocked.CompareExchange(ref _hasBatch, 0, 1) == 1)
                    {
                        await EmitBatchIfNotEmptyAsync(cancellationToken);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private async ValueTask EmitBatchIfNotEmptyAsync(CancellationToken cancellationToken)
        {
            var newBatch = _listPool.Get();
            var oldBatch = Interlocked.Exchange(ref _currentBatch, newBatch);

            try
            {
                if (oldBatch.Count > 0)
                {
                    // Clear the signal and reset for next batch
                    Interlocked.Exchange(ref _hasBatch, 0);
                    _isFirstItem = true;

                    await _outputChannel.WriteAsync(oldBatch.ToArray(), cancellationToken);
                    _onBatchEmitted();
                }
            }
            finally
            {
                _listPool.Return(oldBatch);
            }
        }

        public async ValueTask CompleteAsync()
        {
            _timerCts.Cancel();

            try
            {
                await _timerTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            await EmitBatchIfNotEmptyAsync(CancellationToken.None);
            _timerCts.Dispose();
        }
    }

}


