namespace Uniun.DataFlow.Builder;

public interface IBlockBuilder<TBlock, TBuilder> : IDataFlowBuilder, IHaveBlockType<TBlock>
    where TBlock : IBlock
    where TBuilder : IBlockBuilder<TBlock, TBuilder>
{
    public TBuilder CurrentBuilder { get; set; }

    public TBuilder WithBlock(Action<IBlock> configureBlock)
    {
        configureBlock?.Invoke(Current);
        return CurrentBuilder;
    }
}
