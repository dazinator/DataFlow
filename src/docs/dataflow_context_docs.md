# DataFlow Context Documentation

## Overview

The DataFlow library now provides enhanced context-passing capabilities to make it easier to share data and state across all components in a data flow execution. This addresses previous pain points where accessing shared context required custom types and workarounds.

## Key Concepts

When a data flow is executed, it often has input data (like the message that triggered it) and may need to:
- Add additional context from lookups performed prior to execution
- Make that context accessible to anything in the entire flow during execution
- Provide both strongly-typed and flexible access patterns

## IDataFlowContext Interface

The core interface provides access to execution context and shared state:

```csharp
public interface IDataFlowContext
{
    Guid InvocationId { get; set; }
    CancellationToken CancellationToken { get; set; }
    IServiceProvider ServiceProvider { get; set; }
    string Name { get; set; }
    DataFlowMetricsTagsContext FlowMetricsContext { get; set; }

    /// <summary>
    /// Items that can be used to pass additional data between blocks in the flow. 
    /// Stuff stored here could be accessed concurrently by multiple blocks, so use with care.
    /// </summary>
    ConcurrentDictionary<string, object> Items { get; }

    public AsyncServiceScope CreateNewAsyncScope(out IDataFlowContext branchContext);
}
```

## Context Access Patterns

### 1. Direct Parameter Injection

All producer, transformer, and processor interfaces now accept `IDataFlowContext` as a parameter:

```csharp
internal class LargeDataProducer : IStreamProducer<int>
{
    public async IAsyncEnumerable<int> ProduceAsync(IDataFlowContext context,
        [EnumeratorCancellation] CancellationToken cancellation)
    {
        // Access shared context via context.Items
        var config = context.Items["config"] as MyConfig;
        
        foreach (var i in Enumerable.Range(0, 1000))
        {
            cancellation.ThrowIfCancellationRequested();
            yield return i;
        }
    }
}
```

### 2. Async Local (Ambient Context)

Set context as an ambient value accessible from anywhere in the execution flow:

```csharp
var context = new DataFlowContext()
{
    Name = "my-flow",
    InvocationId = Guid.NewGuid(),
    ServiceProvider = serviceProvider,
    CancellationToken = timeoutCts.Token
};

// Set as ambient context
DataFlowContext.SetCurrent(context);

// Now accessible anywhere in the flow
var executor = serviceProvider.GetRequiredService<FlowExecutor<MyFlowConfig>>();
await executor.ExecuteAsync(context);
```

Access the ambient context from anywhere:

```csharp
var context = DataFlowContext.Current;
var sharedData = context.Items["myKey"];
```

## Generic Context Support

### Strongly-Typed Input Parameters

Use `DataFlowContext<T>` for type-safe access to input parameters:

```csharp
public class DataFlowContext<T> : DataFlowContext
{
    public T? InputParameters { get; }
    
    public static new DataFlowContext<T>? Current { get; set; }
}
```

### Usage Example

```csharp
// Create strongly-typed context
var context = new DataFlowContext<MyMessage>(message)
{
    Name = "message-processor",
    InvocationId = Guid.NewGuid(),
    ServiceProvider = serviceProvider,
    CancellationToken = timeoutCts.Token
};

// Set as current (both generic and non-generic Current will resolve to same instance)
DataFlowContext<MyMessage>.SetCurrent(context);

var executor = serviceProvider.GetRequiredService<FlowExecutor<MessageProcessorConfig>>();
await executor.ExecuteAsync(context);
```

Access strongly-typed parameters anywhere in the flow:

```csharp
// Strongly-typed access - no casting required
MyMessage message = DataFlowContext<MyMessage>.Current.InputParameters;

// Or access via non-generic version
var context = DataFlowContext.Current;
```

## Best Practices

### Thread Safety
The `Items` concurrent dictionary is thread-safe, but values stored within it should be designed for concurrent access:

```csharp
// Safe - immutable data
context.Items["config"] = new ReadOnlyConfiguration();

// Safe - thread-safe collections
context.Items["cache"] = new ConcurrentDictionary<string, object>();

// Unsafe - mutable objects accessed concurrently
context.Items["counter"] = new Counter(); // Could cause race conditions
```

### Service Scoping
Create new service scopes for branched execution paths:

```csharp
using var scope = context.CreateNewAsyncScope(out var branchContext);
// Use branchContext for this execution branch
```

### Initialization Pattern

```csharp
var context = new DataFlowContext<TInput>(inputData)
{
    Name = "descriptive-flow-name",
    InvocationId = Guid.NewGuid(),
    ServiceProvider = serviceProvider,
    CancellationToken = cancellationToken
};

// Add any shared data
context.Items["tenantId"] = tenantId;
context.Items["correlationId"] = correlationId;

// Set as ambient if needed
DataFlowContext<TInput>.SetCurrent(context);

// Execute
await executor.ExecuteAsync(context);
```

## Migration Notes

- Existing flows will continue to work - context parameter is added to interfaces
- No breaking changes to current DataFlow configurations
- New context features are opt-in enhancements
- Both generic and non-generic contexts share the same async local storage

## Common Scenarios

### Tenant Isolation
```csharp
context.Items["tenantId"] = message.TenantId;
context.Items["tenantConfig"] = await GetTenantConfigAsync(message.TenantId);
```

### Correlation Tracking
```csharp
context.Items["correlationId"] = message.CorrelationId;
context.Items["traceId"] = Activity.Current?.TraceId.ToString();
```

### Caching Shared Lookups
```csharp
context.Items["userCache"] = new ConcurrentDictionary<int, User>();
context.Items["configCache"] = await LoadConfigurationAsync();
```