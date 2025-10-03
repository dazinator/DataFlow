namespace Uniun.DataFlow.Builder;

using System;
using Uniun.DataFlow.Blocks;

public class TargetBlockBuilder<T> : ITargetBlockBuilder<T>
{
    private readonly DataFlowBuilderState _state;

    public TargetBlockBuilder(DataFlowBuilderState state, ITargetBlock<T> currentBlock)
    {
        _state = state;
        Current = currentBlock;
        CurrentBuilder = this;
    }

    public DataFlowBuilderState State => _state;

    public ITargetBlockBuilder<T> CurrentBuilder { get; set; }

    public ITargetBlock<T> Current { get; }

    public ISourceBlock<T>? LastBlock
    {
        get => _state.LastSourceBlock as ISourceBlock<T>;
        set => _state.LastSourceBlock = value;
    }
}
