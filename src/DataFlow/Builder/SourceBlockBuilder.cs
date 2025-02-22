namespace Uniun.DataFlow.Builder;

using System;
using Uniun.DataFlow.Blocks;

public class SourceBlockBuilder<T> : ISourceBlockBuilder<T>
{
    public SourceBlockBuilder(Dictionary<string, IBlock> _blocks, ISourceBlock<T> currentBlock, IServiceProvider serviceProvider)
    {
        Blocks = _blocks;
        Current = currentBlock;
        CurrentBuilder = this;
        ServiceProvider = serviceProvider;
    }

    public Dictionary<string, IBlock> Blocks { get; }
    public ISourceBlockBuilder<T> CurrentBuilder { get; set; }
    public ISourceBlock<T> Current { get; }
    public IServiceProvider ServiceProvider { get; }
}
