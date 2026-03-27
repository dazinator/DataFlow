namespace DataFlow.Blazor.Server.Services;

using System.Security.Claims;
using DataFlow.Blazor.Api;
using DataFlow.Blazor.Server.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Default <see cref="IFlowHubDataService"/> implementation backed by EF Core.
/// Registered automatically by AddDataFlowVisualizationServer{TContext}.
///
/// In environments where <typeparamref name="TContext"/> is not resolvable from the
/// root DI container (e.g. multi-tenant apps that swap the container per request),
/// replace this by registering a custom <see cref="IFlowHubDataService"/>:
/// <code>
/// builder.Services.AddScoped&lt;IFlowHubDataService, MyTenantAwareFlowHubDataService&gt;();
/// </code>
/// </summary>
/// <typeparam name="TContext">The DbContext that owns the DataFlow visualization tables.</typeparam>
public class EfCoreFlowHubDataService<TContext> : IFlowHubDataService where TContext : DbContext
{
    private readonly TContext _db;

    public EfCoreFlowHubDataService(TContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<FlowEventDto>> GetMissedEventsAsync(
        Guid flowRunId,
        long fromId,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken = default)
    {
        var records = await _db.Set<FlowEventRecord>()
            .Where(e => e.FlowRunId == flowRunId && e.Id > fromId)
            .OrderBy(e => e.Id)
            .ToListAsync(cancellationToken);

        return records
            .Select(r => new FlowEventDto(r.Id, r.EventType, r.Payload, r.OccurredAt))
            .ToList();
    }
}
