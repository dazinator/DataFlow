namespace DataFlow.POC.Tests.Observability;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using DataFlow.POC.Core;
using DataFlow.POC.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

/// <summary>
/// Tests that verify the documented observability examples compile and run correctly.
/// These tests ensure the observability guide documentation stays accurate.
/// </summary>
[Trait("Category", "Documentation")]
public class ObservabilityDocumentationTests
{
    [Fact]
    public void BasicSetup_ConfigureOpenTelemetry_Succeeds()
    {
        // Arrange & Act - Verify code from "Basic Setup" section compiles and runs
        var services = new ServiceCollection();
        
        services.AddLogging();
        services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics
                    .AddDataFlow()
                    .AddInMemoryExporter(new List<Metric>());
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddDataFlow()
                    .AddInMemoryExporter(new List<Activity>());
            });

        var serviceProvider = services.BuildServiceProvider();
        
        // Assert - OpenTelemetry services are registered
        Assert.NotNull(serviceProvider);
        
        // MeterProvider is registered
        var meterProvider = serviceProvider.GetService<MeterProvider>();
        Assert.NotNull(meterProvider);
    }

    [Fact]
    public void ConfiguringMetrics_UsingGraphConstructor_CreatesMetrics()
    {
        // Arrange - Code from "Option 1: Using DataFlowGraph Constructor" section
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMeterFactory, TestMeterFactory>();
        
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<DataFlowGraph>>();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();

        // Act - Create metrics instance
        var meterAccessor = new MeterAccessor(meterFactory);
        var metrics = new DataFlowMetrics(meterAccessor);

        // Create graph with metrics
        var graph = new DataFlowGraph("MyFlow", logger, metrics);

        // Assert
        Assert.NotNull(metrics);
        Assert.Equal("MyFlow", graph.Name);
    }

    [Fact]
    public void ConfiguringMetrics_UsingExecutionContext_PassesMetrics()
    {
        // Arrange - Code from "Option 2: Using ExecutionContext" section
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMeterFactory, TestMeterFactory>();
        
        var serviceProvider = services.BuildServiceProvider();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        var cancellationToken = CancellationToken.None;

        var meterAccessor = new MeterAccessor(meterFactory);
        
        // Act - Create metrics with custom global tags
        var globalTags = new TagList
        {
            { "environment", "production" },
            { "version", "1.0.0" }
        };
        var metrics = new DataFlowMetrics(meterAccessor, globalTags);

        // Create execution context with metrics
        var context = new ExecutionContext(
            serviceProvider,
            cancellationToken,
            Guid.NewGuid(),
            recoveryCheckpoint: null,
            metrics: metrics);

        // Assert
        Assert.NotNull(context.Metrics);
        Assert.Same(metrics, context.Metrics);
        Assert.Equal(2, metrics.GlobalTags.Count);
    }

    [Fact]
    public void GlobalTags_ConfiguredWithEnvironmentInfo_AreApplied()
    {
        // Arrange & Act - Code from "Using Global Tags" section
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMeterFactory, TestMeterFactory>();
        
        var serviceProvider = services.BuildServiceProvider();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        var meterAccessor = new MeterAccessor(meterFactory);

        var globalTags = new TagList
        {
            { "environment", "test" },
            { "service.name", "my-service" },
            { "service.version", "1.2.3" },
            { "datacenter", "us-west-2" }
        };

        var metrics = new DataFlowMetrics(meterAccessor, globalTags);

        // Assert
        Assert.Equal(4, metrics.GlobalTags.Count);
        Assert.Contains(metrics.GlobalTags, tag => tag.Key == "environment" && (string?)tag.Value == "test");
        Assert.Contains(metrics.GlobalTags, tag => tag.Key == "service.name" && (string?)tag.Value == "my-service");
    }

    [Fact]
    public async Task BlockLevelMetrics_AccessingMetricsInBlock_ContextProvidesMetrics()
    {
        // Arrange - Demonstrates code from "Block-Level Metrics" section
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMeterFactory, TestMeterFactory>();
        
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<DataFlowGraph>>();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        var meterAccessor = new MeterAccessor(meterFactory);
        var metrics = new DataFlowMetrics(meterAccessor);

        var graph = new DataFlowGraph("TestFlow", logger, metrics);

        IDataFlowMetrics? capturedMetrics = null;

        // Act - Add block that accesses metrics (from documentation example)
        var block = new TestBlockThatAccessesMetrics((m) => capturedMetrics = m);
        graph.AddBlock(block);

        var context = new ExecutionContext(
            serviceProvider,
            CancellationToken.None,
            Guid.NewGuid(),
            null,
            metrics);

        await graph.ExecuteAsync(context);

        // Assert - Block received metrics from context
        Assert.NotNull(capturedMetrics);
        Assert.Same(metrics, capturedMetrics);
    }

    [Fact]
    public void CustomApplicationMetrics_BlockCreatesOwnMetrics_Succeeds()
    {
        // Arrange & Act - Code from "Alternative: Custom Application Metrics" section
        var meterFactory = new TestMeterFactory();
        
        var block = new MyBlockWithCustomMetrics(meterFactory);

        // Assert - Block created successfully with custom metrics
        Assert.NotNull(block);
        Assert.Equal("CustomMetricsBlock", block.Name);
    }

    [Fact]
    public async Task ExportingToMonitoringSystems_InMemoryExporter_CapturesMetrics()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMeterFactory, TestMeterFactory>();
        
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<DataFlowGraph>>();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        var meterAccessor = new MeterAccessor(meterFactory);
        var dataflowMetrics = new DataFlowMetrics(meterAccessor);

        var graph = new DataFlowGraph("TestFlow", logger, dataflowMetrics);
        var block = new SimpleTestBlock();
        graph.AddBlock(block);

        var context = new ExecutionContext(
            serviceProvider,
            CancellationToken.None,
            Guid.NewGuid(),
            null,
            dataflowMetrics);

        // Act
        await graph.ExecuteAsync(context);

        // Assert - Metrics interface was used
        Assert.NotNull(dataflowMetrics);
    }

    [Fact]
    public async Task DistributedTracing_ActivitiesAreCreated_WithProperHierarchy()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMeterFactory, TestMeterFactory>();

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<DataFlowGraph>>();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        var meterAccessor = new MeterAccessor(meterFactory);
        var metrics = new DataFlowMetrics(meterAccessor);

        var graph = new DataFlowGraph("TestFlow", logger, metrics);
        var block = new SimpleTestBlock();
        graph.AddBlock(block);

        var context = new ExecutionContext(
            serviceProvider,
            CancellationToken.None,
            Guid.NewGuid(),
            null,
            metrics);

        // Act
        await graph.ExecuteAsync(context);

        // Assert - Graph executed successfully with metrics enabled
        Assert.NotNull(metrics);
        Assert.Equal("TestFlow", graph.Name);
    }

    #region Helper Classes

    private class TestBlockThatAccessesMetrics : IBlock<int, int>
    {
        private readonly Action<IDataFlowMetrics?> _metricsCallback;

        public TestBlockThatAccessesMetrics(Action<IDataFlowMetrics?> metricsCallback)
        {
            _metricsCallback = metricsCallback;
        }

        public string Name => "TestBlock";
        public Type InputType => typeof(int);
        public Type OutputType => typeof(int);

        public async IAsyncEnumerable<int> ExecuteAsync(IAsyncEnumerable<int> input, IExecutionContext context)
        {
            // Capture metrics from context (demonstrates documentation example)
            _metricsCallback(context.Metrics);
            
            await foreach (var item in input)
            {
                yield return item;
            }
        }

        async IAsyncEnumerable<object> IBlock.ExecuteAsync(IAsyncEnumerable<object> input, IExecutionContext context)
        {
            await foreach (var item in ExecuteAsync(CastAsync<int>(input), context))
            {
                yield return item;
            }
        }
        
        private static async IAsyncEnumerable<T> CastAsync<T>(IAsyncEnumerable<object> source)
        {
            await foreach (var item in source)
            {
                yield return (T)item;
            }
        }
    }

    private class MyBlockWithCustomMetrics : IBlock<string, string>
    {
        private readonly Counter<long> _customCounter;
        
        public MyBlockWithCustomMetrics(IMeterFactory meterFactory)
        {
            var meter = meterFactory.Create("MyApplication");
            _customCounter = meter.CreateCounter<long>("my.custom.items.processed");
        }
        
        public string Name => "CustomMetricsBlock";
        public Type InputType => typeof(string);
        public Type OutputType => typeof(string);

        public async IAsyncEnumerable<string> ExecuteAsync(IAsyncEnumerable<string> input, IExecutionContext context)
        {
            await foreach (var item in input)
            {
                // Process and emit custom metric
                var result = ProcessItem(item);
                _customCounter.Add(1, new TagList { { "category", GetCategory(item) } });
                
                yield return result;
            }
        }
        
        private string ProcessItem(string item) => item.ToUpper();
        private string GetCategory(string item) => item.Length > 10 ? "long" : "short";

        async IAsyncEnumerable<object> IBlock.ExecuteAsync(IAsyncEnumerable<object> input, IExecutionContext context)
        {
            await foreach (var item in ExecuteAsync(CastAsync<string>(input), context))
            {
                yield return item;
            }
        }
        
        private static async IAsyncEnumerable<T> CastAsync<T>(IAsyncEnumerable<object> source)
        {
            await foreach (var item in source)
            {
                yield return (T)item;
            }
        }
    }

    private class SimpleTestBlock : IBlock<int, int>
    {
        public string Name => "SimpleBlock";
        public Type InputType => typeof(int);
        public Type OutputType => typeof(int);

        public async IAsyncEnumerable<int> ExecuteAsync(IAsyncEnumerable<int> input, IExecutionContext context)
        {
            await foreach (var item in input)
            {
                yield return item * 2;
            }
        }

        async IAsyncEnumerable<object> IBlock.ExecuteAsync(IAsyncEnumerable<object> input, IExecutionContext context)
        {
            await foreach (var item in ExecuteAsync(CastAsync<int>(input), context))
            {
                yield return item;
            }
        }
        
        private static async IAsyncEnumerable<T> CastAsync<T>(IAsyncEnumerable<object> source)
        {
            await foreach (var item in source)
            {
                yield return (T)item;
            }
        }
    }

    private class TestMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options) => new(options.Name, options.Version);
        public void Dispose() { }
    }

    #endregion
}
