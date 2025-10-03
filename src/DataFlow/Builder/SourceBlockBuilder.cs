namespace Uniun.DataFlow.Builder;

using System;
using Uniun.DataFlow.Blocks;

public class SourceBlockBuilder<T> : ISourceBlockBuilder<T>
{
    private readonly DataFlowBuilderState _state;

    public SourceBlockBuilder(DataFlowBuilderState state, ISourceBlock<T> currentBlock)
    {
        _state = state;
        Current = currentBlock;
        CurrentBuilder = this;
    }

    public DataFlowBuilderState State => _state;
    public ISourceBlockBuilder<T> CurrentBuilder { get; set; }
    public ISourceBlock<T> Current { get; }
}
