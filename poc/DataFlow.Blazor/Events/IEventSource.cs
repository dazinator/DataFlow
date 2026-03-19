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
}
