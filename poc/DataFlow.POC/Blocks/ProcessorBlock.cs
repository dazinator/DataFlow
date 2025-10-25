namespace DataFlow.POC.Blocks;

using DataFlow.POC.Core;

/// <summary>
/// Processor block that consumes items and performs actions on them.
/// This is a terminal block with no output.
/// </summary>
public class ProcessorBlock<T> : BlockBase<T, object>
{
    private readonly Func<T, IExecutionContext, Task> _processor;

    public ProcessorBlock(string name, Func<T, IExecutionContext, Task> processor)
        : base(name)
    {
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
    }

    public override async IAsyncEnumerable<object> ExecuteAsync(
        IAsyncEnumerable<T> input,
        IExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            await _processor(item, context);
        }

        // Terminal blocks don't produce output
        yield break;
    }
}

/// <summary>
/// Concurrent processor that processes items in parallel.
/// </summary>
public class ConcurrentProcessorBlock<T> : BlockBase<T, object>
{
    private readonly Func<T, IExecutionContext, Task> _processor;
    private readonly int _maxConcurrency;

    public ConcurrentProcessorBlock(
        string name,
        Func<T, IExecutionContext, Task> processor,
        int maxConcurrency = 4)
        : base(name)
    {
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        _maxConcurrency = maxConcurrency;
    }

    public override async IAsyncEnumerable<object> ExecuteAsync(
        IAsyncEnumerable<T> input,
        IExecutionContext context)
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
                    await _processor(item, context);
                }
                finally
                {
                    semaphore.Release();
                }
            }, context.CancellationToken);

            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        // Terminal blocks don't produce output
        yield break;
    }
}
