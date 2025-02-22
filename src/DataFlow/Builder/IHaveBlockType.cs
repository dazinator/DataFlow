namespace Uniun.DataFlow.Builder;

using Uniun.DataFlow.Blocks;

public interface IHaveBlockType<TBlock>
    where TBlock : IBlock
{
    public TBlock Current { get; }

}
