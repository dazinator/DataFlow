namespace Uniun.DataFlow.Builder;

using System;
using Uniun.DataFlow.Blocks;

public class PropagatingBlockBuilder<TIn, TOut> : IPropagatingBlockBuilder<TIn, TOut>
{
    private readonly DataFlowBuilderState _state;

    public PropagatingBlockBuilder(DataFlowBuilderState state, IPropagatorBlock<TIn, TOut> currentBlock)
    {
        _state = state;
        Current = currentBlock;
        CurrentBuilder = this;
    }

    public DataFlowBuilderState State => _state;
    public IPropagatorBlock<TIn, TOut> Current { get; }
    public IPropagatingBlockBuilder<TIn, TOut> CurrentBuilder { get; set; }

    public ISourceBlock<TIn>? LastBlock
    {
        get => _state.LastSourceBlock as ISourceBlock<TIn>;
        set => _state.LastSourceBlock = value;
    }
}
