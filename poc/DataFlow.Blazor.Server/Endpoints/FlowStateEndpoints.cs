namespace DataFlow.Blazor.Server.Endpoints;

using DataFlow.Blazor.Api;
using DataFlow.Blazor.Server.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Registers the DataFlow visualization catch-up HTTP endpoint.
/// Called internally by MapDataFlowEndpoints().
/// </summary>
internal static class FlowStateEndpoints
{
    internal static IEndpointRouteBuilder MapFlowStateEndpoints(this IEndpointRouteBuilder app)
    {
        /// <summary>
        /// Returns a materialized snapshot (if available) plus all delta events since that snapshot.
        /// The Blazor client calls this on init, then subscribes to SignalR from AsOfSequence onward.
        /// </summary>
        app.MapGet("/flows/{flowRunId:guid}/state", async (
            Guid flowRunId,
            FlowVisualizationDbContext db,
            CancellationToken cancellationToken) =>
        {
            var snapshot = await db.FlowSnapshotRecords
                .Where(s => s.FlowRunId == flowRunId)
                .FirstOrDefaultAsync(cancellationToken);

            var fromSeq = snapshot?.AsOfSequence ?? 0;

            var deltaRecords = await db.FlowEventRecords
                .Where(e => e.FlowRunId == flowRunId && e.SequenceNumber > fromSeq)
                .OrderBy(e => e.SequenceNumber)
                .ToListAsync(cancellationToken);

            var deltaEvents = deltaRecords
                .Select(r => new FlowEventDto(r.SequenceNumber, r.EventType, r.Payload, r.OccurredAt))
                .ToArray();

            var asOfSequence = deltaRecords.Count > 0
                ? deltaRecords[^1].SequenceNumber
                : fromSeq;

            return Results.Ok(new FlowStateResponse(
                SnapshotJson: snapshot?.SnapshotJson,
                DeltaEvents: deltaEvents,
                AsOfSequence: asOfSequence));
        })
        .WithName("GetFlowState")
        .WithTags("DataFlow");

        return app;
    }
}
