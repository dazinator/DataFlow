namespace DataFlow.Blazor.Server;

using DataFlow.Blazor.Server.Services;
using Microsoft.AspNetCore.SignalR;

/// <summary>
/// SignalR hub for real-time DataFlow event streaming.
///
/// Client connection flow:
/// 1. Client loads snapshot + delta via HTTP GET /flows/{id}/state → receives AsOfId
/// 2. Client connects to this hub and calls Subscribe(flowRunId, asOfId)
/// 3. Hub adds client to the flow's SignalR group FIRST (closes the race window)
/// 4. Hub replays any events with Id > asOfId (gap fill; may overlap with live events)
/// 5. Hub sends "Subscribed" to signal gap-fill is complete
/// 6. New events are pushed via EfCoreFlowEventSink → IHubContext → group
/// The client buffers live events received during steps 3-5, then drains them in
/// sorted order on "Subscribed", deduplicating by Id against the gap-fill results.
///
/// Mapped automatically by MapDataFlowEndpoints() — no need to call MapHub separately.
///
/// Data access is delegated to <see cref="IFlowHubDataService"/> so that the hub itself
/// has no dependency on a specific DbContext type. This allows the hub to be activated
/// from the root application container even in environments where the DbContext lives in
/// a per-tenant or per-request child container.
///
/// To supply a custom data service (e.g. in an app that uses per-tenant DI containers):
/// <code>
/// builder.Services.AddScoped&lt;IFlowHubDataService, MyTenantAwareFlowHubDataService&gt;();
/// </code>
/// </summary>
public class FlowEventsHub : Hub
{
    private readonly IFlowHubDataService _dataService;

    public FlowEventsHub(IFlowHubDataService dataService)
    {
        _dataService = dataService;
    }

    /// <summary>
    /// Called by the client after connecting. Adds the client to the flow's SignalR
    /// group first, then replays any events missed between the HTTP snapshot and the
    /// WebSocket handshake, then sends "Subscribed" to signal gap-fill is complete.
    ///
    /// Order matters: joining the group before the gap-fill ensures no live events
    /// are lost during the DB query window. The "Subscribed" signal lets the client
    /// buffer live events received during the gap-fill and drain them in sorted order,
    /// so the projection always receives events in Id order.
    /// </summary>
    /// <param name="flowRunId">The flow run to subscribe to.</param>
    /// <param name="fromId">The Id of the last event the client has seen (from AsOfId in HTTP response).</param>
    public async Task Subscribe(Guid flowRunId, long fromId)
    {
        // Join the group FIRST so live events broadcast during the gap-fill DB query
        // are not lost. The client buffers them until "Subscribed" is sent.
        await Groups.AddToGroupAsync(Context.ConnectionId, flowRunId.ToString());

        var missed = await _dataService.GetMissedEventsAsync(flowRunId, fromId, Context.User);

        foreach (var dto in missed)
        {
            await Clients.Caller.SendAsync("EventAppended", dto);
        }

        // Signal gap-fill complete. The client will drain its live-event buffer
        // sorted by Id, deduplicating against the gap-fill events already received.
        await Clients.Caller.SendAsync("Subscribed");
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        // Groups membership is automatically cleaned up by SignalR on disconnect
        return base.OnDisconnectedAsync(exception);
    }
}
