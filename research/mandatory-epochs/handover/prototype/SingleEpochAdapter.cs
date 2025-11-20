namespace DataFlow.POC.Core;

using System.Runtime.CompilerServices;

/// <summary>
/// Helper extensions for converting plain streams to epoch streams.
/// Enables treating plain sources as single-epoch sequences.
/// </summary>
public static class SingleEpochExtensions
{
    /// <summary>
    /// Wraps a plain stream in a single epoch.
    /// This allows plain sources to be used in epoch-aware pipelines without modification.
    /// </summary>
    /// <typeparam name="T">The type of items in the stream</typeparam>
    /// <param name="source">The plain stream to wrap</param>
    /// <param name="sourceName">The name of the source (used in epoch vector)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An epoch stream containing all items from the source in a single epoch</returns>
    /// <remarks>
    /// This is the key pattern that enables mandatory epochs:
    /// - Plain sources produce IAsyncEnumerable&lt;T&gt;
    /// - This method wraps them as IAsyncEnumerable&lt;IEpochStream&lt;T&gt;&gt;
    /// - The epoch stream contains all items in a single epoch sequence
    /// - Semantically correct: a stream with no breaks IS one long epoch
    /// 
    /// Performance: Near-zero overhead (just metadata wrapping)
    /// </remarks>
    public static async IAsyncEnumerable<IEpochStream<T>> WrapInSingleEpoch<T>(
        this IAsyncEnumerable<T> source,
        string sourceName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sourceName);

        // Create epoch vector with single sequence from this source
        var epochVector = EpochVector.FromSingleSource(sourceName, 1);
        
        // Yield a single epoch stream containing all items
        yield return new EpochStream<T>(epochVector, source);
    }

    /// <summary>
    /// Wraps a plain stream in a single epoch with coordinator support.
    /// This variant creates an IEpoch instance with DI scope support.
    /// </summary>
    /// <typeparam name="T">The type of items in the stream</typeparam>
    /// <param name="source">The plain stream to wrap</param>
    /// <param name="epoch">The epoch object with DI scope</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An epoch stream containing all items from the source</returns>
    public static async IAsyncEnumerable<IEpochStream<T>> WrapInEpoch<T>(
        this IAsyncEnumerable<T> source,
        IEpoch epoch,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(epoch);

        yield return new EpochStream<T>(epoch, source);
    }
}

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
