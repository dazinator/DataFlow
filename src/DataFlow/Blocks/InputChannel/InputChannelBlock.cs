// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Blocks.InputChannel;

using System.Threading.Channels;
using Uniun.DataFlow;

/// <summary>
/// Source block that allows external code to write input via a channel
/// </summary>
public class InputChannelBlock<T> : BlockBase, ISourceBlock<T>
{
    private readonly Channel<T> _channel;

    public InputChannelBlock(BlockOptions? options = null) : base(options)
    {
        _channel = Channel.CreateBounded<T>(Options.ChannelOptions ?? new BoundedChannelOptions(100));
    }

    // Public property to allow external code to write directly to blocks output buffer
    public ChannelWriter<T> Writer => _channel.Writer;

    /// <summary>
    ///  Exposed for next block to read from.
    /// </summary>
    public ChannelReader<T> Reader => _channel.Reader;

    protected override async Task CoreExecuteAsync(IDataFlowContext context)
    {
        // Just wait for completion since external code writes to the channel
        await _channel.Reader.Completion;
    }
}
