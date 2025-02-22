namespace Uniun.DataFlow.Builder;

using System;
using Uniun.DataFlow.Blocks;

public class TargetBlockBuilder<T> : ITargetBlockBuilder<T>
{
    public TargetBlockBuilder(Dictionary<string, IBlock> _blocks, ITargetBlock<T> currentBlock, IServiceProvider serviceProvider)
    {
        Blocks = _blocks;
        Current = currentBlock;
        CurrentBuilder = this;
        ServiceProvider = serviceProvider;
    }

    public Dictionary<string, IBlock> Blocks { get; }

    public ITargetBlockBuilder<T> CurrentBuilder { get; set; }

    public ITargetBlock<T> Current { get; }
    public IServiceProvider ServiceProvider { get; }
}
