namespace DataFlow.POC.Core;

/// <summary>
/// Base implementation for typed blocks.
/// </summary>
public abstract class BlockBase<TIn, TOut> : IBlock<TIn, TOut>
{
    protected BlockBase(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public string Name { get; }

    public Type InputType => typeof(TIn);

    public Type OutputType => typeof(TOut);

    public abstract IAsyncEnumerable<TOut> ExecuteAsync(
        IAsyncEnumerable<TIn> input,
        IExecutionContext context);

    // Untyped interface implementation
    async IAsyncEnumerable<object> IBlock.ExecuteAsync(
        IAsyncEnumerable<object> input,
        IExecutionContext context)
    {
        var typedInput = CastAsyncEnumerable<TIn>(input);
        await foreach (var item in ExecuteAsync(typedInput, context))
        {
            // Null check for reference types - blocks should not yield null values
            if (item is null && !typeof(TOut).IsValueType)
            {
                throw new InvalidOperationException(
                    $"Block '{Name}' yielded a null value of type '{typeof(TOut)}'. Null values are not allowed in the data flow.");
            }
            yield return item!;
        }
    }

    private static async IAsyncEnumerable<T> CastAsyncEnumerable<T>(IAsyncEnumerable<object> source)
    {
        await foreach (var item in source)
        {
            yield return (T)item;
        }
    }
}
