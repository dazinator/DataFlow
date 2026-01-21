namespace DataFlow.POC.Core;

/// <summary>
/// Reusable implementation of IActorExecutionContext.
/// This context is reset internally by the ActorBlock between rotations to avoid allocations.
/// </summary>
internal sealed class ActorExecutionContext : IActorExecutionContext
{
    private CancellationToken _cancellationToken;
    private Guid _invocationId;
    private Action? _requestRotation;
    private IEpochCoordinator? _epochCoordinator;

    public CancellationToken CancellationToken => _cancellationToken;
    public Guid InvocationId => _invocationId;
    public IEpochCoordinator? EpochCoordinator => _epochCoordinator;

    public void RequestRotation() => _requestRotation?.Invoke();

    /// <summary>
    /// Reset the context state for reuse with a new actor instance.
    /// This is called internally by ActorBlock and is not part of the public interface.
    /// </summary>
    internal void Reset(
        CancellationToken cancellationToken,
        Guid invocationId,
        Action requestRotation,
        IEpochCoordinator? epochCoordinator = null)
    {
        _cancellationToken = cancellationToken;
        _invocationId = invocationId;
        _requestRotation = requestRotation;
        _epochCoordinator = epochCoordinator;
    }
}
