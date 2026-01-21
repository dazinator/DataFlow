# Advanced Design Considerations: Parameter Binding and Validation

**Date**: 2026-01-21  
**Status**: Additional research based on implementation feedback

---

## Design Concerns Identified

During prototype review, two important concerns were raised about the initial design:

### Concern 1: Actor Coupling to Specific Trigger Context Types

**Problem**: Actors must check for specific trigger context types, creating tight coupling:

```csharp
public async IAsyncEnumerable<ProcessedReport> RunAsync(
    IAsyncEnumerable<ReportData> input,
    IActorExecutionContext context)
{
    // Tightly coupled - checks for specific type
    if (context.TriggerContext is ScheduledTriggerContext scheduled)
    {
        var tenantId = scheduled.TenantId;
    }
    else if (context.TriggerContext is MessageQueueTriggerContext queue)
    {
        var tenantId = queue.MessageProperties?["tenantId"];
    }
    else if (context.TriggerContext is JsonTriggerContext json)
    {
        var tenantId = json.Data?["tenantId"]?.GetValue<string>();
    }
    // If new trigger type added, actor needs updating
}
```

**Risk**: Adding new trigger context types requires updating all actors that use parameters from trigger context.

### Concern 2: Unclear Parameter Requirements

**Problem**: No way to know what parameters actors need before execution:

```csharp
// Caller has no way to know what parameters the dataflow needs
var context = new ExecutionContext(serviceProvider, cancellationToken, ...);
await graph.ExecuteAsync(context); // May fail at runtime if required params missing
```

**Risk**: Runtime failures due to missing parameters that could have been caught earlier.

---

## Proposed Solutions

### Solution 1: Parameter Provider Pattern (Recommended for Initial Implementation)

Introduce a parameter provider that decouples actors from specific trigger context types:

```csharp
public interface IParameterProvider
{
    /// <summary>
    /// Get a parameter value by name with optional default.
    /// </summary>
    bool TryGetParameter<T>(string name, out T? value);
    
    /// <summary>
    /// Get a required parameter value by name (throws if missing).
    /// </summary>
    T GetRequiredParameter<T>(string name);
}

public interface IExecutionContext
{
    // ... existing properties ...
    ITriggerContext? TriggerContext { get; }
    
    // NEW: Parameter provider built from trigger context
    IParameterProvider Parameters { get; }
}
```

**Actor Usage** (decoupled):

```csharp
public async IAsyncEnumerable<ProcessedReport> RunAsync(
    IAsyncEnumerable<ReportData> input,
    IActorExecutionContext context)
{
    // Decoupled - works with any trigger context that provides "tenantId"
    var tenantId = context.Parameters.TryGetParameter<string>("tenantId", out var tid) 
        ? tid 
        : "unknown";
    
    // Process with tenant context
}
```

**Implementation**:

```csharp
public class TriggerContextParameterProvider : IParameterProvider
{
    private readonly ITriggerContext? _triggerContext;
    
    public TriggerContextParameterProvider(ITriggerContext? triggerContext)
    {
        _triggerContext = triggerContext;
    }
    
    public bool TryGetParameter<T>(string name, out T? value)
    {
        value = default;
        
        if (_triggerContext == null)
            return false;
            
        // Try static types first
        if (_triggerContext is ScheduledTriggerContext scheduled)
        {
            return TryGetFromScheduled(name, out value);
        }
        else if (_triggerContext is MessageQueueTriggerContext queue)
        {
            return TryGetFromQueue(name, out value);
        }
        else if (_triggerContext is JsonTriggerContext json)
        {
            return TryGetFromJson(name, json.Data, out value);
        }
        
        return false;
    }
    
    private bool TryGetFromJson<T>(string name, JsonObject? data, out T? value)
    {
        value = default;
        if (data == null || !data.ContainsKey(name))
            return false;
            
        try
        {
            value = data[name]?.GetValue<T>();
            return value != null;
        }
        catch
        {
            return false;
        }
    }
}
```

**Benefits**:
- ✅ Actors decoupled from specific trigger context types
- ✅ New trigger types work automatically if they provide same parameter names
- ✅ Backward compatible (existing code using direct type checks still works)
- ✅ Simple to implement

**Limitations**:
- ❌ Still no compile-time validation of parameter requirements
- ❌ No metadata about what parameters are available/required

---

### Solution 2: Parameter Metadata and Validation (Future Enhancement)

For compile-time safety and pre-execution validation, introduce parameter descriptors:

```csharp
public interface IParameterDescriptor
{
    string Name { get; }
    Type ParameterType { get; }
    bool IsRequired { get; }
    string? Description { get; }
}

public interface IParameterizedActor<TIn, TOut> : IStreamActor<TIn, TOut>
{
    /// <summary>
    /// Declare what parameters this actor requires.
    /// </summary>
    IReadOnlyList<IParameterDescriptor> GetParameterDescriptors();
}

public class TenantAwareActor : IParameterizedActor<ReportData, ProcessedReport>
{
    // Declare requirements at compile time
    public IReadOnlyList<IParameterDescriptor> GetParameterDescriptors()
    {
        return new[]
        {
            new ParameterDescriptor<string>("tenantId", isRequired: true, 
                description: "Tenant identifier for multi-tenant processing")
        };
    }
    
    public async IAsyncEnumerable<ProcessedReport> RunAsync(
        IAsyncEnumerable<ReportData> input,
        IActorExecutionContext context)
    {
        // Safe to use - framework validated it exists
        var tenantId = context.Parameters.GetRequiredParameter<string>("tenantId");
        
        await foreach (var data in input)
        {
            yield return new ProcessedReport { TenantId = tenantId, Data = data };
        }
    }
}
```

**Pre-Execution Validation**:

```csharp
// Before executing dataflow
var graph = builder.Build();

// Get all parameter requirements from the graph
var requiredParameters = graph.GetRequiredParameters();

// Create parameter provider and validate
var parameterProvider = new TriggerContextParameterProvider(triggerContext);
var validationErrors = ValidateParameters(requiredParameters, parameterProvider);

if (validationErrors.Any())
{
    throw new ParameterValidationException(
        "Required parameters missing: " + string.Join(", ", validationErrors));
}

// Safe to execute
await graph.ExecuteAsync(context);
```

**Benefits**:
- ✅ Compile-time declaration of parameter requirements
- ✅ Pre-execution validation (fail fast)
- ✅ IDE support (autocomplete, refactoring)
- ✅ Self-documenting (parameter metadata visible)
- ✅ Could generate UI for parameter input

**Limitations**:
- ❌ More complex to implement
- ❌ Requires graph introspection
- ❌ Breaking change (new interface for actors)

---

### Solution 3: Dependency Injection of Parameters (Alternative)

Register parameters in DI container and inject into actors:

```csharp
// At trigger point - register parameters in DI
services.AddScoped<TenantId>(sp => new TenantId("tenant-123"));
services.AddScoped<RequestContext>(sp => new RequestContext { ... });

var context = new ExecutionContext(services, cancellationToken);
await graph.ExecuteAsync(context);

// Actor constructor injection
public class TenantAwareActor : IStreamActor<ReportData, ProcessedReport>
{
    private readonly TenantId _tenantId;
    
    public TenantAwareActor(TenantId tenantId)
    {
        _tenantId = tenantId; // Injected from DI
    }
    
    public async IAsyncEnumerable<ProcessedReport> RunAsync(...)
    {
        // Use injected tenant ID
        var tenantId = _tenantId.Value;
    }
}
```

**Benefits**:
- ✅ Strongly typed (compile-time safety)
- ✅ Standard DI pattern
- ✅ Testable (can mock dependencies)

**Limitations**:
- ❌ Actors constructed once (rotation may cause issues)
- ❌ Requires parameter types defined upfront
- ❌ Less flexible than runtime parameter passing

---

## Recommended Implementation Path

### Phase 1: Initial Implementation (Current)

Keep the current design as-is for initial implementation:
- Direct type checking in actors
- Static and dynamic trigger contexts
- Simple, working solution

**Why**: Gets feature working quickly, validates core concept.

### Phase 2: Add Parameter Provider (Near-term Enhancement)

Add `IParameterProvider` to decouple actors from specific types:
- Implement `TriggerContextParameterProvider`
- Add `Parameters` property to `IExecutionContext`
- Update examples to show both approaches

**Why**: Solves coupling problem without breaking changes.

### Phase 3: Add Parameter Metadata (Future Enhancement)

Introduce parameter descriptors and validation:
- Define `IParameterDescriptor` interface
- Add `IParameterizedActor<TIn, TOut>` interface
- Implement graph introspection
- Add pre-execution validation

**Why**: Provides compile-time safety and better developer experience.

---

## Comparison to Existing Patterns

### ASP.NET Core Model Binding

ASP.NET Core binds request data to action parameters:

```csharp
public IActionResult ProcessReport(
    [FromRoute] string tenantId,
    [FromQuery] int pageSize,
    [FromBody] ReportRequest request)
{
    // Parameters bound from different sources
}
```

**Similarities**:
- Declarative parameter requirements
- Automatic binding from context
- Validation before execution

**Adaptation for DataFlow**:
```csharp
public class TenantAwareActor : IStreamActor<ReportData, ProcessedReport>
{
    [Parameter(Name = "tenantId", Required = true)]
    public string TenantId { get; set; }
    
    public async IAsyncEnumerable<ProcessedReport> RunAsync(...)
    {
        // TenantId already bound from trigger context
    }
}
```

### Azure Functions Bindings

Azure Functions declare input/output bindings:

```csharp
[FunctionName("ProcessQueue")]
public async Task Run(
    [QueueTrigger("myqueue")] string message,
    [Table("MyTable")] IAsyncCollector<MyEntity> table,
    [Blob("container/file")] Stream blob)
{
    // Bindings resolved automatically
}
```

**Similarities**:
- Declarative bindings
- Framework resolves at runtime
- Different binding sources

### Workflow Parameters (AWS Step Functions, Azure Logic Apps)

Workflows accept parameters at invocation:

```json
{
  "parameters": {
    "tenantId": { "type": "string", "required": true },
    "reportDate": { "type": "string", "format": "date" }
  }
}
```

**Benefits**: 
- Clear contract
- Validation before execution
- Discoverable

---

## Implementation Notes for Handover

### Immediate Actions (Phase 1)

1. **Document the coupling concern** in implementation docs
2. **Add best practices** for parameter access:
   - Prefer checking parameter existence, not type
   - Use optional parameters with defaults when possible
   - Document parameter requirements in actor comments

3. **Add examples** showing defensive parameter access:

```csharp
// GOOD: Check for parameter value, not type
public async IAsyncEnumerable<ProcessedReport> RunAsync(...)
{
    string tenantId = "default";
    
    // Try multiple possible sources
    if (context.TriggerContext is ScheduledTriggerContext scheduled && 
        scheduled.TenantId != null)
    {
        tenantId = scheduled.TenantId;
    }
    else if (context.TriggerContext is JsonTriggerContext json &&
        json.Data?.ContainsKey("tenantId") == true)
    {
        tenantId = json.Data["tenantId"]?.GetValue<string>() ?? "default";
    }
    // etc...
}
```

### Future Enhancements (Phase 2+)

1. Design and implement `IParameterProvider`
2. Add parameter metadata support
3. Implement pre-execution validation
4. Create tooling for parameter discovery

---

## Conclusion

The current design is **suitable for initial implementation** with the understanding that:

1. **Known Limitation**: Actors coupled to trigger context types
   - **Mitigation**: Document best practices, plan Parameter Provider
   
2. **Known Limitation**: No pre-execution parameter validation
   - **Mitigation**: Runtime validation with clear error messages, plan metadata support

3. **Evolution Path**: Clear roadmap to more sophisticated parameter binding

**Recommendation**: Proceed with current design for Phase 1, document limitations, and plan Phase 2 Parameter Provider enhancement.

---

## References

- **ASP.NET Core Model Binding**: https://learn.microsoft.com/en-us/aspnet/core/mvc/models/model-binding
- **Azure Functions Bindings**: https://learn.microsoft.com/en-us/azure/azure-functions/functions-triggers-bindings
- **Dependency Injection Patterns**: Martin Fowler's "Inversion of Control Containers and the Dependency Injection pattern"
