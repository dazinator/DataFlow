#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Uniun.DataFlow.Blocks;
#pragma warning restore IDE0130 // Namespace does not match folder structure
using Microsoft.Extensions.DependencyInjection;
using Uniun.DataFlow.Blocks.Transform;

public static class TransformExtensions
{

    // Overload for transform blocks
    public static IPropagatingBlockBuilder<TIn, TOut> AddTransform<TIn, TOut, TTransformer>(
        this IDataFlowBuilder builder,
        string name,
        BlockOptions? options = null)
        where TTransformer : class, IStreamTransformer<TIn, TOut>
    {
        var block = new TransformBlock<TIn, TOut>(
            sp => ActivatorUtilities.CreateInstance<TTransformer>(sp),
            options);

        return builder.AddPropagatorBlock(name, block);
    }

    public static IPropagatingBlockBuilder<TIn, TOut> AddTransform<TIn, TOut>(
        this IDataFlowBuilder builder,
        string name,
        Func<IServiceProvider, IStreamTransformer<TIn, TOut>> factory,
        BlockOptions? options = null)
    {
        var block = new TransformBlock<TIn, TOut>(
            factory,
            options);

        return builder.AddPropagatorBlock(name, block);
    }


    /// <summary>
    /// Adds a transform block and links it to the current block
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="builder"></param>
    /// <param name="name"></param>
    /// <param name="factory"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static IPropagatingBlockBuilder<T, T> ThenTransform<T, TBlock, TBuilder>(
        this ILinkableBlockBuilder<T, TBlock, TBuilder> builder,
    //  this IPropagatingBlockBuilder<T, T> builder,
      string name,
      Func<IServiceProvider, IStreamTransformer<T, T>> factory,
      BlockOptions? options = null)
        where TBlock : ISourceBlock<T>
        where TBuilder : ILinkableBlockBuilder<T, TBlock, TBuilder>
    {
        var currentBlock = builder.Current;
        var block = new TransformBlock<T, T>(
            factory,
            options);

        return builder.AddPropagatorBlock(name, block)
                                 .ReceiveFrom(currentBlock);

    }

}
