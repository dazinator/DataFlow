namespace Uniun.DataFlow.Builder;

using Uniun.DataFlow.Blocks;

public interface IReceivableBlockBuilder<TIn, TBlock, TBuilder> :
    IBlockBuilder<TBlock, TBuilder>
    where TBuilder : IReceivableBlockBuilder<TIn, TBlock, TBuilder>
    where TBlock : ITargetBlock<TIn>
{
    /// <summary>
    /// The last added source block (used for positional chaining via ReceiveFromLast)
    /// </summary>
    public ISourceBlock<TIn>? LastBlock { get; set; }

    /// <summary>
    /// Have the target block receive data from the specified source block.
    /// </summary>
    /// <param name="source"></param>

    /// <returns></returns>
    public TBuilder ReceiveFrom(
        ISourceBlock<TIn> source
    )
    {
        Current.SetSource(source);
        return CurrentBuilder;
    }


    /// <summary>
    /// Have the target block receive data from the specified source block.
    /// </summary>
    /// <param name="sourceBlockName"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    /// <remarks>Convenience method to receive from a named block</remarks>
    public TBuilder ReceiveFrom(
        string sourceBlockName
    )
    {
        var source = CurrentBuilder.Blocks[sourceBlockName] as ISourceBlock<TIn>
                     ?? throw new InvalidOperationException($"Block '{sourceBlockName}' is not a source block of type {typeof(TIn).Name}");
        return ReceiveFrom(source);
    }

    /// <summary>
    /// Have the target block receive data from the last added source block.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    /// <remarks>Convenience method to receive from the last added source block (positional chaining)</remarks>
    public TBuilder ReceiveFromLast()
    {
        if (CurrentBuilder.LastBlock is null)
        {
            throw new InvalidOperationException($"No source block of type {typeof(TIn).Name} has been added yet");
        }

        return ReceiveFrom(CurrentBuilder.LastBlock);
    }


}
