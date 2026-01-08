namespace DataFlow.POC.Core;

/// <summary>
/// Defines a source actor that produces epoch streams with full control over epoch transitions.
/// Source actors can signal new epochs without restarting or reopening upstream queries.
/// All source actors now use IEpochCoordinator for epoch management.
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
/// Base class for coordinator-aware source actors that provides helper methods for epoch management.
/// All source actors now require IEpochCoordinator for epoch creation.
/// </summary>
public abstract class SourceActorBase<T> : ISourceActor<T>
{
    private readonly IEpochCoordinator _coordinator;
    private readonly string _sourceId;

    /// <summary>
    /// Creates a new source actor with epoch coordination.
    /// </summary>
    /// <param name="coordinator">Epoch coordinator for managing epochs</param>
    /// <param name="sourceId">Unique identifier for this source</param>
    protected SourceActorBase(IEpochCoordinator coordinator, string sourceId)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _sourceId = sourceId ?? throw new ArgumentNullException(nameof(sourceId));
    }

    /// <summary>
    /// Produces epoch streams. Subclasses override this to implement their data production logic.
    /// </summary>
    public abstract IAsyncEnumerable<IEpochStream<T>> ProduceEpochsAsync(IActorExecutionContext context);

    /// <summary>
    /// Requests an epoch from the coordinator and creates an epoch stream.
    /// </summary>
    /// <param name="sequence">Sequence number for this source</param>
    /// <param name="items">Data items for this epoch</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Coordinator-managed epoch stream</returns>
    protected async ValueTask<IEpochStream<T>> CreateEpochStreamAsync(
        long sequence,
        IAsyncEnumerable<T> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        var vector = EpochVector.FromSingleSource(_sourceId, sequence);
        var epoch = await _coordinator.GetOrCreateEpochAsync(_sourceId, vector, cancellationToken);
        
        return new EpochStream<T>(epoch, items);
    }

    /// <summary>
    /// Signals readiness to advance to the next epoch.
    /// Call this before creating the next epoch stream.
    /// </summary>
    /// <param name="currentSequence">Current sequence number</param>
    /// <param name="nextSequence">Next sequence number</param>
    protected void SignalReadyForNext(long currentSequence, long nextSequence)
    {
        var currentVector = EpochVector.FromSingleSource(_sourceId, currentSequence);
        var nextVector = EpochVector.FromSingleSource(_sourceId, nextSequence);
        _coordinator.SignalReadyForNext(_sourceId, currentVector, nextVector);
    }
}
