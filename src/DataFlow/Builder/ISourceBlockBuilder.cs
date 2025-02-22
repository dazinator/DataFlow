namespace Uniun.DataFlow.Builder;

public interface ISourceBlockBuilder<TOut> :
    ILinkableBlockBuilder<TOut, ISourceBlock<TOut>, ISourceBlockBuilder<TOut>>
{



}
