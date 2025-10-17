namespace Uniun.DataFlow.Builder;

using System;
using Uniun.DataFlow.Blocks;

public class PropagatingBlockBuilder<TIn, TOut> : IPropagatingBlockBuilder<TIn, TOut>
{
    public PropagatingBlockBuilder(DataFlowBuilderState state, IPropagatorBlock<TIn, TOut> currentBlock)
    {
        State = state;
        Current = currentBlock;
        CurrentBuilder = this;
    }

    public DataFlowBuilderState State { get; }
    public IPropagatorBlock<TIn, TOut> Current { get; }
    public IPropagatingBlockBuilder<TIn, TOut> CurrentBuilder { get; set; }

    public ISourceBlock<TIn>? LastBlock
    {
        get => State.LastSourceBlock as ISourceBlock<TIn>;
        set => State.LastSourceBlock = value;
    }
}
