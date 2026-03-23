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
    string? TriggerParamsJson = null,
    /// <summary>
    /// Stable identity of the originating work item (e.g. a queue message ID).
    /// Shared across all retry attempts for the same logical unit of work.
    /// Null for standalone / ad-hoc invocations.
    /// </summary>
    Guid? CorrelationId = null,
    /// <summary>
    /// 1-based delivery attempt counter for this CorrelationId.
    /// Always 1 for standalone invocations; increments on each retry.
    /// </summary>
    int AttemptNumber = 1
) : IDataFlowEvent;

public record FlowCompletedEvent(
    Guid InvocationId,
    bool Success,
    DateTime Timestamp,
    string? ErrorMessage = null
) : IDataFlowEvent;
