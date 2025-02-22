namespace Uniun.DataFlow.Builder;

using Uniun.DataFlow.Blocks;

public interface ITargetBlockBuilder<TIn> : IReceivableBlockBuilder<TIn, ITargetBlock<TIn>, ITargetBlockBuilder<TIn>>
{

}
