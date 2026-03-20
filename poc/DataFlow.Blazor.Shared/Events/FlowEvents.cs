namespace DataFlow.Blazor.Events;

public record FlowStartedEvent(
    Guid InvocationId,
    string FlowName,
    DateTime Timestamp,
    /// <summary>
    /// Raw JSON string of the trigger parameters passed when this flow was invoked.
    /// Null if no trigger params were provided.
    /// <para>
    /// <b>Security note:</b> this value is stored verbatim and forwarded to all
    /// connected clients without any redaction or encryption. Sensitive values
    /// (secrets, PII) must be encrypted or omitted by the caller before passing
    /// trigger params to the execution context.
    /// </para>
    /// </summary>
    string? TriggerParamsJson = null
) : IDataFlowEvent;

public record FlowCompletedEvent(
    Guid InvocationId,
    bool Success,
    DateTime Timestamp,
    string? ErrorMessage = null
) : IDataFlowEvent;
