namespace DataFlow.Blazor.Server.Services;

using System.Text.Json;
using DataFlow.Blazor.Api;
using DataFlow.Blazor.Events;
using DataFlow.Blazor.Projection;
using DataFlow.Blazor.Server.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// EF Core implementation of IFlowEventSink.
/// Appends events to FlowEventRecords, materializes snapshots per SnapshotPolicy,
/// and pushes real-time notifications via SignalR to connected Blazor clients.
///
/// <para><b>Storage lifecycle for a flow run:</b></para>
/// <list type="number">
///   <item>Every event is persisted to <see cref="FlowEventRecord"/> and immediately broadcast
///         to all connected SignalR clients in the flow's group.</item>
///   <item>When <see cref="SnapshotPolicy"/> fires (on start, completion, and periodically),
///         all events are re-folded via <see cref="FlowStateProjector"/> and the resulting
///         <see cref="FlowSnapshot"/> — including edge rate watermarks and channel buffer
///         watermarks — is written to <see cref="FlowSnapshotRecord"/>.</item>
///   <item>Once <see cref="FlowCompletedEvent"/> is received the snapshot is final.
///         High-frequency telemetry events (<see cref="BlockMetricsEvent"/>,
///         <see cref="EdgeProgressEvent"/>, <see cref="ChannelStatsEvent"/>) are then
///         deleted from <see cref="FlowEventRecord"/>. These represent ~90% of all rows
///         during a run. Because the snapshot already encodes every watermark value,
///         no information is lost — future viewers reconstruct the full display from the
///         snapshot alone.</item>
/// </list>
///
/// <para>Only structural events (FlowStarted/Completed, BlockStarted/Completed) are
/// retained permanently; they form the audit trail for the event history pane.</para>
///
/// <typeparam name="TContext">
/// The DbContext type to use for persistence. Must have
/// <see cref="DataFlowModelBuilderExtensions.AddDataFlowVisualizationEntities"/> called
/// in its OnModelCreating.
/// </typeparam>
/// </summary>
public class EfCoreFlowEventSink<TContext> : IFlowEventSink where TContext : DbContext
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly TContext _db;
    private readonly IHubContext<FlowEventsHub<TContext>> _hub;
    private readonly SnapshotPolicy _snapshotPolicy;

    public EfCoreFlowEventSink(
        TContext db,
        IHubContext<FlowEventsHub<TContext>> hub,
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

        _db.Set<FlowEventRecord>().Add(record);
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

        // Once the flow is complete the snapshot is final and carries all watermarks.
        // Prune the high-frequency telemetry ticks — they have no further replay value
        // and represent the bulk of the event log volume.
        if (evt is FlowCompletedEvent)
        {
            await PruneTelemetryEventsAsync(flowRunId, cancellationToken);
        }
    }

    /// <summary>
    /// The three high-frequency event types emitted on every tick (~500 ms per block/edge/channel).
    /// They are useful during a live run — live clients receive them via SignalR to update the rate
    /// and buffer pills — but their only durable value is feeding the watermark fold. Once the final
    /// snapshot captures those watermarks (see <see cref="PruneTelemetryEventsAsync"/>), the
    /// individual ticks are redundant.
    /// </summary>
    private static readonly HashSet<string> TelemetryEventTypes =
    [
        nameof(BlockMetricsEvent),
        nameof(EdgeProgressEvent),
        nameof(ChannelStatsEvent)
    ];

    /// <summary>
    /// Deletes all telemetry ticks for the completed flow run from <see cref="FlowEventRecord"/>.
    /// Must only be called AFTER <see cref="MaterializeSnapshotAsync"/> for the
    /// <see cref="FlowCompletedEvent"/>, which guarantees the final snapshot already encodes:
    /// <list type="bullet">
    ///   <item>Edge rate watermarks (max / min / average) via <see cref="EdgeSnapshot"/></item>
    ///   <item>Channel buffer watermarks (max / min fill) via <see cref="ChannelSnapshot"/></item>
    ///   <item>Block final item counts via <see cref="BlockSnapshot"/></item>
    /// </list>
    /// The retained structural events (FlowStarted/Completed, BlockStarted/Completed) are
    /// sufficient for the event history pane and any future audit queries.
    /// </summary>
    private async Task PruneTelemetryEventsAsync(Guid flowRunId, CancellationToken cancellationToken)
    {
        var toDelete = await _db.Set<FlowEventRecord>()
            .Where(e => e.FlowRunId == flowRunId && TelemetryEventTypes.Contains(e.EventType))
            .ToListAsync(cancellationToken);

        if (toDelete.Count > 0)
        {
            _db.Set<FlowEventRecord>().RemoveRange(toDelete);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task MaterializeSnapshotAsync(Guid flowRunId, long upToEventId, CancellationToken cancellationToken)
    {
        var allEvents = await _db.Set<FlowEventRecord>()
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

        var existing = await _db.Set<FlowSnapshotRecord>()
            .FindAsync([flowRunId], cancellationToken);

        if (existing is null)
        {
            _db.Set<FlowSnapshotRecord>().Add(new FlowSnapshotRecord
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
