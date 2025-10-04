// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Blocks;

using System.Threading.Channels;

// Interface for blocks that can provide data to downstream blocks
public interface ISourceBlock<T> : IBlock, IAsyncEnumerableSource<T>
{
    // ChannelWriter<T> Writer { get; }
    // ChannelReader<T> Reader { get; }

    /// <summary>
    /// A downstream block calls this when it is executing concurrently, to get the reader for this blocks output channel so it can pull items from it.
    /// This method is kept for backward compatibility with channel-based implementations.
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    public ChannelReader<T> GetReader(ITargetBlock<T> target);
}
