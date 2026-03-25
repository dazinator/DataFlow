namespace DataFlow.Blazor.Api;

/// <summary>
/// Response from GET /flows/{flowRunId}/state.
/// Contains a materialized snapshot (if available) plus any events that occurred
/// after the snapshot was taken. The Blazor client deserializes the snapshot,
/// applies the delta events via FlowStateProjector, then subscribes to SignalR
/// from AsOfId onward — eliminating any gap between HTTP and WebSocket.
/// </summary>
public record FlowStateResponse(
    /// <summary>
    /// JSON-serialized FlowSnapshot, or null if the flow has not started yet.
    /// </summary>
    string? SnapshotJson,

    /// <summary>
    /// Events that occurred after the snapshot (Id > snapshot.AsOfEventId).
    /// Applied via full ProcessEvent — updates both state and EventLog.
    /// </summary>
    FlowEventDto[] DeltaEvents,

    /// <summary>
    /// The highest event Id seen. Pass this to the SignalR Subscribe call
    /// so the server can replay any events that arrived between the HTTP response
    /// and the WebSocket connection being established.
    /// </summary>
    long AsOfId,

    /// <summary>
    /// Structural events (FlowStarted/Completed, BlockStarted/Completed) with
    /// Id &lt;= AsOfId — i.e. already folded into the snapshot.
    /// Applied to EventLog ONLY (no state updates) so the event history pane
    /// remains populated for completed flows loaded from a fresh page.
    /// </summary>
    FlowEventDto[] AuditEvents,

    /// <summary>
    /// Optional friendly display name configured via <c>df.DisplayName("…")</c> on the flow builder.
    /// Resolved server-side from <c>IDataFlowFlowMetadataStore</c> at request time.
    /// Null when no display name has been configured.
    /// </summary>
    string? FlowDisplayName = null
);

/// <summary>
/// Wire representation of a persisted event record, sent to the client
/// via the catch-up endpoint and SignalR.
/// </summary>
public record FlowEventDto(
    long Id,
    string EventType,
    string Payload,
    DateTimeOffset OccurredAt
);
