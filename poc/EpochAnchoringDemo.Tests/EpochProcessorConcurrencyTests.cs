namespace EpochAnchoringDemo.Tests;

using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Collections.Concurrent;

/// <summary>
/// Tests for multi-processor concurrency scenarios with EpochProcessorNode.
/// Validates that multiple processor nodes can work concurrently on different epochs,
/// while maintaining proper serialization within each epoch.
/// 
/// Design v2: Fully serial execution within each epoch (MSDTC safe).
/// </summary>
public class EpochProcessorConcurrencyTests : IAsyncDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly List<IAsyncDisposable> _disposables = new();

    public EpochProcessorConcurrencyTests()
    {
        var services = new ServiceCollection();
        services.AddScoped<TestService>();
        services.AddScoped<TestDbContext>();
        _serviceProvider = services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var disposable in _disposables)
        {
            await disposable.DisposeAsync();
        }
        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task SingleProcessor_ProcessesEpochsInOrder()
    {
        // Arrange
        var coordinator = new EpochCoordinator(_serviceProvider.GetRequiredService<IServiceScopeFactory>());
        _disposables.Add(coordinator);
        
        var source = new EpochSourceNode();
        var completionOrder = new List<int>();
        var lockObject = new object();

        var hooks = new EpochHooks
        {
            OnCommitEpoch = async (epoch, ct) =>
            {
                var sequence = (int)epoch.Vector.GetSequence("test");
                lock (lockObject)
                {
                    completionOrder.Add(sequence);
                }
                await Task.Yield();
            }
        };

        var processor = new EpochProcessorNode(source, hooks);
        _disposables.Add(processor);

        // Act - Create and publish 10 epochs
        for (int i = 1; i <= 10; i++)
        {
            var vector = EpochVector.FromSingleSource("test", i);
            var epoch = await coordinator.GetOrCreateEpochAsync("test", vector);
            await source.PublishEpochAsync(epoch);
        }

        source.SignalCompletion();
        await processor.CompletionTask;

        // Assert - Single processor should complete in creation order
        lock (lockObject)
        {
            completionOrder.ShouldBe(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
        }
    }

    [Fact]
    public async Task DualProcessors_ProcessEpochsConcurrently()
    {
        // Arrange
        var coordinator = new EpochCoordinator(_serviceProvider.GetRequiredService<IServiceScopeFactory>());
        _disposables.Add(coordinator);
        
        var source = new EpochSourceNode();
        var completionOrder = new ConcurrentBag<int>();
        var completionLock = new object();
        var processor1Epochs = new List<int>();
        var processor2Epochs = new List<int>();

        var hooks1 = new EpochHooks
        {
            OnCommitEpoch = async (epoch, ct) =>
            {
                var sequence = (int)epoch.Vector.GetSequence("test");
                lock (completionLock)
                {
                    processor1Epochs.Add(sequence);
                    completionOrder.Add(sequence);
                }
                await Task.Delay(10, ct); // Small delay to encourage interleaving
            }
        };

        var hooks2 = new EpochHooks
        {
            OnCommitEpoch = async (epoch, ct) =>
            {
                var sequence = (int)epoch.Vector.GetSequence("test");
                lock (completionLock)
                {
                    processor2Epochs.Add(sequence);
                    completionOrder.Add(sequence);
                }
                await Task.Delay(10, ct); // Small delay to encourage interleaving
            }
        };

        var processor1 = new EpochProcessorNode(source, hooks1);
        var processor2 = new EpochProcessorNode(source, hooks2);
        _disposables.Add(processor1);
        _disposables.Add(processor2);

        // Act - Create and publish 10 epochs
        for (int i = 1; i <= 10; i++)
        {
            var vector = EpochVector.FromSingleSource("test", i);
            var epoch = await coordinator.GetOrCreateEpochAsync("test", vector);
            await source.PublishEpochAsync(epoch);
        }

        source.SignalCompletion();
        await Task.WhenAll(processor1.CompletionTask, processor2.CompletionTask);

        // Assert - All epochs processed
        completionOrder.Count.ShouldBe(10);
        
        // Both processors should have done some work
        processor1Epochs.Count.ShouldBeGreaterThan(0);
        processor2Epochs.Count.ShouldBeGreaterThan(0);
        var totalProcessed = processor1Epochs.Count + processor2Epochs.Count;
        totalProcessed.ShouldBe(10);
        
        // No epoch should be processed by both processors
        var allEpochs = new HashSet<int>(processor1Epochs);
        allEpochs.UnionWith(processor2Epochs);
        allEpochs.Count.ShouldBe(10); // All unique
    }

    [Fact]
    public async Task QuadProcessors_MaximizeThroughput()
    {
        // Arrange
        var coordinator = new EpochCoordinator(_serviceProvider.GetRequiredService<IServiceScopeFactory>());
        _disposables.Add(coordinator);
        
        var source = new EpochSourceNode();
        var completionOrder = new ConcurrentBag<int>();
        var processorLogs = new ConcurrentDictionary<int, List<int>>();
        
        // Create 4 processors
        var processors = new List<EpochProcessorNode>();
        for (int p = 1; p <= 4; p++)
        {
            var processorId = p;
            processorLogs[processorId] = new List<int>();
            
            var hooks = new EpochHooks
            {
                OnCommitEpoch = async (epoch, ct) =>
                {
                    var sequence = (int)epoch.Vector.GetSequence("test");
                    lock (processorLogs[processorId])
                    {
                        processorLogs[processorId].Add(sequence);
                    }
                    completionOrder.Add(sequence);
                    await Task.Delay(5, ct); // Small delay
                }
            };
            
            var processor = new EpochProcessorNode(source, hooks);
            processors.Add(processor);
            _disposables.Add(processor);
        }

        // Act - Create and publish 20 epochs
        for (int i = 1; i <= 20; i++)
        {
            var vector = EpochVector.FromSingleSource("test", i);
            var epoch = await coordinator.GetOrCreateEpochAsync("test", vector);
            await source.PublishEpochAsync(epoch);
        }

        source.SignalCompletion();
        await Task.WhenAll(processors.Select(p => p.CompletionTask));

        // Assert - All epochs processed
        completionOrder.Count.ShouldBe(20);
        
        // Work should be distributed across all 4 processors
        foreach (var kvp in processorLogs)
        {
            kvp.Value.Count.ShouldBeGreaterThan(0, $"Processor {kvp.Key} should have processed at least one epoch");
        }
        
        // Verify all epochs processed exactly once
        var totalProcessed = processorLogs.Values.Sum(list => list.Count);
        totalProcessed.ShouldBe(20);
    }

    [Fact]
    public async Task MultipleProcessors_HooksExecuteCorrectly()
    {
        // Arrange
        var coordinator = new EpochCoordinator(_serviceProvider.GetRequiredService<IServiceScopeFactory>());
        _disposables.Add(coordinator);
        
        var source = new EpochSourceNode();
        var executionLog = new ConcurrentBag<string>();

        var hooks = new EpochHooks
        {
            OnBeginEpoch = async (epoch, ct) =>
            {
                var sequence = (int)epoch.Vector.GetSequence("test");
                executionLog.Add($"begin-{sequence}");
                await Task.Yield();
            },
            OnCommitEpoch = async (epoch, ct) =>
            {
                var sequence = (int)epoch.Vector.GetSequence("test");
                executionLog.Add($"commit-{sequence}");
                await Task.Yield();
            }
        };

        // Create 2 processors with same hooks
        var processor1 = new EpochProcessorNode(source, hooks);
        var processor2 = new EpochProcessorNode(source, hooks);
        _disposables.Add(processor1);
        _disposables.Add(processor2);

        // Act - Create and publish epochs with operations
        for (int i = 1; i <= 5; i++)
        {
            var vector = EpochVector.FromSingleSource("test", i);
            var epoch = await coordinator.GetOrCreateEpochAsync("test", vector);
            
            var sequence = i; // Capture for closure
            await epoch.QueueSerializedOperationAsync<TestService>(async svc =>
            {
                executionLog.Add($"op-{sequence}");
                await Task.Yield();
            });
            
            await source.PublishEpochAsync(epoch);
        }

        source.SignalCompletion();
        await Task.WhenAll(processor1.CompletionTask, processor2.CompletionTask);

        // Assert - Each epoch should have begin -> op -> commit
        for (int i = 1; i <= 5; i++)
        {
            executionLog.ShouldContain($"begin-{i}");
            executionLog.ShouldContain($"op-{i}");
            executionLog.ShouldContain($"commit-{i}");
        }
        
        // Total should be 15 (3 events per epoch * 5 epochs)
        executionLog.Count.ShouldBe(15);
    }

    [Fact]
    public async Task MultipleProcessors_ErrorHandling()
    {
        // Arrange
        var coordinator = new EpochCoordinator(_serviceProvider.GetRequiredService<IServiceScopeFactory>());
        _disposables.Add(coordinator);
        
        var source = new EpochSourceNode();
        var errorsCaught = new ConcurrentBag<int>();

        var hooks = new EpochHooks
        {
            OnEpochError = async (epoch, ex, ct) =>
            {
                var sequence = (int)epoch.Vector.GetSequence("test");
                errorsCaught.Add(sequence);
                await Task.Yield();
            }
        };

        var processor1 = new EpochProcessorNode(source, hooks);
        var processor2 = new EpochProcessorNode(source, hooks);
        _disposables.Add(processor1);
        _disposables.Add(processor2);

        // Act - Create epochs, with epoch 3 causing an error
        for (int i = 1; i <= 5; i++)
        {
            var vector = EpochVector.FromSingleSource("test", i);
            var epoch = await coordinator.GetOrCreateEpochAsync("test", vector);
            
            if (i == 3)
            {
                // This epoch will fail
                await epoch.QueueSerializedOperationAsync<TestService>(async svc =>
                {
                    await Task.Yield();
                    throw new InvalidOperationException("Simulated error");
                });
            }
            
            await source.PublishEpochAsync(epoch);
        }

        source.SignalCompletion();

        // One of the processors will fail due to epoch 3
        var processor1Task = processor1.CompletionTask;
        var processor2Task = processor2.CompletionTask;
        
        try
        {
            await Task.WhenAll(processor1Task, processor2Task);
        }
        catch
        {
            // Expected - one processor encountered the error
        }

        // Assert - Error hook should have been called for epoch 3
        errorsCaught.ShouldContain(3);
        
        // At least one processor should have faulted
        (processor1Task.IsFaulted || processor2Task.IsFaulted).ShouldBeTrue();
    }

    [Fact]
    public async Task MultipleProcessors_TransactionIsolation()
    {
        // Arrange
        var coordinator = new EpochCoordinator(_serviceProvider.GetRequiredService<IServiceScopeFactory>());
        _disposables.Add(coordinator);
        
        var source = new EpochSourceNode();
        var transactionStates = new ConcurrentDictionary<int, bool>();

        var hooks = new EpochHooks
        {
            OnBeginEpoch = async (epoch, ct) =>
            {
                await epoch.QueueSerializedOperationAsync<TestDbContext>(async db =>
                {
                    await db.BeginTransactionAsync(ct);
                    var sequence = (int)epoch.Vector.GetSequence("test");
                    transactionStates[sequence] = db.IsInTransaction;
                }, ct);
            },
            OnCommitEpoch = async (epoch, ct) =>
            {
                await epoch.QueueSerializedOperationAsync<TestDbContext>(async db =>
                {
                    await db.CommitTransactionAsync(ct);
                }, ct);
            }
        };

        var processor1 = new EpochProcessorNode(source, hooks);
        var processor2 = new EpochProcessorNode(source, hooks);
        _disposables.Add(processor1);
        _disposables.Add(processor2);

        // Act - Create and publish epochs
        for (int i = 1; i <= 10; i++)
        {
            var vector = EpochVector.FromSingleSource("test", i);
            var epoch = await coordinator.GetOrCreateEpochAsync("test", vector);
            await source.PublishEpochAsync(epoch);
        }

        source.SignalCompletion();
        await Task.WhenAll(processor1.CompletionTask, processor2.CompletionTask);

        // Assert - Each epoch should have successfully managed its own transaction
        transactionStates.Count.ShouldBe(10);
        transactionStates.Values.ShouldAllBe(inTx => inTx == true);
    }

    // Test helper classes
    private class TestService
    {
        public int Value { get; set; }
    }

    private class TestDbContext
    {
        public bool IsInTransaction { get; private set; }

        public Task BeginTransactionAsync(CancellationToken ct)
        {
            if (IsInTransaction)
                throw new InvalidOperationException("Already in transaction");
            IsInTransaction = true;
            return Task.CompletedTask;
        }

        public Task CommitTransactionAsync(CancellationToken ct)
        {
            if (!IsInTransaction)
                throw new InvalidOperationException("No transaction to commit");
            IsInTransaction = false;
            return Task.CompletedTask;
        }
    }
}
