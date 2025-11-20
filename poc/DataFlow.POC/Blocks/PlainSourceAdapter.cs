namespace DataFlow.POC.Blocks;

using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Source block adapter that wraps plain sources in single epochs.
/// This enables legacy plain sources to work in epoch-only architecture.
/// </summary>
/// <typeparam name="T">The type of items produced</typeparam>
/// <typeparam name="TActor">The plain source actor type</typeparam>
/// <remarks>
/// Design Pattern: Adapter
/// 
/// This block bridges the gap between:
/// - Legacy: Plain sources producing IAsyncEnumerable&lt;T&gt;
/// - New: Epoch-based architecture expecting IAsyncEnumerable&lt;IEpochStream&lt;T&gt;&gt;
/// 
/// Usage:
/// ```csharp
/// // Legacy plain source
/// public class MyLegacySource : IPlainSourceActor&lt;int&gt;
/// {
///     public async IAsyncEnumerable&lt;int&gt; ProduceAsync(IActorExecutionContext context)
///     {
///         for (int i = 0; i &lt; 100; i++)
///             yield return i;
///     }
/// }
/// 
/// // Automatically wrapped in single epoch
/// var sourceBlock = new PlainSourceAdapter&lt;int, MyLegacySource&gt;(
///     blockContext,
///     scopeFactory,
///     sourceName: "legacy-source");
/// 
/// // Output: IAsyncEnumerable&lt;IEpochStream&lt;int&gt;&gt; with one epoch containing 100 items
/// ```
/// 
/// Future: This adapter can be deprecated once all sources are epoch-aware.
/// </remarks>
/// <remarks>
/// <para>
/// <strong>Thread Safety:</strong> This block follows the same pattern as PlainSourceBlock and EpochSourceBlock.
/// The shared _context field is reset at the start of ExecuteAsync before use. Source blocks are designed to be
/// executed once per graph execution, not concurrently. If concurrent execution is needed, create separate
/// block instances.
/// </para>
/// </remarks>
public sealed class PlainSourceAdapter<T, TActor> : BlockBase<object, IEpochStream<T>>
    where TActor : IPlainSourceActor<T>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ActorExecutionContext _context = new();
    private readonly string _sourceName;

    /// <summary>
    /// Constructor with IBlockContext for proper dependency injection.
    /// </summary>
    /// <param name="context">Block context</param>
    /// <param name="scopeFactory">Service scope factory for DI</param>
    /// <param name="sourceName">Name of the source (used in epoch vector)</param>
    public PlainSourceAdapter(
        IBlockContext context, 
        IServiceScopeFactory scopeFactory,
        string sourceName)
        : base(context)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _sourceName = sourceName ?? throw new ArgumentNullException(nameof(sourceName));
    }

    public override async IAsyncEnumerable<IEpochStream<T>> ExecuteAsync(
        IAsyncEnumerable<object> input,
        IExecutionContext context)
    {
        // Source blocks ignore input - they generate data
        await using var scope = _scopeFactory.CreateAsyncScope();
        
        InitializeActorContext(context);
        var actor = scope.ServiceProvider.GetRequiredService<TActor>();
        
        // Get plain stream from actor
        var plainStream = actor.ProduceAsync(_context);
        
        // Wrap in single epoch and yield
        await foreach (var epochStream in plainStream.WrapInSingleEpoch(_sourceName, context.CancellationToken))
        {
            yield return epochStream;
        }
    }

    private void InitializeActorContext(IExecutionContext context)
    {
        _context.Reset(
            context.CancellationToken,
            context.InvocationId,
            () => { }); // Source actors don't rotate, provide no-op action
    }
}
