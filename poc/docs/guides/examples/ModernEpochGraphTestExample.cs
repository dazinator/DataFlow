/// <summary>
/// Modern example of testing epoch-based DataFlow graphs.
/// 
/// This example demonstrates the recommended approach for testing DataFlow graphs
/// with epoch sources and actors, using the simplified APIs introduced in recent versions.
/// 
/// Key Features:
/// - Auto-registered IEpochCoordinator (no manual setup needed)
/// - AddSourceBlock and AddActorBlock extension methods
/// - Clean DI-based graph construction
/// - Proper resource cleanup
/// 
/// Compare this to the old pattern which required manual BlockContext creation,
/// coordinator registration, and verbose block instantiation.
/// </summary>

using DataFlow.POC.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using Xunit;
using ExecutionContext = DataFlow.POC.Core.ExecutionContext;

namespace DataFlow.POC.Examples;

public class ModernEpochGraphTestExample
{
    /// <summary>
    /// Example source actor that produces 3 epochs with integers.
    /// </summary>
    public class SimpleIntegerSource : DataFlow.POC.Core.SourceActorBase<int>
    {
        public SimpleIntegerSource() : base("simple-int-source") { }

        public override async IAsyncEnumerable<DataFlow.POC.Core.IEpochStream<int>> ProduceEpochsAsync(
            DataFlow.POC.Core.IActorExecutionContext context)
        {
            for (int epochNum = 1; epochNum <= 3; epochNum++)
            {
                var items = ProduceEpochItems(epochNum);
                var epochStream = await CreateEpochStreamAsync(
                    context, 
                    epochNum, 
                    items, 
                    context.CancellationToken);
                
                yield return epochStream;
            }
        }

        private async IAsyncEnumerable<int> ProduceEpochItems(int epochNum)
        {
            // Produce single item per epoch for simplicity
            yield return epochNum;
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Example actor that doubles input values.
    /// </summary>
    public class DoublerActor : DataFlow.POC.Core.IStreamActor<int, int>
    {
        public async IAsyncEnumerable<int> RunAsync(
            IAsyncEnumerable<int> input,
            DataFlow.POC.Core.IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                yield return item * 2;
            }
        }
    }

    /// <summary>
    /// Reusable collector actor for test assertions.
    /// This is a common pattern - create once and reuse across tests.
    /// </summary>
    public class CollectorActor<T> : DataFlow.POC.Core.IStreamActor<T, object>
    {
        private readonly List<T> _results;

        public CollectorActor(List<T> results)
        {
            _results = results;
        }

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<T> input,
            DataFlow.POC.Core.IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                _results.Add(item);
            }
            yield break; // Terminal actor - no output
        }
    }

    [Fact]
    public async Task EpochGraph_WithModernAPI_ProcessesAllEpochs()
    {
        // Arrange
        var results = new List<int>();
        var services = new ServiceCollection();
        
        // Register actors as scoped (standard DI pattern)
        services.AddScoped<SimpleIntegerSource>();
        services.AddScoped<DoublerActor>();
        services.AddScoped(_ => new CollectorActor<int>(results));
        
        // Modern API: AddDataFlows auto-registers IEpochCoordinator as scoped
        // No need for manual coordinator creation or registration!
        services.AddDataFlows("test", df =>
        {
            // Simple one-line registrations - all dependencies injected automatically
            df.AddSourceBlock<int, SimpleIntegerSource>("source");
            df.AddActorBlock<int, int, DoublerActor>("doubler");
            df.AddActorBlock<int, object, CollectorActor<int>>("collector");
            
            // Define graph topology using fluent API
            df.AddGraph("main", g =>
            {
                g.UseBlock("source")
                 .UseBlock("doubler")
                 .UseBlock("collector")
                 .Connect("source", "doubler")
                 .Connect("doubler", "collector");
            });
        });
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Act
        var graph = serviceProvider.GetKeyedService<DataFlow.POC.Core.DataFlowGraph>("test:main");
        Assert.NotNull(graph);
        
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);
        await graph.ExecuteAsync(context);
        
        // Assert
        Assert.Equal(3, results.Count);
        Assert.Equal(2, results[0]);  // 1 * 2
        Assert.Equal(4, results[1]);  // 2 * 2
        Assert.Equal(6, results[2]);  // 3 * 2
        
        // Cleanup - important for proper resource disposal
        await serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task EpochGraph_WithTerminalBlock_ConsumesAllData()
    {
        // This example shows adding a terminal block to ensure all data is consumed
        
        // Arrange
        var results = new List<int>();
        var services = new ServiceCollection();
        
        services.AddScoped<SimpleIntegerSource>();
        services.AddScoped<DoublerActor>();
        services.AddScoped(_ => new CollectorActor<int>(results));
        
        services.AddDataFlows("test", df =>
        {
            df.AddSourceBlock<int, SimpleIntegerSource>("source");
            df.AddActorBlock<int, int, DoublerActor>("doubler");
            df.AddActorBlock<int, object, CollectorActor<int>>("terminal");
            
            df.AddGraph("terminal-test", g =>
            {
                g.UseBlock("source")
                 .UseBlock("doubler")
                 .UseBlock("terminal")
                 .Connect("source", "doubler")
                 .Connect("doubler", "terminal");
            });
        });
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Act
        var graph = serviceProvider.GetKeyedService<DataFlow.POC.Core.DataFlowGraph>("test:terminal-test");
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);
        await graph!.ExecuteAsync(context);
        
        // Assert
        Assert.True(results.Count >= 3, $"Expected at least 3 results, got {results.Count}");
        
        // Cleanup
        await serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task EpochGraph_WithCancellation_StopsGracefully()
    {
        // This example shows proper cancellation handling
        
        // Arrange
        var results = new List<int>();
        var services = new ServiceCollection();
        
        services.AddScoped<SimpleIntegerSource>();
        services.AddScoped<DoublerActor>();
        services.AddScoped(_ => new CollectorActor<int>(results));
        
        services.AddDataFlows("test", df =>
        {
            df.AddSourceBlock<int, SimpleIntegerSource>("source");
            df.AddActorBlock<int, int, DoublerActor>("doubler");
            df.AddActorBlock<int, object, CollectorActor<int>>("collector");
            
            df.AddGraph("cancel-test", g =>
            {
                g.UseBlock("source")
                 .UseBlock("doubler")
                 .UseBlock("collector")
                 .Connect("source", "doubler")
                 .Connect("doubler", "collector");
            });
        });
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Act
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100)); // Cancel quickly
        
        var graph = serviceProvider.GetKeyedService<DataFlow.POC.Core.DataFlowGraph>("test:cancel-test");
        var context = new ExecutionContext(serviceProvider, cts.Token);
        
        // Note: Cancellation may or may not throw depending on timing
        try
        {
            await graph!.ExecuteAsync(context);
        }
        catch (OperationCanceledException)
        {
            // Expected if cancellation happened during execution
        }
        
        // Cleanup
        await serviceProvider.DisposeAsync();
    }
}

/// <summary>
/// Alternative: Using IAsyncLifetime for better test lifecycle management.
/// This pattern is recommended for test classes with expensive setup.
/// </summary>
public class ModernEpochGraphTestsWithLifetime : IAsyncLifetime
{
    private ServiceProvider? _serviceProvider;
    private readonly List<int> _results = new();

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        
        services.AddScoped<ModernEpochGraphTestExample.SimpleIntegerSource>();
        services.AddScoped<ModernEpochGraphTestExample.DoublerActor>();
        services.AddScoped(_ => new ModernEpochGraphTestExample.CollectorActor<int>(_results));
        
        services.AddDataFlows("test", df =>
        {
            df.AddSourceBlock<int, ModernEpochGraphTestExample.SimpleIntegerSource>("source");
            df.AddActorBlock<int, int, ModernEpochGraphTestExample.DoublerActor>("doubler");
            df.AddActorBlock<int, object, ModernEpochGraphTestExample.CollectorActor<int>>("collector");
            
            df.AddGraph("main", g =>
            {
                g.UseBlock("source")
                 .UseBlock("doubler")
                 .UseBlock("collector")
                 .Connect("source", "doubler")
                 .Connect("doubler", "collector");
            });
        });
        
        _serviceProvider = services.BuildServiceProvider();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider != null)
        {
            await _serviceProvider.DisposeAsync();
        }
    }

    [Fact]
    public async Task EpochGraph_ProcessesData_UsingLifetimePattern()
    {
        // Arrange - already done in InitializeAsync
        Assert.NotNull(_serviceProvider);
        _results.Clear();
        
        // Act
        var graph = _serviceProvider.GetKeyedService<DataFlow.POC.Core.DataFlowGraph>("test:main");
        var context = new ExecutionContext(_serviceProvider, CancellationToken.None);
        await graph!.ExecuteAsync(context);
        
        // Assert
        Assert.Equal(3, _results.Count);
        Assert.Equal(2, _results[0]);
        Assert.Equal(4, _results[1]);
        Assert.Equal(6, _results[2]);
    }
}
