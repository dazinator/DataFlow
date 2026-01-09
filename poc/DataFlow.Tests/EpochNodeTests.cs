namespace DataFlow.POC.Tests;

using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Tests for EpochSourceNode, EpochProcessorNode, and EpochHooks.
/// Validates epoch stream management, operation processing, and lifecycle hooks.
/// </summary>
public class EpochNodeTests : IAsyncDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly EpochCoordinator _coordinator;
    private readonly List<IAsyncDisposable> _disposables = new();

    public EpochNodeTests()
    {
        var services = new ServiceCollection();
        services.AddScoped<TestService>();
        services.AddScoped<TestDbContext>();
        _serviceProvider = services.BuildServiceProvider();
        
        _coordinator = new EpochCoordinator(_serviceProvider.GetRequiredService<IServiceScopeFactory>());
        _disposables.Add(_coordinator);
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
    public async Task EpochSourceNode_PublishesEpochToStream()
    {
        // Arrange
        var source = new EpochSourceNode();
        var vector = EpochVector.FromSingleSource("test", 1);
        var epoch = await _coordinator.GetOrCreateEpochAsync("test", vector);

        // Act
        await source.PublishEpochAsync(epoch);
        source.SignalCompletion();

        // Assert
        var readEpoch = await source.EpochReader.ReadAsync();
        Assert.Same(epoch, readEpoch);
        
        // Stream should be complete
        Assert.True(source.EpochReader.Completion.IsCompleted);
    }

    [Fact]
    public async Task EpochProcessorNode_DrainsOperationQueue()
    {
        // Arrange
        var source = new EpochSourceNode();
        var processor = new EpochProcessorNode(source);
        _disposables.Add(processor);

        var vector = EpochVector.FromSingleSource("test", 1);
        var epoch = await _coordinator.GetOrCreateEpochAsync("test", vector);

        var executionLog = new List<string>();

        // Queue operations
        await epoch.QueueSerializedOperationAsync<TestService>(async svc =>
        {
            executionLog.Add("op1");
            await Task.Yield();
        });
        
        await epoch.QueueSerializedOperationAsync<TestService>(async svc =>
        {
            executionLog.Add("op2");
            await Task.Yield();
        });

        epoch.CompleteOperations();

        // Act
        await source.PublishEpochAsync(epoch);
        source.SignalCompletion();

        // Wait for processing to complete
        await processor.CompletionTask;

        // Assert
        Assert.Equal(2, executionLog.Count);
        Assert.Equal("op1", executionLog[0]);
        Assert.Equal("op2", executionLog[1]);
    }

    [Fact]
    public async Task EpochProcessorNode_ExecutesHooks_InCorrectOrder()
    {
        // Arrange
        var source = new EpochSourceNode();
        var executionLog = new List<string>();
        
        var hooks = new EpochHooks
        {
            OnBeginEpoch = async (epoch, ct) =>
            {
                executionLog.Add("begin");
                await Task.Yield();
            },
            OnCommitEpoch = async (epoch, ct) =>
            {
                executionLog.Add("commit");
                await Task.Yield();
            }
        };

        var processor = new EpochProcessorNode(source, hooks);
        _disposables.Add(processor);

        var vector = EpochVector.FromSingleSource("test", 1);
        var epoch = await _coordinator.GetOrCreateEpochAsync("test", vector);

        await epoch.QueueSerializedOperationAsync<TestService>(async svc =>
        {
            executionLog.Add("operation");
            await Task.Yield();
        });

        epoch.CompleteOperations();

        // Act
        await source.PublishEpochAsync(epoch);
        source.SignalCompletion();

        await processor.CompletionTask;

        // Assert
        Assert.Equal(3, executionLog.Count);
        Assert.Equal("begin", executionLog[0]);
        Assert.Equal("operation", executionLog[1]);
        Assert.Equal("commit", executionLog[2]);
    }

    [Fact]
    public async Task EpochProcessorNode_ErrorHook_CalledOnFailure()
    {
        // Arrange
        var source = new EpochSourceNode();
        Exception? capturedError = null;
        
        var hooks = new EpochHooks
        {
            OnEpochError = async (epoch, ex, ct) =>
            {
                capturedError = ex;
                await Task.Yield();
            }
        };

        var processor = new EpochProcessorNode(source, hooks);
        _disposables.Add(processor);

        var vector = EpochVector.FromSingleSource("test", 1);
        var epoch = await _coordinator.GetOrCreateEpochAsync("test", vector);

        var expectedError = new InvalidOperationException("Test error");
        await epoch.QueueSerializedOperationAsync<TestService>(async svc =>
        {
            await Task.Yield();
            throw expectedError;
        });

        epoch.CompleteOperations();

        // Act
        await source.PublishEpochAsync(epoch);
        source.SignalCompletion();

        // Assert - Wait with timeout and expect exception
        var timeoutTask = Task.Delay(5000);
        var completedTask = await Task.WhenAny(processor.CompletionTask, timeoutTask);
        
        Assert.NotSame(timeoutTask, completedTask); // Should not timeout
        Assert.True(processor.CompletionTask.IsFaulted);
        Assert.IsType<InvalidOperationException>(processor.CompletionTask.Exception?.InnerException);
        Assert.NotNull(capturedError);
        Assert.Same(expectedError, capturedError);
    }

    [Fact]
    public async Task MultipleProcessors_ConsumeFromSameSource()
    {
        // Arrange
        var source = new EpochSourceNode();
        
        var processor1Log = new List<int>();
        var processor2Log = new List<int>();

        var hooks1 = new EpochHooks
        {
            OnCommitEpoch = async (epoch, ct) =>
            {
                processor1Log.Add((int)epoch.Vector.GetSequence("test"));
                await Task.Yield();
            }
        };

        var hooks2 = new EpochHooks
        {
            OnCommitEpoch = async (epoch, ct) =>
            {
                processor2Log.Add((int)epoch.Vector.GetSequence("test"));
                await Task.Yield();
            }
        };

        var processor1 = new EpochProcessorNode(source, hooks1);
        var processor2 = new EpochProcessorNode(source, hooks2);
        _disposables.Add(processor1);
        _disposables.Add(processor2);

        // Create and publish multiple epochs
        for (int i = 1; i <= 5; i++)
        {
            var vector = EpochVector.FromSingleSource("test", i);
            var epoch = await _coordinator.GetOrCreateEpochAsync("test", vector);
            epoch.CompleteOperations();
            await source.PublishEpochAsync(epoch);
        }

        source.SignalCompletion();

        // Act
        await Task.WhenAll(processor1.CompletionTask, processor2.CompletionTask);

        // Assert - Both processors should have processed all epochs
        Assert.Equal(5, processor1Log.Count + processor2Log.Count);
        
        // Each epoch should be processed by exactly one processor
        var allProcessed = processor1Log.Concat(processor2Log).OrderBy(x => x).ToList();
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, allProcessed);
    }

    [Fact]
    public async Task EpochOperations_ExecuteSerially()
    {
        // Arrange
        var source = new EpochSourceNode();
        var processor = new EpochProcessorNode(source);
        _disposables.Add(processor);

        var vector = EpochVector.FromSingleSource("test", 1);
        var epoch = await _coordinator.GetOrCreateEpochAsync("test", vector);

        var executionLog = new List<string>();
        var semaphore = new SemaphoreSlim(0);

        // Queue operations that must execute serially
        await epoch.QueueSerializedOperationAsync<TestService>(async svc =>
        {
            executionLog.Add("op1-start");
            await semaphore.WaitAsync(); // Wait for signal
            executionLog.Add("op1-end");
        });
        
        await epoch.QueueSerializedOperationAsync<TestService>(async svc =>
        {
            executionLog.Add("op2");
            await Task.Yield();
        });

        epoch.CompleteOperations();
        await source.PublishEpochAsync(epoch);

        // Wait a bit for first operation to start
        await Task.Delay(50);

        // Assert - op2 should not have started yet
        Assert.Contains("op1-start", executionLog);
        Assert.DoesNotContain("op2", executionLog);

        // Release first operation
        semaphore.Release();
        source.SignalCompletion();

        await processor.CompletionTask;

        // Assert - All operations completed in order
        Assert.Equal(new[] { "op1-start", "op1-end", "op2" }, executionLog);
    }

    [Fact]
    public async Task Epoch_CompletionTask_WaitsForAllOperations()
    {
        // Arrange
        var source = new EpochSourceNode();
        var processor = new EpochProcessorNode(source);
        _disposables.Add(processor);

        var vector = EpochVector.FromSingleSource("test", 1);
        var epoch = await _coordinator.GetOrCreateEpochAsync("test", vector);

        var operationsCompleted = false;

        await epoch.QueueSerializedOperationAsync<TestService>(async svc =>
        {
            await Task.Delay(100);
            operationsCompleted = true;
        });

        epoch.CompleteOperations();
        await source.PublishEpochAsync(epoch);
        source.SignalCompletion();

        // Act
        await processor.CompletionTask;

        // Assert - Operations must be complete
        Assert.True(operationsCompleted);
    }

    [Fact]
    public async Task EpochHooks_CanQueueOperations()
    {
        // Arrange
        var source = new EpochSourceNode();
        var executionLog = new List<string>();
        
        var hooks = new EpochHooks
        {
            OnBeginEpoch = async (epoch, ct) =>
            {
                await epoch.QueueSerializedOperationAsync<TestDbContext>(async db =>
                {
                    executionLog.Add("begin-operation");
                    await db.BeginTransactionAsync(ct);
                }, ct);
            },
            OnCommitEpoch = async (epoch, ct) =>
            {
                await epoch.QueueSerializedOperationAsync<TestDbContext>(async db =>
                {
                    executionLog.Add("commit-operation");
                    await db.CommitTransactionAsync(ct);
                }, ct);
            }
        };

        var processor = new EpochProcessorNode(source, hooks);
        _disposables.Add(processor);

        var vector = EpochVector.FromSingleSource("test", 1);
        var epoch = await _coordinator.GetOrCreateEpochAsync("test", vector);

        await epoch.QueueSerializedOperationAsync<TestService>(async svc =>
        {
            executionLog.Add("user-operation");
            await Task.Yield();
        });

        // Note: Don't call CompleteOperations() - the processor will do it after hooks run

        // Act
        await source.PublishEpochAsync(epoch);
        source.SignalCompletion();

        await processor.CompletionTask;

        // Assert - Hook operations should be queued and executed
        Assert.Contains("begin-operation", executionLog);
        Assert.Contains("user-operation", executionLog);
        Assert.Contains("commit-operation", executionLog);
    }

    // Test helper classes
    private class TestService
    {
        public int Value { get; set; }
    }

    private class TestDbContext
    {
        private bool _inTransaction;

        public Task BeginTransactionAsync(CancellationToken ct)
        {
            _inTransaction = true;
            return Task.CompletedTask;
        }

        public Task CommitTransactionAsync(CancellationToken ct)
        {
            if (!_inTransaction)
                throw new InvalidOperationException("No transaction to commit");
            _inTransaction = false;
            return Task.CompletedTask;
        }
    }
}
