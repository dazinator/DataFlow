namespace Uniun.DataFlow.Builder;

using System;
using Uniun.DataFlow.Blocks;

public class TargetBlockBuilder<T> : ITargetBlockBuilder<T>
{
    public TargetBlockBuilder(DataFlowBuilderState state, ITargetBlock<T> currentBlock)
    {
        State = state;
        Current = currentBlock;
        CurrentBuilder = this;
    }

    public DataFlowBuilderState State { get; }

    public ITargetBlockBuilder<T> CurrentBuilder { get; set; }

    public ITargetBlock<T> Current { get; }

    public ISourceBlock<T>? LastBlock
    {
        get => State.LastSourceBlock as ISourceBlock<T>;
        set => State.LastSourceBlock = value;
    }
}
