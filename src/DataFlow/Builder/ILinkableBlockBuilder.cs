namespace Uniun.DataFlow.Builder;

using Uniun.DataFlow.Blocks;

public interface ILinkableBlockBuilder<TOut, TBlock, TBuilder> :
    IBlockBuilder<TBlock, TBuilder>
    where TBuilder : ILinkableBlockBuilder<TOut, TBlock, TBuilder>
    where TBlock : ISourceBlock<TOut>
{

    /// <summary>
    /// Have the target block receive data from the current source block.
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    public TBuilder LinkTo(ITargetBlock<TOut> target)
    {
        // Set the target's source to the current source block
        target.SetSource(Current);
        return CurrentBuilder;
    }

    /// <summary>
    /// Have the target block receive data from the current source block.
    /// </summary>
    /// <param name="targetBlockName"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    /// <remarks>Convenience method to link to a named block</remarks>
    public TBuilder LinkTo(string targetBlockName)
    {
        // Set the target's source to the current source block
        var target = CurrentBuilder.Blocks[targetBlockName] as ITargetBlock<TOut>
                     ?? throw new InvalidOperationException($"Block '{targetBlockName}' is not a block of type {typeof(TOut).Name}");

        target.SetSource(Current);
        return CurrentBuilder;
    }
}
