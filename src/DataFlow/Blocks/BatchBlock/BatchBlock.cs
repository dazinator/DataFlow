namespace Uniun.DataFlow.Blocks.BatchBlock;
using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.ObjectPool;
using Uniun.DataFlow;
using Uniun.DataFlow.Blocks;

/// <summary>
/// The block will batch incoming items and emit them as arrays either when:
/// - The batch size is reached
/// - The window period has elapsed since the first item in the current batch
/// - The source completes(remaining items are flushed)
/// </summary>
/// <typeparam name="T"></typeparam>
public class BatchBlock<T> : BlockBase, IPropagatorBlock<T, T[]>
{
    private readonly BatchProcessor<T> _batchProcessor;
    private readonly Channel<T[]> _outputChannel;
    private ISourceBlock<T>? _source;

    public BatchBlock(
        int maxBatchSize,
        TimeSpan windowPeriod,
        BlockOptions? options = null) : base(options)
    {
        if (maxBatchSize <= 0)
        {
            throw new ArgumentException("Max batch size must be greater than 0", nameof(maxBatchSize));
        }

        _outputChannel = Channel.CreateBounded<T[]>(Options.ChannelOptions ?? new BoundedChannelOptions(100));
        _batchProcessor = new BatchProcessor<T>(maxBatchSize, windowPeriod, _outputChannel.Writer);
    }

    public void SetSource(ISourceBlock<T> source)
    {
        _source = source;
    }

    public ChannelReader<T[]> Reader => _outputChannel.Reader;

    protected override async Task CoreExecuteAsync(IDataFlowContext context)
    {
        if (_source == null)
        {
            throw new InvalidOperationException("No source block configured");
        }

        try
        {
            await ReadAllAsync(context);
        }
        finally
        {
            await _batchProcessor.CompleteAsync();
            _outputChannel.Writer.Complete();
        }
    }

    protected async Task ReadAllAsync(IDataFlowContext context)
    {
        await foreach (var item in _source!.Reader.ReadAllAsync(context.CancellationToken))
        {
            await _batchProcessor.AddAsync(item, context.CancellationToken);
        }

        await _source.Reader.Completion;

    }

    private class BatchProcessor<TItem>
    {
        private readonly int _maxBatchSize;
        private readonly TimeSpan _windowPeriod;
        private readonly ChannelWriter<TItem[]> _outputChannel;
        private readonly CancellationTokenSource _timerCts;
        private volatile List<TItem> _currentBatch;
        private volatile bool _isFirstItem = true;
        private readonly object _timerLock = new object();
        private Task? _timerTask;
        private readonly ObjectPool<List<TItem>> _listPool;

        public BatchProcessor(
            int maxBatchSize,
            TimeSpan windowPeriod,
            ChannelWriter<TItem[]> outputChannel)
        {
            _maxBatchSize = maxBatchSize;
            _windowPeriod = windowPeriod;
            _outputChannel = outputChannel;

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
