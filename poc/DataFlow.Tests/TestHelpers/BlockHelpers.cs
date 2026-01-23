namespace DataFlow.POC.Tests.TestHelpers;

using DataFlow.POC.Blocks;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using DataFlow.POC.Registry;

/// <summary>
/// Helper methods for creating block instances in tests with consistent patterns.
/// Reduces boilerplate and provides a single place to update if block construction changes.
/// 
/// This helper consolidates 300+ block instantiation call sites across tests into
/// a unified, maintainable pattern. It eliminates 50-70% of boilerplate code and
/// encapsulates obsolete constructor warnings in a single location.
/// 
/// Benefits:
/// - Consistent pattern across all block types
/// - Encapsulation of name-setting logic
/// - Single place to update if block construction changes
/// - Improved test readability
/// 
/// Usage Examples:
/// 
/// // Producer blocks
/// var producer = BlockHelpers.CreateProducer("producer", TestStreams.Integers(10));
/// var producer = BlockHelpers.CreateProducer("producer", ctx => TestStreams.Integers(10));
/// 
/// // Actor blocks
/// var actor = BlockHelpers.CreateActor<int, string, TransformActor>(
///     "actor", 
///     TestServiceBuilder.Create().WithScoped(new TransformActor()).BuildScopeFactory());
/// 
/// // Batch blocks
/// var batcher = BlockHelpers.CreateBatch<int>("batcher", maxBatchSize: 100);
/// var batcher = BlockHelpers.CreateBatch<int>("batcher", 100, TimeSpan.FromSeconds(5));
/// </summary>
public static class BlockHelpers
{
    #region Temporary Migration Helpers (Producer Wrappers)
    
    /// <summary>
    /// Creates a source block from a static enumerable.
    /// TEMPORARY: Wraps plain stream in epoch for compatibility.
    /// </summary>
    public static IBlock<object, T> CreateProducer<T>(
        string name,
        IEnumerable<T> items)
    {
        return CreateProducer(name, ToAsyncEnumerable(items));
    }

    /// <summary>
    /// Creates a source block from an async enumerable.
    /// TEMPORARY: Wraps plain stream in epoch for compatibility.
    /// </summary>
    public static IBlock<object, T> CreateProducer<T>(
        string name,
        IAsyncEnumerable<T> items)
    {
        return new PlainProducerWrapper<T>(name, _ => items);
    }

    /// <summary>
    /// Creates a source block from a producer function.
    /// TEMPORARY: Wraps plain stream in epoch for compatibility.
    /// </summary>
    public static IBlock<object, T> CreateProducer<T>(
        string name,
        Func<IExecutionContext, IAsyncEnumerable<T>> producer)
    {
        return new PlainProducerWrapper<T>(name, producer);
    }

    /// <summary>
    /// Wrapper block that wraps a plain producer in a single epoch.
    /// </summary>
    private class PlainProducerWrapper<T> : BlockBase<object, T>
    {
        private readonly Func<IExecutionContext, IAsyncEnumerable<T>> _producer;

        public PlainProducerWrapper(string name, Func<IExecutionContext, IAsyncEnumerable<T>> producer)
            : base(new BlockContext(name))
        {
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
        }

        public override async IAsyncEnumerable<T> ExecuteAsync(
            IAsyncEnumerable<object> input,
            IExecutionContext context)
        {
            // Source blocks ignore input - they generate data
            await foreach (var item in _producer(context).WithCancellation(context.CancellationToken))
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// Wrapper for concurrent producer (multi-stream merge).
    /// </summary>
    private class ConcurrentProducerWrapper<T> : BlockBase<object, T>
    {
        private readonly Func<IExecutionContext, IEnumerable<IAsyncEnumerable<T>>> _producersFactory;
        private readonly int _maxConcurrency;

        public ConcurrentProducerWrapper(
            string name,
            Func<IExecutionContext, IEnumerable<IAsyncEnumerable<T>>> producersFactory,
            int maxConcurrency)
            : base(new BlockContext(name))
        {
            _producersFactory = producersFactory ?? throw new ArgumentNullException(nameof(producersFactory));
            _maxConcurrency = maxConcurrency;
        }

        public override async IAsyncEnumerable<T> ExecuteAsync(
            IAsyncEnumerable<object> input,
            IExecutionContext context)
        {
            var producers = _producersFactory(context).ToList();
            
            // Use bounded channel
            var capacity = Math.Max(100, producers.Count * 10);
            var channel = System.Threading.Channels.Channel.CreateBounded<T>(
                new System.Threading.Channels.BoundedChannelOptions(capacity)
                {
                    FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait
                });

            var tasks = producers.Select(producer => Task.Run(async () =>
            {
                try
                {
                    await foreach (var item in producer.WithCancellation(context.CancellationToken))
                    {
                        await channel.Writer.WriteAsync(item, context.CancellationToken);
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            }, context.CancellationToken)).ToList();

            // Complete channel when all producers are done
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.WhenAll(tasks);
                    channel.Writer.Complete();
                }
                catch (Exception ex)
                {
                    channel.Writer.Complete(ex);
                }
            }, context.CancellationToken);

            // Yield items from the channel
            await foreach (var item in channel.Reader.ReadAllAsync(context.CancellationToken))
            {
                yield return item;
            }
        }
    }

    #endregion

    #region Producer Blocks (Removed - Use Epoch Sources)

    // Producer blocks have been removed. Use epoch-based sources instead:
    // - For simple producers: Wrap streams with .WrapInSingleEpoch("source-name")
    // - For actor-based sources: Use PlainSourceAdapter or EpochSourceBlock
    // - See SingleEpochExtensions for wrapping utilities

    #endregion

    #region Temporary Migration Helpers (Plain to Epoch Wrappers)
    
    /// <summary>
    /// Creates an epoch-wrapped actor that accepts plain input streams.
    /// TEMPORARY: This wraps the input in a single epoch for easier test migration.
    /// Eventually tests should use CreateEpochActor directly with epoch streams.
    /// </summary>
    public static IBlock<TIn, TOut> CreateActor<TIn, TOut, TActor>(
        string name,
        IServiceScopeFactory scopeFactory)
        where TActor : IStreamActor<TIn, TOut>
    {
        // Return a wrapper block that converts plain input to epoch input
        return new PlainToEpochActorWrapper<TIn, TOut, TActor>(name, scopeFactory);
    }

    /// <summary>
    /// Creates an epoch-wrapped actor with a single actor instance.
    /// TEMPORARY: This wraps the input in a single epoch for easier test migration.
    /// </summary>
    public static IBlock<TIn, TOut> CreateActor<TIn, TOut, TActor>(
        string name,
        TActor actor)
        where TActor : class, IStreamActor<TIn, TOut>
    {
        var scopeFactory = TestServiceBuilder.Create()
            .WithScoped(actor)
            .BuildScopeFactory();
        return CreateActor<TIn, TOut, TActor>(name, scopeFactory);
    }

    /// <summary>
    /// Creates a concurrent producer wrapper.
    /// TEMPORARY: For test migration compatibility.
    /// </summary>
    public static IBlock<object, T> CreateConcurrentProducer<T>(
        string name,
        Func<IExecutionContext, IEnumerable<IAsyncEnumerable<T>>> producersFactory,
        int maxConcurrency = 4)
    {
        return new ConcurrentProducerWrapper<T>(name, producersFactory, maxConcurrency);
    }

    /// <summary>
    /// Wrapper block that converts plain input to epoch streams for actor processing.
    /// This allows tests written for plain ActorBlock to work with EpochActorBlock.
    /// </summary>
    private class PlainToEpochActorWrapper<TIn, TOut, TActor> : BlockBase<TIn, TOut>
        where TActor : IStreamActor<TIn, TOut>
    {
        private readonly EpochActorBlock<TIn, TOut, TActor> _epochActor;

        public PlainToEpochActorWrapper(string name, IServiceScopeFactory scopeFactory)
            : base(new BlockContext(name))
        {
            _epochActor = new EpochActorBlock<TIn, TOut, TActor>(new BlockContext(name + "-epoch"), scopeFactory);
        }

        public override async IAsyncEnumerable<TOut> ExecuteAsync(
            IAsyncEnumerable<TIn> input,
            IExecutionContext context)
        {
            // Wrap plain input in single epoch
            var epochInput = input.WrapInSingleEpoch("test-source", context.CancellationToken);
            
            // Process through epoch actor
            var epochOutput = _epochActor.ExecuteAsync(epochInput, context);
            
            // Unwrap epoch output to plain output
            await foreach (var epochStream in epochOutput)
            {
                await foreach (var item in epochStream.Items)
                {
                    yield return item;
                }
            }
        }
    }

    #endregion

    #region Actor Blocks (Removed - Use Epoch Actor Blocks)

    // Plain ActorBlock has been removed. Use EpochActorBlock instead:
    // - Use CreateEpochActor<TIn, TOut, TActor>(name, scopeFactory)
    // - Input must be IAsyncEnumerable<IEpochStream<TIn>>
    // - For plain sources, wrap with .WrapInSingleEpoch("source-name")

    #endregion

    #region Temporary Migration Helpers (Batch Wrappers)
    
    /// <summary>
    /// Creates an epoch-wrapped batch block that accepts plain input streams.
    /// TEMPORARY: This wraps the input in a single epoch for easier test migration.
    /// </summary>
    public static IBlock<T, T[]> CreateBatch<T>(
        string name,
        int maxBatchSize)
    {
        return new PlainToEpochBatchWrapper<T>(name, maxBatchSize, null);
    }

    /// <summary>
    /// Creates an epoch-wrapped batch block with time window that accepts plain input streams.
    /// TEMPORARY: This wraps the input in a single epoch for easier test migration.
    /// </summary>
    public static IBlock<T, T[]> CreateBatch<T>(
        string name,
        int maxBatchSize,
        TimeSpan windowPeriod)
    {
        return new PlainToEpochBatchWrapper<T>(name, maxBatchSize, windowPeriod);
    }

    /// <summary>
    /// Wrapper block that converts plain input to epoch streams for batch processing.
    /// </summary>
    private class PlainToEpochBatchWrapper<T> : BlockBase<T, T[]>
    {
        private readonly EpochBatchBlock<T> _epochBatch;

        public PlainToEpochBatchWrapper(string name, int maxBatchSize, TimeSpan? windowPeriod)
            : base(new BlockContext(name))
        {
            _epochBatch = new EpochBatchBlock<T>(new BlockContext(name + "-epoch"), maxBatchSize, windowPeriod);
        }

        public override async IAsyncEnumerable<T[]> ExecuteAsync(
            IAsyncEnumerable<T> input,
            IExecutionContext context)
        {
            // Wrap plain input in single epoch
            var epochInput = input.WrapInSingleEpoch("test-source", context.CancellationToken);
            
            // Process through epoch batch
            var epochOutput = _epochBatch.ExecuteAsync(epochInput, context);
            
            // Unwrap epoch output to plain output
            await foreach (var epochStream in epochOutput)
            {
                await foreach (var batch in epochStream.Items)
                {
                    yield return batch;
                }
            }
        }
    }

    #endregion

    #region Batch Blocks (Removed - Use Epoch Batch Blocks)

    // Plain BatchBlock has been removed. Use EpochBatchBlock instead:
    // - Use CreateEpochBatch<T>(name, maxBatchSize)
    // - Input must be IAsyncEnumerable<IEpochStream<T>>
    // - For plain sources, wrap with .WrapInSingleEpoch("source-name")

    #endregion

    #region Router Blocks

    #endregion

    #region Envelope Blocks

    /// <summary>
    /// Creates a SimpleEnvelopeTransformerBlock with a synchronous transform function.
    /// </summary>
    public static SimpleEnvelopeTransformerBlock<TIn, TOut> CreateSimpleEnvelopeTransformer<TIn, TOut>(
        string name,
        Func<TIn, TOut> transform)
    {
        return new SimpleEnvelopeTransformerBlock<TIn, TOut>(new BlockContext(name), transform);
    }

    /// <summary>
    /// Creates an AsyncEnvelopeTransformerBlock with an async transform function.
    /// </summary>
    public static AsyncEnvelopeTransformerBlock<TIn, TOut> CreateAsyncEnvelopeTransformer<TIn, TOut>(
        string name,
        Func<TIn, IExecutionContext, Task<TOut>> transformAsync)
    {
        return new AsyncEnvelopeTransformerBlock<TIn, TOut>(new BlockContext(name), transformAsync);
    }

    /// <summary>
    /// Creates an EnvelopeProjectorBlock with a projection function.
    /// </summary>
    public static EnvelopeProjectorBlock<TIn, TOut> CreateEnvelopeProjector<TIn, TOut>(
        string name,
        Func<TIn, IExecutionContext, IAsyncEnumerable<TOut>> project)
    {
        return new EnvelopeProjectorBlock<TIn, TOut>(new BlockContext(name), project);
    }

    /// <summary>
    /// Creates an EnvelopeProcessorBlock with a data processor function.
    /// </summary>
    public static EnvelopeProcessorBlock<T> CreateEnvelopeProcessor<T>(
        string name,
        Func<T, IExecutionContext, Task> processData,
        Func<IDataEnvelope, IExecutionContext, Task>? processControl = null)
    {
        return new EnvelopeProcessorBlock<T>(new BlockContext(name), processData, processControl);
    }

    #endregion

    #region Epoch Blocks

    /// <summary>
    /// Creates an EpochSourceBlock with a service scope factory and coordinator.
    /// </summary>
    public static EpochSourceBlock<T, TActor> CreateEpochSource<T, TActor>(
        string name,
        IServiceScopeFactory scopeFactory,
        IEpochCoordinator coordinator)
        where TActor : ISourceActor<T>
    {
        return new EpochSourceBlock<T, TActor>(new BlockContext(name), scopeFactory, coordinator);
    }

    /// <summary>
    /// Creates an EpochSourceBlock with a single actor instance (automatically wraps in scope factory).
    /// </summary>
    public static EpochSourceBlock<T, TActor> CreateEpochSource<T, TActor>(
        string name,
        TActor actor,
        IEpochCoordinator coordinator)
        where TActor : class, ISourceActor<T>
    {
        var scopeFactory = TestServiceBuilder.Create()
            .WithScoped(actor)
            .BuildScopeFactory();
        return new EpochSourceBlock<T, TActor>(new BlockContext(name), scopeFactory, coordinator);
    }

    /// <summary>
    /// Creates an EpochActorBlock with a service scope factory.
    /// </summary>
    public static EpochActorBlock<TIn, TOut, TActor> CreateEpochActor<TIn, TOut, TActor>(
        string name,
        IServiceScopeFactory scopeFactory)
        where TActor : IStreamActor<TIn, TOut>
    {
        return new EpochActorBlock<TIn, TOut, TActor>(new BlockContext(name), scopeFactory);
    }

    /// <summary>
    /// Creates an EpochActorBlock with a single actor instance (automatically wraps in scope factory).
    /// </summary>
    public static EpochActorBlock<TIn, TOut, TActor> CreateEpochActor<TIn, TOut, TActor>(
        string name,
        TActor actor)
        where TActor : class, IStreamActor<TIn, TOut>
    {
        var scopeFactory = TestServiceBuilder.Create()
            .WithScoped(actor)
            .BuildScopeFactory();
        return new EpochActorBlock<TIn, TOut, TActor>(new BlockContext(name), scopeFactory);
    }

    /// <summary>
    /// Creates an EpochBatchBlock with max batch size.
    /// </summary>
    public static EpochBatchBlock<T> CreateEpochBatch<T>(
        string name,
        int maxBatchSize)
    {
        return new EpochBatchBlock<T>(new BlockContext(name), maxBatchSize, windowPeriod: null);
    }

    /// <summary>
    /// Creates an EpochBatchBlock with max batch size and time window.
    /// </summary>
    public static EpochBatchBlock<T> CreateEpochBatch<T>(
        string name,
        int maxBatchSize,
        TimeSpan windowPeriod)
    {
        return new EpochBatchBlock<T>(new BlockContext(name), maxBatchSize, windowPeriod);
    }

    #endregion

    #region Helper Methods

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
        }
    }

    #endregion
}
