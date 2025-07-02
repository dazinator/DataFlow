// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Blocks;

using System.Threading.Channels;

// Interface for blocks that have an output channel (source blocks)
public interface ISourceBlock<T> : IBlock
{
    // ChannelWriter<T> Writer { get; }
    // ChannelReader<T> Reader { get; }

    /// <summary>
    /// A downstream block calls this when it is executing concurrently, to get the reader for this blocks output channel so it can pull items from it.
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    public ChannelReader<T> GetReader(ITargetBlock<T> target);
}
