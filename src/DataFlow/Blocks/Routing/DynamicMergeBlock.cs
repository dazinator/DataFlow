namespace Uniun.DataFlow.Blocks.Routing;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Uniun.DataFlow.Blocks;
using Uniun.DataFlow.Metrics;

/// <summary>
/// Non-generic interface for DynamicMergeBlock to allow completion signaling without knowing the type parameter.
/// </summary>
public interface IDynamicMergeBlock
{
    void Complete();
}

/// <summary>
/// A specialized block for merging outputs from dynamically created routes.
/// This is a "dummy block" that exposes a channel and allows the StructuredRoutingBlock
/// to control its completion via a TaskCompletionSource.
/// </summary>
public class DynamicMergeBlock<T> : BlockBase, ITargetBlock<T>, ISourceBlock<T>, IDynamicMergeBlock
{
    private readonly ILogger<DynamicMergeBlock<T>> _logger;
    private readonly MonitoredChannel<T> _channel;
    private readonly TaskCompletionSource<bool> _completionSource = new();

    public DynamicMergeBlock(
        string name,
        ILogger<DynamicMergeBlock<T>> logger,
        IBoundedChannelFactory channelFactory,
        BlockOptions? options = null) : base(name, options, logger)
    {
        _logger = logger;
        _channel = channelFactory.CreateMonitoredChannel<T>(name, options?.Capacity ?? 1000);
    }

    /// <summary>
    /// Gets the channel writer so that producer tasks can write to this block's channel.
    /// </summary>
    public ChannelWriter<T> Writer => _channel.Writer;

    /// <summary>
    /// Adds a source block. This is called by MergeConnectorBlock during route initialization.
    /// Note: The actual data pumping is handled by StructuredRoutingBlock via producer tasks on RouteInstance.
    /// </summary>
    public void SetSource(ISourceBlock<T> source)
    {
        // This method is called to register the source, but the actual connection
        // and data pumping is handled externally by StructuredRoutingBlock
        _logger.LogDebug("DynamicMergeBlock '{BlockName}': Source registered", Name);
        (source as IExpectsDownstreamTargets)?.RegisterExpectedTarget();
    }

    /// <summary>
    /// Downstream blocks call this to get items from the merge.
    /// </summary>
    public async IAsyncEnumerable<T> GetAsyncEnumerable(
        ITargetBlock<T> target,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var reader = _channel.Reader;
        await foreach (var item in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    /// <summary>
    /// Signals that all routes have completed and no more data will be written.
    /// This should be called by StructuredRoutingBlock when all routes are done.
    /// </summary>
    public void Complete()
    {
        _completionSource.TrySetResult(true);
        _logger.LogDebug("DynamicMergeBlock '{BlockName}': Completion signaled", Name);
    }

    protected override async Task CoreExecuteAsync(IDataFlowContext context)
    {
        using var monitoringLease = _channel.StartMonitoring(context);

        try
        {
            // Wait for external completion signal from StructuredRoutingBlock
            await _completionSource.Task.ConfigureAwait(false);
        }
        finally
        {
            _channel.Writer.Complete();
            _logger.LogDebug("DynamicMergeBlock '{BlockName}': Execution completed", Name);
        }
    }
}
