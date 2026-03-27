namespace DataFlow.Blazor.Server;

using DataFlow.Blazor.Server.Services;
using Microsoft.AspNetCore.SignalR;

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
/// Data access is delegated to <see cref="IFlowHubDataService"/> so that the hub itself
/// has no dependency on a specific DbContext type. This allows the hub to be activated
/// from the root application container even in environments where the DbContext lives in
/// a per-tenant or per-request child container.
///
/// To supply a custom data service (e.g. in a multi-tenant app):
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
    /// Called by the client after connecting. Replays any events missed between
    /// the HTTP response and the WebSocket handshake, then subscribes to live events.
    /// </summary>
    /// <param name="flowRunId">The flow run to subscribe to.</param>
    /// <param name="fromId">The Id of the last event the client has seen (from AsOfId in HTTP response).</param>
    public async Task Subscribe(Guid flowRunId, long fromId)
    {
        var missed = await _dataService.GetMissedEventsAsync(flowRunId, fromId);

        foreach (var dto in missed)
        {
            await Clients.Caller.SendAsync("EventAppended", dto);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, flowRunId.ToString());
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        // Groups membership is automatically cleaned up by SignalR on disconnect
        return base.OnDisconnectedAsync(exception);
    }
}
