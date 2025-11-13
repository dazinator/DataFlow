# Dependency Injection Integration for Epoch-Scoped Services

**Status**: Draft  
**Last Updated**: 2025-11-13

---

## Overview

This document details how epoch-scoped services integrate with the Microsoft.Extensions.DependencyInjection framework and provides patterns for common scenarios.

---

## Core Concepts

### Epoch Scope Lifetime

**Standard DI Lifetimes**:
- **Singleton**: One instance per application
- **Scoped**: One instance per DI scope
- **Transient**: New instance every time

**Epoch-Scoped Lifetime**:
- **One instance per epoch** - shared across all blocks processing that epoch
- **Disposed when epoch completes** - automatic cleanup
- **Thread-safe** - concurrent access from multiple blocks

### How It Works

```csharp
// 1. Register service as scoped in DI container
services.AddScoped<MyService>();

// 2. Epoch creates its own DI scope
var epochScope = rootServiceProvider.CreateScope();

// 3. All blocks in same epoch use the same scope
var epoch = new Epoch(vector, epochScope);

// 4. Service resolution gets scoped instance
var service = epoch.ServiceProvider.GetRequiredService<MyService>();
// Same instance for all blocks in this epoch

// 5. Epoch disposal disposes the scope
await epoch.DisposeAsync(); // Disposes epochScope and all scoped services
```

**Key Insight**: We leverage standard `Scoped` lifetime, but control **when scopes are created** (per epoch) and **when they're disposed** (when epoch completes).

---

## Registration Patterns

### Pattern 1: Simple Service Registration

```csharp
// In Startup.cs or Program.cs
public void ConfigureServices(IServiceCollection services)
{
    // Register as scoped - epoch manager will create one per epoch
    services.AddScoped<OrderProcessor>();
    services.AddScoped<InventoryTracker>();
}

// In block
public class OrderProcessingBlock : IEpochCompatibleBlock
{
    public async Task ProcessAsync(IEpoch epoch, Order order)
    {
        var processor = epoch.GetService<OrderProcessor>();
        var tracker = epoch.GetService<InventoryTracker>();
        
        await processor.ProcessAsync(order);
        await tracker.UpdateAsync(order.Items);
    }
}
```

### Pattern 2: Service with Dependencies

```csharp
// Service with dependencies
public class OrderProcessor
{
    private readonly InventoryTracker _inventory;
    private readonly ILogger<OrderProcessor> _logger;
    
    public OrderProcessor(
        InventoryTracker inventory,  // Epoch-scoped
        ILogger<OrderProcessor> logger) // Singleton
    {
        _inventory = inventory;
        _logger = logger;
    }
}

// Registration
services.AddScoped<InventoryTracker>();
services.AddScoped<OrderProcessor>();
services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

// Usage - dependencies automatically resolved
var processor = epoch.GetService<OrderProcessor>();
// InventoryTracker is epoch-scoped (same instance per epoch)
// ILogger is singleton (same instance for all epochs)
```

### Pattern 3: Factory Pattern

```csharp
// Factory interface
public interface IOrderProcessorFactory
{
    OrderProcessor Create(Order order);
}

// Factory implementation
public class OrderProcessorFactory : IOrderProcessorFactory
{
    private readonly IServiceProvider _epochServices;
    
    public OrderProcessorFactory(IServiceProvider epochServices)
    {
        _epochServices = epochServices;
    }
    
    public OrderProcessor Create(Order order)
    {
        // Resolve epoch-scoped dependencies
        var tracker = _epochServices.GetRequiredService<InventoryTracker>();
        return new OrderProcessor(order, tracker);
    }
}

// Registration
services.AddScoped<InventoryTracker>();
services.AddScoped<IOrderProcessorFactory, OrderProcessorFactory>();

// Usage
var factory = epoch.GetService<IOrderProcessorFactory>();
var processor = factory.Create(order);
```

### Pattern 4: EF Core DbContext

```csharp
// Registration
public void ConfigureServices(IServiceCollection services)
{
    services.AddDbContext<OrderDbContext>(
        options => options.UseSqlite("Data Source=orders.db"),
        ServiceLifetime.Scoped); // Key: Use Scoped lifetime
}

// Usage in multiple blocks
public class OrderIngestionBlock : IEpochCompatibleBlock
{
    public async Task ProcessAsync(IEpoch epoch, Order order)
    {
        // Get epoch-scoped DbContext
        var db = epoch.GetService<OrderDbContext>();
        
        db.Orders.Add(order);
        // Don't call SaveChanges yet - wait for epoch completion
    }
}

public class OrderValidationBlock : IEpochCompatibleBlock
{
    public async Task ProcessAsync(IEpoch epoch, Order order)
    {
        // Get THE SAME DbContext instance
        var db = epoch.GetService<OrderDbContext>();
        
        // Can access orders added by OrderIngestionBlock
        var existing = await db.Orders.FindAsync(order.Id);
    }
}

public class OrderCommitBlock : IEpochCompatibleBlock
{
    public async Task ProcessAsync(IEpoch epoch, Order order)
    {
        // Get THE SAME DbContext instance
        var db = epoch.GetService<OrderDbContext>();
        
        // Commit all changes made in this epoch
        await db.SaveChangesAsync();
    }
}
```

**Why This Works**:
- All three blocks use the same epoch
- Epoch has one DI scope
- DbContext is scoped, so same instance per scope
- All blocks see the same DbContext instance
- Changes tracked across all blocks
- One SaveChanges at the end commits everything

---

## Extension Methods

### AddEpochScoped Helper

For clarity and discoverability, provide helper extension:

```csharp
public static class EpochServiceCollectionExtensions
{
    /// <summary>
    /// Adds a service with epoch-scoped lifetime.
    /// Equivalent to AddScoped, but makes intent clear.
    /// </summary>
    public static IServiceCollection AddEpochScoped<TService, TImplementation>(
        this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        return services.AddScoped<TService, TImplementation>();
    }
    
    public static IServiceCollection AddEpochScoped<TService>(
        this IServiceCollection services)
        where TService : class
    {
        return services.AddScoped<TService>();
    }
    
    /// <summary>
    /// Adds a service with epoch-scoped lifetime using a factory.
    /// </summary>
    public static IServiceCollection AddEpochScoped<TService>(
        this IServiceCollection services,
        Func<IServiceProvider, TService> implementationFactory)
        where TService : class
    {
        return services.AddScoped(implementationFactory);
    }
}
```

**Usage**:
```csharp
// Makes intent clear - this service is epoch-scoped
services.AddEpochScoped<OrderProcessor>();
services.AddEpochScoped<IInventoryTracker, InventoryTracker>();

// Equivalent to AddScoped, but clearer for DataFlow developers
```

---

## Common Scenarios

### Scenario 1: Shared State Aggregation

**Use Case**: Multiple blocks contribute to shared state, final block aggregates.

```csharp
public class EpochMetrics
{
    public int ProcessedCount { get; set; }
    public int ErrorCount { get; set; }
    public List<string> Warnings { get; } = new();
}

// Registration
services.AddEpochScoped<EpochMetrics>();

// Block 1: Increment counter
public class ProcessingBlock : IEpochCompatibleBlock
{
    public async Task ProcessAsync(IEpoch epoch, Order order)
    {
        var metrics = epoch.GetService<EpochMetrics>();
        metrics.ProcessedCount++;
        
        if (order.HasWarnings)
        {
            metrics.Warnings.Add($"Order {order.Id} has warnings");
        }
    }
}

// Block 2: Log metrics at epoch completion
public class MetricsBlock : IEpochCompatibleBlock, IEpochLifecycleParticipant
{
    public async ValueTask OnEpochCompletedAsync(
        EpochVector epoch, 
        IBlockContext block, 
        CancellationToken ct)
    {
        var metrics = block.CurrentEpoch.GetService<EpochMetrics>();
        
        _logger.LogInformation(
            "Epoch {Epoch}: Processed {Count} orders, {Errors} errors, {Warnings} warnings",
            epoch,
            metrics.ProcessedCount,
            metrics.ErrorCount,
            metrics.Warnings.Count);
    }
}
```

### Scenario 2: Transaction Coordination

**Use Case**: All blocks participate in a single transaction, committed at epoch boundary.

```csharp
// Registration
services.AddDbContext<OrderDbContext>(ServiceLifetime.Scoped);
services.AddEpochScoped<EpochTransactionCoordinator>();

public class EpochTransactionCoordinator : IAsyncDisposable
{
    private readonly OrderDbContext _db;
    private IDbContextTransaction? _transaction;
    
    public EpochTransactionCoordinator(OrderDbContext db)
    {
        _db = db;
    }
    
    public async Task<IDbContextTransaction> GetOrBeginTransactionAsync()
    {
        if (_transaction == null)
        {
            _transaction = await _db.Database.BeginTransactionAsync();
        }
        return _transaction;
    }
    
    public async Task CommitAsync()
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync();
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_transaction != null)
        {
            await _transaction.DisposeAsync();
        }
    }
}

// Blocks automatically participate in the transaction
public class OrderBlock : IEpochCompatibleBlock
{
    public async Task ProcessAsync(IEpoch epoch, Order order)
    {
        var db = epoch.GetService<OrderDbContext>();
        var coordinator = epoch.GetService<EpochTransactionCoordinator>();
        
        // Ensure transaction started
        await coordinator.GetOrBeginTransactionAsync();
        
        // Make changes
        db.Orders.Add(order);
    }
}

// Commit at epoch completion
public class CommitBlock : IEpochLifecycleParticipant
{
    public async ValueTask OnEpochCompletedAsync(
        EpochVector epoch, 
        IBlockContext block, 
        CancellationToken ct)
    {
        var coordinator = block.CurrentEpoch.GetService<EpochTransactionCoordinator>();
        await coordinator.CommitAsync();
    }
}
```

### Scenario 3: Resource Pooling

**Use Case**: Expensive resource shared across blocks in same epoch.

```csharp
public class HttpClientPool : IAsyncDisposable
{
    private readonly HttpClient _client = new();
    private int _requestCount;
    
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        Interlocked.Increment(ref _requestCount);
        return await _client.SendAsync(request);
    }
    
    public int RequestCount => _requestCount;
    
    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
    }
}

// Registration
services.AddEpochScoped<HttpClientPool>();

// All blocks in epoch share the same HttpClient
public class OrderApiBlock : IEpochCompatibleBlock
{
    public async Task ProcessAsync(IEpoch epoch, Order order)
    {
        var pool = epoch.GetService<HttpClientPool>();
        
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders");
        request.Content = JsonContent.Create(order);
        
        var response = await pool.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}
```

---

## Testing Patterns

### Pattern 1: Mock Epoch for Unit Tests

```csharp
public class MockEpoch : IEpoch
{
    private readonly IServiceProvider _serviceProvider;
    
    public EpochVector Vector { get; }
    public IServiceProvider ServiceProvider => _serviceProvider;
    
    public MockEpoch(EpochVector vector, IServiceProvider serviceProvider)
    {
        Vector = vector;
        _serviceProvider = serviceProvider;
    }
    
    public T GetService<T>() where T : notnull
    {
        return _serviceProvider.GetRequiredService<T>();
    }
    
    public T? GetServiceOrNull<T>() where T : class
    {
        return _serviceProvider.GetService<T>();
    }
    
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

// Usage in tests
[Fact]
public async Task Block_ShouldProcessOrder()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddScoped<OrderProcessor>();
    var serviceProvider = services.BuildServiceProvider().CreateScope().ServiceProvider;
    
    var epoch = new MockEpoch(
        EpochVector.FromSingleSource("test", 1),
        serviceProvider);
    
    var block = new OrderProcessingBlock();
    
    // Act
    await block.ProcessAsync(epoch, testOrder);
    
    // Assert
    var processor = epoch.GetService<OrderProcessor>();
    Assert.Equal(1, processor.ProcessedCount);
}
```

### Pattern 2: Integration Test with Real Epoch Manager

```csharp
[Fact]
public async Task MultipleBlocks_ShouldShareEpochScopedService()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddScoped<SharedState>();
    var serviceProvider = services.BuildServiceProvider();
    
    var epochManager = new EpochManager(serviceProvider);
    var vector = EpochVector.FromSingleSource("test", 1);
    
    // Act - Two blocks access same epoch
    var epoch1 = epochManager.GetOrCreateEpoch(vector);
    var state1 = epoch1.GetService<SharedState>();
    state1.Value = 42;
    
    var epoch2 = epochManager.GetOrCreateEpoch(vector);
    var state2 = epoch2.GetService<SharedState>();
    
    // Assert
    Assert.Same(epoch1, epoch2); // Same epoch object
    Assert.Same(state1, state2); // Same service instance
    Assert.Equal(42, state2.Value); // State preserved
}
```

---

## Performance Considerations

### Scope Creation Overhead

**Benchmark Results** (estimated - to be measured in implementation):

```
Operation                          | Time (μs) | Allocations
-----------------------------------|-----------|-------------
Create DI scope                    |     5-10  |     ~1 KB
Resolve scoped service (cached)    |     0.5-1 |     ~100 B
Resolve scoped service (uncached)  |     2-5   |     ~500 B
Dispose scope                      |     3-8   |     ~200 B
```

**Impact**:
- For epochs with 100-1000 items: **< 0.01% overhead**
- Scope creation happens once per epoch
- Service resolution is very fast (dictionary lookup)

### Optimization: Lazy Scope Creation

```csharp
public class EpochManager : IEpochManager
{
    public IEpoch GetOrCreateEpoch(EpochVector vector)
    {
        return _activeEpochs.GetOrAdd(vector, v =>
        {
            // Lazy: Don't create scope until first service requested
            return new LazyEpoch(v, _rootServiceProvider);
        });
    }
}

internal sealed class LazyEpoch : IEpoch
{
    private readonly IServiceProvider _root;
    private IServiceScope? _scope;
    private readonly object _lock = new();
    
    public IServiceProvider ServiceProvider
    {
        get
        {
            if (_scope == null)
            {
                lock (_lock)
                {
                    _scope ??= _root.CreateScope();
                }
            }
            return _scope.ServiceProvider;
        }
    }
}
```

**Benefit**: Only pay scope creation cost for epochs that actually use scoped services.

---

## Error Handling

### Missing Service Registration

```csharp
public class OrderBlock : IEpochCompatibleBlock
{
    public async Task ProcessAsync(IEpoch epoch, Order order)
    {
        try
        {
            var service = epoch.GetService<OrderProcessor>();
        }
        catch (InvalidOperationException ex)
        {
            // Message: "No service for type 'OrderProcessor' has been registered."
            throw new InvalidOperationException(
                "OrderProcessor must be registered as epoch-scoped. " +
                "Add services.AddEpochScoped<OrderProcessor>() to DI configuration.",
                ex);
        }
    }
}
```

**Better**: Use `GetServiceOrNull<T>()` for optional services:

```csharp
var service = epoch.GetServiceOrNull<OptionalService>();
if (service != null)
{
    await service.ProcessAsync();
}
```

### Disposed Epoch Access

```csharp
var epoch = epochManager.GetOrCreateEpoch(vector);
var service = epoch.GetService<MyService>();

// Epoch disposed by another block
await epochManager.NotifyEpochCompletedAsync(vector, block, ct);

// Attempt to use service - undefined behavior!
await service.DoSomethingAsync(); // May throw ObjectDisposedException
```

**Mitigation**: Blocks should not retain references to epoch-scoped services beyond epoch boundaries. The framework ensures proper lifecycle through reference counting.

---

## Best Practices

### ✅ DO

1. **Register services as `Scoped`** for epoch-scoped lifetime
   ```csharp
   services.AddScoped<MyService>();
   ```

2. **Use `AddEpochScoped<T>()` for clarity**
   ```csharp
   services.AddEpochScoped<OrderProcessor>();
   ```

3. **Resolve services from epoch, not constructor**
   ```csharp
   public async Task ProcessAsync(IEpoch epoch, Order order)
   {
       var service = epoch.GetService<OrderProcessor>();
   }
   ```

4. **Dispose resources at epoch completion**
   - Let the epoch handle disposal - don't manually dispose epoch-scoped services

5. **Use epoch-scoped services for shared state**
   - Metrics, caches, transaction coordinators

### ❌ DON'T

1. **Don't retain service references beyond epoch**
   ```csharp
   // BAD - service may be disposed
   private MyService? _service;
   
   public async Task ProcessAsync(IEpoch epoch, Order order)
   {
       _service = epoch.GetService<MyService>();
   }
   ```

2. **Don't mix singleton and epoch-scoped incorrectly**
   ```csharp
   // BAD - singleton can't depend on epoch-scoped
   services.AddSingleton<MySingleton>();
   services.AddEpochScoped<MyEpochService>();
   
   public class MySingleton
   {
       public MySingleton(MyEpochService service) // ERROR!
       {
       }
   }
   ```

3. **Don't manually create DbContext**
   ```csharp
   // BAD - won't be shared across blocks
   var db = new OrderDbContext(options);
   
   // GOOD - epoch-scoped
   var db = epoch.GetService<OrderDbContext>();
   ```

4. **Don't register as Transient or Singleton**
   ```csharp
   // BAD - new instance every time
   services.AddTransient<MyService>();
   
   // BAD - same instance for ALL epochs
   services.AddSingleton<MyService>();
   
   // GOOD - one per epoch
   services.AddScoped<MyService>();
   ```

---

## Next Steps

1. Implement extension methods in Phase 4
2. Create example applications demonstrating each pattern
3. Add diagnostic analyzers to catch common mistakes
4. Performance benchmark scope creation overhead
5. Document advanced scenarios (nested scopes, etc.)
