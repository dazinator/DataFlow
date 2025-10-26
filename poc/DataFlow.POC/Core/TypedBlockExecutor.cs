namespace DataFlow.POC.Core;

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

/// <summary>
/// Factory for creating executable block adapters that eliminate boxing at block output boundary.
/// Uses compiled delegates created once at build time to execute blocks with their native types.
/// Follows the pattern: reflection at build time, typed delegates at runtime.
/// </summary>
public static class ExecutableBlockFactory
{
    // Cache for compiled adapters: (InputType, OutputType) -> Func that creates adapter
    private static readonly ConcurrentDictionary<(Type, Type), Func<IBlock, IExecutableBlock>> _adapterFactoryCache = new();

    /// <summary>
    /// Creates an executable block adapter for the specified block.
    /// Uses reflection once at build time to create a strongly-typed adapter that avoids boxing.
    /// </summary>
    public static IExecutableBlock CreateAdapter(IBlock block)
    {
        var inputType = block.InputType;
        var outputType = block.OutputType;
        var key = (inputType, outputType);

        var factory = _adapterFactoryCache.GetOrAdd(key, _ =>
        {
            // Create a compiled delegate that instantiates ExecutableBlockAdapter<TIn, TOut>
            // This happens once per type combination at build time
            var adapterType = typeof(ExecutableBlockAdapter<,>).MakeGenericType(inputType, outputType);
            var constructor = adapterType.GetConstructor(new[] { typeof(IBlock) })
                ?? throw new InvalidOperationException($"Could not find constructor for {adapterType.Name}");

            var blockParam = Expression.Parameter(typeof(IBlock), "block");
            var newExpr = Expression.New(constructor, blockParam);
            var lambda = Expression.Lambda<Func<IBlock, IExecutableBlock>>(newExpr, blockParam);
            return lambda.Compile();
        });

        return factory(block);
    }
}

/// <summary>
/// Non-generic interface for executable blocks.
/// Returns object representing the entire typed stream, NOT IAsyncEnumerable<object>.
/// This avoids boxing individual items.
/// </summary>
public interface IExecutableBlock
{
    /// <summary>
    /// Execute the block with untyped orchestration.
    /// Input and output are objects representing typed streams (IAsyncEnumerable<T>).
    /// The cast happens at the control layer, not per item, avoiding boxing.
    /// </summary>
    Task<object> ExecuteUntypedAsync(object input, IExecutionContext context);
    
    /// <summary>
    /// The output type of this block's stream (e.g., if block outputs IAsyncEnumerable<int>, this is typeof(int)).
    /// </summary>
    Type OutputItemType { get; }
    
    /// <summary>
    /// The input type of this block's stream.
    /// </summary>
    Type InputItemType { get; }
}

/// <summary>
/// Generic executable block adapter that eliminates boxing by maintaining typed streams.
/// The adapter casts the entire stream object, not individual items.
/// </summary>
/// <typeparam name="TIn">The input item type</typeparam>
/// <typeparam name="TOut">The output item type</typeparam>
public sealed class ExecutableBlockAdapter<TIn, TOut> : IExecutableBlock
{
    private readonly IBlock<TIn, TOut> _typedBlock;

    public ExecutableBlockAdapter(IBlock block)
    {
        _typedBlock = (IBlock<TIn, TOut>)block;
    }

    public Type OutputItemType => typeof(TOut);
    public Type InputItemType => typeof(TIn);

    /// <summary>
    /// Execute with typed streams - no boxing of individual items.
    /// </summary>
    public Task<object> ExecuteUntypedAsync(object input, IExecutionContext context)
    {
        // Cast the entire stream object, not individual items - no boxing per item
        var typedInput = (IAsyncEnumerable<TIn>)input;
        
        // Execute block with fully typed input/output - NO BOXING
        var typedOutput = _typedBlock.ExecuteAsync(typedInput, context);
        
        // Return the typed stream as object - the cast happens at control layer, not per item
        return Task.FromResult((object)typedOutput);
    }
}
