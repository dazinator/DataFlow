namespace EpochAnchoringDemo.Tests;

using DataFlow.POC.Core;
using EpochAnchoringDemo.Core;
using EpochAnchoringDemo.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

/// <summary>
/// Integration tests for epoch anchoring functionality.
/// Validates domain anchor management, resume logic, and transactional boundaries.
/// 
/// NOTE: These tests demonstrate the source-internal anchor management pattern.
/// In production, anchors would be contributed to checkpoints at global epoch alignment
/// rather than being persisted through a standalone store.
/// </summary>
public class EpochAnchoringIntegrationTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly DbContextOptions<DemoDbContext> _dbOptions;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ServiceProvider _serviceProvider;

    public EpochAnchoringIntegrationTests()
    {
        // Create unique database file for this test instance
        var testId = Guid.NewGuid().ToString("N");
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_data_{testId}.db");

        _dbOptions = new DbContextOptionsBuilder<DemoDbContext>()
            .UseSqlite($"Data Source={_testDbPath}")
            .Options;

        // Initialize the database
        using var context = new DemoDbContext(_dbOptions);
        context.Database.EnsureCreated();

        // Setup DI for epoch coordinator
        var services = new ServiceCollection();
        services.AddDbContext<DemoDbContext>(options =>
            options.UseSqlite($"Data Source={_testDbPath}"));
        _serviceProvider = services.BuildServiceProvider();
        _scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
    }

    public void Dispose()
    {
        // Dispose service provider
        _serviceProvider?.Dispose();
        
        // Clean up database file
        try
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public async Task DatabaseSourceActor_Should_StreamDataInEpochs()
    {
        // Arrange
        const int totalRecords = 500;
        const int epochSize = 100;
        const string sourceId = "test-source";

        // Seed database
        using (var context = new DemoDbContext(_dbOptions))
        {
            for (int i = 1; i <= totalRecords; i++)
            {
                context.DataRecords.Add(new DataRecord
                {
                    Id = i,
                    Name = $"Record {i}", Value = i * 10.5m,
                    Processed = false
                });
            }
            await context.SaveChangesAsync();
        }

        await using var coordinator = new EpochCoordinator(_scopeFactory);
        var actor = new Blocks.DatabaseSourceActor(
            coordinator,
            _dbOptions,
            epochSize,
            sourceId);

        var context2 = new TestActorContext(coordinator);

        // Act
        var epochs = new List<EpochVector>();
        var itemCounts = new List<int>();
        
        await foreach (var epochStream in actor.ProduceEpochsAsync(context2))
        {
            epochs.Add(epochStream.Epoch);
            
            // Count items in this epoch
            var count = 0;
            await foreach (var item in epochStream.Items)
            {
                count++;
            }
            itemCounts.Add(count);
        }

        // Assert
        epochs.Count.ShouldBe(5); // 500 / 100 = 5 epochs
        itemCounts.ShouldAllBe(count => count == epochSize);
        
        // Verify epochs have correct sequence numbers
        for (int i = 0; i < epochs.Count; i++)
        {
            epochs[i].GetSequence(sourceId).ShouldBe(i + 1);
        }
        
        // Verify domain anchor was updated
        actor.GetLastProcessedId().ShouldBe(500);
    }

    [Fact]
    public async Task DatabaseSourceActor_Should_ResumeFromAnchor()
    {
        // Arrange
        const int totalRecords = 500;
        const int epochSize = 100;
        const string sourceId = "test-source";

        // Seed database
        using (var context = new DemoDbContext(_dbOptions))
        {
            for (int i = 1; i <= totalRecords; i++)
            {
                context.DataRecords.Add(new DataRecord
                {
                    Id = i,
                    Name = $"Record {i}", Value = i * 10.5m,
                    Processed = false
                });
            }
            await context.SaveChangesAsync();
        }

        // First run - process first 300 records (3 epochs)
        int anchorAfterThreeEpochs;
        {
            await using var coordinator = new EpochCoordinator(_scopeFactory);
            var actor = new Blocks.DatabaseSourceActor(
                coordinator,
                _dbOptions,
                epochSize,
                sourceId);

            var context = new TestActorContext(coordinator);

            var epochCount = 0;
            await foreach (var epochStream in actor.ProduceEpochsAsync(context))
            {
                await foreach (var item in epochStream.Items)
                {
                    // Process items (consume them)
                }
                
                epochCount++;
                if (epochCount >= 3)
                {
                    break; // Stop after 3 epochs
                }
            }
            
            // Capture the anchor after 3 epochs
            anchorAfterThreeEpochs = actor.GetLastProcessedId();
            anchorAfterThreeEpochs.ShouldBe(300); // 3 epochs * 100 items
        }

        // Second run - resume from saved anchor
        // In production, this anchor would come from a loaded checkpoint
        {
            await using var coordinator = new EpochCoordinator(_scopeFactory);
            var actor = new Blocks.DatabaseSourceActor(
                coordinator,
                _dbOptions,
                epochSize,
                sourceId,
                initialLastProcessedId: anchorAfterThreeEpochs); // Resume from anchor

            var context = new TestActorContext(coordinator);

            // Act
            var epochs = new List<EpochVector>();
            var itemCounts = new List<int>();
            var processedIds = new List<int>();
            
            await foreach (var epochStream in actor.ProduceEpochsAsync(context))
            {
                epochs.Add(epochStream.Epoch);
                
                // Count items and track IDs in this epoch
                var count = 0;
                await foreach (var item in epochStream.Items)
                {
                    count++;
                    processedIds.Add(item.Id);
                }
                itemCounts.Add(count);
            }

            // Assert
            // Should only process remaining unprocessed records (epochs with records 301-500)
            epochs.Count.ShouldBe(2); // Remaining 200 records = 2 epochs
            itemCounts[0].ShouldBe(100);
            itemCounts[1].ShouldBe(100);
            
            // Verify IDs are correct (301-500, not 1-200)
            processedIds.Min().ShouldBe(301);
            processedIds.Max().ShouldBe(500);
            processedIds.Count.ShouldBe(200);
            
            // Verify final anchor
            actor.GetLastProcessedId().ShouldBe(500);
        }
    }

    [Fact]
    public async Task EpochLifecycleNotifier_Should_NotifyAllObservers()
    {
        // Arrange
        var notifier = new EpochLifecycleNotifier();
        var observer1 = new TestObserver();
        var observer2 = new TestObserver();

        notifier.RegisterObserver(observer1);
        notifier.RegisterObserver(observer2);

        var epoch = EpochVector.FromSingleSource("test", 1);

        // Act
        await notifier.NotifyEpochCreatedAsync(epoch);
        await notifier.NotifyEpochCompletedAsync(epoch);
        await notifier.NotifyGlobalEpochAlignedAsync(epoch);

        // Assert
        observer1.CreatedEpochs.ShouldContain(epoch);
        observer1.CompletedEpochs.ShouldContain(epoch);
        observer1.AlignedWatermarks.ShouldContain(epoch);

        observer2.CreatedEpochs.ShouldContain(epoch);
        observer2.CompletedEpochs.ShouldContain(epoch);
        observer2.AlignedWatermarks.ShouldContain(epoch);
    }

    [Fact]
    public async Task WriteContextBlock_Should_ProcessItemsTransactionally()
    {
        // Arrange
        const int totalRecords = 100;

        // Seed database
        using (var context = new DemoDbContext(_dbOptions))
        {
            for (int i = 1; i <= totalRecords; i++)
            {
                context.DataRecords.Add(new DataRecord
                {
                    Id = i,
                    Name = $"Record {i}", Value = i * 10.5m,
                    Processed = false
                });
            }
            await context.SaveChangesAsync();
        }

        var writeBlock = new Blocks.WriteContextBlock();

        // Act
        await using var coordinator = new EpochCoordinator(_scopeFactory);
        var epoch = EpochVector.FromSingleSource("test", 1);
        var epochScope = await coordinator.GetOrCreateEpochAsync("test", epoch);
        
        using var dbContext = new DemoDbContext(_dbOptions);
        var records = await dbContext.DataRecords.Take(totalRecords).ToListAsync();
        
        var epochStream = new EpochStreamImpl<DataRecord>(epochScope, ToAsyncEnumerable(records));

        var processedCount = 0;
        await foreach (var outputStream in writeBlock.ProcessAsync(ToAsyncEnumerable(new[] { epochStream })))
        {
            await foreach (var item in outputStream.Items)
            {
                processedCount++;
            }
        }

        // Assert
        processedCount.ShouldBe(totalRecords);

        // Verify all records were marked as processed in the database
        using var verifyContext = new DemoDbContext(_dbOptions);
        var allProcessed = await verifyContext.DataRecords
            .Where(r => r.Id <= totalRecords)
            .AllAsync(r => r.Processed);
        allProcessed.ShouldBeTrue();
    }

    [Fact]
    public async Task EndToEnd_Should_ProcessWithDomainAnchor()
    {
        // Arrange
        const int totalRecords = 500;
        const int epochSize = 100;
        const string sourceId = "end-to-end-source";

        // Seed database
        using (var context = new DemoDbContext(_dbOptions))
        {
            for (int i = 1; i <= totalRecords; i++)
            {
                context.DataRecords.Add(new DataRecord
                {
                    Id = i,
                    Name = $"Record {i}", Value = i * 10.5m,
                    Processed = false
                });
            }
            await context.SaveChangesAsync();
        }

        await using var coordinator = new EpochCoordinator(_scopeFactory);
        var actor = new Blocks.DatabaseSourceActor(
            coordinator,
            _dbOptions,
            epochSize,
            sourceId);

        var writeBlock = new Blocks.WriteContextBlock();
        var context2 = new TestActorContext(coordinator);

        // Act
        var processedCount = 0;
        await foreach (var epochStream in writeBlock.ProcessAsync(actor.ProduceEpochsAsync(context2)))
        {
            await foreach (var item in epochStream.Items)
            {
                processedCount++;
            }
        }

        // Assert
        processedCount.ShouldBe(totalRecords);
        actor.GetLastProcessedId().ShouldBe(totalRecords);

        // Verify all records were processed
        using var verifyContext = new DemoDbContext(_dbOptions);
        var allProcessed = await verifyContext.DataRecords.AllAsync(r => r.Processed);
        allProcessed.ShouldBeTrue();
    }

    [Fact]
    public async Task EndToEnd_Should_ResumeWithoutDuplicates()
    {
        // Arrange
        const int totalRecords = 500;
        const int epochSize = 100;
        const string sourceId = "resume-source";

        // Seed database
        using (var context = new DemoDbContext(_dbOptions))
        {
            for (int i = 1; i <= totalRecords; i++)
            {
                context.DataRecords.Add(new DataRecord
                {
                    Id = i,
                    Name = $"Record {i}", Value = i * 10.5m,
                    Processed = false
                });
            }
            await context.SaveChangesAsync();
        }

        // First run - process partially (3 epochs = 300 records)
        int savedAnchor;
        {
            await using var coordinator = new EpochCoordinator(_scopeFactory);
            var actor = new Blocks.DatabaseSourceActor(
                coordinator,
                _dbOptions,
                epochSize,
                sourceId);

            var writeBlock = new Blocks.WriteContextBlock();
            var context = new TestActorContext(coordinator);

            var epochCount = 0;
            await foreach (var epochStream in writeBlock.ProcessAsync(actor.ProduceEpochsAsync(context)))
            {
                await foreach (var item in epochStream.Items)
                {
                    // Process items
                }
                
                epochCount++;
                if (epochCount >= 3)
                {
                    break; // Simulate interruption after 3 epochs
                }
            }
            
            // Save the anchor (in production, this would be part of a checkpoint)
            savedAnchor = actor.GetLastProcessedId();
            savedAnchor.ShouldBe(300);
        }

        // Verify exactly 300 records were processed
        using (var context = new DemoDbContext(_dbOptions))
        {
            var processedCount = await context.DataRecords.CountAsync(r => r.Processed);
            processedCount.ShouldBe(300);
        }

        // Second run - resume from checkpoint
        // In production, savedAnchor would come from a loaded checkpoint
        {
            await using var coordinator = new EpochCoordinator(_scopeFactory);
            var actor = new Blocks.DatabaseSourceActor(
                coordinator,
                _dbOptions,
                epochSize,
                sourceId,
                initialLastProcessedId: savedAnchor); // Resume from checkpoint

            var writeBlock = new Blocks.WriteContextBlock();
            var context = new TestActorContext(coordinator);

            var resumeProcessedCount = 0;
            await foreach (var epochStream in writeBlock.ProcessAsync(actor.ProduceEpochsAsync(context)))
            {
                await foreach (var item in epochStream.Items)
                {
                    resumeProcessedCount++;
                }
            }

            // Assert - should only process remaining 200 records
            resumeProcessedCount.ShouldBe(200);
            actor.GetLastProcessedId().ShouldBe(500);
        }

        // Final verification - exactly 500 records processed total, no duplicates
        using (var context = new DemoDbContext(_dbOptions))
        {
            var totalProcessed = await context.DataRecords.CountAsync(r => r.Processed);
            totalProcessed.ShouldBe(500); // Exactly 500, not 600 (no duplicates)
        }
    }

    // Helper methods
    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
            await Task.Yield();
        }
    }

    private class EpochStreamImpl<T> : IEpochStream<T>
    {
        public EpochStreamImpl(IEpoch epochScope, IAsyncEnumerable<T> items)
        {
            EpochScope = epochScope ?? throw new ArgumentNullException(nameof(epochScope));
            Epoch = epochScope.Vector;
            Items = items;
        }

        public EpochVector Epoch { get; }
        public IAsyncEnumerable<T> Items { get; }
        public IEpoch EpochScope { get; }
        
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private class TestActorContext : IActorExecutionContext
    {
        public TestActorContext(IEpochCoordinator? coordinator = null)
        {
            EpochCoordinator = coordinator;
        }
        
        public CancellationToken CancellationToken => CancellationToken.None;
        public Guid InvocationId { get; } = Guid.NewGuid();
        public IEpochCoordinator? EpochCoordinator { get; }
        public ITriggerContext? TriggerContext { get; } = null;
        public IParameterProvider Parameters => new TriggerContextParameterProvider(TriggerContext);
        public void RequestRotation() { }
    }
    
    private class TestObserver : IEpochLifecycleObserver
    {
        public List<EpochVector> CreatedEpochs { get; } = new();
        public List<EpochVector> CompletedEpochs { get; } = new();
        public List<EpochVector> AlignedWatermarks { get; } = new();

        public Task OnEpochCreatedAsync(EpochVector epoch)
        {
            CreatedEpochs.Add(epoch);
            return Task.CompletedTask;
        }

        public Task OnEpochCompletedAsync(EpochVector epoch)
        {
            CompletedEpochs.Add(epoch);
            return Task.CompletedTask;
        }

        public Task OnGlobalEpochAlignedAsync(EpochVector watermark)
        {
            AlignedWatermarks.Add(watermark);
            return Task.CompletedTask;
        }
    }
}
