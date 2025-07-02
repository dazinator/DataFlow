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
/// - The batch size is reached
/// - The window period has elapsed since the first item in the current batch
/// - The source completes(remaining items are flushed)
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
        _outputChannel = _channelFactory.CreateMonitoredChannel<T[]>(name, options?.Capacity);      
        _batchProcessor = new BatchProcessor<T>(options.MaxBatchSize, options.WindowPeriod, _outputChannel.Writer);       
    }

    public void SetSource(ISourceBlock<T> source)
    {
        _source = source;
        SourceReader = _source.GetReader(this);
    }
    public ChannelReader<T> SourceReader { get; private set; }
    private void EnsureSourceReader()
    {
        if (_source is null)
        {
            throw new InvalidOperationException("No source block configured");
        }
        if (SourceReader is null)
        {
            throw new InvalidOperationException("No source reader configured");
        }
    }

    public ChannelReader<T[]> Reader => _outputChannel.Reader;


    protected override async Task CoreExecuteAsync(IDataFlowContext context)
    {
        using var monitoringLease = _outputChannel.StartMonitoring(context);

        try
        {           
            EnsureSourceReader();
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
        await foreach (var item in SourceReader.ReadAllAsync(context.CancellationToken))
        {
            await _batchProcessor.AddAsync(item, context.CancellationToken);
        }
    }

    public ChannelReader<T[]> GetReader(ITargetBlock<T[]> target)
    {
        return Reader;
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


