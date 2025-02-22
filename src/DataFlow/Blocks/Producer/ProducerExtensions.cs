#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Uniun.DataFlow.Blocks;
#pragma warning restore IDE0130 // Namespace does not match folder structure
using Microsoft.Extensions.DependencyInjection;
using Uniun.DataFlow.Blocks.Producer;

public static class ProducerExtensions
{
    /// <summary>
    /// Create a single producer using ActivatorUtilities.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="name"></param>
    /// <param name="options"></param>
    /// <typeparam name="TOutput"></typeparam>
    /// <typeparam name="TProducer"></typeparam>
    /// <returns></returns>
    public static ISourceBlockBuilder<TOutput> AddProducer<TOutput, TProducer>(
        this IDataFlowBuilder builder,
        string name,
        BlockOptions? options = null
    )
        where TProducer : class, IStreamProducer<TOutput>
    {
        var block = new ProducerBlock<TOutput>(
            async (context, ct) => new[]
            {
                ActivatorUtilities.CreateInstance<TProducer>(context.ServiceProvider)
            },
            options);

        return builder.AddSourceBlock(name, block);
    }

    /// <summary>
    /// Create a single producer using ActivatorUtilities.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="name"></param>
    /// <param name="options"></param>
    /// <param name="args"></param>
    /// <typeparam name="TOutput"></typeparam>
    /// <typeparam name="TProducer"></typeparam>
    /// <returns></returns>
    public static ISourceBlockBuilder<TOutput> AddProducer<TOutput, TProducer>(
        this IDataFlowBuilder builder,
        string name,
        BlockOptions? options = null,
        params object[] args
    )
        where TProducer : class, IStreamProducer<TOutput>
    {
        var block = new ProducerBlock<TOutput>(
            async (context, ct) => new[]
            {
                ActivatorUtilities.CreateInstance<TProducer>(context.ServiceProvider, args)
            },
            options);

        return builder.AddSourceBlock(name, block);
    }

    /// <summary>
    /// Create a single producer using factory.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="name"></param>
    /// <param name="factory"></param>
    /// <param name="options"></param>
    /// <typeparam name="TOutput"></typeparam>
    /// <returns></returns>
    public static ISourceBlockBuilder<TOutput> AddProducer<TOutput>(
        this IDataFlowBuilder builder,
        string name,
        Func<IServiceProvider, IStreamProducer<TOutput>> factory,
        BlockOptions? options = null
    )
    {
        var block = new ProducerBlock<TOutput>(
            async (context, ct) => new[]
            {
                factory(context.ServiceProvider)
            },
            options);

        return builder.AddSourceBlock(name, block);
    }

    /// <summary>
    /// Create a producer block that has multiple producers provided by the <see cref="IProducerFactory{T}"/>. The factory is activated from current DI scope.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="name"></param>
    /// <param name="options"></param>
    /// <typeparam name="TOutput"></typeparam>
    /// <typeparam name="TFactory"></typeparam>
    /// <returns></returns>
    /// <remarks>They will be executed concurrently according to <see cref="BlockOptions.MaxConcurrency"/> </remarks>
    public static ISourceBlockBuilder<TOutput> AddProducers<TOutput, TFactory>(
        this IDataFlowBuilder builder,
        string name,
        BlockOptions? options = null)
        where TFactory : class, IProducerFactory<TOutput>
    {
        var block = new ProducerBlock<TOutput>(async (context, ct) =>
        {
            var factory = ActivatorUtilities.CreateInstance<TFactory>(context.ServiceProvider);
            var producers = new List<IStreamProducer<TOutput>>();
            await foreach (var producer in factory.CreateProducersAsync(context, ct))
            {
                producers.Add(producer);
            }
            return producers;
        }, options);
        return builder.AddSourceBlock(name, block);
    }


    /// <summary>
    /// Adds a producer block that has multiple producers provided by the <see cref="IProducerFactory{T}"/>.
    /// The factory is activated from current DI scope. Overload that allows additional constructor args to be passed to the factory when its activated.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="name"></param>
    /// <param name="options"></param>
    /// <param name="args"></param>
    /// <typeparam name="TOutput"></typeparam>
    /// <typeparam name="TFactory"></typeparam>
    /// <returns></returns>
    public static ISourceBlockBuilder<TOutput> AddProducers<TOutput, TFactory>(
        this IDataFlowBuilder builder,
        string name,
        BlockOptions? options = null,
        params object[] args)
        where TFactory : class, IProducerFactory<TOutput>
    {
        var block = new ProducerBlock<TOutput>(async (context, ct) =>
        {
            var factory = ActivatorUtilities.CreateInstance<TFactory>(context.ServiceProvider, args);
            var producers = new List<IStreamProducer<TOutput>>();
            await foreach (var producer in factory.CreateProducersAsync(context, ct))
            {
                producers.Add(producer);
            }
            return producers;
        }, options);
        return builder.AddSourceBlock(name, block);
    }


    /// <summary>
    /// Create multiple producers from factory.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="name"></param>
    /// <param name="factory"></param>
    /// <param name="options"></param>
    /// <typeparam name="TOutput"></typeparam>
    /// <returns></returns>
    public static ISourceBlockBuilder<TOutput> AddProducers<TOutput>(
        this IDataFlowBuilder builder,
        string name,
        IProducerFactory<TOutput> factory,
        BlockOptions? options = null
    )
    {
        var block = new ProducerBlock<TOutput>(async (context, ct) =>
        {
            var producers = new List<IStreamProducer<TOutput>>();
            await foreach (var producer in factory.CreateProducersAsync(context, ct))
            {
                producers.Add(producer);
            }

            return producers;
        }, options);
        return builder.AddSourceBlock(name, block);
    }
}
