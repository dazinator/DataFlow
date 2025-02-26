// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Blocks.InputChannel;

using System.Threading.Channels;
using Uniun.DataFlow;
using Uniun.DataFlow.Metrics;

/// <summary>
/// Source block that allows external code to write input via a channel
/// </summary>
public class InputChannelBlock<T> : BlockBase, ISourceBlock<T>
{
    private readonly BoundedChannelOptions _channelOptions;
    private readonly Channel<T> _channel;

    public InputChannelBlock(string name, BlockOptions? options = null) : base(name, options)
    {
        _channelOptions = Options.ChannelOptions ?? new BoundedChannelOptions(100);
        _channel = Channel.CreateBounded<T>(_channelOptions);


    }

    // Public property to allow external code to write directly to blocks output buffer
    public ChannelWriter<T> Writer => _channel.Writer;

    /// <summary>
    ///  Exposed for next block to read from.
    /// </summary>
    public ChannelReader<T> Reader => _channel.Reader;

    protected override async Task CoreExecuteAsync(IDataFlowContext context)
    {
        // TODO: Maybe we should compare exchange and dispose of old channel - in case where this block might be re-executed and we need to reset the channel.
        // Note: the above wont' work because we have blocks linked to old channel reference.
        // We might need to revise out model to have referencer's to the block, so we can swap put channels on re-execute?
        using var monitoredChannel = this.CreateMonitoredChannel(_channelOptions, context, _channel);
        //  context.CreateMonitoredChannel(this.Name, _channel, Options.MaxConcurrency, _channelOptions.Capacity);
        // Just wait for completion since external code writes to the channel
        await _channel.Reader.Completion;

    }
}
