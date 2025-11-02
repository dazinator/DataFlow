namespace DataFlow.POC.Core;

/// <summary>
/// Defines a source actor that produces epoch streams with full control over epoch transitions.
/// Source actors can signal new epochs without restarting or reopening upstream queries.
/// </summary>
/// <typeparam name="T">The type of items produced</typeparam>
public interface ISourceActor<T>
{
    /// <summary>
    /// Produces a stream of epoch streams. Each epoch stream represents a logical batch
    /// or checkpoint boundary. The actor controls when to start a new epoch by yielding
    /// a new IEpochStream instance.
    /// </summary>
    /// <param name="context">Execution context with cancellation support</param>
    /// <returns>A stream of epoch streams</returns>
    IAsyncEnumerable<IEpochStream<T>> ProduceEpochsAsync(IActorExecutionContext context);
}

/// <summary>
/// Base class for source actors that provides helper methods for common patterns.
/// </summary>
public abstract class SourceActorBase<T> : ISourceActor<T>
{
    /// <summary>
    /// Produces epoch streams. Subclasses override this to implement their data production logic.
    /// </summary>
    public abstract IAsyncEnumerable<IEpochStream<T>> ProduceEpochsAsync(IActorExecutionContext context);

    /// <summary>
    /// Helper to create an epoch stream from an async enumerable of items.
    /// </summary>
    protected static IEpochStream<T> CreateEpochStream(EpochVector epoch, IAsyncEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(epoch);
        ArgumentNullException.ThrowIfNull(items);
        return new EpochStream<T>(epoch, items);
    }

    /// <summary>
    /// Helper to create an epoch vector for a single source.
    /// </summary>
    protected static EpochVector CreateEpoch(string sourceId, long sequence)
    {
        return EpochVector.FromSingleSource(sourceId, sequence);
    }
}
