namespace DataFlow.POC.Core;

/// <summary>
/// Base implementation for typed blocks.
/// </summary>
public abstract class BlockBase<TIn, TOut> : IBlock<TIn, TOut>
{
    private IBlockContext? _context;

    protected BlockBase(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        _context = new BlockContext(name);
    }

    /// <summary>
    /// Protected constructor for DI-friendly blocks where context is set after construction.
    /// </summary>
    protected BlockBase()
    {
        // Context will be set by registration infrastructure
    }

    /// <summary>
    /// Sets the block context. Should be called once after construction.
    /// </summary>
    internal void SetContext(IBlockContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_context is not null)
        {
            throw new InvalidOperationException("Block context has already been set");
        }
        _context = context;
    }

    public string Name => _context?.BlockName ?? string.Empty;

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
