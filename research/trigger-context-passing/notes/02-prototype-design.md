# Trigger Context Prototype

## Prototype Goal

Validate that Approach 1 (Extend IExecutionContext) works for all trigger scenarios.

## Code Changes

### 1. Extend IExecutionContext

```csharp
// File: /poc/DataFlow/Core/IExecutionContext.cs

public interface ITriggerContext
{
    /// <summary>
    /// Marker interface for trigger-specific context data.
    /// Implementations should be immutable and contain trigger-specific information.
    /// </summary>
}

public interface IExecutionContext
{
    CancellationToken CancellationToken { get; }
    IServiceProvider ServiceProvider { get; }
    Guid InvocationId { get; }
    ICheckpoint? RecoveryCheckpoint { get; }
    IDataFlowMetrics? Metrics { get; }
    
    /// <summary>
    /// Optional trigger context that initiated this dataflow execution.
    /// Contains trigger-specific information like tenant ID, message metadata, etc.
    /// </summary>
    ITriggerContext? TriggerContext { get; }
}
```

### 2. Update ExecutionContext implementation

```csharp
public class ExecutionContext : IExecutionContext
{
    private static readonly AsyncLocal<ExecutionContext?> _current = new();
    
    public static ExecutionContext? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }

    public ExecutionContext(
        IServiceProvider serviceProvider, 
        CancellationToken cancellationToken, 
        Guid invocationId, 
        ICheckpoint? recoveryCheckpoint,
        IDataFlowMetrics? metrics,
        ITriggerContext? triggerContext = null)  // NEW parameter
    {
        ServiceProvider = serviceProvider;
        CancellationToken = cancellationToken;
        InvocationId = invocationId;
        RecoveryCheckpoint = recoveryCheckpoint;
        Metrics = metrics;
        TriggerContext = triggerContext;  // NEW property
    }

    public CancellationToken CancellationToken { get; }
    public IServiceProvider ServiceProvider { get; }
    public Guid InvocationId { get; }
    public ICheckpoint? RecoveryCheckpoint { get; }
    public IDataFlowMetrics? Metrics { get; }
    public ITriggerContext? TriggerContext { get; }  // NEW property
}
```

### 3. Extend IActorExecutionContext

```csharp
// File: /poc/DataFlow/Core/IActorExecutionContext.cs

public interface IActorExecutionContext
{
    CancellationToken CancellationToken { get; }
    Guid InvocationId { get; }
    void RequestRotation();
    IEpochCoordinator? EpochCoordinator { get; }
    
    /// <summary>
    /// Optional trigger context that initiated this dataflow execution.
    /// Same context as available in IExecutionContext.
    /// </summary>
    ITriggerContext? TriggerContext { get; }  // NEW property
}
```

### 4. Update ActorExecutionContext implementation

```csharp
// File: /poc/DataFlow/Core/ActorExecutionContext.cs

internal sealed class ActorExecutionContext : IActorExecutionContext
{
    private CancellationToken _cancellationToken;
    private Guid _invocationId;
    private Action? _requestRotation;
    private IEpochCoordinator? _epochCoordinator;
    private ITriggerContext? _triggerContext;  // NEW field

    public CancellationToken CancellationToken => _cancellationToken;
    public Guid InvocationId => _invocationId;
    public IEpochCoordinator? EpochCoordinator => _epochCoordinator;
    public ITriggerContext? TriggerContext => _triggerContext;  // NEW property

    public void RequestRotation() => _requestRotation?.Invoke();

    internal void Reset(
        CancellationToken cancellationToken,
        Guid invocationId,
        Action requestRotation,
        IEpochCoordinator? epochCoordinator = null,
        ITriggerContext? triggerContext = null)  // NEW parameter
    {
        _cancellationToken = cancellationToken;
        _invocationId = invocationId;
        _requestRotation = requestRotation;
        _epochCoordinator = epochCoordinator;
        _triggerContext = triggerContext;  // NEW assignment
    }
}
```

### 5. Propagate in EpochActorBlock

```csharp
// File: /poc/DataFlow/Blocks/EpochActorBlock.cs

// In ExecuteAsync method, when calling context.Reset():
_context.Reset(
    context.CancellationToken,
    context.InvocationId,
    RequestActorRotation,
    context.EpochCoordinator,
    context.TriggerContext);  // NEW: propagate trigger context
```

### 6. Propagate in EpochSourceBlock

```csharp
// File: /poc/DataFlow/Blocks/EpochSourceBlock.cs

// In ExecuteAsync method, when calling context.Reset():
_context.Reset(
    context.CancellationToken,
    context.InvocationId,
    RequestActorRotation,
    epochCoordinator: null,
    context.TriggerContext);  // NEW: propagate trigger context
```

## Example Trigger Context Implementations

### Scheduled Trigger Context

```csharp
public record ScheduledTriggerContext : ITriggerContext
{
    public required string JobName { get; init; }
    public required string TenantId { get; init; }
    public required DateTime ScheduledTime { get; init; }
    public string? CronExpression { get; init; }
}
```

### Message Queue Trigger Context

```csharp
public record MessageQueueTriggerContext : ITriggerContext
{
    public required string MessageId { get; init; }
    public required string QueueName { get; init; }
    public required int DeliveryCount { get; init; }
    public DateTime EnqueuedTime { get; init; }
    public Dictionary<string, string>? MessageProperties { get; init; }
}
```

### Web Request Trigger Context

```csharp
public record WebRequestTriggerContext : ITriggerContext
{
    public required string RequestId { get; init; }
    public required string Path { get; init; }
    public required string Method { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public string? UserId { get; init; }
}
```

## Usage Examples

### Example 1: Scheduled Job with Tenant Context

```csharp
// Scheduled job trigger
var triggerContext = new ScheduledTriggerContext
{
    JobName = "DailyReportGeneration",
    TenantId = "tenant-abc-123",
    ScheduledTime = DateTime.UtcNow,
    CronExpression = "0 0 * * *"
};

var context = new ExecutionContext(
    serviceProvider,
    cancellationToken,
    Guid.NewGuid(),
    recoveryCheckpoint: null,
    metrics: null,
    triggerContext);

await graph.ExecuteAsync(context);

// Actor using tenant context
public class TenantAwareReportActor : IStreamActor<ReportData, ProcessedReport>
{
    private readonly ILogger _logger;
    
    public TenantAwareReportActor(ILogger<TenantAwareReportActor> logger)
    {
        _logger = logger;
    }
    
    public async IAsyncEnumerable<ProcessedReport> RunAsync(
        IAsyncEnumerable<ReportData> input,
        IActorExecutionContext context)
    {
        var scheduledContext = context.TriggerContext as ScheduledTriggerContext;
        var tenantId = scheduledContext?.TenantId ?? "unknown";
        
        _logger.LogInformation(
            "Processing reports for tenant {TenantId} from job {JobName}",
            tenantId,
            scheduledContext?.JobName);
        
        await foreach (var data in input)
        {
            yield return new ProcessedReport 
            { 
                TenantId = tenantId,
                Data = data 
            };
        }
    }
}
```

### Example 2: Message Queue Trigger

```csharp
// Message pulled from queue
var triggerContext = new MessageQueueTriggerContext
{
    MessageId = "msg-12345",
    QueueName = "orders-queue",
    DeliveryCount = 1,
    EnqueuedTime = DateTime.UtcNow.AddMinutes(-5),
    MessageProperties = new Dictionary<string, string>
    {
        ["TenantId"] = "tenant-xyz",
        ["Priority"] = "High"
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

// Actor with retry awareness
public class RetryAwareProcessorActor : IStreamActor<Order, ProcessedOrder>
{
    public async IAsyncEnumerable<ProcessedOrder> RunAsync(
        IAsyncEnumerable<Order> input,
        IActorExecutionContext context)
    {
        var queueContext = context.TriggerContext as MessageQueueTriggerContext;
        var isRetry = (queueContext?.DeliveryCount ?? 1) > 1;
        
        await foreach (var order in input)
        {
            if (isRetry)
            {
                // Special handling for retries
                yield return ProcessWithRetryLogic(order);
            }
            else
            {
                yield return ProcessNormally(order);
            }
        }
    }
}
```

### Example 3: Web Request Trigger

```csharp
// Web request received
var triggerContext = new WebRequestTriggerContext
{
    RequestId = "req-abc-123",
    Path = "/api/reports",
    Method = "POST",
    Headers = new Dictionary<string, string>
    {
        ["User-Agent"] = "MyClient/1.0",
        ["X-Correlation-Id"] = "corr-123"
    },
    UserId = "user-456"
};

var context = new ExecutionContext(
    serviceProvider,
    cancellationToken,
    Guid.NewGuid(),
    recoveryCheckpoint: null,
    metrics: null,
    triggerContext);

await graph.ExecuteAsync(context);

// Actor with user context awareness
public class UserAwareActor : IStreamActor<Data, ProcessedData>
{
    public async IAsyncEnumerable<ProcessedData> RunAsync(
        IAsyncEnumerable<Data> input,
        IActorExecutionContext context)
    {
        var webContext = context.TriggerContext as WebRequestTriggerContext;
        var userId = webContext?.UserId;
        var correlationId = webContext?.Headers?["X-Correlation-Id"];
        
        await foreach (var data in input)
        {
            yield return new ProcessedData
            {
                UserId = userId,
                CorrelationId = correlationId,
                Data = data
            };
        }
    }
}
```

## Validation

### Test: Trigger Context Available in Actors

```csharp
[Fact]
public async Task TriggerContext_AvailableInActor()
{
    // Arrange
    var triggerContext = new ScheduledTriggerContext
    {
        JobName = "TestJob",
        TenantId = "test-tenant",
        ScheduledTime = DateTime.UtcNow
    };
    
    var context = TestContext.Create(triggerContext: triggerContext);
    var actor = new TenantAwareReportActor(NullLogger<TenantAwareReportActor>.Instance);
    var input = TestStreams.FromItems(new ReportData { Id = 1 });
    
    // Act
    var results = await TestStreams.CollectAsync(actor.RunAsync(input, context));
    
    // Assert
    Assert.Single(results);
    Assert.Equal("test-tenant", results[0].TenantId);
}
```

### Test: Trigger Context Null When Not Provided

```csharp
[Fact]
public async Task TriggerContext_NullWhenNotProvided()
{
    // Arrange
    var context = TestContext.Create(); // No trigger context
    var actor = new TenantAwareReportActor(NullLogger<TenantAwareReportActor>.Instance);
    var input = TestStreams.FromItems(new ReportData { Id = 1 });
    
    // Act
    var results = await TestStreams.CollectAsync(actor.RunAsync(input, context));
    
    // Assert
    Assert.Single(results);
    Assert.Equal("unknown", results[0].TenantId); // Falls back to default
}
```

## Files to Modify

1. `/poc/DataFlow/Core/IExecutionContext.cs` - Add ITriggerContext interface and property
2. `/poc/DataFlow/Core/IActorExecutionContext.cs` - Add TriggerContext property
3. `/poc/DataFlow/Core/ActorExecutionContext.cs` - Add field and update Reset()
4. `/poc/DataFlow/Blocks/EpochActorBlock.cs` - Propagate trigger context
5. `/poc/DataFlow/Blocks/EpochSourceBlock.cs` - Propagate trigger context
6. `/poc/DataFlow.Tests/TestHelpers/TestContext.cs` - Add trigger context support

## Backward Compatibility

- All changes are additive (new optional parameters/properties)
- Existing code continues to work (trigger context defaults to null)
- No breaking changes to existing APIs
