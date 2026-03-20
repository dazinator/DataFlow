namespace DataFlow.Blazor.Events;

public record BlockStartedEvent(
    string BlockName,
    string BlockType,
    DateTime Timestamp,
    bool IsSource = false
) : IDataFlowEvent;

public record BlockCompletedEvent(
    string BlockName,
    bool Success,
    DateTime Timestamp,
    string? ErrorMessage = null
) : IDataFlowEvent;

public record BlockProgressEvent(
    string BlockName,
    long ItemsProcessed,
    DateTime Timestamp
) : IDataFlowEvent;
