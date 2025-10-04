// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Blocks;

// Interface for blocks that can provide data to downstream blocks
public interface ISourceBlock<T> : IBlock, IAsyncEnumerableSource<T>
{
    // Note: GetReader() has been removed. All blocks now use GetAsyncEnumerable() for channel-free operation.
    // Blocks that need internal ChannelReader access should use it as a private implementation detail.
}
