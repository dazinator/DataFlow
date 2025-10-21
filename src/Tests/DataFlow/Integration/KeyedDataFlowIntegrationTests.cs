namespace Tests.DataFlow.Integration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Uniun.DataFlow;
using Uniun.DataFlow.Builder.Graph;
using Xunit.Abstractions;

/// <summary>
/// Integration tests demonstrating real-world usage of keyed dataflow registration.
/// </summary>
[IntegrationTest]
public class KeyedDataFlowIntegrationTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public KeyedDataFlowIntegrationTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task Should_ExecuteCompleteETLPipeline_UsingKeyedDataFlow()
    {
        // Arrange - Simulate an ETL pipeline scenario
        var sourceData = Enumerable.Range(1, 100).ToArray();
        var processedData = new List<string>();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(_testOutputHelper));
        services.AddDataFlows();
        services.AddDataFlowMetrics();

        // Register a complete ETL pipeline
        services.AddKeyedDataFlow("etl-pipeline", builder =>
        {
            builder.AddProducer("extract", sp => new TestProducer<int>(sourceData))
                .AddTransform("transform", sp => new NumberTransformer("item"))
                .AddBatch("batch", maxBatchSize: 10, windowPeriod: TimeSpan.FromSeconds(1))
                .AddProcessor("load", sp => new TestProcessor<string[]>(
                    onProcessItem: batch => 
                    {
                        foreach (var item in batch)
                        {
                            processedData.Add(item);
                        }
                    }));
        });

        var serviceProvider = services.BuildServiceProvider();
        var dataflow = serviceProvider.GetDataFlow("etl-pipeline");
        var context = DataFlowContextTestUtils.GetContext("etl", Guid.NewGuid(), serviceProvider);

        // Act
        await dataflow.ExecuteAsync(context);

        // Assert
        processedData.Count.ShouldBe(100);
        processedData.ShouldAllBe(item => item.StartsWith("item"));
    }

    [Fact]
    public async Task Should_ExecuteMultipleParallelInstances_Independently()
    {
        // Arrange - Simulate processing multiple data sets in parallel
        var dataset1 = Enumerable.Range(1, 50).ToArray();
        var dataset2 = Enumerable.Range(51, 50).ToArray();
        var dataset3 = Enumerable.Range(101, 50).ToArray();

        var results1 = new List<int>();
        var results2 = new List<int>();
        var results3 = new List<int>();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(_testOutputHelper));
        services.AddDataFlows();
        services.AddDataFlowMetrics();

        // Create three independent instances
        var serviceProvider = services.BuildServiceProvider();

        var flow1 = new StructuredDataFlowBuilder(serviceProvider, "flow1");
        flow1.AddProducer("source", sp => new TestProducer<int>(dataset1))
            .AddProcessor("processor", sp => new TestProcessor<int>(
                onProcessItem: item => results1.Add(item)));
        var dataflow1 = await flow1.Build();

        var flow2 = new StructuredDataFlowBuilder(serviceProvider, "flow2");
        flow2.AddProducer("source", sp => new TestProducer<int>(dataset2))
            .AddProcessor("processor", sp => new TestProcessor<int>(
                onProcessItem: item => results2.Add(item)));
        var dataflow2 = await flow2.Build();

        var flow3 = new StructuredDataFlowBuilder(serviceProvider, "flow3");
        flow3.AddProducer("source", sp => new TestProducer<int>(dataset3))
            .AddProcessor("processor", sp => new TestProcessor<int>(
                onProcessItem: item => results3.Add(item)));
        var dataflow3 = await flow3.Build();

        var context1 = DataFlowContextTestUtils.GetContext("batch1", Guid.NewGuid(), serviceProvider);
        var context2 = DataFlowContextTestUtils.GetContext("batch2", Guid.NewGuid(), serviceProvider);
        var context3 = DataFlowContextTestUtils.GetContext("batch3", Guid.NewGuid(), serviceProvider);

        // Act - Execute all three in parallel
        await Task.WhenAll(
            dataflow1.ExecuteAsync(context1),
            dataflow2.ExecuteAsync(context2),
            dataflow3.ExecuteAsync(context3)
        );

        // Assert - Each flow processed its own dataset
        results1.Count.ShouldBe(50);
        results2.Count.ShouldBe(50);
        results3.Count.ShouldBe(50);

        results1.ShouldBe(dataset1);
        results2.ShouldBe(dataset2);
        results3.ShouldBe(dataset3);
    }

    [Fact]
    public void Should_InspectDataFlowGraph_WithoutBuilding()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(_testOutputHelper));
        services.AddDataFlows();
        services.AddDataFlowMetrics();

        services.AddKeyedDataFlow("inspectable-flow", builder =>
        {
            builder.AddProducer("source", sp => new TestProducer<int>(new[] { 1, 2, 3 }))
                .AddTransform("transform", sp => new NumberTransformer("num"))
                .AddBatch("batcher", maxBatchSize: 5, windowPeriod: TimeSpan.FromSeconds(1))
                .AddProcessor("sink", sp => new TestProcessor<string[]>());
        });

        var serviceProvider = services.BuildServiceProvider();
        var dataflow = serviceProvider.GetDataFlow("inspectable-flow");

        // Act - Get the graph from the dataflow
        var graph = dataflow.Graph;

        // Assert - Can inspect the complete graph structure
        graph.Name.ShouldBe("inspectable-flow");
        graph.BlockDefinitions.Count.ShouldBe(4);

        var sourceBlock = graph.GetBlockDefinition("source");
        sourceBlock.OutputType.ShouldBe(typeof(int));

        var transformBlock = graph.GetBlockDefinition("transform");
        transformBlock.InputType.ShouldBe(typeof(int));
        transformBlock.OutputType.ShouldBe(typeof(string));

        var batchBlock = graph.GetBlockDefinition("batcher");
        batchBlock.InputType.ShouldBe(typeof(string));
        batchBlock.OutputType.ShouldBe(typeof(string[]));

        var sinkBlock = graph.GetBlockDefinition("sink");
        sinkBlock.InputType.ShouldBe(typeof(string[]));

        // Verify connections
        var connections = graph.Connections.ToList();
        connections.Count.ShouldBe(3);

        connections[0].SourceBlockName.ShouldBe("source");
        connections[0].TargetBlockName.ShouldBe("transform");

        connections[1].SourceBlockName.ShouldBe("transform");
        connections[1].TargetBlockName.ShouldBe("batcher");

        connections[2].SourceBlockName.ShouldBe("batcher");
        connections[2].TargetBlockName.ShouldBe("sink");
    }

    [Fact]
    public async Task Should_SupportMultipleNamedFlows_InSameApplication()
    {
        // Arrange - Simulate an application with multiple data processing pipelines
        var userIds = new[] { 1, 2, 3, 4, 5 };
        var orderIds = new[] { 101, 102, 103 };

        var processedUsers = new List<string>();
        var processedOrders = new List<string>();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(_testOutputHelper));
        services.AddDataFlows();
        services.AddDataFlowMetrics();

        // Register user import pipeline
        services.AddKeyedDataFlow("user-import", builder =>
        {
            builder.AddProducer("user-source", sp => new TestProducer<int>(userIds))
                .AddTransform("user-transform", sp => new NumberTransformer("user"))
                .AddProcessor("user-sink", sp => new TestProcessor<string>(
                    onProcessItem: user => processedUsers.Add(user)));
        });

        // Register order processing pipeline
        services.AddKeyedDataFlow("order-processing", builder =>
        {
            builder.AddProducer("order-source", sp => new TestProducer<int>(orderIds))
                .AddTransform("order-transform", sp => new NumberTransformer("order"))
                .AddProcessor("order-sink", sp => new TestProcessor<string>(
                    onProcessItem: order => processedOrders.Add(order)));
        });

        var serviceProvider = services.BuildServiceProvider();

        // Act - Execute both pipelines
        var userFlow = serviceProvider.GetDataFlow("user-import");
        var orderFlow = serviceProvider.GetDataFlow("order-processing");

        var userContext = DataFlowContextTestUtils.GetContext("user-import", Guid.NewGuid(), serviceProvider);
        var orderContext = DataFlowContextTestUtils.GetContext("order-processing", Guid.NewGuid(), serviceProvider);

        await Task.WhenAll(
            userFlow.ExecuteAsync(userContext),
            orderFlow.ExecuteAsync(orderContext)
        );

        // Assert
        processedUsers.Count.ShouldBe(5);
        processedOrders.Count.ShouldBe(3);

        processedUsers.ShouldAllBe(u => u.StartsWith("user"));
        processedOrders.ShouldAllBe(o => o.StartsWith("order"));
    }

    [Fact]
    public async Task Should_WorkWithScopedServices()
    {
        // Arrange - Simulate working with scoped database context
        var data = Enumerable.Range(1, 10).ToArray();
        var processedItems = new List<int>();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(_testOutputHelper));
        services.AddDataFlows();
        services.AddDataFlowMetrics();

        // Register a scoped service
        services.AddScoped<TestScopedService>();

        services.AddKeyedDataFlow("scoped-flow", builder =>
        {
            builder.AddProducer("source", sp => new TestProducer<int>(data))
                .AddProcessor("processor", sp => 
                {
                    // Access the scoped service
                    var scopedService = sp.GetRequiredService<TestScopedService>();
                    return new TestProcessor<int>(
                        onProcessItem: item =>
                        {
                            scopedService.Process(item);
                            processedItems.Add(item);
                        });
                });
        });

        var rootProvider = services.BuildServiceProvider();

        // Act - Execute within a scope
        using (var scope = rootProvider.CreateScope())
        {
            var dataflow = scope.ServiceProvider.GetDataFlow("scoped-flow");
            var context = DataFlowContextTestUtils.GetContext("scoped-test", Guid.NewGuid(), scope.ServiceProvider);
            await dataflow.ExecuteAsync(context);
        }

        // Assert
        processedItems.Count.ShouldBe(10);
        processedItems.ShouldBe(data);
    }

    [Fact]
    public void Should_GetSameDataFlow_WhenRegisteredAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(_testOutputHelper));
        services.AddDataFlows();
        services.AddDataFlowMetrics();

        services.AddKeyedDataFlow("singleton-flow", builder =>
        {
            builder.AddProducer("source", sp => new TestProducer<int>(new[] { 1 }))
                .AddProcessor("processor", sp => new TestProcessor<int>());
        }, ServiceLifetime.Singleton);

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var dataflow1 = serviceProvider.GetDataFlow("singleton-flow");
        var dataflow2 = serviceProvider.GetDataFlow("singleton-flow");

        // Assert
        dataflow1.ShouldBeSameAs(dataflow2);
    }

    // Helper class for scoped service test
    private class TestScopedService
    {
        public void Process(int item)
        {
            // Simulate some scoped operation
        }
    }
}
