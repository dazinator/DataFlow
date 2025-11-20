namespace DataFlow.POC.Core;

/// <summary>
/// Base implementation for typed blocks.
/// </summary>
public abstract class BlockBase<TIn, TOut> : IBlock<TIn, TOut>
{
    private readonly IBlockContext _context;

    /// <summary>
    /// Legacy constructor for inline graph building.
    /// Prefer using constructor with IBlockContext via DI registration.
    /// </summary>
    [Obsolete("Use the constructor with IBlockContext parameter via services.AddDataFlows(). This constructor will be removed in a future version.")]
    protected BlockBase(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        _context = new BlockContext(name);
    }

    /// <summary>
    /// Constructor with IBlockContext for proper dependency injection.
    /// Context is required and immutable after construction.
    /// </summary>
    protected BlockBase(IBlockContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public string Name => _context.BlockName;

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
