namespace DataFlow.POC.Blocks;

using DataFlow.POC.Core;

/// <summary>
/// Transformer block that transforms items from TIn to TOut.
/// Can be 1-to-1, 1-to-many, or filtering (1-to-0).
/// </summary>
public class TransformerBlock<TIn, TOut> : BlockBase<TIn, TOut>
{
    private readonly Func<TIn, IExecutionContext, IAsyncEnumerable<TOut>> _transformer;

    public TransformerBlock(
        string name,
        Func<TIn, IExecutionContext, IAsyncEnumerable<TOut>> transformer)
        : base(name)
    {
        _transformer = transformer ?? throw new ArgumentNullException(nameof(transformer));
    }

    public override async IAsyncEnumerable<TOut> ExecuteAsync(
        IAsyncEnumerable<TIn> input,
        IExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            await foreach (var result in _transformer(item, context).WithCancellation(context.CancellationToken))
            {
                yield return result;
            }
        }
    }
}

/// <summary>
/// Simple 1-to-1 transformer using a synchronous function.
/// </summary>
public class SimpleTransformerBlock<TIn, TOut> : BlockBase<TIn, TOut>
{
    private readonly Func<TIn, TOut> _transformer;

    public SimpleTransformerBlock(string name, Func<TIn, TOut> transformer)
        : base(name)
    {
        _transformer = transformer ?? throw new ArgumentNullException(nameof(transformer));
    }

    public override async IAsyncEnumerable<TOut> ExecuteAsync(
        IAsyncEnumerable<TIn> input,
        IExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            yield return _transformer(item);
        }
    }
}

/// <summary>
/// Concurrent transformer that processes items in parallel.
/// Note: This may not preserve order.
/// </summary>
public class ConcurrentTransformerBlock<TIn, TOut> : BlockBase<TIn, TOut>
{
    private readonly Func<TIn, IExecutionContext, Task<TOut>> _transformer;
    private readonly int _maxConcurrency;

    public ConcurrentTransformerBlock(
        string name,
        Func<TIn, IExecutionContext, Task<TOut>> transformer,
        int maxConcurrency = 4)
        : base(name)
    {
        _transformer = transformer ?? throw new ArgumentNullException(nameof(transformer));
        _maxConcurrency = maxConcurrency;
    }

    public override async IAsyncEnumerable<TOut> ExecuteAsync(
        IAsyncEnumerable<TIn> input,
        IExecutionContext context)
    {
        var channel = System.Threading.Channels.Channel.CreateBounded<TOut>(
            new System.Threading.Channels.BoundedChannelOptions(_maxConcurrency * 2)
            {
                FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait
            });

        // Producer task
        var producerTask = Task.Run(async () =>
        {
            try
            {
                var semaphore = new SemaphoreSlim(_maxConcurrency);
                var tasks = new List<Task>();

                await foreach (var item in input.WithCancellation(context.CancellationToken))
                {
                    await semaphore.WaitAsync(context.CancellationToken);

                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            var result = await _transformer(item, context);
                            await channel.Writer.WriteAsync(result, context.CancellationToken);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }, context.CancellationToken);

                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);
                channel.Writer.Complete();
            }
            catch (Exception ex)
            {
                channel.Writer.Complete(ex);
            }
        }, context.CancellationToken);

        // Yield items from channel
        await foreach (var item in channel.Reader.ReadAllAsync(context.CancellationToken))
        {
            yield return item;
        }

        await producerTask; // Ensure producer completes
    }
}
