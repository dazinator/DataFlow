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
        
        // Try each trigger context type
        if (_triggerContext is ScheduledTriggerContext scheduled)
        {
            return TryGetFromScheduled(name, out value);
        }
        else if (_triggerContext is MessageQueueTriggerContext queue)
        {
            return TryGetFromQueue(name, out value);
        }
        else if (_triggerContext is WebRequestTriggerContext web)
        {
            return TryGetFromWeb(name, out value);
        }
        else if (_triggerContext is JsonTriggerContext json)
        {
            return TryGetFromJson(name, json.Data, out value);
        }
        
        return false;
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
    
    private bool TryGetFromScheduled<T>(string name, out T? value)
    {
        value = default;
        
        if (_triggerContext is not ScheduledTriggerContext scheduled)
            return false;
        
        object? objValue = name.ToLowerInvariant() switch
        {
            "jobname" => scheduled.JobName,
            "tenantid" => scheduled.TenantId,
            "scheduledtime" => scheduled.ScheduledTime,
            _ => scheduled.Metadata?.GetValueOrDefault(name)
        };
        
        return TryConvert(objValue, out value);
    }
    
    private bool TryGetFromQueue<T>(string name, out T? value)
    {
        value = default;
        
        if (_triggerContext is not MessageQueueTriggerContext queue)
            return false;
        
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
    
    private bool TryGetFromWeb<T>(string name, out T? value)
    {
        value = default;
        
        if (_triggerContext is not WebRequestTriggerContext web)
            return false;
        
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
            
            // Handle direct type conversion
            if (typeof(T) == typeof(string))
            {
                value = (T)(object)jsonValue.GetValue<string>();
                return true;
            }
            else if (typeof(T) == typeof(int))
            {
                value = (T)(object)jsonValue.GetValue<int>();
                return true;
            }
            else if (typeof(T) == typeof(long))
            {
                value = (T)(object)jsonValue.GetValue<long>();
                return true;
            }
            else if (typeof(T) == typeof(bool))
            {
                value = (T)(object)jsonValue.GetValue<bool>();
                return true;
            }
            else if (typeof(T) == typeof(DateTime))
            {
                value = (T)(object)jsonValue.GetValue<DateTime>();
                return true;
            }
            else if (typeof(T) == typeof(double))
            {
                value = (T)(object)jsonValue.GetValue<double>();
                return true;
            }
            else if (typeof(T) == typeof(decimal))
            {
                value = (T)(object)jsonValue.GetValue<decimal>();
                return true;
            }
            else
            {
                // Try generic conversion
                value = jsonValue.GetValue<T>();
                return value != null;
            }
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
            
            // Try conversion for common types
            if (typeof(T) == typeof(string))
            {
                value = (T)(object)objValue.ToString()!;
                return true;
            }
            
            // Use Convert for numeric and other convertible types
            value = (T)Convert.ChangeType(objValue, typeof(T));
            return true;
        }
        catch
        {
            return false;
        }
    }
}
