namespace Uniun.DataFlow.Blocks.Buffer;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Uniun.DataFlow;
using Uniun.DataFlow.Blocks;
using Uniun.DataFlow.Metrics;

/// <summary>
/// A buffer block that accepts items from multiple upstream producers and distributes them
/// to multiple downstream consumers in a competing consumer pattern.
/// 
/// Unlike BroadcastBlock which sends each item to ALL downstream blocks (fanout),
/// BufferBlock sends each item to exactly ONE downstream consumer (competing consumers).
/// 
/// Multiple upstream blocks can connect to write items, and multiple downstream blocks
/// can connect to read items, with each item being consumed by only one reader.
/// </summary>
/// <typeparam name="T">The type of items to buffer</typeparam>
public class BufferBlock<T> : BlockBase, ITargetBlock<T>, ISourceBlock<T>
{
    private readonly ILogger<BufferBlock<T>> _logger;
    private readonly MonitoredChannel<T> _channel;
    private readonly List<ISourceBlock<T>> _sources = new();
    private readonly object _sourcesLock = new();
    private int _activeProducers = 0;

    public BufferBlock(
        string name,
        ILogger<BufferBlock<T>> logger,
        IBoundedChannelFactory channelFactory,
        BlockOptions? options = null) : base(name, options, logger)
    {
        _logger = logger;
        _channel = channelFactory.CreateMonitoredChannel<T>(name, options?.Capacity);
    }

    /// <summary>
    /// Sets a single source block that will produce items for this buffer.
    /// Can be called multiple times to add multiple producers.
    /// </summary>
    public void SetSource(ISourceBlock<T> source)
    {
        lock (_sourcesLock)
        {
            _sources.Add(source);
            _logger.LogDebug(
                "Added source to BufferBlock '{BlockName}'. Total sources: {SourceCount}",
                Name,
                _sources.Count);
        }
        (source as IExpectsDownstreamTargets)?.RegisterExpectedTarget();
    }

    /// <summary>
    /// Downstream blocks call this to get items from the buffer.
    /// Multiple downstream blocks can connect and will compete for items.
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

    protected override async Task CoreExecuteAsync(IDataFlowContext context)
    {
        if (_sources.Count == 0)
        {
            _logger.LogWarning(
                "BufferBlock '{BlockName}' has no connected sources. No items will be produced.",
                Name);
            return;
        }

        using var monitoringLease = _channel.StartMonitoring(context);

        try
        {
            // Start all producer tasks
            var producerTasks = new List<Task>();
            lock (_sourcesLock)
            {
                _activeProducers = _sources.Count;
                foreach (var source in _sources)
                {
                    producerTasks.Add(ProduceFromSourceAsync(source, context.CancellationToken));
                }
            }

            // Wait for all producers to complete
            await Task.WhenAll(producerTasks);
        }
        finally
        {
            // Complete the channel when all producers are done
            _channel.Writer.Complete();
            _logger.LogDebug(
                "BufferBlock '{BlockName}' completed. All producers finished.",
                Name);
        }
    }

    private async Task ProduceFromSourceAsync(ISourceBlock<T> source, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var item in source.GetAsyncEnumerable(this, cancellationToken).ConfigureAwait(false))
            {
                await _channel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
                RecordOperation();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error producing items from source in BufferBlock '{BlockName}'",
                Name);

            // Mark the channel as failed - this will:
            // 1. Stop all downstream consumers from reading more items
            // 2. Propagate the exception to consumers via ChannelClosedException
            // 3. Signal other producers to stop via Task.WhenAll cancellation
            _channel.Writer.Complete(ex);
            throw;
        }
        finally
        {
            // Decrement active producers count
            var remaining = Interlocked.Decrement(ref _activeProducers);
            _logger.LogDebug(
                "Producer completed in BufferBlock '{BlockName}'. Remaining producers: {RemainingProducers}",
                Name,
                remaining);
        }
    }
}
