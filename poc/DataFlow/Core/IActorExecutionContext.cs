namespace DataFlow.POC.Core;

/// <summary>
/// Execution context for actors that support scope rotation.
/// Provides cancellation and rotation request capabilities.
/// </summary>
public interface IActorExecutionContext
{
    /// <summary>
    /// Cancellation token for the actor execution.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Unique identifier for this execution context.
    /// </summary>
    Guid InvocationId { get; }

    /// <summary>
    /// Request rotation of the actor's DI scope.
    /// The actor will be disposed and a new instance will be created in a new scope.
    /// </summary>
    void RequestRotation();
}
