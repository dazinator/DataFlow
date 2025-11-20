namespace DataFlow.POC.Blocks;

using DataFlow.POC.Core;

/// <summary>
/// Producer block that generates items from a source.
/// This is a source block with no input.
/// </summary>
/// <remarks>
/// <para>
/// <strong>⚠️ DEPRECATED:</strong> This plain producer is deprecated in favor of the unified epoch-based architecture.
/// All sources now produce epoch streams by default. Plain producers should be wrapped in single-epoch streams.
/// </para>
/// <para>
/// <strong>Migration Guide:</strong>
/// </para>
/// <list type="bullet">
/// <item>For simple producers: Wrap output with <c>.WrapInSingleEpoch(sourceName)</c></item>
/// <item>For actor-based sources: Use <c>PlainSourceAdapter&lt;T, TActor&gt;</c></item>
/// <item>For epoch-aware sources: Use <c>EpochSourceBlock&lt;T, TActor&gt;</c> directly</item>
/// <item>For automatic wrapping in graph builder: Use <c>builder.AddPlainSource&lt;T, TActor&gt;(name)</c></item>
/// </list>
/// <para>
/// <strong>Performance:</strong> Single-epoch wrapping has &lt;5% overhead (research validated: 4.08%).
/// </para>
/// <para>
/// <strong>Removal Timeline:</strong> This class will be removed in v3.0 (Q1 2027).
/// </para>
/// </remarks>
[Obsolete("Use EpochSourceBlock for epoch-aware sources or wrap plain streams with WrapInSingleEpoch extension method. See XML docs for migration guide.", false)]
public class ProducerBlock<T> : BlockBase<object, T>
{
    private readonly Func<IExecutionContext, IAsyncEnumerable<T>> _producer;

    /// <summary>
    /// Constructor with IBlockContext for proper dependency injection.
    /// All dependencies are passed via constructor.
    /// </summary>
    public ProducerBlock(IBlockContext context, Func<IExecutionContext, IAsyncEnumerable<T>> producer)
        : base(context)
    {
        _producer = producer ?? throw new ArgumentNullException(nameof(producer));
    }

    public override async IAsyncEnumerable<T> ExecuteAsync(
        IAsyncEnumerable<object> input,
        IExecutionContext context)
    {
        // Source blocks ignore input
        await foreach (var item in _producer(context).WithCancellation(context.CancellationToken))
        {
            yield return item;
        }
    }
}

/// <summary>
/// Producer block that generates items from multiple concurrent producers.
/// </summary>
public class ConcurrentProducerBlock<T> : BlockBase<object, T>
{
    private readonly Func<IExecutionContext, IEnumerable<IAsyncEnumerable<T>>> _producersFactory;
    private readonly int _maxConcurrency;

    /// <summary>
    /// Constructor with IBlockContext for proper dependency injection.
    /// All dependencies are passed via constructor.
    /// </summary>
    public ConcurrentProducerBlock(
        IBlockContext context,
        Func<IExecutionContext, IEnumerable<IAsyncEnumerable<T>>> producersFactory,
        int maxConcurrency = 4)
        : base(context)
    {
        _producersFactory = producersFactory ?? throw new ArgumentNullException(nameof(producersFactory));
        _maxConcurrency = maxConcurrency;
    }

    public override async IAsyncEnumerable<T> ExecuteAsync(
        IAsyncEnumerable<object> input,
        IExecutionContext context)
    {
        var producers = _producersFactory(context).ToList();
        
        // Use bounded channel to be consistent with POC design principles
        // Capacity scales with producer count to avoid blocking while maintaining backpressure
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
                // Let exceptions propagate through the channel
                throw;
            }
        }, context.CancellationToken)).ToList();

        // Start a task to complete the channel when all producers are done
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
