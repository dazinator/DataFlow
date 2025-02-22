namespace Uniun.DataFlow.Builder;

using Uniun.DataFlow.Blocks;

public interface IPropagatingBlockBuilder<TIn, TOut> :
    ILinkableBlockBuilder<TOut, IPropagatorBlock<TIn, TOut>, IPropagatingBlockBuilder<TIn, TOut>>,
    IReceivableBlockBuilder<TIn, IPropagatorBlock<TIn, TOut>, IPropagatingBlockBuilder<TIn, TOut>>
{




}
