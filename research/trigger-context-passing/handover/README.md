# Implementation Handover: Trigger Context Passing

**Research Reference**: [Research README](../README.md)

---

## Implementation Work Item

Use this as the template for creating the implementation work item.

---

### Title

Implement Trigger Context Passing for Dataflows

---

### Description

## Implementation: Trigger Context Passing

**Research Reference**: #[RESEARCH_ISSUE_NUMBER]  
**Research Documentation**: `/research/trigger-context-passing/`

---

## Objective

Implement trigger context passing to allow dataflows to access trigger-specific information (e.g., tenant ID, message metadata, request details) during execution.

This enables dataflows to be executed from different trigger sources (scheduled jobs, message queues, web requests) with appropriate context.

---

## Approach (Validated by Research)

Extend `IExecutionContext` and `IActorExecutionContext` with an optional `ITriggerContext` property:

```csharp
public interface ITriggerContext { }

public interface IExecutionContext
{
    // ... existing properties ...
    ITriggerContext? TriggerContext { get; }
}

public interface IActorExecutionContext
{
    // ... existing properties ...
    ITriggerContext? TriggerContext { get; }
}
```

**Why this approach**:
- ✅ Works with all block types (including EpochSourceBlocks)
- ✅ Global invariant - available everywhere
- ✅ No AsyncLocal reliability issues
- ✅ Backward compatible
- ✅ Validated via working prototype with passing tests

---

## Known Limitations and Future Enhancements

### Design Limitations (Phase 1)

This implementation has two known limitations documented during research review:

#### Limitation 1: Actor Coupling to Trigger Context Types

Actors must check for specific trigger context types, creating tight coupling:

```csharp
// Actors check specific types
if (context.TriggerContext is ScheduledTriggerContext scheduled)
    tenantId = scheduled.TenantId;
else if (context.TriggerContext is JsonTriggerContext json)
    tenantId = json.Data?["tenantId"]?.GetValue<string>();
```

**Impact**: Adding new trigger types may require updating actors.

**Mitigation**:
- Use `JsonTriggerContext` for flexible scenarios
- Document parameter requirements in actor comments
- Plan Parameter Provider pattern (Phase 2)

#### Limitation 2: No Pre-Execution Parameter Validation

No way to validate required parameters before execution:

```csharp
// Runtime failure if parameter missing
var context = new ExecutionContext(..., triggerContext);
await graph.ExecuteAsync(context); // May fail if actor needs missing parameter
```

**Impact**: Parameters validated only at runtime.

**Mitigation**:
- Clear error messages in actors
- Document parameter requirements
- Plan parameter metadata support (Phase 3)

### Best Practices for Phase 1

#### For Actor Developers

**1. Check for parameter availability, not type:**

```csharp
// GOOD: Defensive with defaults
string tenantId = "default";

if (context.TriggerContext is ScheduledTriggerContext scheduled && 
    scheduled.TenantId != null)
{
    tenantId = scheduled.TenantId;
}
else if (context.TriggerContext is MessageQueueTriggerContext queue &&
    queue.MessageProperties?.ContainsKey("tenantId") == true)
{
    tenantId = queue.MessageProperties["tenantId"];
}
else if (context.TriggerContext is JsonTriggerContext json &&
    json.Data?.ContainsKey("tenantId") == true)
{
    tenantId = json.Data["tenantId"]?.GetValue<string>() ?? "default";
}
```

**2. Document parameter requirements:**

```csharp
/// <summary>
/// Processes reports with tenant-specific logic.
/// </summary>
/// <remarks>
/// <para><b>Required Parameters:</b></para>
/// <list type="bullet">
/// <item><term>tenantId</term><description>String - Tenant identifier</description></item>
/// </list>
/// <para><b>Optional Parameters:</b></para>
/// <list type="bullet">
/// <item><term>reportDate</term><description>DateTime - Report generation date</description></item>
/// </list>
/// </remarks>
public class TenantAwareActor : IStreamActor<ReportData, ProcessedReport>
{
    // Implementation
}
```

**3. Validate parameters early:**

```csharp
public async IAsyncEnumerable<ProcessedReport> RunAsync(
    IAsyncEnumerable<ReportData> input,
    IActorExecutionContext context)
{
    // Validate required parameters upfront
    string tenantId = ExtractTenantId(context.TriggerContext);
    
    if (string.IsNullOrEmpty(tenantId))
    {
        throw new InvalidOperationException(
            "TenantId is required. Please provide it in trigger context.");
    }
    
    await foreach (var data in input)
    {
        yield return new ProcessedReport { TenantId = tenantId, Data = data };
    }
}
```

#### For Dataflow Callers

**1. Use JsonTriggerContext for maximum flexibility:**

```csharp
// Flexible - works with any actor expecting these parameters
var triggerContext = new JsonTriggerContext
{
    Data = new JsonObject
    {
        ["tenantId"] = "tenant-123",
        ["reportDate"] = JsonValue.Create(DateTime.UtcNow)
    }
};
```

**2. Document trigger requirements:**

```csharp
/// <summary>
/// Executes daily report generation for a tenant.
/// </summary>
/// <param name="tenantId">Required. Tenant identifier.</param>
/// <param name="reportDate">Optional. Report date (defaults to today).</param>
public async Task ExecuteDailyReport(string tenantId, DateTime? reportDate = null)
{
    var triggerContext = new ScheduledTriggerContext
    {
        JobName = "DailyReport",
        TenantId = tenantId,
        ScheduledTime = reportDate ?? DateTime.UtcNow
    };
    
    var context = new ExecutionContext(services, cancellationToken, 
        Guid.NewGuid(), null, null, triggerContext);
    
    await graph.ExecuteAsync(context);
}
```

### Future Enhancement Roadmap

**Phase 2: Parameter Provider Pattern**

Decouple actors from specific trigger context types:

```csharp
// Future: Parameter provider abstraction
public interface IParameterProvider
{
    bool TryGetParameter<T>(string name, out T? value);
    T GetRequiredParameter<T>(string name);
}

// Actor usage
var tenantId = context.Parameters.GetRequiredParameter<string>("tenantId");
```

**Phase 3: Parameter Metadata & Validation**

Add compile-time parameter declarations and pre-execution validation:

```csharp
// Future: Declare requirements
public class TenantAwareActor : IParameterizedActor<ReportData, ProcessedReport>
{
    public IReadOnlyList<IParameterDescriptor> GetParameterDescriptors()
    {
        return new[]
        {
            new ParameterDescriptor<string>("tenantId", required: true)
        };
    }
}

// Pre-execution validation
var requiredParams = graph.GetRequiredParameters();
ValidateParameters(requiredParams, triggerContext); // Fails fast if missing
await graph.ExecuteAsync(context);
```

**See**: [`/research/trigger-context-passing/design/advanced-considerations.md`](../design/advanced-considerations.md) for complete analysis and patterns.

---

## Success Criteria

- [ ] `ITriggerContext` interface added to `/poc/DataFlow/Core/IExecutionContext.cs`
- [ ] `IExecutionContext.TriggerContext` property added
- [ ] `IActorExecutionContext.TriggerContext` property added
- [ ] `ActorExecutionContext` implementation updated
- [ ] Trigger context propagated in `EpochActorBlock`
- [ ] Trigger context propagated in `EpochSourceBlock`
- [ ] Example trigger context implementations provided
- [ ] Test helpers updated to support trigger context
- [ ] All existing tests continue to pass
- [ ] New tests added validating trigger context functionality
- [ ] Documentation updated
- [ ] No performance regression

---

## Test Scenarios

Research prototype included comprehensive tests. Implementation should include:

### 1. Scheduled Job Context
```csharp
[Fact]
public async Task TriggerContext_ScheduledJob_AvailableInActor()
{
    // Validate tenant-aware actor can access scheduled job context
    var triggerContext = new ScheduledTriggerContext { TenantId = "tenant-123" };
    // ... test implementation
}
```

### 2. Message Queue Context
```csharp
[Fact]
public async Task TriggerContext_MessageQueue_RetryLogicWorks()
{
    // Validate actors can access delivery count for retry logic
    var triggerContext = new MessageQueueTriggerContext { DeliveryCount = 2 };
    // ... test implementation
}
```

### 3. Null Context Handling
```csharp
[Fact]
public async Task TriggerContext_Null_ActorHandlesGracefully()
{
    // Validate actors handle null trigger context gracefully
    // ... test implementation
}
```

### 4. Web Request Context
```csharp
[Fact]
public async Task TriggerContext_WebRequest_PropertiesAccessible()
{
    // Validate all web request properties accessible
    var triggerContext = new WebRequestTriggerContext { UserId = "user-123" };
    // ... test implementation
}
```

### 5. Dynamic JSON Context
```csharp
[Fact]
public async Task TriggerContext_JsonDynamic_PropertiesAccessible()
{
    // Validate dynamic JSON-based trigger context works
    var triggerContext = new JsonTriggerContext
    {
        Data = new JsonObject
        {
            ["tenantId"] = "tenant-123",
            ["customProperty"] = JsonValue.Create(42)
        }
    };
    // ... test implementation
}
```

### 4. Web Request Context
```csharp
[Fact]
public async Task TriggerContext_WebRequest_PropertiesAccessible()
{
    // Validate all web request properties accessible
    var triggerContext = new WebRequestTriggerContext { UserId = "user-123" };
    // ... test implementation
}
```

---

## Performance Requirements

- No measurable performance overhead (< 1% in benchmarks)
- Memory overhead: Single nullable reference per execution context
- All existing benchmarks continue to pass

---

## Design References

- **Research**: `/research/trigger-context-passing/README.md`
- **Architecture Analysis**: `/research/trigger-context-passing/notes/01-architecture-analysis.md`
- **Approach Comparison**: `/research/trigger-context-passing/design/approach-comparison.md`
- **Prototype Design**: `/research/trigger-context-passing/notes/02-prototype-design.md`
- **Prototype Code**: See research commit (code was reverted, saved in `/research/trigger-context-passing/handover/prototype/`)

---

## Implementation Checklist

### Phase 1: Core Interfaces
- [ ] Add `ITriggerContext` marker interface to `IExecutionContext.cs`
- [ ] Add `TriggerContext` property to `IExecutionContext`
- [ ] Add `TriggerContext` property to `IActorExecutionContext`
- [ ] Update `ExecutionContext` implementation with new property
- [ ] Update `ExecutionContext` constructors (maintain backward compatibility)
- [ ] Update `ActorExecutionContext` implementation with new field
- [ ] Update `ActorExecutionContext.Reset()` method signature and implementation

### Phase 2: Block Propagation
- [ ] Update `EpochActorBlock` to propagate trigger context in `Reset()` call
- [ ] Update `EpochSourceBlock` to propagate trigger context in `InitializeActorContext()`
- [ ] Verify all other blocks don't need changes (research indicates only these two)

### Phase 3: Example Implementations
- [ ] Create `TriggerContexts.cs` with example implementations:
  - [ ] `JsonTriggerContext` (dynamic JSON-based for flexible scenarios)
  - [ ] `ScheduledTriggerContext`
  - [ ] `MessageQueueTriggerContext`
  - [ ] `WebRequestTriggerContext`

### Phase 4: Test Infrastructure
- [ ] Update `TestExecutionContextBase` to include `TriggerContext` property
- [ ] Update `TestContext.CreateExecution()` to accept optional trigger context
- [ ] Update `TestContext.CreateActor()` to accept optional trigger context
- [ ] Update `SimpleActorExecutionContext` to include `TriggerContext` property
- [ ] Fix all existing `TestExecutionContext` implementations in test files

### Phase 5: Tests
- [ ] Create `TriggerContextTests.cs` with comprehensive tests
- [ ] Validate scheduled job context test
- [ ] Validate message queue context test
- [ ] Validate null context handling test
- [ ] Validate web request context test
- [ ] Validate dynamic JSON context test
- [ ] Validate null context handling test
- [ ] Validate web request context test
- [ ] Run all existing tests to ensure no regressions

### Phase 6: Documentation
- [ ] Update user guide with trigger context examples
- [ ] Document common trigger scenarios
- [ ] Add migration guide for existing code
- [ ] Update API documentation
- [ ] **Update `/poc/docs/guides/getting-started.md`** to include:
  - Static typing scenarios (using `ScheduledTriggerContext`, `MessageQueueTriggerContext`, `WebRequestTriggerContext`)
  - Dynamic typing scenarios (using `JsonTriggerContext` for flexible/runtime-determined trigger data)
  - Examples showing both approaches and when to use each

### Phase 7: Validation
- [ ] Run full test suite - all tests pass
- [ ] Run benchmarks - no performance regression
- [ ] Code review
- [ ] Final validation

---

## Files to Modify

| File | Change Type | Description |
|------|-------------|-------------|
| `/poc/DataFlow/Core/IExecutionContext.cs` | Modify | Add ITriggerContext interface and property |
| `/poc/DataFlow/Core/IActorExecutionContext.cs` | Modify | Add TriggerContext property |
| `/poc/DataFlow/Core/ActorExecutionContext.cs` | Modify | Add field and update Reset() method |
| `/poc/DataFlow/Core/TriggerContexts.cs` | Create | Example trigger context implementations |
| `/poc/DataFlow/Blocks/EpochActorBlock.cs` | Modify | Propagate trigger context in Reset() call |
| `/poc/DataFlow/Blocks/EpochSourceBlock.cs` | Modify | Propagate trigger context in InitializeActorContext() |
| `/poc/DataFlow.Tests/TestHelpers/TestContext.cs` | Modify | Support trigger context parameter |
| `/poc/DataFlow.Tests/TestHelpers/TestExecutionContextBase.cs` | Modify | Add TriggerContext property |
| `/poc/DataFlow.Tests/TriggerContextTests.cs` | Create | Comprehensive tests |
| Various test files | Modify | Fix TestExecutionContext implementations |

---

## Breaking Changes

**Minor breaking change**: New property on interfaces

**Mitigation**:
- Property is optional (nullable)
- Constructors use method overloading for backward compatibility
- Existing code continues to work (null trigger context)

**Impact**: Minimal - existing tests pass without modification (except test helper classes)

---

## Migration Guide

### For Existing Dataflows

**No changes required** - existing dataflows continue to work:

```csharp
// Existing code - still works
var context = new ExecutionContext(serviceProvider, cancellationToken);
await graph.ExecuteAsync(context);
```

### For New Dataflows with Trigger Context

#### Static Typing (Recommended for known structure)

```csharp
// Static typing - with strongly-typed trigger context
var triggerContext = new ScheduledTriggerContext
{
    JobName = "DailyReport",
    TenantId = "tenant-123",
    ScheduledTime = DateTime.UtcNow
};

var context = new ExecutionContext(
    serviceProvider,
    cancellationToken,
    Guid.NewGuid(),
    recoveryCheckpoint: null,
    metrics: null,
    triggerContext);  // NEW parameter

await graph.ExecuteAsync(context);
```

#### Dynamic Typing (For flexible/runtime-determined data)

```csharp
// Dynamic typing - with JSON-based trigger context
var triggerContext = new JsonTriggerContext
{
    Data = new JsonObject
    {
        ["tenantId"] = "tenant-123",
        ["jobName"] = "DailyReport",
        ["customProperty"] = JsonValue.Create(42),
        ["metadata"] = new JsonObject
        {
            ["source"] = "scheduler",
            ["priority"] = "high"
        }
    }
};

var context = new ExecutionContext(
    serviceProvider,
    cancellationToken,
    Guid.NewGuid(),
    recoveryCheckpoint: null,
    metrics: null,
    triggerContext);

await graph.ExecuteAsync(context);

// Can also create from JSON string
var triggerContext2 = JsonTriggerContext.FromJson(@"{
    ""tenantId"": ""tenant-456"",
    ""customData"": ""value""
}");
```

### For Actors

#### Accessing Static Trigger Context

```csharp
public class MyActor : IStreamActor<int, string>
{
    public async IAsyncEnumerable<string> RunAsync(
        IAsyncEnumerable<int> input,
        IActorExecutionContext context)
    {
        // Access strongly-typed trigger context
        if (context.TriggerContext is ScheduledTriggerContext scheduled)
        {
            var tenantId = scheduled.TenantId;
            // Use tenant ID in processing
        }
        
        await foreach (var item in input)
        {
            yield return ProcessItem(item);
        }
    }
}
```

#### Accessing Dynamic Trigger Context

```csharp
public class MyDynamicActor : IStreamActor<int, string>
{
    public async IAsyncEnumerable<string> RunAsync(
        IAsyncEnumerable<int> input,
        IActorExecutionContext context)
    {
        // Access JSON-based dynamic trigger context
        if (context.TriggerContext is JsonTriggerContext jsonContext)
        {
            var tenantId = jsonContext.Data?["tenantId"]?.GetValue<string>();
            var customProperty = jsonContext.Data?["customProperty"]?.GetValue<int>();
            
            // Access nested properties
            var source = jsonContext.Data?["metadata"]?["source"]?.GetValue<string>();
            
            // Use dynamic values in processing
        }
        
        await foreach (var item in input)
        {
            yield return ProcessItem(item);
        }
    }
}
```

---

## Acceptance Criteria

- [ ] All files modified as specified
- [ ] All tests passing (existing + new)
- [ ] No performance regression in benchmarks
- [ ] Documentation updated
- [ ] Code reviewed and approved
- [ ] Research findings validated in production code

---

## Questions?

See research documentation for complete details:
- `/research/trigger-context-passing/README.md`
- `/research/trigger-context-passing/design/approach-comparison.md`

Contact research team if clarification needed.

---

**Research Status**: ✅ Complete  
**Implementation Status**: ⏳ Ready to Start
