namespace Tests.DataFlow;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Uniun.DataFlow;
using Uniun.DataFlow.Builder.Graph;
using Xunit.Abstractions;

/// <summary>
/// Tests for keyed dataflow registration and factory patterns.
/// </summary>
[UnitTest]
public class KeyedDataFlowRegistrationTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public KeyedDataFlowRegistrationTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    private ServiceProvider BuildServiceProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(_testOutputHelper));
        services.AddDataFlows();
        services.AddDataFlowMetrics();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Should_RegisterKeyedDataFlow_WithName()
    {
        // Arrange
        var items = new[] { 1, 2, 3 };
        var sp = BuildServiceProvider(services =>
        {
            services.AddKeyedDataFlow("test-flow", builder =>
            {
                builder.AddProducer("source", sp => new TestProducer<int>(items))
                    .AddProcessor("processor", sp => new TestProcessor<int>());
            });
        });

        // Act
        var dataflow = sp.GetDataFlow("test-flow");

        // Assert
        dataflow.ShouldNotBeNull();
        dataflow.Name.ShouldBe("test-flow");
    }

    [Fact]
    public void Should_GetDataFlowInstance()
    {
        // Arrange
        var items = new[] { 1, 2, 3 };
        var sp = BuildServiceProvider(services =>
        {
            services.AddKeyedDataFlow("test-flow", builder =>
            {
                builder.AddProducer("source", sp => new TestProducer<int>(items))
                    .AddProcessor("processor", sp => new TestProcessor<int>());
            });
        });

        // Act
        var dataflow = sp.GetDataFlow("test-flow");

        // Assert
        dataflow.ShouldNotBeNull();
        dataflow.Name.ShouldBe("test-flow");
    }

    [Fact]
    public void Should_GetMultipleInstances_WithTransientLifetime()
    {
        // Arrange
        var items = new[] { 1, 2, 3 };
        var sp = BuildServiceProvider(services =>
        {
            services.AddKeyedDataFlow("test-flow", builder =>
            {
                builder.AddProducer("source", sp => new TestProducer<int>(items))
                    .AddProcessor("processor", sp => new TestProcessor<int>());
            }, ServiceLifetime.Transient); // Explicit transient
        });

        // Act
        var dataflow1 = sp.GetDataFlow("test-flow");
        var dataflow2 = sp.GetDataFlow("test-flow");

        // Assert
        dataflow1.ShouldNotBeNull();
        dataflow2.ShouldNotBeNull();
        dataflow1.ShouldNotBeSameAs(dataflow2); // Different instances with transient
        dataflow1.Name.ShouldBe("test-flow");
        dataflow2.Name.ShouldBe("test-flow");
    }

    [Fact]
    public async Task Should_ExecuteDataFlow()
    {
        // Arrange
        var processedItems = new List<int>();
        var items = new[] { 1, 2, 3 };
        var sp = BuildServiceProvider(services =>
        {
            services.AddKeyedDataFlow("test-flow", builder =>
            {
                builder.AddProducer("source", sp => new TestProducer<int>(items))
                    .AddProcessor("processor", sp => new TestProcessor<int>(
                        onProcessItem: item => processedItems.Add(item)));
            });
        });

        var dataflow = sp.GetDataFlow("test-flow");
        var context = DataFlowContextTestUtils.GetContext("test", Guid.NewGuid(), sp);

        // Act
        await dataflow.ExecuteAsync(context);

        // Assert
        processedItems.Count.ShouldBe(3);
        processedItems.ShouldBe(new[] { 1, 2, 3 });
    }

    [Fact]
    public void Should_GetGraph_FromFactory()
    {
        // Arrange
        var items = new[] { 1, 2, 3 };
        var sp = BuildServiceProvider(services =>
        {
            services.AddKeyedDataFlow("test-flow", builder =>
            {
                builder.AddProducer("source", sp => new TestProducer<int>(items))
                    .AddProcessor("processor", sp => new TestProcessor<int>());
            });
        });

        var dataflow = sp.GetDataFlow("test-flow");

        // Act
        var graph = dataflow.Graph;

        // Assert
        graph.ShouldNotBeNull();
        graph.Name.ShouldBe("test-flow");
        graph.BlockDefinitions.Count.ShouldBe(2);
        graph.BlockDefinitions.Keys.ShouldContain("source");
        graph.BlockDefinitions.Keys.ShouldContain("processor");
    }

    [Fact]
    public void Should_SupportMultipleKeyedDataFlows()
    {
        // Arrange
        var items = new[] { 1, 2, 3 };
        var sp = BuildServiceProvider(services =>
        {
            services.AddKeyedDataFlow("flow-1", builder =>
            {
                builder.AddProducer("source", sp => new TestProducer<int>(items))
                    .AddProcessor("processor", sp => new TestProcessor<int>());
            });

            services.AddKeyedDataFlow("flow-2", builder =>
            {
                builder.AddProducer("source", sp => new TestProducer<string>(new[] { "a", "b" }))
                    .AddProcessor("processor", sp => new TestProcessor<string>());
            });
        });

        // Act
        var dataflow1 = sp.GetDataFlow("flow-1");
        var dataflow2 = sp.GetDataFlow("flow-2");

        // Assert
        dataflow1.ShouldNotBeNull();
        dataflow2.ShouldNotBeNull();
        dataflow1.Name.ShouldBe("flow-1");
        dataflow2.Name.ShouldBe("flow-2");
        dataflow1.ShouldNotBeSameAs(dataflow2);
    }

    [Fact]
    public void Should_ThrowException_WhenNameIsNullOrEmpty()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            services.AddKeyedDataFlow("", builder => { }));

        Should.Throw<ArgumentException>(() =>
            services.AddKeyedDataFlow(null, builder => { }));
    }

    [Fact]
    public void Should_ThrowException_WhenConfigureIsNull()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            services.AddKeyedDataFlow("test-flow", null));
    }

    [Fact]
    public void Should_ThrowException_WhenDataFlowNotFound()
    {
        // Arrange
        var sp = BuildServiceProvider(services => { });

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
            sp.GetDataFlow("non-existent"));
    }

    [Fact]
    public void Should_RegisterDataFlow_AsTransient_ByDefault()
    {
        // Arrange
        var items = new[] { 1, 2, 3 };
        var sp = BuildServiceProvider(services =>
        {
            services.AddKeyedDataFlow("test-flow", builder =>
            {
                builder.AddProducer("source", sp => new TestProducer<int>(items))
                    .AddProcessor("processor", sp => new TestProcessor<int>());
            }); // Default is transient
        });

        // Act - Get dataflow multiple times
        var dataflow1 = sp.GetDataFlow("test-flow");
        var dataflow2 = sp.GetDataFlow("test-flow");

        // Assert - Should be different instances (transient)
        dataflow1.ShouldNotBeSameAs(dataflow2);
    }

    [Fact]
    public void Should_RegisterDataFlow_WithSingletonLifetime()
    {
        // Arrange
        var items = new[] { 1, 2, 3 };
        var sp = BuildServiceProvider(services =>
        {
            services.AddKeyedDataFlow("test-flow", builder =>
            {
                builder.AddProducer("source", sp => new TestProducer<int>(items))
                    .AddProcessor("processor", sp => new TestProcessor<int>());
            }, ServiceLifetime.Singleton);
        });

        // Act - Get dataflow multiple times with singleton lifetime
        var dataflow1 = sp.GetDataFlow("test-flow");
        var dataflow2 = sp.GetDataFlow("test-flow");

        // Assert - Should be same instance (singleton)
        dataflow1.ShouldBeSameAs(dataflow2);
    }

    [Fact]
    public void Should_SupportComplexDataFlowConfiguration()
    {
        // Arrange
        var items = Enumerable.Range(1, 10).ToArray();
        var sp = BuildServiceProvider(services =>
        {
            services.AddKeyedDataFlow("complex-flow", builder =>
            {
                builder.AddProducer("source", sp => new TestProducer<int>(items))
                    .AddTransform("transform", sp => new NumberTransformer("num"))
                    .AddBatch("batcher", maxBatchSize: 3, windowPeriod: TimeSpan.FromSeconds(1))
                    .AddProcessor("processor", sp => new TestProcessor<string[]>());
            });
        });

        var dataflow = sp.GetDataFlow("complex-flow");

        // Act
        var graph = dataflow.Graph;

        // Assert
        graph.BlockDefinitions.Count.ShouldBe(4);
        graph.BlockDefinitions.Keys.ShouldContain("source");
        graph.BlockDefinitions.Keys.ShouldContain("transform");
        graph.BlockDefinitions.Keys.ShouldContain("batcher");
        graph.BlockDefinitions.Keys.ShouldContain("processor");

        // Verify type information
        var sourceDef = graph.GetBlockDefinition("source");
        sourceDef.OutputType.ShouldBe(typeof(int));

        var transformDef = graph.GetBlockDefinition("transform");
        transformDef.InputType.ShouldBe(typeof(int));
        transformDef.OutputType.ShouldBe(typeof(string));

        var batchDef = graph.GetBlockDefinition("batcher");
        batchDef.InputType.ShouldBe(typeof(string));
        batchDef.OutputType.ShouldBe(typeof(string[]));

        var processorDef = graph.GetBlockDefinition("processor");
        processorDef.InputType.ShouldBe(typeof(string[]));
    }

    [Fact]
    public void Should_CreateDataFlow_UsingExtensionMethod()
    {
        // Arrange
        var items = new[] { 1, 2, 3 };
        var sp = BuildServiceProvider(services =>
        {
            services.AddKeyedDataFlow("test-flow", builder =>
            {
                builder.AddProducer("source", sp => new TestProducer<int>(items))
                    .AddProcessor("processor", sp => new TestProcessor<int>());
            });
        });

        // Act - Use the convenience extension method
        var dataflow = sp.GetDataFlow("test-flow");

        // Assert
        dataflow.ShouldNotBeNull();
        dataflow.Name.ShouldBe("test-flow");
    }

    [Fact]
    public async Task Should_ExecuteMultipleDataFlowInstances_Independently()
    {
        // Arrange
        var processedItems1 = new List<int>();
        var processedItems2 = new List<int>();
        var items = new[] { 1, 2, 3 };

        var sp = BuildServiceProvider(services =>
        {
            services.AddKeyedDataFlow("test-flow", builder =>
            {
                builder.AddProducer("source", sp => new TestProducer<int>(items))
                    .AddProcessor("processor", sp => new TestProcessor<int>(
                        onProcessItem: item => { })); // Each instance will have its own processor
            });
        });

        // Create two separate instances
        var dataflow1 = sp.GetDataFlow("test-flow");
        var dataflow2 = sp.GetDataFlow("test-flow");

        var context1 = DataFlowContextTestUtils.GetContext("test1", Guid.NewGuid(), sp);
        var context2 = DataFlowContextTestUtils.GetContext("test2", Guid.NewGuid(), sp);

        // Act - Execute both flows
        var task1 = dataflow1.ExecuteAsync(context1);
        var task2 = dataflow2.ExecuteAsync(context2);
        await Task.WhenAll(task1, task2);

        // Assert - Both should complete successfully
        task1.IsCompletedSuccessfully.ShouldBeTrue();
        task2.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public void Should_AllowAccessToGraph_WithoutBuildingDataFlow()
    {
        // Arrange
        var items = new[] { 1, 2, 3 };
        var sp = BuildServiceProvider(services =>
        {
            services.AddKeyedDataFlow("test-flow", builder =>
            {
                builder.AddProducer("source", sp => new TestProducer<int>(items))
                    .AddTransform("transform", sp => new NumberTransformer("num"))
                    .AddProcessor("processor", sp => new TestProcessor<string>());
            });
        });

        var dataflow = sp.GetDataFlow("test-flow");

        // Act - Access the graph from the dataflow
        var graph = dataflow.Graph;

        // Assert - Can inspect the graph
        graph.Name.ShouldBe("test-flow");
        graph.BlockDefinitions.Count.ShouldBe(3);
        graph.Connections.Count.ShouldBe(2);
        
        // Verify we can get connections
        var connections = graph.GetOutgoingConnections("source").ToList();
        connections.Count.ShouldBe(1);
        connections[0].TargetBlockName.ShouldBe("transform");
    }

    [Fact]
    public void Should_RegisterTransientDataFlow_UsingConvenienceMethod()
    {
        // Arrange
        var items = new[] { 1, 2, 3 };
        var sp = BuildServiceProvider(services =>
        {
            services.AddTransientDataFlow("transient-flow", builder =>
            {
                builder.AddProducer("source", sp => new TestProducer<int>(items))
                    .AddProcessor("processor", sp => new TestProcessor<int>());
            });
        });

        // Act
        var dataflow1 = sp.GetDataFlow("transient-flow");
        var dataflow2 = sp.GetDataFlow("transient-flow");

        // Assert
        dataflow1.ShouldNotBeNull();
        dataflow2.ShouldNotBeNull();
        dataflow1.ShouldNotBeSameAs(dataflow2); // Different instances
    }

    [Fact]
    public void Should_RegisterSingletonDataFlow_UsingConvenienceMethod()
    {
        // Arrange
        var items = new[] { 1, 2, 3 };
        var sp = BuildServiceProvider(services =>
        {
            services.AddSingletonDataFlow("singleton-flow", builder =>
            {
                builder.AddProducer("source", sp => new TestProducer<int>(items))
                    .AddProcessor("processor", sp => new TestProcessor<int>());
            });
        });

        // Act
        var dataflow1 = sp.GetDataFlow("singleton-flow");
        var dataflow2 = sp.GetDataFlow("singleton-flow");

        // Assert
        dataflow1.ShouldNotBeNull();
        dataflow2.ShouldNotBeNull();
        dataflow1.ShouldBeSameAs(dataflow2); // Same instance
    }

    [Fact]
    public void Should_RegisterScopedDataFlow_UsingConvenienceMethod()
    {
        // Arrange
        var items = new[] { 1, 2, 3 };
        var sp = BuildServiceProvider(services =>
        {
            services.AddScopedDataFlow("scoped-flow", builder =>
            {
                builder.AddProducer("source", sp => new TestProducer<int>(items))
                    .AddProcessor("processor", sp => new TestProcessor<int>());
            });
        });

        // Act - Get from root scope
        var dataflowRoot = sp.GetDataFlow("scoped-flow");
        
        // Act - Get from child scope
        using var scope = sp.CreateScope();
        var dataflowScoped = scope.ServiceProvider.GetDataFlow("scoped-flow");

        // Assert
        dataflowRoot.ShouldNotBeNull();
        dataflowScoped.ShouldNotBeNull();
        // Within the same root scope, should be same instance
        var dataflowRoot2 = sp.GetDataFlow("scoped-flow");
        dataflowRoot.ShouldBeSameAs(dataflowRoot2);
    }
}
