#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Uniun.DataFlow.Blocks;
#pragma warning restore IDE0130 // Namespace does not match folder structure
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Uniun.DataFlow.Blocks.Transform;

public static class TransformExtensions
{

    // Overload for transform blocks
    public static IPropagatingBlockBuilder<TIn, TOut> AddTransform<TIn, TOut, TTransformer>(
        this IDataFlowBuilder builder,
        string name,
        Action<TransformBlockOptions<TIn, TOut>>? configureOptions = null)
        where TTransformer : class, IStreamTransformer<TIn, TOut>
    {
        return AddTransform<TIn, TOut>(builder, name, (options) =>
        {
            options.TransformerFactory = (sp) => ActivatorUtilities.CreateInstance<TTransformer>(sp);
            configureOptions?.Invoke(options);
        });       
    }

    public static IPropagatingBlockBuilder<TIn, TOut> AddTransform<TIn, TOut>(
        this IDataFlowBuilder builder,
        string name,
        Func<IServiceProvider, IStreamTransformer<TIn, TOut>> factory,
        Action<TransformBlockOptions<TIn, TOut>>? configureOptions = null)
    {

        return AddTransform<TIn, TOut>(builder, name, (options) =>
        {
            options.TransformerFactory = factory;
            configureOptions?.Invoke(options);
        });       
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
      Action<TransformBlockOptions<T, T>>? configureOptions = null)
        where TBlock : ISourceBlock<T>
        where TBuilder : ILinkableBlockBuilder<T, TBlock, TBuilder>
    {
        var currentBlock = builder.Current;

        return AddTransform<T, T>(builder, name, (options) =>
        {
            options.TransformerFactory = factory;
            configureOptions?.Invoke(options);
        })
            .ReceiveFrom(currentBlock);       

    }


    /// <summary>
    /// Adds a transform block.
    /// </summary>
    public static IPropagatingBlockBuilder<TIn, TOut> AddTransform<TIn, TOut>(
        this IDataFlowBuilder builder,
        string name,
        Action<TransformBlockOptions<TIn, TOut>> configureOptions
    )
    {
        var options = new TransformBlockOptions<TIn, TOut>();
        configureOptions?.Invoke(options);
        //  configureOptions?.Invoke(options);
        var block = ActivatorUtilities.CreateInstance<TransformBlock<TIn, TOut>>(builder.ServiceProvider, name, options);
        return builder.AddPropagatorBlock(name, block);
    }

}
