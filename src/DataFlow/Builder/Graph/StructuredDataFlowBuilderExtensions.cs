namespace Uniun.DataFlow.Builder.Graph;

using Microsoft.Extensions.DependencyInjection;
using Uniun.DataFlow.Blocks;
using Uniun.DataFlow.Blocks.BatchBlock;
using Uniun.DataFlow.Blocks.Broadcast;
using Uniun.DataFlow.Blocks.Buffer;
using Uniun.DataFlow.Blocks.Processor;
using Uniun.DataFlow.Blocks.Producer;
using Uniun.DataFlow.Blocks.Transform;

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
                    ProducersFactory = (context, ct) => Task.FromResult<IEnumerable<IStreamProducer<T>>>(new[] { producerFactory(sp) })
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

    #region Broadcast Blocks

    /// <summary>
    /// Adds a broadcast block to the graph.
    /// A broadcast block fans out input items to all connected downstream blocks concurrently.
    /// Downstream blocks connect using the standard .ReceiveFrom() API - no special configuration needed.
    /// 
    /// The optional defaultCloneFunc parameter provides a default cloning strategy for all targets.
    /// You can override this for specific targets using .WithTarget(targetName, cloneFunc).
    /// For example: defaultCloneFunc: item => item with { } (for records) or item => item.Clone() (for ICloneable).
    /// If null, the same reference is passed to all subscribers by default (suitable for immutable types).
    /// </summary>
    /// <typeparam name="T">The input type to broadcast</typeparam>
    /// <param name="builder">The builder</param>
    /// <param name="name">The name of the broadcast block</param>
    /// <param name="defaultCloneFunc">Optional default function to clone/copy items for each subscriber</param>
    /// <param name="options">Optional block options (capacity, concurrency)</param>
    /// <returns>A broadcast block builder for configuring per-target settings</returns>
    public static StructuredBroadcastBlockBuilder<T> AddBroadcast<T>(
        this IStructuredDataFlowBuilder builder,
        string name,
        Func<T, T>? defaultCloneFunc = null,
        BlockOptions? options = null)
    {
        // Dictionary shared between the builder and the factory to store target configurations
        var targetConfigurations = new Dictionary<string, Func<T, T>?>();

        builder.AddBlockDefinition(
            name,
            sp =>
            {
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<BroadcastBlock<T>>>();
                var broadcastBlock = new BroadcastBlock<T>(name, logger, defaultCloneFunc, options);

                // Apply any target configurations that were set via WithTarget
                foreach (var (targetName, cloneFunc) in targetConfigurations)
                {
                    broadcastBlock.ConfigureTarget(targetName, cloneFunc);
                }

                return broadcastBlock;
            },
            inputType: typeof(T),
            outputType: typeof(T),
            metadata: new Dictionary<string, object>
            {
                ["BlockType"] = "Broadcast"
            });

        builder.SetLastSourceBlock(name);
        var propagatorBuilder = new StructuredPropagatorBlockBuilder<T, T>(builder, name);

        // Return a builder that stores target configurations in the shared dictionary
        return new StructuredBroadcastBlockBuilder<T>(propagatorBuilder, targetConfigurations);
    }

    #endregion

    #region Buffer Blocks

    /// <summary>
    /// Adds a buffer block to the graph.
    /// A buffer block accepts items from multiple upstream producers and distributes them
    /// to multiple downstream consumers in a competing consumer pattern (each item goes to exactly one consumer).
    /// 
    /// Unlike BroadcastBlock (fanout to all), BufferBlock creates competing consumers where
    /// multiple downstream blocks compete for items from a shared buffer.
    /// </summary>
    /// <typeparam name="T">The type to buffer</typeparam>
    /// <param name="builder">The builder</param>
    /// <param name="name">The name of the buffer block</param>
    /// <param name="options">Optional block options (capacity, concurrency)</param>
    /// <returns>A propagator block builder for fluent chaining</returns>
    public static StructuredPropagatorBlockBuilder<T, T> AddBuffer<T>(
        this IStructuredDataFlowBuilder builder,
        string name,
        BlockOptions? options = null)
    {
        builder.AddBlockDefinition(
            name,
            sp =>
            {
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<BufferBlock<T>>>();
                var channelFactory = sp.GetRequiredService<IBoundedChannelFactory>();
                return new BufferBlock<T>(name, logger, channelFactory, options);
            },
            inputType: typeof(T),
            outputType: typeof(T),
            metadata: new Dictionary<string, object>
            {
                ["BlockType"] = "Buffer",
                ["SupportsMultipleSources"] = true
            });

        builder.SetLastSourceBlock(name);
        return new StructuredPropagatorBlockBuilder<T, T>(builder, name);
    }

    #endregion

    #region Buffer Block Extensions

    /// <summary>
    /// Convenience method to connect a buffer block to receive from all branches created from a specific source block.
    /// This is useful when you have multiple branches transforming data and you want to merge them all into a buffer.
    /// </summary>
    /// <typeparam name="T">The type of items in the buffer</typeparam>
    /// <param name="bufferBuilder">The buffer block builder</param>
    /// <param name="sourceBlockName">The name of the block that the branches were created from (typically a broadcast block)</param>
    /// <returns>The buffer block builder for fluent chaining</returns>
    public static StructuredPropagatorBlockBuilder<T, T> ReceiveFromBranches<T>(
        this StructuredPropagatorBlockBuilder<T, T> bufferBuilder,
        string sourceBlockName)
    {
        if (bufferBuilder.Builder is not StructuredDataFlowBuilder structuredBuilder)
        {
            throw new InvalidOperationException(
                "ReceiveFromBranches can only be used with StructuredDataFlowBuilder");
        }

        var branches = structuredBuilder.GetBranches();
        
        // Find branches that were created from the specified source block
        var relevantBranches = branches.Values
            .Where(b => b.ParentBlockAtCreation == sourceBlockName)
            .ToList();

        if (relevantBranches.Count == 0)
        {
            throw new InvalidOperationException(
                $"No branches found that were created from source block '{sourceBlockName}'. " +
                $"Make sure branches were created with AddBranch() after adding '{sourceBlockName}'.");
        }

        // Connect from the last block in each branch
        foreach (var branch in relevantBranches)
        {
            var lastBlockInBranch = branch.LastSourceBlockInBranch;
            if (lastBlockInBranch != null)
            {
                bufferBuilder.ReceiveFrom(lastBlockInBranch);
            }
        }

        return bufferBuilder;
    }

    #endregion
}
