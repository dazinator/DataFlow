namespace DataFlow.POC.Core;

using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Implementation of IParameterProvider that extracts parameters from JSON trigger contexts.
/// Provides a unified API for parameter access from JSON-based trigger data.
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
        
        // Handle JsonTriggerContext
        if (_triggerContext is JsonTriggerContext json)
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
    
    public T? Deserialize<T>() where T : class
    {
        if (_triggerContext is not JsonTriggerContext json || json.Data == null)
            return null;
        
        try
        {
            return JsonSerializer.Deserialize<T>(json.Data);
        }
        catch
        {
            return null;
        }
    }
    
    public T? Deserialize<T>(string sectionName) where T : class
    {
        if (_triggerContext is not JsonTriggerContext json || json.Data == null)
            return null;
        
        if (!json.Data.ContainsKey(sectionName))
            return null;
        
        try
        {
            var section = json.Data[sectionName];
            if (section == null)
                return null;
            
            return JsonSerializer.Deserialize<T>(section);
        }
        catch
        {
            return null;
        }
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
                Type when typeof(T) == typeof(string) => (T)(object)jsonValue.GetValue<string>(),
                Type when typeof(T) == typeof(int) => (T)(object)jsonValue.GetValue<int>(),
                Type when typeof(T) == typeof(long) => (T)(object)jsonValue.GetValue<long>(),
                Type when typeof(T) == typeof(bool) => (T)(object)jsonValue.GetValue<bool>(),
                Type when typeof(T) == typeof(DateTime) => (T)(object)jsonValue.GetValue<DateTime>(),
                Type when typeof(T) == typeof(double) => (T)(object)jsonValue.GetValue<double>(),
                Type when typeof(T) == typeof(decimal) => (T)(object)jsonValue.GetValue<decimal>(),
                _ => jsonValue.GetValue<T>()
            };
            
            return value != null;
        }
        catch
        {
            return false;
        }
    }
}
