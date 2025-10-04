namespace Uniun.DataFlow.Blocks.RateLimiting;
using System;
using System.Threading.Channels;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Uniun.DataFlow.Blocks.Producer;
using Uniun.DataFlow.Metrics;

/// <summary>
/// Simplified rate limiting block that accepts a factory function for rate limiter creation
/// </summary>
/// <typeparam name="T"></typeparam>
public class RateLimitBlock<T> : BlockBase, IPropagatorBlock<T, T>, IDisposable
{
    private readonly RateLimiter _rateLimiter;
    private readonly MonitoredChannel<T> _outputChannel;
   // private readonly Timer? _statisticsTimer;
    private ISourceBlock<T>? _source;

    public RateLimitBlock(
        string name,
         ILogger<RateLimitBlock<T>> logger,
        IBoundedChannelFactory channelFactory,
        BlockOptions blockOptions,
        Func<RateLimiter> rateLimiterFactory,
        IDataFlowMetrics? metrics = null) : base(name, blockOptions, logger)
    {
        // Simply call the factory function to get the rate limiter
        _rateLimiter = rateLimiterFactory();
        _outputChannel = channelFactory.CreateMonitoredChannel<T>(name, blockOptions?.Capacity);

        //// Optional metrics integration
        //if (metrics != null)
        //{
        //    _statisticsTimer = new Timer(_ => CollectStatistics(metrics), null,
        //        TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        //}
    }

    //private void CollectStatistics(IDataFlowMetrics metrics)
    //{
    //    try
    //    {
    //        var stats = _rateLimiter.GetStatistics();
    //        if (stats != null)
    //        {
    //            // Use your integrated metrics approach
    //            metrics.UpdateRateLimiterStatistics(Name, stats);
    //        }
    //    }
    //    catch (ObjectDisposedException)
    //    {
    //        _statisticsTimer?.Dispose();
    //    }
    //    catch (Exception)
    //    {
    //        // Log but continue
    //    }
    //}

    public void SetSource(ISourceBlock<T> source)
    {
        _source = source;
        SourceReader = _source.GetReader(this);
    }

    public ChannelReader<T> SourceReader { get; private set; }

    private void EnsureSourceReader()
    {
        if (_source is null || SourceReader is null)
        {
            throw new InvalidOperationException("No source block configured");
        }
    }

    public ChannelReader<T> GetReader(ITargetBlock<T> target)
    {
        return _outputChannel.Reader;
    }

    public async IAsyncEnumerable<T> GetAsyncEnumerable(
        ITargetBlock<T> target,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var reader = _outputChannel.Reader;
        await foreach (var item in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    protected override async Task CoreExecuteAsync(IDataFlowContext context)
    {
        try
        {
            _outputChannel.StartMonitoring(context);
            EnsureSourceReader();

            await ProcessItemsSeriallyAsync(context);
            //await SourceReader.Completion; seems redundant as ReadAllAsync will complete when the source completes or cancellation token signals,
            //and in either case seems redundant to keep from returning longer than necessary.
        }
        finally
        {
            _outputChannel.Writer.Complete();
        }
    }

    private async Task ProcessItemsSeriallyAsync(IDataFlowContext context)
    {
        await foreach (var item in SourceReader.ReadAllAsync(context.CancellationToken))
        {
            using var lease = await _rateLimiter.AcquireAsync(permitCount: 1, context.CancellationToken);

            if (lease.IsAcquired)
            {
                await _outputChannel.Writer.WriteAsync(item, context.CancellationToken);
            }
            else
            {
                // Handle failure based on configuration
                throw new InvalidOperationException(
                    $"Rate limiter rejected request for block '{Name}'. " +
                    "This may indicate queue limit exceeded or rate limiter disposed.");
            }
        }
    }

    public void Dispose()
    {
       // _statisticsTimer?.Dispose();
        _rateLimiter?.Dispose();
    }
}
