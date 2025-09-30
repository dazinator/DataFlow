#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Uniun.DataFlow.Blocks;

using System.Reflection.Metadata.Ecma335;
#pragma warning restore IDE0130 // Namespace does not match folder structure
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
        Action<ProducerBlockOptions<TOutput>>? configureOptions = null
    )
        where TProducer : class, IStreamProducer<TOutput>
    {
        return AddProducer<TOutput>(builder, name, (options) =>
        {
           
            options.ProducersFactory = async (context, ct) => new[]
            {              
                ActivatorUtilities.CreateInstance<TProducer>(context.ServiceProvider)
            };
            configureOptions?.Invoke(options);
        });
    }

    /// <summary>
    /// Create a single producer using ActivatorUtilities with args.
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
        Action<ProducerBlockOptions<TOutput>>? configureOptions = null,
        params object[] args
    )
        where TProducer : class, IStreamProducer<TOutput>
    {
        return AddProducer<TOutput>(builder, name, (options) =>
        {
            options.ProducersFactory = async (context, ct) =>
            {              
                var producer = ActivatorUtilities.CreateInstance<TProducer>(context.ServiceProvider, args);
                return new[] { producer };
            };
            configureOptions?.Invoke(options);
        });
    }

    /// <summary>
    /// Adds a producer block.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="name"></param>
    /// <param name="options"></param>
    /// <typeparam name="TOutput"></typeparam>
    /// <typeparam name="TProducer"></typeparam>
    /// <returns></returns>
    public static ISourceBlockBuilder<TOutput> AddProducer<TOutput>(
        this IDataFlowBuilder builder,
        string name,
        Action<ProducerBlockOptions<TOutput>> configureOptions
    )
    {
        var options = new ProducerBlockOptions<TOutput>();
        configureOptions?.Invoke(options);
        //  configureOptions?.Invoke(options);
        var block = ActivatorUtilities.CreateInstance<ProducerBlock<TOutput>>(builder.ServiceProvider, name, options);
        return builder.AddSourceBlock(name, block);
    }




    /// <summary>
    /// Adds a producer block using a factory delegate to supply the <see cref="IStreamProducer{TOutput}"/> actors.
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
        Func<IDataFlowContext, IStreamProducer<TOutput>> factory,
        Action<ProducerBlockOptions<TOutput>>? configureOptions = null
    )
    {
        return AddProducer<TOutput>(builder, name, (options) =>
        {
            options.ProducersFactory = async (context, ct) => new[]
            {
                 factory(context)
            };
            configureOptions?.Invoke(options);
        });
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
        Action<ProducerBlockOptions<TOutput>>? configureOptions = null)
        where TFactory : class, IProducerFactory<TOutput>
    {
        return AddProducer<TOutput>(builder, name, (options) =>
        {
            options.ProducersFactory = async (context, ct) =>
            {
                var factory = ActivatorUtilities.CreateInstance<TFactory>(context.ServiceProvider);
                var producers = new List<IStreamProducer<TOutput>>();
                await foreach (var producer in factory.CreateProducersAsync(context, ct))
                {
                    producers.Add(producer);
                }
                return producers;
            };
            configureOptions?.Invoke(options);
        });

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
         Action<ProducerBlockOptions<TOutput>>? configureOptions = null,
        params object[] args)
        where TFactory : class, IProducerFactory<TOutput>
    {
        return AddProducer<TOutput>(builder, name, (options) =>
        {
            options.ProducersFactory = async (context, ct) =>
            {
                var factory = ActivatorUtilities.CreateInstance<TFactory>(context.ServiceProvider, args);
                var producers = new List<IStreamProducer<TOutput>>();
                await foreach (var producer in factory.CreateProducersAsync(context, ct))
                {
                    producers.Add(producer);
                }
                return producers;
            };
            configureOptions?.Invoke(options);
        });
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
        Action<ProducerBlockOptions<TOutput>>? configureOptions = null
    )
    {
        return AddProducer<TOutput>(builder, name, (options) =>
        {
            options.ProducersFactory = async (context, ct) =>
            {
                var producers = new List<IStreamProducer<TOutput>>();
                await foreach (var producer in factory.CreateProducersAsync(context, ct))
                {
                    producers.Add(producer);
                }

                return producers;
            };
            configureOptions?.Invoke(options);
        });
    }
}
