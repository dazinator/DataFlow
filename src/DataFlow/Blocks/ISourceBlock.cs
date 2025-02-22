// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Blocks;

using System.Threading.Channels;

// Interface for blocks that have an output channel (source blocks)
public interface ISourceBlock<T> : IBlock
{
    // ChannelWriter<T> Writer { get; }
    ChannelReader<T> Reader { get; }
}
