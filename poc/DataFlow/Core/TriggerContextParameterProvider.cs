namespace DataFlow.POC.Core;

using System.Text.Json.Nodes;

/// <summary>
/// Implementation of IParameterProvider that extracts parameters from trigger contexts.
/// Supports all built-in trigger context types and provides a unified API for parameter access.
/// </summary>
public class TriggerContextParameterProvider : IParameterProvider
{
    private readonly ITriggerContext? _triggerContext;
    
    /// <summary>
    /// Initializes a new instance of TriggerContextParameterProvider.
    /// </summary>
    /// <param name="triggerContext">The trigger context to extract parameters from. Can be null.</param>
    public TriggerContextParameterProvider(ITriggerContext? triggerContext)
    {
        _triggerContext = triggerContext;
    }
    
    public bool TryGetParameter<T>(string name, out T? value)
    {
        value = default;
        
        if (_triggerContext == null)
            return false;
        
        // Try each trigger context type - guards removed since we already checked the type
        return _triggerContext switch
        {
            ScheduledTriggerContext scheduled => TryGetFromScheduled<T>(name, scheduled, out value),
            MessageQueueTriggerContext queue => TryGetFromQueue<T>(name, queue, out value),
            WebRequestTriggerContext web => TryGetFromWeb<T>(name, web, out value),
            JsonTriggerContext json => TryGetFromJson(name, json.Data, out value),
            _ => false
        };
    }
    
    public T GetRequiredParameter<T>(string name)
    {
        if (TryGetParameter<T>(name, out var value) && value != null)
        {
            return value;
        }
        
        throw new InvalidOperationException(
            $"Required parameter '{name}' of type '{typeof(T).Name}' was not found in trigger context. " +
            $"Ensure the parameter is provided when creating the execution context.");
    }
    
    public T GetParameter<T>(string name, T defaultValue)
    {
        if (TryGetParameter<T>(name, out var value) && value != null)
        {
            return value;
        }
        
        return defaultValue;
    }
    
    private bool TryGetFromScheduled<T>(string name, ScheduledTriggerContext scheduled, out T? value)
    {
        value = default;
        
        object? objValue = name.ToLowerInvariant() switch
        {
            "jobname" => scheduled.JobName,
            "tenantid" => scheduled.TenantId,
            "scheduledtime" => scheduled.ScheduledTime,
            _ => scheduled.Metadata?.GetValueOrDefault(name)
        };
        
        return TryConvert(objValue, out value);
    }
    
    private bool TryGetFromQueue<T>(string name, MessageQueueTriggerContext queue, out T? value)
    {
        value = default;
        
        object? objValue = name.ToLowerInvariant() switch
        {
            "queuename" => queue.QueueName,
            "messageid" => queue.MessageId,
            "correlationid" => queue.CorrelationId,
            "deliverycount" => queue.DeliveryCount,
            "enqueuedtime" => queue.EnqueuedTime,
            _ => queue.MessageProperties?.GetValueOrDefault(name)
        };
        
        return TryConvert(objValue, out value);
    }
    
    private bool TryGetFromWeb<T>(string name, WebRequestTriggerContext web, out T? value)
    {
        value = default;
        
        object? objValue = name.ToLowerInvariant() switch
        {
            "userid" => web.UserId,
            "tenantid" => web.TenantId,
            "requestpath" => web.RequestPath,
            "requestmethod" => web.RequestMethod,
            "clientip" => web.ClientIp,
            _ => web.RequestHeaders?.GetValueOrDefault(name)
        };
        
        return TryConvert(objValue, out value);
    }
    
    private bool TryGetFromJson<T>(string name, JsonObject? data, out T? value)
    {
        value = default;
        
        if (data == null || !data.ContainsKey(name))
            return false;
        
        try
        {
            var jsonValue = data[name];
            if (jsonValue == null)
                return false;
            
            // Use a switch expression for cleaner type handling
            value = typeof(T) switch
            {
                Type t when t == typeof(string) => (T)(object)jsonValue.GetValue<string>(),
                Type t when t == typeof(int) => (T)(object)jsonValue.GetValue<int>(),
                Type t when t == typeof(long) => (T)(object)jsonValue.GetValue<long>(),
                Type t when t == typeof(bool) => (T)(object)jsonValue.GetValue<bool>(),
                Type t when t == typeof(DateTime) => (T)(object)jsonValue.GetValue<DateTime>(),
                Type t when t == typeof(double) => (T)(object)jsonValue.GetValue<double>(),
                Type t when t == typeof(decimal) => (T)(object)jsonValue.GetValue<decimal>(),
                _ => jsonValue.GetValue<T>()
            };
            
            return value != null;
        }
        catch
        {
            return false;
        }
    }
    
    private bool TryConvert<T>(object? objValue, out T? value)
    {
        value = default;
        
        if (objValue == null)
            return false;
        
        try
        {
            // Direct assignment if types match
            if (objValue is T typedValue)
            {
                value = typedValue;
                return true;
            }
            
            // Handle string conversion
            if (typeof(T) == typeof(string))
            {
                value = (T)(object)objValue.ToString()!;
                return true;
            }
            
            // Only use Convert.ChangeType for known convertible types
            var targetType = typeof(T);
            if (targetType.IsPrimitive || targetType == typeof(decimal) || targetType == typeof(DateTime))
            {
                value = (T)Convert.ChangeType(objValue, targetType);
                return true;
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }
}
