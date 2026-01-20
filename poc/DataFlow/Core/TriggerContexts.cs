namespace DataFlow.POC.Core;

using System.Text.Json.Nodes;

/// <summary>
/// General-purpose JSON-based trigger context for dynamic scenarios.
/// Allows trigger context to be provided as a flexible JSON object without requiring
/// strongly-typed classes. Useful for scenarios where trigger data structure varies
/// or is determined at runtime.
/// </summary>
/// <remarks>
/// This implementation uses System.Text.Json.Nodes.JsonObject which is easily
/// serializable/deserializable and supports dynamic property access.
/// <example>
/// Usage:
/// <code>
/// var triggerContext = new JsonTriggerContext
/// {
///     Data = new JsonObject
///     {
///         ["tenantId"] = "tenant-123",
///         ["requestId"] = "req-456",
///         ["customProperty"] = JsonValue.Create(42)
///     }
/// };
/// 
/// // In actor - access dynamically
/// if (context.TriggerContext is JsonTriggerContext jsonContext)
/// {
///     var tenantId = jsonContext.Data?["tenantId"]?.GetValue&lt;string&gt;();
///     var requestId = jsonContext.Data?["requestId"]?.GetValue&lt;string&gt;();
/// }
/// </code>
/// </example>
/// </remarks>
public record JsonTriggerContext : ITriggerContext
{
    /// <summary>
    /// The JSON object containing trigger context data.
    /// Supports dynamic property access and is easily serializable.
    /// </summary>
    public JsonObject? Data { get; init; }

    /// <summary>
    /// Creates a JsonTriggerContext from a JSON string.
    /// </summary>
    /// <param name="json">JSON string to parse</param>
    /// <returns>JsonTriggerContext with parsed data</returns>
    public static JsonTriggerContext FromJson(string json)
    {
        var jsonObject = JsonNode.Parse(json)?.AsObject();
        return new JsonTriggerContext { Data = jsonObject };
    }

    /// <summary>
    /// Serializes the trigger context to a JSON string.
    /// </summary>
    /// <returns>JSON string representation</returns>
    public string ToJson()
    {
        return Data?.ToJsonString() ?? "null";
    }
}

/// <summary>
/// Trigger context for scheduled/cron job executions.
/// Contains information about the scheduled job that triggered the dataflow.
/// </summary>
public record ScheduledTriggerContext : ITriggerContext
{
    /// <summary>
    /// Name of the scheduled job.
    /// </summary>
    public required string JobName { get; init; }

    /// <summary>
    /// Tenant ID or organization ID for multi-tenant scenarios.
    /// </summary>
    public string? TenantId { get; init; }

    /// <summary>
    /// Scheduled time of this execution.
    /// </summary>
    public required DateTime ScheduledTime { get; init; }

    /// <summary>
    /// Cron expression that triggered this execution (if applicable).
    /// </summary>
    public string? CronExpression { get; init; }

    /// <summary>
    /// Additional job-specific parameters.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Parameters { get; init; }
}

/// <summary>
/// Trigger context for message queue-driven executions.
/// Contains information about the message that triggered the dataflow.
/// </summary>
public record MessageQueueTriggerContext : ITriggerContext
{
    /// <summary>
    /// Unique identifier of the message.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// Name of the queue from which the message was received.
    /// </summary>
    public required string QueueName { get; init; }

    /// <summary>
    /// Number of times this message has been delivered (for retry logic).
    /// </summary>
    public required int DeliveryCount { get; init; }

    /// <summary>
    /// When the message was enqueued.
    /// </summary>
    public DateTime EnqueuedTime { get; init; }

    /// <summary>
    /// Message properties/headers.
    /// </summary>
    public IReadOnlyDictionary<string, string>? MessageProperties { get; init; }
}

/// <summary>
/// Trigger context for web request-driven executions.
/// Contains information about the HTTP request that triggered the dataflow.
/// </summary>
public record WebRequestTriggerContext : ITriggerContext
{
    /// <summary>
    /// Unique identifier for this request.
    /// </summary>
    public required string RequestId { get; init; }

    /// <summary>
    /// Request path.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// HTTP method.
    /// </summary>
    public required string Method { get; init; }

    /// <summary>
    /// Request headers.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>
    /// Authenticated user ID (if authenticated).
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Client IP address.
    /// </summary>
    public string? ClientIpAddress { get; init; }
}
