using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DataFlow.POC.Core;
using EpochAnchoringDemo.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DataFlow.POC.Registry;

namespace DataFlow.POC.Benchmarks;

/// <summary>
/// Realistic workload benchmarks for epoch architecture with EF Core DbContext.
/// Tests actual database operations (SaveChanges) to validate Gen1/Gen2 behavior
/// under production-like conditions with varying epoch counts and item counts.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class EpochRealisticWorkloadBenchmark : IDisposable
{
    private ServiceProvider? _serviceProvider;
    private IServiceScopeFactory? _scopeFactory;

    [Params(10, 50, 100)]
    public int EpochCount { get; set; } = 10;

    [Params(10, 50, 100)]
    public int ItemsPerEpoch { get; set; } = 10;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        
        // Use in-memory database for consistent benchmarking
        services.AddDbContext<DemoDbContext>(options =>
            options.UseInMemoryDatabase($"BenchmarkDb_{Guid.NewGuid()}"));
        
        _serviceProvider = services.BuildServiceProvider();
        _scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _serviceProvider?.Dispose();
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }

    /// <summary>
    /// Benchmark: Realistic workload with EF Core SaveChanges.
    /// Measures actual database operations to validate GC behavior (Gen1/Gen2 traffic).
    /// Note: In-memory database is used, so transactions are simulated.
    /// </summary>
    [Benchmark(Description = "EF Core SaveChanges")]
    public async Task RealisticWorkloadWithEfCore()
    {
        var coordinator = new EpochCoordinator(_scopeFactory!);
        var source = new EpochSourceNode();
        
        var hooks = new EpochHooks
        {
            OnCommitEpoch = async (epoch, ct) =>
            {
                await epoch.QueueSerializedOperationAsync<DemoDbContext>(async db =>
                {
                    // Save changes (in-memory DB doesn't support transactions)
                    await db.SaveChangesAsync(ct);
                }, ct);
            }
        };

        var processor = new EpochProcessorNode(source, hooks);

        // Process epochs with database operations
        for (int i = 1; i <= EpochCount; i++)
        {
            var vector = EpochVector.FromSingleSource("test", i);
            var epoch = await coordinator.GetOrCreateEpochAsync("test", vector);

            // Queue database insert operations
            for (int j = 0; j < ItemsPerEpoch; j++)
            {
                var itemIndex = j; // Capture for closure
                await epoch.QueueSerializedOperationAsync<DemoDbContext>(async db =>
                {
                    var record = new DataRecord
                    {
                        Name = $"Record {itemIndex}",
                        Category = "Benchmark",
                        Value = itemIndex * 10.5m,
                        CreatedAt = DateTime.UtcNow,
                        Processed = false
                    };
                    db.DataRecords.Add(record);
                    await Task.Yield();
                });
            }

            await source.PublishEpochAsync(epoch);
        }

        source.SignalCompletion();
        await processor.CompletionTask;

        await processor.DisposeAsync();
        await coordinator.DisposeAsync();
    }

    /// <summary>
    /// Benchmark: Realistic workload with query and update operations.
    /// Simulates reading entities, modifying them, and saving changes.
    /// </summary>
    [Benchmark(Description = "EF Core Query and Update")]
    public async Task RealisticWorkloadWithQueryAndUpdate()
    {
        // Pre-populate database
        using (var scope = _scopeFactory!.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DemoDbContext>();
            for (int i = 0; i < EpochCount * ItemsPerEpoch; i++)
            {
                db.DataRecords.Add(new DataRecord
                {
                    Name = $"Record {i}",
                    Category = "Update",
                    Value = i * 5.0m,
                    CreatedAt = DateTime.UtcNow,
                    Processed = false
                });
            }
            await db.SaveChangesAsync();
        }

        var coordinator = new EpochCoordinator(_scopeFactory!);
        var source = new EpochSourceNode();
        
        var hooks = new EpochHooks
        {
            OnCommitEpoch = async (epoch, ct) =>
            {
                await epoch.QueueSerializedOperationAsync<DemoDbContext>(async db =>
                {
                    await db.SaveChangesAsync(ct);
                }, ct);
            }
        };

        var processor = new EpochProcessorNode(source, hooks);

        // Process epochs with query and update operations
        for (int i = 1; i <= EpochCount; i++)
        {
            var vector = EpochVector.FromSingleSource("test", i);
            var epoch = await coordinator.GetOrCreateEpochAsync("test", vector);

            // Queue query and update operations
            var startIndex = (i - 1) * ItemsPerEpoch;
            await epoch.QueueSerializedOperationAsync<DemoDbContext>(async db =>
            {
                // Query entities
                var records = await db.DataRecords
                    .Where(r => !r.Processed)
                    .Skip(startIndex)
                    .Take(ItemsPerEpoch)
                    .ToListAsync();

                // Update entities
                foreach (var record in records)
                {
                    record.Processed = true;
                    record.Value *= 1.1m; // 10% increase
                }
            });

            await source.PublishEpochAsync(epoch);
        }

        source.SignalCompletion();
        await processor.CompletionTask;

        await processor.DisposeAsync();
        await coordinator.DisposeAsync();
    }

    /// <summary>
    /// Benchmark: Single processor baseline for comparison.
    /// Measures performance with standard single-processor epoch processing.
    /// </summary>
    [Benchmark(Baseline = true, Description = "Single Processor Baseline")]
    public async Task SingleProcessorBaseline()
    {
        var coordinator = new EpochCoordinator(_scopeFactory!);
        var source = new EpochSourceNode();
        
        var hooks = new EpochHooks
        {
            OnCommitEpoch = async (epoch, ct) =>
            {
                await epoch.QueueSerializedOperationAsync<DemoDbContext>(async db =>
                {
                    await db.SaveChangesAsync(ct);
                }, ct);
            }
        };

        var processor = new EpochProcessorNode(source, hooks);

        // Simple workload
        for (int i = 1; i <= EpochCount; i++)
        {
            var vector = EpochVector.FromSingleSource("test", i);
            var epoch = await coordinator.GetOrCreateEpochAsync("test", vector);

            for (int j = 0; j < ItemsPerEpoch; j++)
            {
                await epoch.QueueSerializedOperationAsync<DemoDbContext>(async _ =>
                {
                    // Minimal operation
                    await Task.Yield();
                });
            }

            await source.PublishEpochAsync(epoch);
        }

        source.SignalCompletion();
        await processor.CompletionTask;

        await processor.DisposeAsync();
        await coordinator.DisposeAsync();
    }
}
