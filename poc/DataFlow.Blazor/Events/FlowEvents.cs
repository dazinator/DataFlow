namespace DataFlow.Blazor.Events;

/// <summary>
/// Event raised when a DataFlow execution starts.
/// </summary>
public record FlowStartedEvent(
    Guid InvocationId,
    string FlowName,
    DateTime Timestamp
);

/// <summary>
/// Event raised when a DataFlow execution completes.
/// </summary>
public record FlowCompletedEvent(
    Guid InvocationId,
    bool Success,
    DateTime Timestamp,
    string? ErrorMessage = null
);
