namespace DataFlow.POC.Core;

using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Dynamic JSON-based trigger context for flexible scenarios.
/// Provides runtime-determined trigger data without requiring compile-time types.
/// </summary>
/// <remarks>
/// Use JsonTriggerContext when:
/// - Trigger structure is determined at runtime
/// - Need maximum flexibility for different trigger types
/// - Working with external systems with varying schemas
/// 
/// Example:
/// <code>
/// var triggerContext = new JsonTriggerContext
/// {
///     Data = new JsonObject
///     {
///         ["tenantId"] = "tenant-123",
///         ["customProperty"] = JsonValue.Create(42),
///         ["metadata"] = new JsonObject
///         {
///             ["source"] = "scheduler",
///             ["priority"] = "high"
///         }
///     }
/// };
/// </code>
/// </remarks>
public class JsonTriggerContext : ITriggerContext
{
    /// <summary>
    /// Dynamic JSON data containing trigger properties.
    /// </summary>
    public JsonObject? Data { get; set; }

    /// <summary>
    /// Creates a JsonTriggerContext from a JSON string.
    /// </summary>
    public static JsonTriggerContext FromJson(string json)
    {
        var jsonObject = JsonNode.Parse(json) as JsonObject;
        return new JsonTriggerContext { Data = jsonObject };
    }
}

/// <summary>
/// Trigger context for scheduled job executions.
/// </summary>
/// <remarks>
/// Use ScheduledTriggerContext when:
/// - Dataflow is triggered by a scheduled job (e.g., cron, timer)
/// - Need to track job identity and schedule information
/// - Implementing tenant-aware scheduled processing
/// 
/// Example:
/// <code>
/// var triggerContext = new ScheduledTriggerContext
/// {
///     JobName = "DailyReport",
///     TenantId = "tenant-123",
///     ScheduledTime = DateTime.UtcNow
/// };
/// </code>
/// </remarks>
public class ScheduledTriggerContext : ITriggerContext
{
    /// <summary>
    /// Name of the scheduled job.
    /// </summary>
    public string? JobName { get; set; }

    /// <summary>
    /// Tenant identifier for multi-tenant scenarios.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Scheduled execution time.
    /// </summary>
    public DateTime? ScheduledTime { get; set; }

    /// <summary>
    /// Additional metadata for the scheduled job.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Trigger context for message queue executions.
/// </summary>
/// <remarks>
/// Use MessageQueueTriggerContext when:
/// - Dataflow is triggered by a message from a queue (e.g., RabbitMQ, Azure Service Bus)
/// - Need to implement retry logic based on delivery count
/// - Tracking message metadata and correlation
/// 
/// Example:
/// <code>
/// var triggerContext = new MessageQueueTriggerContext
/// {
///     QueueName = "reports-queue",
///     MessageId = "msg-456",
///     DeliveryCount = 1,
///     MessageProperties = new Dictionary&lt;string, string&gt;
///     {
///         ["tenantId"] = "tenant-123",
///         ["correlationId"] = "corr-789"
///     }
/// };
/// </code>
/// </remarks>
public class MessageQueueTriggerContext : ITriggerContext
{
    /// <summary>
    /// Name of the queue from which the message was received.
    /// </summary>
    public string? QueueName { get; set; }

    /// <summary>
    /// Unique identifier for the message.
    /// </summary>
    public string? MessageId { get; set; }

    /// <summary>
    /// Correlation ID for tracking related messages.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Number of times the message has been delivered (useful for retry logic).
    /// </summary>
    public int DeliveryCount { get; set; }

    /// <summary>
    /// Custom properties associated with the message.
    /// </summary>
    public Dictionary<string, string>? MessageProperties { get; set; }

    /// <summary>
    /// Timestamp when the message was enqueued.
    /// </summary>
    public DateTime? EnqueuedTime { get; set; }
}

/// <summary>
/// Trigger context for web request executions.
/// </summary>
/// <remarks>
/// Use WebRequestTriggerContext when:
/// - Dataflow is triggered by an HTTP request (e.g., REST API endpoint)
/// - Need to access user identity and request metadata
/// - Implementing request-scoped processing
/// 
/// Example:
/// <code>
/// var triggerContext = new WebRequestTriggerContext
/// {
///     UserId = "user-123",
///     TenantId = "tenant-456",
///     RequestPath = "/api/reports",
///     RequestHeaders = new Dictionary&lt;string, string&gt;
///     {
///         ["X-Correlation-Id"] = "corr-789"
///     }
/// };
/// </code>
/// </remarks>
public class WebRequestTriggerContext : ITriggerContext
{
    /// <summary>
    /// User identifier from the request.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Tenant identifier for multi-tenant scenarios.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Request path.
    /// </summary>
    public string? RequestPath { get; set; }

    /// <summary>
    /// Request method (GET, POST, etc.).
    /// </summary>
    public string? RequestMethod { get; set; }

    /// <summary>
    /// Selected request headers.
    /// </summary>
    public Dictionary<string, string>? RequestHeaders { get; set; }

    /// <summary>
    /// Client IP address.
    /// </summary>
    public string? ClientIp { get; set; }
}
