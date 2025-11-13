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
/// OLD IMPLEMENTATION: The block will batch incoming items and emit them as arrays either when:
/// - The batch size is reached
/// - The window period has elapsed since the first item in the current batch
/// - The source completes(remaining items are flushed)
/// 
/// This is the original implementation with continuous background timer for benchmarking purposes.
/// </summary>
/// <typeparam name="T"></typeparam>
public class OldBatchBlock<T> : BlockBase, IPropagatorBlock<T, T[]>
{
    private readonly BatchProcessor<T> _batchProcessor;
    private readonly MonitoredChannel<T[]> _outputChannel;
    private readonly IBoundedChannelFactory _channelFactory;
    private ISourceBlock<T>? _source;

    public OldBatchBlock(
        string name,
        ILogger<OldBatchBlock<T>> logger,
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
            await ReadAllAsync(context);
        }
        finally
        {
            try
            {
                await _batchProcessor.CompleteAsync();
            }
            finally
            {
                _outputChannel.Writer.Complete();
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
        private readonly CancellationTokenSource _timerCts;
        private volatile List<TItem> _currentBatch;
        private volatile bool _isFirstItem = true;
        private readonly object _timerLock = new object();
        private Task? _timerTask;
        private readonly ObjectPool<List<TItem>> _listPool;

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
            StartTimerIfNeeded();

            _currentBatch.Add(item);

            if (_currentBatch.Count >= _maxBatchSize)
            {
                await EmitBatchIfNotEmptyAsync(cancellationToken);
            }
        }

        private void StartTimerIfNeeded()
        {
            if (!_isFirstItem)
            {
                return;
            }

            lock (_timerLock)
            {
                if (!_isFirstItem)
                {
                    return;
                }

                _isFirstItem = false;
                _timerTask = RunTimerAsync(_timerCts.Token);
            }
        }

        private async Task RunTimerAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_windowPeriod, cancellationToken);
                    await EmitBatchIfNotEmptyAsync(cancellationToken);
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
            if (_timerTask != null)
            {
                await _timerTask;
            }

            await EmitBatchIfNotEmptyAsync(CancellationToken.None);
            _timerCts.Dispose();
        }
    }

}
