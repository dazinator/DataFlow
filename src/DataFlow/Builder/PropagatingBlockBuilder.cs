namespace Uniun.DataFlow.Builder;

using System;
using Uniun.DataFlow.Blocks;

public class PropagatingBlockBuilder<TIn, TOut> : IPropagatingBlockBuilder<TIn, TOut>
{
    public PropagatingBlockBuilder(Dictionary<string, IBlock> _blocks, IPropagatorBlock<TIn, TOut> currentBlock, IServiceProvider serviceProvider)
    {
        Blocks = _blocks;
        Current = currentBlock;
        CurrentBuilder = this;
        ServiceProvider = serviceProvider;
    }
    public Dictionary<string, IBlock> Blocks { get; }
    public IPropagatorBlock<TIn, TOut> Current { get; }
    public IPropagatingBlockBuilder<TIn, TOut> CurrentBuilder { get; set; }
    public IServiceProvider ServiceProvider { get; }
}
