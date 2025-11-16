namespace EpochAnchoringDemo.Tests;

using DataFlow.POC.Core;
using EpochAnchoringDemo.Core;
using EpochAnchoringDemo.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Integration tests demonstrating real-world scenarios with fire-and-forget serialized execution.
/// </summary>
public class EpochTransactionIntegrationTests
{
    [Fact]
    public async Task MultipleBlocks_CanParticipateInSingleEpochTransaction()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<DemoDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        await using var coordinator = new EpochCoordinator(scopeFactory);
        var vector = EpochVector.FromSingleSource("order-source", 1);
        var epoch = await coordinator.GetOrCreateEpochAsync("order-source", vector);
        
        // Simulate multiple blocks processing orders concurrently
        var tasks = new List<Task>();
        
        // Block 1: Create orders (fire-and-forget)
        for (int i = 1; i <= 5; i++)
        {
            var orderId = i;
            var task = epoch.QueueSerializedOperationAsync<DemoDbContext>(async db =>
            {
                var record = new DataRecord
                {
                    Name = $"Order {orderId}",
                    Processed = false
                };
                db.DataRecords.Add(record);
                await db.SaveChangesAsync();
            });
            tasks.Add(task);
        }
        
        // Block 2: Mark orders as processed (also fire-and-forget, will execute after block 1)
        for (int i = 1; i <= 5; i++)
        {
            var task = epoch.QueueSerializedOperationAsync<DemoDbContext>(async db =>
            {
                var records = db.DataRecords.Where(r => !r.Processed).ToList();
                if (records.Any())
                {
                    records.First().Processed = true;
                    await db.SaveChangesAsync();
                }
            });
            tasks.Add(task);
        }
        
        // All queue operations return immediately (fire-and-forget)
        await Task.WhenAll(tasks);
        
        // Wait for all operations to actually execute
        await epoch.WhenAllOperationsCompletedAsync();
        
        // Assert - All operations completed
        var dbContext = epoch.GetService<DemoDbContext>();
        var totalRecords = await dbContext.DataRecords.CountAsync();
        var processedRecords = await dbContext.DataRecords.CountAsync(r => r.Processed);
        
        Assert.Equal(5, totalRecords);
        Assert.Equal(5, processedRecords);
    }

    [Fact]
    public async Task SerializedExecution_MaintainsTransactionalConsistency()
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
        
        var orderIdCapture = new int[1]; // Use array for closure capture
        
        // Act - Simulate a saga-like pattern with multiple steps (all fire-and-forget)
        // Step 1: Create order header
        await epoch.QueueSerializedOperationAsync<DemoDbContext>(async db =>
        {
            var order = new DataRecord
            {
                Name = "Customer Order",
                Processed = false
            };
            db.DataRecords.Add(order);
            await db.SaveChangesAsync();
            orderIdCapture[0] = order.Id;
        });
        
        // Step 2: Add line items (fire-and-forget, will execute after step 1)
        var lineItemTasks = Enumerable.Range(1, 10).Select(async lineNumber =>
        {
            await epoch.QueueSerializedOperationAsync<DemoDbContext>(async db =>
            {
                // OrderId will be set by the time this executes (serialized)
                var order = await db.DataRecords.FindAsync(orderIdCapture[0]);
                if (order != null)
                {
                    order.Name += $" +Item{lineNumber}";
                    await db.SaveChangesAsync();
                }
            });
        });
        
        await Task.WhenAll(lineItemTasks);
        
        // Step 3: Finalize order
        await epoch.QueueSerializedOperationAsync<DemoDbContext>(async db =>
        {
            var order = await db.DataRecords.FindAsync(orderIdCapture[0]);
            if (order != null)
            {
                order.Processed = true;
                await db.SaveChangesAsync();
            }
        });
        
        // Wait for all operations to execute
        await epoch.WhenAllOperationsCompletedAsync();
        
        // Assert - All changes visible
        var dbContext = epoch.GetService<DemoDbContext>();
        var finalOrder = await dbContext.DataRecords.FindAsync(orderIdCapture[0]);
        
        Assert.NotNull(finalOrder);
        Assert.True(finalOrder.Processed);
        Assert.Contains("+Item1", finalOrder.Name);
        Assert.Contains("+Item10", finalOrder.Name);
    }

    [Fact]
    public async Task DifferentEpochs_HaveIsolatedTransactions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<DemoDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        await using var coordinator = new EpochCoordinator(scopeFactory);
        
        // Create two different epochs
        var vector1 = EpochVector.FromSingleSource("source", 1);
        var epoch1 = await coordinator.GetOrCreateEpochAsync("source", vector1);
        
        coordinator.SignalReadyForNext("source", vector1, EpochVector.FromSingleSource("source", 2));
        
        var vector2 = EpochVector.FromSingleSource("source", 2);
        var epoch2 = await coordinator.GetOrCreateEpochAsync("source", vector2);
        
        // Act - Add records in different epochs
        await epoch1.QueueSerializedOperationAsync<DemoDbContext>(async db =>
        {
            var record = new DataRecord { Name = "Epoch 1 Record" };
            db.DataRecords.Add(record);
            await db.SaveChangesAsync();
        });
        
        await epoch2.QueueSerializedOperationAsync<DemoDbContext>(async db =>
        {
            var record = new DataRecord { Name = "Epoch 2 Record" };
            db.DataRecords.Add(record);
            await db.SaveChangesAsync();
        });
        
        await epoch1.WhenAllOperationsCompletedAsync();
        await epoch2.WhenAllOperationsCompletedAsync();
        
        // Assert - Each epoch has its own DbContext instance
        var db1 = epoch1.GetService<DemoDbContext>();
        var db2 = epoch2.GetService<DemoDbContext>();
        
        Assert.NotSame(db1, db2);
        
        // Both records exist in the shared in-memory database
        var count1 = await db1.DataRecords.CountAsync(r => r.Name.Contains("Epoch 1"));
        var count2 = await db2.DataRecords.CountAsync(r => r.Name.Contains("Epoch 2"));
        
        Assert.Equal(1, count1);
        Assert.Equal(1, count2);
    }

    [Fact]
    public async Task ConcurrentBlocks_WithSerializedAccess_NoDeadlocks()
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
        
        // Act - Simulate heavy concurrent load
        const int blockCount = 20;
        const int operationsPerBlock = 10;
        var tasks = new List<Task>();
        
        for (int block = 0; block < blockCount; block++)
        {
            var blockId = block;
            var task = Task.Run(async () =>
            {
                for (int op = 0; op < operationsPerBlock; op++)
                {
                    await epoch.QueueSerializedOperationAsync<DemoDbContext>(async db =>
                    {
                        var record = new DataRecord
                        {
                            Name = $"Block{blockId}-Op{op}"
                        };
                        db.DataRecords.Add(record);
                        await db.SaveChangesAsync();
                    });
                }
            });
            tasks.Add(task);
        }
        
        // All queue operations complete (fire-and-forget)
        await Task.WhenAll(tasks);
        
        // Wait for all operations to execute
        await epoch.WhenAllOperationsCompletedAsync();
        
        // Assert - All records saved
        var dbContext = epoch.GetService<DemoDbContext>();
        var count = await dbContext.DataRecords.CountAsync();
        
        Assert.Equal(blockCount * operationsPerBlock, count);
    }
}
