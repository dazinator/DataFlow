namespace DataFlow.Blazor.Events;

/// <summary>
/// Base interface for all DataFlow events.
/// </summary>
public interface IDataFlowEvent
{
    /// <summary>
    /// When the event occurred.
    /// </summary>
    DateTime Timestamp { get; }
}

/// <summary>
/// Interface for event sources that provide DataFlow events.
/// This abstraction allows for different event sources to be plugged in
/// (e.g., mock generators, SignalR, direct integration).
/// </summary>
public interface IEventSource
{
    /// <summary>
    /// Gets an observable stream of events for a specific invocation.
    /// </summary>
    /// <param name="invocationId">The invocation ID to monitor.</param>
    /// <returns>An async enumerable of events.</returns>
    IAsyncEnumerable<object> GetEventsAsync(Guid invocationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current state snapshot for an invocation (aggregated from historical events).
    /// </summary>
    /// <param name="invocationId">The invocation ID to get snapshot for.</param>
    /// <returns>The snapshot, or null if not available.</returns>
    Task<FlowSnapshot?> GetSnapshotAsync(Guid invocationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// A snapshot of the current state of a flow execution.
/// Aggregated from all historical events for fast initial load.
/// </summary>
public record FlowSnapshot(
    Guid InvocationId,
    string FlowName,
    DateTime StartTime,
    FlowState State,
    Dictionary<string, BlockSnapshot> Blocks,
    Dictionary<string, ChannelSnapshot> Channels
);

/// <summary>
/// Snapshot of a block's current state.
/// </summary>
public record BlockSnapshot(
    string BlockName,
    string BlockType,
    BlockState State,
    long ItemsProcessed,
    DateTime? StartTime,
    DateTime? EndTime,
    string? ErrorMessage
);

/// <summary>
/// Snapshot of a channel's current state.
/// </summary>
public record ChannelSnapshot(
    string BlockName,
    int BufferCapacity,
    int CurrentCount,
    DateTime LastUpdate
);

/// <summary>
/// The state of a flow execution.
/// </summary>
public enum FlowState
{
    NotStarted,
    Running,
    Completed,
    Failed
}

/// <summary>
/// The state of a block in a flow execution.
/// </summary>
public enum BlockState
{
    Idle,
    Running,
    Completed,
    Failed
}
