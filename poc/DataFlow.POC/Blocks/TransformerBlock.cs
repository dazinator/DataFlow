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
