// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Blocks;

using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

// Interface for blocks that have an output channel (source blocks)
public interface ISourceBlock<T> : IBlock
{
    // ChannelWriter<T> Writer { get; }
    // ChannelReader<T> Reader { get; }

    /// <summary>
    /// A downstream block calls this when it is executing concurrently, to get the reader for this block to supply data to it.
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    public ChannelReader<T> GetReader(ITargetBlock<T> target);
}
