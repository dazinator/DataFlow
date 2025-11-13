namespace Uniun.DataFlow;

using Uniun.DataFlow.Builder;

public static class DataFlowBuilderExtensions
{
    public static void AddBlock(this IDataFlowBuilder builder, string name, IBlock block)
    {
        if (builder.Blocks.ContainsKey(name))
        {
            throw new ArgumentException($"Block with name '{name}' already exists", nameof(name));
        }

        builder.Blocks.Add(name, block);
    }

    public static IBlock GetBlock<T>(this IDataFlowBuilder builder, string name)
    {
        IBlock? block = null;

        if (builder.Blocks.TryGetValue(name, out var val))
        {
            block = val as IBlock;
        }

        if (block is null)
        {
            throw new InvalidOperationException($"No such block with name '{name}' has been registered with the builder");
        }

        return block;
    }

    public static TBlock GetBlockType<TBlock>(this IDataFlowBuilder builder, string name)
        where TBlock : class, IBlock
    {
        TBlock? block = null;

        if (builder.Blocks.TryGetValue(name, out var val))
        {
            block = val as TBlock;
        }

        if (block is null)
        {
            throw new InvalidOperationException($"No such block with name '{name}' has been registered with the builder");
        }

        return block;
    }

    #region Source Blocks

    public static ISourceBlockBuilder<T> AddSourceBlock<T>(this IDataFlowBuilder builder, string name, ISourceBlock<T> block)
    {
        builder.AddBlock(name, (IBlock)block);
        builder.State.LastSourceBlock = block;
        return new SourceBlockBuilder<T>(builder.State, block);
    }

    public static ISourceBlock<T> GetSourceBlock<T>(this IDataFlowBuilder builder, string name)
    {
        if (!builder.Blocks.TryGetValue(name, out var block))
        {
            throw new InvalidOperationException($"No block found with name '{name}'");
        }

        if (block is not ISourceBlock<T> sourceBlock)
        {
            throw new InvalidOperationException($"Block '{name}' is not of type {typeof(ISourceBlock<T>).Name}");
        }

        return sourceBlock;
    }

    public static ISourceBlockBuilder<T> WithSourceBlock<T>(this IDataFlowBuilder builder, string name)
    {
        var sourceBlock = builder.GetSourceBlock<T>(name);
        return new SourceBlockBuilder<T>(builder.State, sourceBlock);
    }

    #endregion

    #region Target Blocks

    public static ITargetBlockBuilder<T> AddTargetBlock<T>(this IDataFlowBuilder builder, string name, ITargetBlock<T> block)
    {
        builder.AddBlock(name, (IBlock)block);
        return new TargetBlockBuilder<T>(builder.State, block);
    }

    public static ITargetBlock<T> GetTargetBlock<T>(this IDataFlowBuilder builder, string name)
    {
        if (!builder.Blocks.TryGetValue(name, out var block))
        {
            throw new InvalidOperationException($"No block found with name '{name}'");
        }

        if (block is not ITargetBlock<T> targetBlock)
        {
            throw new InvalidOperationException($"Block '{name}' is not of type {typeof(ITargetBlock<T>).Name}");
        }

        return targetBlock;
    }

    public static ITargetBlockBuilder<T> WithTargetBlock<T>(this IDataFlowBuilder builder, string name)
    {
        var block = builder.GetTargetBlock<T>(name);
        return new TargetBlockBuilder<T>(builder.State, block);
    }

    #endregion

    #region Propagator Blocks

    public static IPropagatingBlockBuilder<TIn, TOut> AddPropagatorBlock<TIn, TOut>(this IDataFlowBuilder builder, string name, IPropagatorBlock<TIn, TOut> block)
    {
        builder.AddBlock(name, (IBlock)block);
        builder.State.LastSourceBlock = block;
        return new PropagatingBlockBuilder<TIn, TOut>(builder.State, block);
    }


    public static IPropagatorBlock<TIn, TOut> GetPropagatorBlock<TIn, TOut>(this IDataFlowBuilder builder, string name)
    {
        if (!builder.Blocks.TryGetValue(name, out var block))
        {
            throw new InvalidOperationException($"No block found with name '{name}'");
        }

        if (block is not IPropagatorBlock<TIn, TOut> targetBlock)
        {
            throw new InvalidOperationException($"Block '{name}' is not of type {typeof(IPropagatorBlock<TIn, TOut>).Name}");
        }

        return targetBlock;
    }

    public static IPropagatingBlockBuilder<TIn, TOut> WithPropagatorBlock<TIn, TOut>(this IDataFlowBuilder builder, string name)
    {
        var block = builder.GetPropagatorBlock<TIn, TOut>(name);
        return new PropagatingBlockBuilder<TIn, TOut>(builder.State, block);
    }

    #endregion

}
