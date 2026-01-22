namespace DataFlow.Blazor.Events;

/// <summary>
/// Event raised when a block starts processing.
/// </summary>
public record BlockStartedEvent(
    string BlockName,
    string BlockType,
    DateTime Timestamp
);

/// <summary>
/// Event raised when a block completes processing.
/// </summary>
public record BlockCompletedEvent(
    string BlockName,
    bool Success,
    DateTime Timestamp,
    string? ErrorMessage = null
);

/// <summary>
/// Event raised to report block progress.
/// </summary>
public record BlockProgressEvent(
    string BlockName,
    long ItemsProcessed,
    DateTime Timestamp
);
