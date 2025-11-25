namespace DataFlow.POC.Tests.Observability;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using DataFlow.POC.Core;
using DataFlow.POC.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

/// <summary>
/// Tests for DataFlow POC metrics collection.
/// Verifies basic metrics functionality.
/// </summary>
public class MetricsTests
{
    [Fact]
    public void DataFlowMetrics_CanBeCreated()
    {
        // Arrange
        var meterFactory = new TestMeterFactory();
        var meterAccessor = new MeterAccessor(meterFactory);

        // Act
        var metrics = new DataFlowMetrics(meterAccessor);

        // Assert
        Assert.NotNull(metrics);
        Assert.NotNull(metrics.GlobalTags);
    }

    [Fact]
    public void DataFlowGraph_CanBeCreatedWithMetrics()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<DataFlowGraph>>();

        var meterFactory = new TestMeterFactory();
        var meterAccessor = new MeterAccessor(meterFactory);
        var metrics = new DataFlowMetrics(meterAccessor);

        // Act
        var graph = new DataFlowGraph("TestFlow", logger, metrics);

        // Assert
        Assert.NotNull(graph);
        Assert.Equal("TestFlow", graph.Name);
    }

    [Fact]
    public void DataFlowGraph_CanBeCreatedWithoutMetrics()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<DataFlowGraph>>();

        // Act
        var graph = new DataFlowGraph("TestFlow", logger, null);

        // Assert
        Assert.NotNull(graph);
        Assert.Equal("TestFlow", graph.Name);
    }

    [Fact]
    public void ExecutionContext_ContainsMetricsProperty()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        var meterFactory = new TestMeterFactory();
        var meterAccessor = new MeterAccessor(meterFactory);
        var metrics = new DataFlowMetrics(meterAccessor);

        // Act
        var context = new ExecutionContext(
            serviceProvider,
            CancellationToken.None,
            Guid.NewGuid(),
            null,
            metrics);

        // Assert
        Assert.Same(metrics, context.Metrics);
    }
}

internal class TestMeterFactory : IMeterFactory
{
    public Meter Create(MeterOptions options) => new(options.Name, options.Version);
    public void Dispose() { }
}



