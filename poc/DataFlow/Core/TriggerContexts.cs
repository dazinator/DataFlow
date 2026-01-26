namespace DataFlow.POC.Core;

using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Dynamic JSON-based trigger context for flexible scenarios.
/// Provides runtime-determined trigger data without requiring compile-time types.
/// </summary>
/// <remarks>
/// JsonTriggerContext is the only built-in trigger context type, providing maximum flexibility.
/// Users can serialize their strongly-typed objects to JSON when setting the data, and deserialize
/// from JSON in their actors if they need strong typing.
/// 
/// This approach:
/// - Keeps the framework generic and not coupled to specific trigger types
/// - Allows validation to be added via JSON schema in the future
/// - Enables any trigger mechanism to work with the same context type
/// 
/// Example - Simple usage:
/// <code>
/// var triggerContext = new JsonTriggerContext
/// {
///     Data = new JsonObject
///     {
///         ["tenantId"] = "tenant-123",
///         ["jobName"] = "DailyReport",
///         ["scheduledTime"] = JsonValue.Create(DateTime.UtcNow)
///     }
/// };
/// </code>
/// 
/// Example - From strongly-typed object:
/// <code>
/// // Define your own type
/// public class ScheduledJobParams
/// {
///     public string JobName { get; set; }
///     public string TenantId { get; set; }
///     public DateTime ScheduledTime { get; set; }
/// }
/// 
/// // Serialize to JSON
/// var parameters = new ScheduledJobParams { JobName = "DailyReport", TenantId = "tenant-123" };
/// var json = JsonSerializer.SerializeToNode(parameters) as JsonObject;
/// var triggerContext = new JsonTriggerContext { Data = json };
/// 
/// // In actor, deserialize if needed
/// var jobParams = JsonSerializer.Deserialize&lt;ScheduledJobParams&gt;(context.TriggerContext.Data);
/// </code>
/// </remarks>
public class JsonTriggerContext : ITriggerContext
{
    /// <summary>
    /// Dynamic JSON data containing trigger properties.
    /// Can contain any structure needed for the trigger scenario.
    /// </summary>
    public JsonObject? Data { get; set; }

    /// <summary>
    /// Creates a JsonTriggerContext from a JSON string.
    /// </summary>
    /// <param name="json">JSON string to parse</param>
    /// <returns>JsonTriggerContext with parsed data</returns>
    public static JsonTriggerContext FromJson(string json)
    {
        var jsonObject = JsonNode.Parse(json) as JsonObject;
        return new JsonTriggerContext { Data = jsonObject };
    }
    
    /// <summary>
    /// Creates a JsonTriggerContext from a strongly-typed object by serializing it to JSON.
    /// </summary>
    /// <typeparam name="T">Type of the object to serialize</typeparam>
    /// <param name="obj">Object to serialize</param>
    /// <returns>JsonTriggerContext with serialized object data</returns>
    public static JsonTriggerContext FromObject<T>(T obj)
    {
        var jsonNode = JsonSerializer.SerializeToNode(obj);
        return new JsonTriggerContext { Data = jsonNode as JsonObject };
    }
}
