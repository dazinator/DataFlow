namespace DataFlow.Blazor.Server.Endpoints;

using System.Text.Json;
using DataFlow.Blazor.Api;
using DataFlow.Blazor.Events;
using DataFlow.Blazor.Server.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Registers the DataFlow flow-list HTTP endpoint.
/// Called internally by MapDataFlowEndpoints().
/// </summary>
internal static class FlowListEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal static IEndpointRouteBuilder MapFlowListEndpoints<TContext>(this IEndpointRouteBuilder app)
        where TContext : DbContext
    {
        /// <summary>
        /// Returns a summary of all flow runs, ordered most-recent first.
        /// Derived from materialized snapshots — only flows that have emitted at least
        /// one event appear (FlowStartedEvent triggers a snapshot immediately).
        /// </summary>
        app.MapGet("/flows", async (
            TContext db,
            CancellationToken cancellationToken) =>
        {
            var snapshots = await db.Set<FlowSnapshotRecord>()
                .OrderByDescending(s => s.AsOfEventId)
                .ToListAsync(cancellationToken);

            var summaries = snapshots
                .Select(s =>
                {
                    var snapshot = JsonSerializer.Deserialize<FlowSnapshot>(s.SnapshotJson, JsonOptions);
                    if (snapshot is null) return null;

                    return new FlowSummaryDto(
                        FlowRunId: s.FlowRunId,
                        FlowName: snapshot.FlowName,
                        Status: snapshot.State,
                        StartedAt: snapshot.StartTime,
                        CompletedAt: snapshot.CompletedAt,
                        ErrorMessage: snapshot.ErrorMessage,
                        BlockCount: snapshot.Blocks.Count,
                        TriggerParamsJson: snapshot.TriggerParamsJson,
                        CorrelationId: snapshot.CorrelationId,
                        AttemptNumber: snapshot.AttemptNumber);
                })
                .Where(s => s is not null)
                .ToArray();

            return Results.Ok(summaries);
        })
        .WithName("GetFlows")
        .WithTags("DataFlow");

        return app;
    }
}
