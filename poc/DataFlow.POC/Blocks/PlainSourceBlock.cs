namespace DataFlow.POC.Blocks;

using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Source block that hosts a plain source actor producing continuous data streams
/// without epoch knowledge. For epoch-based processing, connect this block's output
/// to an EpochSegmenterBlock.
/// </summary>
/// <typeparam name="T">The type of items produced</typeparam>
/// <typeparam name="TActor">The plain source actor type</typeparam>
/// <remarks>
/// <para>
/// <strong>⚠️ DEPRECATED:</strong> This plain source block is deprecated in favor of the unified epoch-based architecture.
/// Use <c>PlainSourceAdapter&lt;T, TActor&gt;</c> instead, which automatically wraps plain sources in single-epoch streams.
/// </para>
/// <para>
/// <strong>Migration Guide:</strong>
/// </para>
/// <list type="bullet">
/// <item><strong>Recommended:</strong> Use <c>PlainSourceAdapter&lt;T, TActor&gt;</c> - drop-in replacement with automatic epoch wrapping</item>
/// <item>Alternative: Make your source epoch-aware by implementing <c>ISourceActor&lt;T&gt;</c> and use <c>EpochSourceBlock&lt;T, TActor&gt;</c></item>
/// <item>For automatic wrapping in graph builder: Use <c>builder.AddPlainSource&lt;T, TActor&gt;(name)</c></item>
/// </list>
/// <para>
/// <strong>Performance:</strong> PlainSourceAdapter adds &lt;5% overhead (research validated: 4.08%).
/// </para>
/// <para>
/// <strong>Removal Timeline:</strong> This class will be removed in v3.0 (Q1 2027).
/// </para>
/// </remarks>
[Obsolete("Use PlainSourceAdapter<T, TActor> for automatic epoch wrapping, or convert to ISourceActor<T> and use EpochSourceBlock. See XML docs for migration guide.", false)]
public sealed class PlainSourceBlock<T, TActor> : BlockBase<object, T>
    where TActor : IPlainSourceActor<T>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ActorExecutionContext _context = new();

    /// <summary>
    /// Constructor with IBlockContext for proper dependency injection.
    /// All dependencies are passed via constructor.
    /// </summary>
    public PlainSourceBlock(IBlockContext context, IServiceScopeFactory scopeFactory)
        : base(context)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    public override async IAsyncEnumerable<T> ExecuteAsync(
        IAsyncEnumerable<object> input,
        IExecutionContext context)
    {
        // Source blocks ignore input - they generate data
        await using var scope = _scopeFactory.CreateAsyncScope();
        
        InitializeActorContext(context);
        var actor = scope.ServiceProvider.GetRequiredService<TActor>();
        
        // Stream plain items from the actor
        await foreach (var item in actor.ProduceAsync(_context).WithCancellation(context.CancellationToken))
        {
            yield return item;
        }
    }

    private void InitializeActorContext(IExecutionContext context)
    {
        _context.Reset(
            context.CancellationToken,
            context.InvocationId,
            () => { }); // Source actors don't rotate
    }
}
