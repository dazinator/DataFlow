// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Blocks;
public interface ITargetBlock<T> : IBlock
{
    // ChannelWriter<T> Writer { get; }

    /// <summary>
    /// Stores the reference to the source block that this block will consume from.
    /// </summary>
    /// <param name="source"></param>
    void SetSource(ISourceBlock<T> source);
}
