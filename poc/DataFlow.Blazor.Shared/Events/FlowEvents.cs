namespace DataFlow.Blazor.Events;

public record FlowStartedEvent(
    Guid InvocationId,
    string FlowName,
    DateTime Timestamp
) : IDataFlowEvent;

public record FlowCompletedEvent(
    Guid InvocationId,
    bool Success,
    DateTime Timestamp,
    string? ErrorMessage = null
) : IDataFlowEvent;
