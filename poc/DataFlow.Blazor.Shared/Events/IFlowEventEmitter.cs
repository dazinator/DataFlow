namespace DataFlow.Blazor.Events;

/// <summary>
/// Emits DataFlow events for a specific flow run without the caller needing to
/// know the InvocationId. Pre-bound at construction time by the graph infrastructure.
/// Blocks and actors should use this rather than IFlowEventSink directly.
/// </summary>
public interface IFlowEventEmitter
{
    /// <summary>
    /// Appends an event to the flow's event log.
    /// </summary>
    Task EmitAsync(IDataFlowEvent evt, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation that delegates to IFlowEventSink with a fixed InvocationId.
/// </summary>
public sealed class BoundFlowEventEmitter : IFlowEventEmitter
{
    private readonly IFlowEventSink _sink;
    private readonly Guid _invocationId;

    public BoundFlowEventEmitter(IFlowEventSink sink, Guid invocationId)
    {
        _sink = sink;
        _invocationId = invocationId;
    }

    public Task EmitAsync(IDataFlowEvent evt, CancellationToken cancellationToken = default)
        => _sink.AppendAsync(_invocationId, evt, cancellationToken);
}
