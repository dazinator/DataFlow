namespace DataFlow.Blazor.Server.Services;

using System.Text.Json;
using DataFlow.Blazor.Events;
using DataFlow.Blazor.Projection;
using DataFlow.Blazor.Server.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using DataFlow.Blazor.Api;

/// <summary>
/// EF Core implementation of IFlowEventSink.
/// Appends events to FlowEventRecords, materializes snapshots per SnapshotPolicy,
/// and pushes real-time notifications via SignalR to connected Blazor clients.
/// </summary>
public class EfCoreFlowEventSink : IFlowEventSink
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly FlowVisualizationDbContext _db;
    private readonly IHubContext<FlowEventsHub> _hub;
    private readonly SnapshotPolicy _snapshotPolicy;

    public EfCoreFlowEventSink(
        FlowVisualizationDbContext db,
        IHubContext<FlowEventsHub> hub,
        SnapshotPolicy snapshotPolicy)
    {
        _db = db;
        _hub = hub;
        _snapshotPolicy = snapshotPolicy;
    }

    public async Task AppendAsync(Guid flowRunId, IDataFlowEvent evt, CancellationToken cancellationToken = default)
    {
        var record = new FlowEventRecord
        {
            FlowRunId = flowRunId,
            EventType = evt.GetType().Name,
            // Serialize using the concrete runtime type so all properties are included
            Payload = JsonSerializer.Serialize(evt, evt.GetType(), JsonOptions),
            OccurredAt = DateTimeOffset.UtcNow,
            // Denormalize CorrelationId onto the record for efficient cross-attempt queries
            CorrelationId = (evt as FlowStartedEvent)?.CorrelationId
        };

        _db.FlowEventRecords.Add(record);
        await _db.SaveChangesAsync(cancellationToken);

        // Push to SignalR group immediately (record.Id is now DB-assigned)
        var dto = new FlowEventDto(record.Id, record.EventType, record.Payload, record.OccurredAt);
        await _hub.Clients
            .Group(flowRunId.ToString())
            .SendAsync("EventAppended", dto, cancellationToken);

        // Optionally materialize a snapshot
        if (_snapshotPolicy.ShouldSnapshot(evt, record.Id))
        {
            await MaterializeSnapshotAsync(flowRunId, record.Id, cancellationToken);
        }
    }

    private async Task MaterializeSnapshotAsync(Guid flowRunId, long upToEventId, CancellationToken cancellationToken)
    {
        var allEvents = await _db.FlowEventRecords
            .Where(e => e.FlowRunId == flowRunId && e.Id <= upToEventId)
            .OrderBy(e => e.Id)
            .ToListAsync(cancellationToken);

        var state = allEvents.Aggregate(
            FlowRunState.Empty(flowRunId),
            (s, rec) =>
            {
                var evt = EventDeserializer.Deserialize(rec.EventType, rec.Payload);
                return evt is not null ? FlowStateProjector.Apply(s, evt) : s;
            });

        var snapshot = FlowStateProjector.ToSnapshot(state);
        var snapshotJson = JsonSerializer.Serialize(snapshot, JsonOptions);

        var existing = await _db.FlowSnapshotRecords
            .FindAsync([flowRunId], cancellationToken);

        if (existing is null)
        {
            _db.FlowSnapshotRecords.Add(new FlowSnapshotRecord
            {
                FlowRunId = flowRunId,
                AsOfEventId = upToEventId,
                SnapshotJson = snapshotJson,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.AsOfEventId = upToEventId;
            existing.SnapshotJson = snapshotJson;
            existing.CreatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
