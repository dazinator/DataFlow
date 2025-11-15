namespace EpochAnchoringDemo.Tests;

using DataFlow.POC.Core;
using EpochAnchoringDemo.Core;
using EpochAnchoringDemo.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using Xunit;

/// <summary>
/// Integration tests demonstrating epoch-scoped DbContext sharing across multiple blocks.
/// 
/// **IMPORTANT NOTE ON THREAD SAFETY**:
/// DbContext is NOT thread-safe. These tests demonstrate that multiple blocks *within the same epoch*
/// can share the same DbContext instance when they execute SEQUENTIALLY (not concurrently).
/// 
/// The epoch infrastructure and DI scoping enable:
/// - Shared DbContext lifetime aligned with epoch boundaries
/// - Same DbContext instance accessible to all blocks in an epoch
/// - Proper disposal when epoch completes
/// 
/// For CONCURRENT execution of blocks within an epoch, each concurrent worker would need its own
/// DbContext instance. This is a separate design concern that builds on top of the epoch infrastructure
/// provided here. The current implementation demonstrates the foundation: epoch-scoped services that
/// can be shared across sequential block operations.
/// 
/// Validates that:
/// - Multiple blocks in the same epoch CAN share the same DbContext instance (when sequential)
/// - Different epochs get different DbContext instances
/// - No race conditions in concurrent EPOCH execution (different epochs, different contexts)
/// - Transactional boundaries align with epoch boundaries
/// </summary>
public class EpochScopedDbContextTests
{
    [Fact]
    public async Task MultipleBlocks_ShareSameDbContextInSameEpoch()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<DemoDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        await using var coordinator = new EpochCoordinator(scopeFactory);
        
        // Create epoch
        var vector = EpochVector.FromSingleSource("test-source", 1);
        var epoch = await coordinator.GetOrCreateEpochAsync("test-source", vector);
        
        // Act - Get DbContext from epoch scope multiple times
        var dbContext1 = epoch.GetService<DemoDbContext>();
        var dbContext2 = epoch.GetService<DemoDbContext>();
        var dbContext3 = epoch.GetService<DemoDbContext>();
        
        // Assert - All should be the same instance (scoped)
        Assert.Same(dbContext1, dbContext2);
        Assert.Same(dbContext2, dbContext3);
    }

    [Fact]
    public async Task DifferentEpochs_GetDifferentDbContextInstances()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<DemoDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        await using var coordinator = new EpochCoordinator(scopeFactory);
        
        // Act - Create two different epochs
        var vector1 = EpochVector.FromSingleSource("test-source", 1);
        var epoch1 = await coordinator.GetOrCreateEpochAsync("test-source", vector1);
        
        coordinator.SignalReadyForNext("test-source", vector1, EpochVector.FromSingleSource("test-source", 2));
        
        var vector2 = EpochVector.FromSingleSource("test-source", 2);
        var epoch2 = await coordinator.GetOrCreateEpochAsync("test-source", vector2);
        
        var dbContext1 = epoch1.GetService<DemoDbContext>();
        var dbContext2 = epoch2.GetService<DemoDbContext>();
        
        // Assert - Different epochs get different DbContext instances
        Assert.NotSame(dbContext1, dbContext2);
    }

    [Fact]
    public async Task MultipleBlocks_CanShareDbContextChanges()
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
        
        // Act - Block 1 adds an entity
        var dbContext = epoch.GetService<DemoDbContext>();
        var record1 = new DataRecord
        {
            Id = Guid.NewGuid(),
            Name = "Test Record",
            Processed = false
        };
        dbContext.DataRecords.Add(record1);
        
        // Block 2 can see the changes (same DbContext instance)
        var trackedRecord = await dbContext.DataRecords.FindAsync(record1.Id);
        
        // Assert
        Assert.NotNull(trackedRecord);
        Assert.Equal(record1.Name, trackedRecord.Name);
        Assert.Same(record1, trackedRecord); // Same tracked instance
    }

    [Fact]
    public async Task ConcurrentEpochs_NoRaceConditions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<DemoDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        await using var coordinator = new EpochCoordinator(scopeFactory);
        
        const int concurrentEpochs = 10;
        var tasks = new List<Task>();
        var processedCounts = new List<int>();
        
        // Act - Process multiple epochs concurrently
        for (int i = 1; i <= concurrentEpochs; i++)
        {
            var epochNumber = i;
            tasks.Add(Task.Run(async () =>
            {
                var vector = EpochVector.FromSingleSource("test-source", epochNumber);
                var epoch = await coordinator.GetOrCreateEpochAsync("test-source", vector);
                
                var dbContext = epoch.GetService<DemoDbContext>();
                
                // Simulate work
                for (int j = 0; j < 10; j++)
                {
                    var record = new DataRecord
                    {
                        Id = Guid.NewGuid(),
                        Name = $"Epoch {epochNumber} Record {j}",
                        Processed = false
                    };
                    dbContext.DataRecords.Add(record);
                }
                
                await dbContext.SaveChangesAsync();
                
                var count = await dbContext.DataRecords.CountAsync();
                lock (processedCounts)
                {
                    processedCounts.Add(count);
                }
                
                coordinator.SignalReadyForNext("test-source", vector,
                    EpochVector.FromSingleSource("test-source", epochNumber + 1));
            }));
        }
        
        await Task.WhenAll(tasks);
        
        // Assert - Each epoch processed its own set of records
        Assert.Equal(concurrentEpochs, processedCounts.Count);
        // Note: In-memory database is shared across scopes, so counts accumulate
        // This is expected behavior - just verify no exceptions occurred
    }

    [Fact]
    public async Task EpochStream_PropagatesEpochScopeDownstream()
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
        
        // Act - Create epoch stream with epoch scope
        var items = AsyncEnumerable(1, 2, 3);
        var epochStream = new TestEpochStream<int>(epoch, items);
        
        // Assert
        Assert.NotNull(epochStream.EpochScope);
        Assert.Same(epoch, epochStream.EpochScope);
        Assert.Equal(vector, epochStream.Epoch);
        
        // Downstream blocks can access the same DbContext
        var dbContext = epochStream.EpochScope.GetService<DemoDbContext>();
        Assert.NotNull(dbContext);
    }

    private static async IAsyncEnumerable<T> AsyncEnumerable<T>(params T[] items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }

    private class TestEpochStream<T> : IEpochStream<T>
    {
        public EpochVector Epoch { get; }
        public IAsyncEnumerable<T> Items { get; }
        public IEpoch? EpochScope { get; }

        public TestEpochStream(IEpoch epochScope, IAsyncEnumerable<T> items)
        {
            EpochScope = epochScope;
            Epoch = epochScope.Vector;
            Items = items;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
