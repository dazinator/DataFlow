namespace Uniun.DataFlow.Builder.Graph;

using Microsoft.Extensions.DependencyInjection;
using Uniun.DataFlow.Blocks;
using Uniun.DataFlow.Blocks.Producer;
using Uniun.DataFlow.Blocks.Processor;
using Uniun.DataFlow.Blocks.Transform;
using Uniun.DataFlow.Blocks.BatchBlock;

/// <summary>
/// Extension methods for the structured dataflow builder.
/// These provide a fluent API for building dataflows with graph metadata.
/// </summary>
public static class StructuredDataFlowBuilderExtensions
{
    #region Producer Blocks

    /// <summary>
    /// Adds a producer block to the graph.
    /// </summary>
    public static StructuredSourceBlockBuilder<T> AddProducer<T>(
        this IStructuredDataFlowBuilder builder,
        string name,
        Func<IServiceProvider, IStreamProducer<T>> producerFactory,
        BlockOptions? options = null)
    {
        builder.AddBlockDefinition(
            name,
            sp =>
            {
                var producerOptions = new ProducerBlockOptions<T>
                {
                    ProducersFactory = async (context, ct) => new[] { producerFactory(sp) }
                };
                if (options != null)
                {
                    producerOptions.MaxConcurrency = options.MaxConcurrency;
                    producerOptions.Capacity = options.Capacity;
                }
                return ActivatorUtilities.CreateInstance<ProducerBlock<T>>(sp, name, producerOptions);
            },
            inputType: null,
            outputType: typeof(T));

        builder.SetLastSourceBlock(name);
        return new StructuredSourceBlockBuilder<T>(builder, name);
    }

    #endregion

    #region Processor Blocks

    /// <summary>
    /// Adds a processor block to the graph.
    /// </summary>
    public static StructuredTargetBlockBuilder<T> AddProcessor<T>(
        this IStructuredDataFlowBuilder builder,
        string name,
        Func<IServiceProvider, IStreamProcessor<T>> processorFactory,
        BlockOptions? options = null)
    {
        builder.AddBlockDefinition(
            name,
            sp =>
            {
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ProcessorBlock<T>>>();
                return new ProcessorBlock<T>(name, logger, processorFactory, options);
            },
            inputType: typeof(T),
            outputType: null);

        return new StructuredTargetBlockBuilder<T>(builder, name);
    }

    #endregion

    #region Transform Blocks

    /// <summary>
    /// Adds a transform block to the graph.
    /// </summary>
    public static StructuredPropagatorBlockBuilder<TIn, TOut> AddTransform<TIn, TOut>(
        this IStructuredDataFlowBuilder builder,
        string name,
        Func<IServiceProvider, IStreamTransformer<TIn, TOut>> transformerFactory,
        BlockOptions? options = null)
    {
        builder.AddBlockDefinition(
            name,
            sp =>
            {
                var transformOptions = new TransformBlockOptions<TIn, TOut>
                {
                    TransformerFactory = transformerFactory
                };
                if (options != null)
                {
                    transformOptions.MaxConcurrency = options.MaxConcurrency;
                    transformOptions.Capacity = options.Capacity;
                }
                return ActivatorUtilities.CreateInstance<TransformBlock<TIn, TOut>>(sp, name, transformOptions);
            },
            inputType: typeof(TIn),
            outputType: typeof(TOut));

        builder.SetLastSourceBlock(name);
        return new StructuredPropagatorBlockBuilder<TIn, TOut>(builder, name);
    }

    #endregion

    #region Batch Blocks

    /// <summary>
    /// Adds a batch block to the graph.
    /// </summary>
    public static StructuredPropagatorBlockBuilder<T, T[]> AddBatch<T>(
        this IStructuredDataFlowBuilder builder,
        string name,
        int maxBatchSize,
        TimeSpan? windowPeriod = null,
        BlockOptions? options = null)
    {
        builder.AddBlockDefinition(
            name,
            sp =>
            {
                var batchOptions = new BatchBlockOptions
                {
                    MaxBatchSize = maxBatchSize,
                    WindowPeriod = windowPeriod ?? TimeSpan.FromSeconds(5)
                };
                if (options != null)
                {
                    batchOptions.MaxConcurrency = options.MaxConcurrency;
                    batchOptions.Capacity = options.Capacity;
                }
                return ActivatorUtilities.CreateInstance<BatchBlock<T>>(sp, name, batchOptions);
            },
            inputType: typeof(T),
            outputType: typeof(T[]));

        builder.SetLastSourceBlock(name);
        return new StructuredPropagatorBlockBuilder<T, T[]>(builder, name);
    }

    #endregion
}
