namespace DataFlow.Blazor.Api;

using DataFlow.Blazor.Events;

/// <summary>
/// Slim summary of a single flow run, returned by GET /flows.
/// Derived from the flow's materialized snapshot.
/// </summary>
public record FlowSummaryDto(
    Guid FlowRunId,
    string FlowName,
    FlowState Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? ErrorMessage,
    int BlockCount,
    string? TriggerParamsJson = null,
    Guid? CorrelationId = null,
    int AttemptNumber = 1,
    /// <summary>
    /// Optional friendly display name configured via <c>df.DisplayName("…")</c> on the flow builder.
    /// Null when no display name has been configured.
    /// </summary>
    string? FlowDisplayName = null
);
