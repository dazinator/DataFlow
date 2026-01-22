namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Checkpointing;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using Xunit;
using DataFlow.POC.Tests.TestHelpers;

/// <summary>
/// Integration tests for epoch graph configuration via ConfigureEpochs API.
/// Tests the integration of epoch nodes into the DataFlow graph execution model.
/// </summary>
public class EpochGraphIntegrationTests : IAsyncDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly EpochCoordinator _coordinator;
    private readonly List<IAsyncDisposable> _disposables = new();

    public EpochGraphIntegrationTests()
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
    public void ConfigureEpochs_WorksWithoutFactoryWhenServiceProviderAvailable()
    {
        // Arrange
        var builder = GraphHelpers.CreateGraphBuilder("test");

        // Act - No factory provided, should use default
        builder.ConfigureEpochs(config =>
        {
            config.SetPolicy(EpochPolicy.ByCount(10));
            config.AddProcessor("proc1");
        });
        
        var graph = builder.Build();

        // Assert
        Assert.NotNull(graph);
        Assert.Equal("test", graph.Name);
    }

    [Fact]
    public void ConfigureEpochs_ThrowsWhenNoServiceProviderAndNoFactory()
    {
        // Arrange - Create legacy builder without service provider
#pragma warning disable CS0618
        var builder = new DataFlowGraphBuilder("test");
#pragma warning restore CS0618

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.ConfigureEpochs(config =>
            {
                config.SetPolicy(EpochPolicy.ByCount(10));
                config.AddProcessor("proc1");
            }));
        
        Assert.Contains("service provider", ex.Message);
        Assert.Contains("coordinatorFactory", ex.Message);
    }

    [Fact]
    public void ConfigureEpochs_RequiresAtLeastOneProcessor()
    {
        // Arrange
        var builder = GraphHelpers.CreateGraphBuilder("test");

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.ConfigureEpochs(config =>
            {
                config.SetPolicy(EpochPolicy.ByCount(10));
                // No processor added
            }, _ => _coordinator));
        
        Assert.Contains("processor", ex.Message);
    }

    [Fact]
    public void ConfigureEpochs_SingleProcessor_CreatesNodes()
    {
        // Arrange & Act
        var builder = GraphHelpers.CreateGraphBuilder("test");
        builder.ConfigureEpochs(config =>
        {
            config.SetPolicy(EpochPolicy.ByCount(100));
            config.AddProcessor("processor1");
        }, _ => _coordinator);
        
        var graph = builder.Build();
        
        // Assert - graph should build successfully
        Assert.NotNull(graph);
        Assert.Equal("test", graph.Name);
    }

    [Fact]
    public void ConfigureEpochs_MultipleProcessors_CreatesMultipleNodes()
    {
        // Arrange & Act
        var builder = GraphHelpers.CreateGraphBuilder("test");
        builder.ConfigureEpochs(config =>
        {
            config.SetPolicy(EpochPolicy.ByCount(50));
            config.AddProcessor("processor1");
            config.AddProcessor("processor2");
            config.AddProcessor("processor3");
        }, _ => _coordinator);
        
        var graph = builder.Build();
        
        // Assert
        Assert.NotNull(graph);
    }

    [Fact]
    public void EpochConfiguration_RejectsNullPolicy()
    {
        // Arrange
        var config = new EpochConfiguration();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => config.SetPolicy(null!));
    }

    [Fact]
    public void EpochConfiguration_RejectsDuplicateProcessorNames()
    {
        // Arrange
        var config = new EpochConfiguration();
        config.AddProcessor("proc1");

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => config.AddProcessor("proc1"));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public void EpochConfiguration_RejectsEmptyProcessorName()
    {
        // Arrange
        var config = new EpochConfiguration();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => config.AddProcessor(""));
        Assert.Throws<ArgumentException>(() => config.AddProcessor("   "));
        Assert.Throws<ArgumentException>(() => config.AddProcessor(null!));
    }

    [Fact]
    public async Task ConfigureEpochs_WithHooks_ExecutesCorrectly()
    {
        // Arrange
        var executionLog = new List<string>();
        
        var builder = GraphHelpers.CreateGraphBuilder("test");
        builder.ConfigureEpochs(config =>
        {
            config.SetPolicy(EpochPolicy.ByCount(10));
            config.AddProcessor("processor1");
            
            config.OnBeginEpoch(async (epoch, ct) =>
            {
                executionLog.Add("begin");
                await Task.Yield();
            });
            
            config.OnCommitEpoch(async (epoch, ct) =>
            {
                executionLog.Add("commit");
                await Task.Yield();
            });
        }, _ => _coordinator);

        var graph = builder.Build();

        // Create and publish an epoch
        var vector = EpochVector.FromSingleSource("test", 1);
        var epoch = await _coordinator.GetOrCreateEpochAsync("test", vector);
        
        await epoch.QueueSerializedOperationAsync<TestService>(async svc =>
        {
            executionLog.Add("operation");
            await Task.Yield();
        });
        
        epoch.CompleteOperations();

        // Get the source node and publish the epoch
        // Note: In a real scenario, this would be done automatically by the segmenter
        var source = GetEpochSource(graph);
        await source.PublishEpochAsync(epoch);
        source.SignalCompletion();

        // Act - Execute graph
        var context = new TestExecutionContext();
        await graph.ExecuteAsync(context);

        // Assert
        Assert.Equal(3, executionLog.Count);
        Assert.Equal("begin", executionLog[0]);
        Assert.Equal("operation", executionLog[1]);
        Assert.Equal("commit", executionLog[2]);
    }

    [Fact]
    public async Task ConfigureEpochs_ErrorHook_CalledOnFailure()
    {
        // Arrange
        Exception? capturedError = null;
        var builder = GraphHelpers.CreateGraphBuilder("test");
        
        builder.ConfigureEpochs(config =>
        {
            config.AddProcessor("processor1");
            
            config.OnEpochError(async (epoch, ex, ct) =>
            {
                capturedError = ex;
                await Task.Yield();
            });
        }, _ => _coordinator);

        var graph = builder.Build();

        // Create epoch with failing operation
        var vector = EpochVector.FromSingleSource("test", 1);
        var epoch = await _coordinator.GetOrCreateEpochAsync("test", vector);
        
        var expectedError = new InvalidOperationException("Test error");
        await epoch.QueueSerializedOperationAsync<TestService>(async svc =>
        {
            await Task.Yield();
            throw expectedError;
        });
        
        epoch.CompleteOperations();

        var source = GetEpochSource(graph);
        await source.PublishEpochAsync(epoch);
        source.SignalCompletion();

        // Act & Assert
        var context = new TestExecutionContext();
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await graph.ExecuteAsync(context);
        });
        
        Assert.NotNull(capturedError);
        Assert.Same(expectedError, capturedError);
    }

    [Fact]
    public async Task ConfigureEpochs_MultipleProcessors_AllReceiveEpochs()
    {
        // Arrange
        var processedEpochs = new System.Collections.Concurrent.ConcurrentBag<int>();
        
        var builder = GraphHelpers.CreateGraphBuilder("test");
        builder.ConfigureEpochs(config =>
        {
            config.AddProcessor("processor1");
            config.AddProcessor("processor2");
            
            config.OnCommitEpoch(async (epoch, ct) =>
            {
                var seq = (int)epoch.Vector.GetSequence("test");
                processedEpochs.Add(seq);
                await Task.Yield();
            });
        }, _ => _coordinator);

        var graph = builder.Build();

        // Publish multiple epochs
        var source = GetEpochSource(graph);
        for (int i = 1; i <= 3; i++)
        {
            var vector = EpochVector.FromSingleSource("test", i);
            var epoch = await _coordinator.GetOrCreateEpochAsync("test", vector);
            epoch.CompleteOperations();
            await source.PublishEpochAsync(epoch);
        }
        source.SignalCompletion();

        // Act
        var context = new TestExecutionContext();
        await graph.ExecuteAsync(context);

        // Assert - Multiple processors compete for epochs (load balancing)
        // Total epochs processed = 3 (each epoch processed by exactly one processor)
        Assert.Equal(3, processedEpochs.Count);
        Assert.Contains(1, processedEpochs);
        Assert.Contains(2, processedEpochs);
        Assert.Contains(3, processedEpochs);
        // Verify each epoch was processed exactly once
        Assert.Equal(1, processedEpochs.Count(e => e == 1));
        Assert.Equal(1, processedEpochs.Count(e => e == 2));
        Assert.Equal(1, processedEpochs.Count(e => e == 3));
    }

    [Fact]
    public void EpochPolicy_ByCount_ValidatesInput()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => EpochPolicy.ByCount(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => EpochPolicy.ByCount(-1));
        
        var policy = EpochPolicy.ByCount(100);
        Assert.Equal(100, policy.ItemCount);
        Assert.False(policy.HasTimeWindow);
    }

    [Fact]
    public void EpochPolicy_ByTime_ValidatesInput()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => EpochPolicy.ByTime(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => EpochPolicy.ByTime(TimeSpan.FromSeconds(-1)));
        
        var policy = EpochPolicy.ByTime(TimeSpan.FromSeconds(5));
        Assert.True(policy.HasTimeWindow);
        Assert.Equal(TimeSpan.FromSeconds(5), policy.WindowPeriod);
    }

    [Fact]
    public void EpochPolicy_ByCountOrTime_ValidatesInput()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            EpochPolicy.ByCountOrTime(0, TimeSpan.FromSeconds(5)));
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            EpochPolicy.ByCountOrTime(100, TimeSpan.Zero));
        
        var policy = EpochPolicy.ByCountOrTime(50, TimeSpan.FromSeconds(10));
        Assert.Equal(50, policy.ItemCount);
        Assert.Equal(TimeSpan.FromSeconds(10), policy.WindowPeriod);
        Assert.True(policy.HasTimeWindow);
    }

    [Fact]
    public void EpochPolicy_Default_IsValid()
    {
        // Act
        var policy = EpochPolicy.Default;

        // Assert
        Assert.Equal(100, policy.ItemCount);
        Assert.False(policy.HasTimeWindow);
    }

    // Helper method to access epoch source via reflection (since it's internal)
    private EpochSourceNode GetEpochSource(DataFlowGraph graph)
    {
        var field = typeof(DataFlowGraph).GetField("_epochSource", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (EpochSourceNode)field!.GetValue(graph)!;
    }

    // Test helper classes
    private class TestService
    {
        public int CallCount { get; private set; }
        public void DoWork() => CallCount++;
    }

    private class TestDbContext
    {
        public bool TransactionStarted { get; private set; }
        public bool TransactionCommitted { get; private set; }
        
        public Task BeginTransactionAsync() 
        { 
            TransactionStarted = true;
            return Task.CompletedTask;
        }
        
        public Task CommitTransactionAsync() 
        { 
            TransactionCommitted = true;
            return Task.CompletedTask;
        }
    }

    private class TestExecutionContext : IExecutionContext
    {
        private readonly CancellationTokenSource _cts = new();
        
        public CancellationToken CancellationToken => _cts.Token;
        public IServiceProvider ServiceProvider => throw new NotImplementedException();
        public Guid InvocationId { get; } = Guid.NewGuid();
        public ICheckpoint? RecoveryCheckpoint { get; } = null;
        public POC.Observability.IDataFlowMetrics? Metrics { get; } = null;
        public ITriggerContext? TriggerContext { get; } = null;
        public IParameterProvider Parameters => new TriggerContextParameterProvider(TriggerContext);
        
        public void Cancel() => _cts.Cancel();
    }
}
