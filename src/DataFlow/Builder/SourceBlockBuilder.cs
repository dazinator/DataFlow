namespace Uniun.DataFlow.Builder;

using System;
using Uniun.DataFlow.Blocks;

public class SourceBlockBuilder<T> : ISourceBlockBuilder<T>
{
    public SourceBlockBuilder(DataFlowBuilderState state, ISourceBlock<T> currentBlock)
    {
        State = state;
        Current = currentBlock;
        CurrentBuilder = this;
    }

    public DataFlowBuilderState State { get; }
    public ISourceBlockBuilder<T> CurrentBuilder { get; set; }
    public ISourceBlock<T> Current { get; }
}
