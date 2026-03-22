namespace DataFlow.Blazor.Events;

/// <summary>
/// Client-side abstraction for receiving DataFlow events.
/// The mock implementations (MockEventSource etc.) are used for local development.
/// For production, register HttpSignalREventSource which connects to the ASP.NET Core
/// server provided by Uniun.DataFlow.Blazor.Server.
/// </summary>
public interface IEventSource
{
    /// <summary>
    /// Gets a stream of events for the given invocation.
    /// For the HTTP+SignalR implementation, this first yields any delta events from
    /// the catch-up endpoint, then yields live events pushed via SignalR.
    /// </summary>
    IAsyncEnumerable<IDataFlowEvent> GetEventsAsync(Guid invocationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a pre-computed snapshot for fast initial render, or null if unavailable.
    /// For the HTTP+SignalR implementation this is derived from the catch-up endpoint response.
    /// </summary>
    Task<FlowSnapshot?> GetSnapshotAsync(Guid invocationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns structural events (FlowStarted/Completed, BlockStarted/Completed) that are
    /// already encoded in the snapshot — i.e. events that won't appear in GetEventsAsync
    /// because they occurred before the snapshot point.
    ///
    /// These should be applied to EventLog ONLY (not re-processed for state), so that
    /// the event history pane is populated for flows loaded from a completed snapshot.
    ///
    /// The default implementation returns an empty list, which is correct for mock sources
    /// that emit events sequentially from the start (EventLog is populated normally via
    /// ProcessEvent). Only HTTP+SignalR sources need to override this.
    /// </summary>
    Task<IReadOnlyList<IDataFlowEvent>> GetAuditLogAsync(Guid invocationId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<IDataFlowEvent>>([]);
}
