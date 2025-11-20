// Prototype: BlockBase with Constructor Injection
// This demonstrates the recommended approach (Approach 4)

namespace DataFlow.POC.Core;

/// <summary>
/// PROTOTYPE: BlockBase with IBlockContext constructor injection.
/// This removes the SetContext method and two-phase initialization.
/// </summary>
public abstract class BlockBase_Prototype<TIn, TOut> : IBlock<TIn, TOut>
{
    private readonly IBlockContext _context;

    /// <summary>
    /// Constructor with IBlockContext - the only way to create a block.
    /// Context is required and immutable after construction.
    /// </summary>
    protected BlockBase_Prototype(IBlockContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>
    /// Gets the block name from the context.
    /// No longer nullable - always available.
    /// </summary>
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
