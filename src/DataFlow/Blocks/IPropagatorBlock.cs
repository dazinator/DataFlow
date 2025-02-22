namespace Uniun.DataFlow.Blocks;
public interface IPropagatorBlock<TIn, TOut> : ITargetBlock<TIn>, ISourceBlock<TOut>
{

}
