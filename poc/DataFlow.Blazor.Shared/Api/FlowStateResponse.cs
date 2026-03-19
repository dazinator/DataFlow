namespace DataFlow.Blazor.Api;

/// <summary>
/// Response from GET /flows/{flowRunId}/state.
/// Contains a materialized snapshot (if available) plus any events that occurred
/// after the snapshot was taken. The Blazor client deserializes the snapshot,
/// applies the delta events via FlowStateProjector, then subscribes to SignalR
/// from AsOfSequence onward — eliminating any gap between HTTP and WebSocket.
/// </summary>
public record FlowStateResponse(
    /// <summary>
    /// JSON-serialized FlowSnapshot, or null if the flow has not started yet.
    /// </summary>
    string? SnapshotJson,

    /// <summary>
    /// Events that occurred after the snapshot (sequence > snapshot.AsOfSequence).
    /// </summary>
    FlowEventDto[] DeltaEvents,

    /// <summary>
    /// The highest sequence number seen. Pass this to the SignalR Subscribe call
    /// so the server can replay any events that arrived between the HTTP response
    /// and the WebSocket connection being established.
    /// </summary>
    long AsOfSequence
);

/// <summary>
/// Wire representation of a persisted event record, sent to the client
/// via the catch-up endpoint and SignalR.
/// </summary>
public record FlowEventDto(
    long SequenceNumber,
    string EventType,
    string Payload,
    DateTimeOffset OccurredAt
);
