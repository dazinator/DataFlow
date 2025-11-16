namespace EpochAnchoringDemo.Tests;

using DataFlow.POC.Core;
using EpochAnchoringDemo.Core;
using EpochAnchoringDemo.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Tests for channel-backed serialized execution of operations on epoch-scoped services.
/// 
/// The new API uses fire-and-forget semantics:
/// - QueueSerializedOperationAsync returns when operation is queued (not executed)
/// - Blocks should queue their work and continue
/// - All operations execute before epoch completes (via WhenAllOperationsCompletedAsync)
/// </summary>
public class SerializedExecutionTests
{
    [Fact]
    public async Task QueueSerializedOperation_SingleOperation_ExecutesSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<DemoDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        await using var coordinator = new EpochCoordinator(scopeFactory);
        var vector = EpochVector.FromSingleSource("test-source", 1);
        var epoch = await coordinator.GetOrCreateEpochAsync("test-source", vector);
        
        // Act - Queue a single operation (returns immediately)
        await epoch.QueueSerializedOperationAsync<DemoDbContext>(async db =>
        {
            var record = new DataRecord { Name = "Test Record" };
            db.DataRecords.Add(record);
            await db.SaveChangesAsync();
        });
        
        // Wait for all operations to complete
        await epoch.WhenAllOperationsCompletedAsync();
        
        // Assert - Verify the record was saved
        var dbContext = epoch.GetService<DemoDbContext>();
        var count = await dbContext.DataRecords.CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task QueueSerializedOperation_ConcurrentOperations_ExecuteInOrder()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<DemoDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        await using var coordinator = new EpochCoordinator(scopeFactory);
        var vector = EpochVector.FromSingleSource("test-source", 1);
        var epoch = await coordinator.GetOrCreateEpochAsync("test-source", vector);
        
        const int operationCount = 100;
        var tasks = new List<Task>();
        
        // Act - Queue many concurrent operations (all return immediately)
        for (int i = 0; i < operationCount; i++)
        {
            var index = i;
            var task = epoch.QueueSerializedOperationAsync<DemoDbContext>(async db =>
            {
                var record = new DataRecord { Name = $"Record {index}" };
                db.DataRecords.Add(record);
                await db.SaveChangesAsync();
            });
            tasks.Add(task);
        }
        
        // All queue operations should complete quickly (just queuing, not executing)
        await Task.WhenAll(tasks);
        
        // Wait for all operations to actually execute
        await epoch.WhenAllOperationsCompletedAsync();
        
        // Assert - All records saved
        var dbContext = epoch.GetService<DemoDbContext>();
        var count = await dbContext.DataRecords.CountAsync();
        Assert.Equal(operationCount, count);
    }

    [Fact]
    public async Task QueueSerializedOperation_MultipleConcurrentBlocks_ShareSameDbContext()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<DemoDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        await using var coordinator = new EpochCoordinator(scopeFactory);
        var vector = EpochVector.FromSingleSource("test-source", 1);
        var epoch = await coordinator.GetOrCreateEpochAsync("test-source", vector);
        
        var contextIds = new List<int>();
        var tasks = new List<Task>();
        
        // Act - Multiple blocks queue operations
        for (int i = 0; i < 10; i++)
        {
            var task = epoch.QueueSerializedOperationAsync<DemoDbContext>(async db =>
            {
                var contextId = db.GetHashCode();
                lock (contextIds)
                {
                    contextIds.Add(contextId);
                }
                await Task.Yield();
            });
            tasks.Add(task);
        }
        
        await Task.WhenAll(tasks);
        await epoch.WhenAllOperationsCompletedAsync();
        
        // Assert - All operations used the same DbContext instance
        Assert.Equal(10, contextIds.Count);
        var uniqueContextId = contextIds.First();
        Assert.All(contextIds, id => Assert.Equal(uniqueContextId, id));
    }

    [Fact]
    public async Task QueueSerializedOperation_DifferentServiceTypes_UseDifferentExecutors()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<DemoDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        services.AddScoped<TestService>();
        
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        await using var coordinator = new EpochCoordinator(scopeFactory);
        var vector = EpochVector.FromSingleSource("test-source", 1);
        var epoch = await coordinator.GetOrCreateEpochAsync("test-source", vector);
        
        var dbContextId = 0;
        var testServiceId = 0;
        
        // Act - Queue operations on different service types concurrently
        var task1 = epoch.QueueSerializedOperationAsync<DemoDbContext>(async db =>
        {
            dbContextId = db.GetHashCode();
            await Task.Delay(50);
        });
        
        var task2 = epoch.QueueSerializedOperationAsync<TestService>(async svc =>
        {
            testServiceId = svc.GetHashCode();
            await Task.Delay(50);
        });
        
        await Task.WhenAll(task1, task2);
        await epoch.WhenAllOperationsCompletedAsync();
        
        // Assert - Different service types have different hash codes
        Assert.NotEqual(0, dbContextId);
        Assert.NotEqual(0, testServiceId);
        Assert.NotEqual(dbContextId, testServiceId);
    }

    [Fact]
    public async Task QueueSerializedOperation_EpochDisposal_CompletesAllOperations()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<DemoDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        var coordinator = new EpochCoordinator(scopeFactory);
        var vector = EpochVector.FromSingleSource("test-source", 1);
        var epoch = await coordinator.GetOrCreateEpochAsync("test-source", vector);
        
        var operationCompleted = false;
        
        // Act - Queue an operation
        await epoch.QueueSerializedOperationAsync<DemoDbContext>(async db =>
        {
            await Task.Delay(100);
            operationCompleted = true;
        });
        
        // Dispose the epoch (should wait for operation to complete)
        await epoch.DisposeAsync();
        
        // Assert - Operation completed before disposal
        Assert.True(operationCompleted);
        await coordinator.DisposeAsync();
    }

    [Fact]
    public async Task QueueSerializedOperation_OrderPreservation_MaintainsFIFO()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<OrderTracker>();
        
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        await using var coordinator = new EpochCoordinator(scopeFactory);
        var vector = EpochVector.FromSingleSource("test-source", 1);
        var epoch = await coordinator.GetOrCreateEpochAsync("test-source", vector);
        
        const int operationCount = 50;
        var tasks = new List<Task>();
        
        // Act - Queue operations in order
        for (int i = 0; i < operationCount; i++)
        {
            var index = i;
            var task = epoch.QueueSerializedOperationAsync<OrderTracker>(async tracker =>
            {
                tracker.RecordOperation(index);
                await Task.Yield();
            });
            tasks.Add(task);
        }
        
        await Task.WhenAll(tasks);
        await epoch.WhenAllOperationsCompletedAsync();
        
        // Assert - Operations executed in FIFO order
        var tracker = epoch.GetService<OrderTracker>();
        Assert.Equal(operationCount, tracker.Operations.Count);
        for (int i = 0; i < operationCount; i++)
        {
            Assert.Equal(i, tracker.Operations[i]);
        }
    }

    [Fact]
    public async Task QueueSerializedOperation_CancellationDuringExecution_OperationCancelled()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<DemoDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        await using var coordinator = new EpochCoordinator(scopeFactory);
        var vector = EpochVector.FromSingleSource("test-source", 1);
        var epoch = await coordinator.GetOrCreateEpochAsync("test-source", vector);
        
        using var cts = new CancellationTokenSource();
        var operationStarted = false;
        var operationCompleted = false;
        
        // Act - Queue operation with cancellation token
        await epoch.QueueSerializedOperationAsync<DemoDbContext>(async db =>
        {
            operationStarted = true;
            await Task.Delay(1000, cts.Token);
            operationCompleted = true;
        }, cts.Token);
        
        // Give operation time to start
        await Task.Delay(100);
        
        // Cancel the operation
        cts.Cancel();
        
        // Wait for operations to complete
        await epoch.WhenAllOperationsCompletedAsync();
        
        // Assert - Operation started but didn't complete
        Assert.True(operationStarted);
        Assert.False(operationCompleted);
    }

    private class TestService
    {
        public int Value { get; set; }
    }

    private class OrderTracker
    {
        public List<int> Operations { get; } = new();

        public void RecordOperation(int operationId)
        {
            Operations.Add(operationId);
        }
    }
}
