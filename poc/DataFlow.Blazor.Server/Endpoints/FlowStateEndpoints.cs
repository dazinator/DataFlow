namespace DataFlow.Blazor.Server.Endpoints;

using DataFlow.Blazor.Api;
using DataFlow.Blazor.Server.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Registers the DataFlow visualization catch-up HTTP endpoint.
/// Called internally by MapDataFlowEndpoints().
/// </summary>
internal static class FlowStateEndpoints
{
    // Only these types belong in the audit log — telemetry (Progress/ChannelStats) is excluded.
    private static readonly HashSet<string> StructuralEventTypes =
    [
        "FlowStartedEvent",
        "FlowCompletedEvent",
        "BlockStartedEvent",
        "BlockCompletedEvent"
    ];

    internal static IEndpointRouteBuilder MapFlowStateEndpoints<TContext>(this IEndpointRouteBuilder app)
        where TContext : DbContext
    {
        /// <summary>
        /// Returns a materialized snapshot (if available) plus all delta events since that snapshot.
        /// The Blazor client calls this on init, then subscribes to SignalR from AsOfId onward.
        /// </summary>
        app.MapGet("/flows/{flowRunId:guid}/state", async (
            Guid flowRunId,
            [FromServices] TContext db,
            CancellationToken cancellationToken) =>
        {
            var snapshot = await db.Set<FlowSnapshotRecord>()
                .Where(s => s.FlowRunId == flowRunId)
                .FirstOrDefaultAsync(cancellationToken);

            var fromId = snapshot?.AsOfEventId ?? 0;

            var deltaRecords = await db.Set<FlowEventRecord>()
                .Where(e => e.FlowRunId == flowRunId && e.Id > fromId)
                .OrderBy(e => e.Id)
                .ToListAsync(cancellationToken);

            var deltaEvents = deltaRecords
                .Select(r => new FlowEventDto(r.Id, r.EventType, r.Payload, r.OccurredAt))
                .ToArray();

            var asOfId = deltaRecords.Count > 0
                ? deltaRecords[^1].Id
                : fromId;

            // Structural events already folded into the snapshot (Id <= asOfId).
            // Sent separately so the client can populate EventLog without re-applying
            // state changes (replaying BlockStartedEvent would reset blocks to Running).
            var auditRecords = fromId > 0
                ? await db.Set<FlowEventRecord>()
                    .Where(e => e.FlowRunId == flowRunId
                             && e.Id <= fromId
                             && StructuralEventTypes.Contains(e.EventType))
                    .OrderBy(e => e.Id)
                    .ToListAsync(cancellationToken)
                : [];

            var auditEvents = auditRecords
                .Select(r => new FlowEventDto(r.Id, r.EventType, r.Payload, r.OccurredAt))
                .ToArray();

            return Results.Ok(new FlowStateResponse(
                SnapshotJson: snapshot?.SnapshotJson,
                DeltaEvents: deltaEvents,
                AsOfId: asOfId,
                AuditEvents: auditEvents));
        })
        .WithName("GetFlowState")
        .WithTags("DataFlow");

        return app;
    }
}
