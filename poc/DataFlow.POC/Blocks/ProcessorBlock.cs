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
