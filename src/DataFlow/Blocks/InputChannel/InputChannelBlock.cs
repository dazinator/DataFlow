// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Blocks.InputChannel;

using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Uniun.DataFlow;
using Uniun.DataFlow.Metrics;

/// <summary>
/// Source block that allows external code to write input via a channel
/// </summary>
public class InputChannelBlock<T> : BlockBase, ISourceBlock<T>
{
    private readonly MonitoredChannel<T> _outputChannel;
    private readonly ILogger<InputChannelBlock<T>> _logger;
    private readonly object _completionLock = new();
    private TaskCompletionSource _writerCompletionSource = new();

    public InputChannelBlock(string name, ILogger<InputChannelBlock<T>> logger, IBoundedChannelFactory channelFactory, BlockOptions? options = null) : base(name, options, logger)
    {
        _outputChannel = channelFactory.CreateMonitoredChannel<T>(name, options?.Capacity);
        _logger = logger;
    }

    /// <summary>
    /// Downstream blocks obtain the reader to read items from this blocks output channel.
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
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

    public virtual async ValueTask WriteAsync(T item, CancellationToken cancellationToken = default)
    {
        await Writer.WriteAsync(item, cancellationToken);
        RecordOperation();
    }

    /// <summary>
    /// Downstream blocks obtain the reader to read items from this blocks output channel.
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    protected ChannelWriter<T> Writer { get { return _outputChannel.Writer; } }

    public void Complete()
    {
        lock (_completionLock)
        {
            // complete the channel so downstream blocks can know there is no more data expected from this block.
            _outputChannel.Writer.TryComplete();
            _writerCompletionSource.SetResult(); // CoreExecuteAsync is awaiting this- the block will finish executing and return once we signal this.
        }
    }

    protected override async Task CoreExecuteAsync(IDataFlowContext context)
    {
        // TODO: Maybe we should compare exchange and dispose of old channel - in case where this block might be re-executed and we need to reset the channel.
        // Note: the above wont' work because we have blocks linked to old channel reference.
        // We might need to revise out model to have referencer's to the block, so we can swap put channels on re-execute?
        // using var monitoredChannel = this.CreateMonitoredChannel(_channelOptions.Capacity, context, _channel);
        //  context.CreateMonitoredChannel(this.Name, _channel, Options.MaxConcurrency, _channelOptions.Capacity);
        // Just wait for completion since external code writes to the channel

        // If Complete() has already been called before CoreExecuteAsync() starts, just return immediately
        // Use lock to prevent race condition with Complete()
        lock (_completionLock)
        {
            if (_writerCompletionSource.Task.IsCompleted)
            {
                return;
            }
            // Create a new TaskCompletionSource for this execution
            _writerCompletionSource = new TaskCompletionSource();
        }

        using var monitoringLease = _outputChannel.StartMonitoring(context);
        await _writerCompletionSource.Task; // wait for the external code to signal completion that it has finished writing.  
    }
}
