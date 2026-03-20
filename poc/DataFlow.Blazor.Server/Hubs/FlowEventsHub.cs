namespace DataFlow.Blazor.Server;

using DataFlow.Blazor.Api;
using DataFlow.Blazor.Server.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// SignalR hub for real-time DataFlow event streaming.
///
/// Client connection flow:
/// 1. Client loads snapshot + delta via HTTP GET /flows/{id}/state → receives AsOfId
/// 2. Client connects to this hub and calls Subscribe(flowRunId, asOfId)
/// 3. Hub replays any events with Id > asOfId (race-condition safe gap fill)
/// 4. Hub adds client to the flow's SignalR group
/// 5. New events are pushed via EfCoreFlowEventSink → IHubContext → group
///
/// Mapped automatically by MapDataFlowEndpoints() — no need to call MapHub separately.
///
/// <typeparam name="TContext">
/// The DbContext type, matching the one registered via AddDataFlowVisualizationServer.
/// </typeparam>
/// </summary>
public class FlowEventsHub<TContext> : Hub where TContext : DbContext
{
    private readonly TContext _db;

    public FlowEventsHub(TContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Called by the client after connecting. Replays any events missed between
    /// the HTTP response and the WebSocket handshake, then subscribes to live events.
    /// </summary>
    /// <param name="flowRunId">The flow run to subscribe to.</param>
    /// <param name="fromId">The Id of the last event the client has seen (from AsOfId in HTTP response).</param>
    public async Task Subscribe(Guid flowRunId, long fromId)
    {
        // Replay any events that arrived between HTTP call and WebSocket connection
        var missed = await _db.Set<FlowEventRecord>()
            .Where(e => e.FlowRunId == flowRunId && e.Id > fromId)
            .OrderBy(e => e.Id)
            .ToListAsync();

        foreach (var record in missed)
        {
            var dto = new FlowEventDto(record.Id, record.EventType, record.Payload, record.OccurredAt);
            await Clients.Caller.SendAsync("EventAppended", dto);
        }

        // Join the group for future live events
        await Groups.AddToGroupAsync(Context.ConnectionId, flowRunId.ToString());
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Groups membership is automatically cleaned up by SignalR on disconnect
        await base.OnDisconnectedAsync(exception);
    }
}
