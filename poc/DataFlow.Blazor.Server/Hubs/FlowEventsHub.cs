namespace DataFlow.Blazor.Server;

using DataFlow.Blazor.Api;
using DataFlow.Blazor.Server.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// SignalR hub for real-time DataFlow event streaming.
///
/// Client connection flow:
/// 1. Client loads snapshot + delta via HTTP GET /flows/{id}/state → receives AsOfSequence
/// 2. Client connects to this hub and calls Subscribe(flowRunId, asOfSequence)
/// 3. Hub replays any events with SequenceNumber > asOfSequence (race-condition safe gap fill)
/// 4. Hub adds client to the flow's SignalR group
/// 5. New events are pushed via EfCoreFlowEventSink → IHubContext → group
///
/// Register in Program.cs:
/// <code>
/// app.MapHub&lt;FlowEventsHub&gt;("/hubs/flow-events");
/// </code>
/// </summary>
public class FlowEventsHub : Hub
{
    private readonly FlowVisualizationDbContext _db;

    public FlowEventsHub(FlowVisualizationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Called by the client after connecting. Replays any events missed between
    /// the HTTP response and the WebSocket handshake, then subscribes to live events.
    /// </summary>
    /// <param name="flowRunId">The flow run to subscribe to.</param>
    /// <param name="fromSequence">The last sequence number the client has seen (from AsOfSequence in HTTP response).</param>
    public async Task Subscribe(Guid flowRunId, long fromSequence)
    {
        // Replay any events that arrived between HTTP call and WebSocket connection
        var missed = await _db.FlowEventRecords
            .Where(e => e.FlowRunId == flowRunId && e.SequenceNumber > fromSequence)
            .OrderBy(e => e.SequenceNumber)
            .ToListAsync();

        foreach (var record in missed)
        {
            var dto = new FlowEventDto(record.SequenceNumber, record.EventType, record.Payload, record.OccurredAt);
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
