namespace Tests.DataFlow;

using System.Collections.Concurrent;

[IntegrationTest]
public class DataFlowIntegrationTests
{
    // Create wrapper classes to make the types distinct
    public class ProducedItems : ConcurrentBag<string>
    {
    }

    public class ProcessedItems : ConcurrentBag<string>
    {
    }

    public class ExecutionTimes : ConcurrentQueue<DateTime>
    {
    }

    private readonly ProducedItems _producedItems = new();
    private readonly ProcessedItems _processedItems = new();
    private readonly ExecutionTimes _executionTimes = new();

    public IServiceCollection Services { get; set; }

    public DataFlowIntegrationTests()
    {
        Services = new ServiceCollection();
        AddDefaultServices(Services);
    }

    private void AddDefaultServices(IServiceCollection services)
    {
        //services.AddScoped(typeof(FlowExecutor<>));
        services.AddDataFlows((o)=> o.MaxConcurrentFlows = 2);
        Services.AddDataFlowMetrics();
    }

    public ServiceProvider GetServiceProvider()
    {
        return Services.BuildServiceProvider();
    }

    [Fact]
    public async Task StringFlow_ProcessesAllItems()
    {
        // Arrange
        var inputItems = new[]
        {
            "test1", "test2", "test3"
        };

        Services.AddDataFlow<StringProcessingFlowConfig>(sp => new StringProcessingFlowConfig(
            new TestProducer<string>(
                inputItems,
                onItemProduced: item => _producedItems.Add(item),
                delay: TimeSpan.FromMilliseconds(10)),
            new TestProcessor<string>(
                onProcessItem: item => _processedItems.Add(item))));

        using var sp = GetServiceProvider();
        var executor = sp.GetRequiredService<FlowExecutor<StringProcessingFlowConfig>>();
        var context = DataFlowContextTestUtils.GetContext("test", Guid.NewGuid(), sp);

        // Act
        await executor.ExecuteAsync(context);

        // Assert
        Assert.Equal(inputItems.Length, _processedItems.Count);
        foreach (var item in inputItems)
        {
            Assert.Contains(_producedItems, p => p == item);
            Assert.Contains(_processedItems, p => p == item);
        }
    }

    [Fact]
    public async Task WhenCancellationRequested_StopsProcessingGracefully()
    {
        // Arrange
        var producedItems = new ConcurrentBag<int>();
        var processedItems = new ConcurrentBag<int>();

        Services.AddSingleton(new TestProducer<int>(
            Enumerable.Range(1, 100),
            onItemProduced: item => producedItems.Add(item),
            delay: TimeSpan.FromMilliseconds(100)));

        Services.AddSingleton(new TestProcessor<int>(
            onProcessItem: item => processedItems.Add(item)));

        Services.AddDataFlow<SlowFlowConfig>("test");

        using var sp = GetServiceProvider();
        var executor = sp.GetRequiredService<FlowExecutor<SlowFlowConfig>>();
        using var cts = new CancellationTokenSource();
        var context = DataFlowContextTestUtils.GetContext("test", Guid.NewGuid(), sp, cts.Token);

        // Act & Assert
        var executionTask = executor.ExecuteAsync(context);
        await Task.Delay(100); // Let it start
        cts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => executionTask);

        Assert.True(_producedItems.Count < 100); // Should not complete all items
    }

    [Fact]
    public async Task ProcessingLargeDataSet_WithBackpressure_WorksCorrectly()
    {
        // Arrange
        var processedItems = new ConcurrentBag<int>();

        Services.AddDataFlows()
            .AddDataFlow<LargeDataFlowConfig>("test");

        // Register test components
        Services.AddSingleton(new TestProducer<int>(
            Enumerable.Range(0, 1000),
            delay: TimeSpan.FromMilliseconds(1))); // Fast production

        Services.AddSingleton(new TestProcessor<int>(
            onProcessItem: item => processedItems.Add(item),
            delay: TimeSpan.FromMilliseconds(10))); // Slower processing

        await using var provider = Services.BuildServiceProvider();
        var executor = provider.GetRequiredService<FlowExecutor<LargeDataFlowConfig>>();

        // Act
        var context = DataFlowContextTestUtils.GetContext("test", Guid.NewGuid(), provider);
        await executor.ExecuteAsync(context);

        // Assert
        Assert.Equal(1000, processedItems.Count);
        Assert.Equal(Enumerable.Range(0, 1000).ToList(), processedItems.OrderBy(x => x).ToList());
    }

    // Flow Configurations
    private class StringProcessingFlowConfig : IDataFlowConfiguration
    {
        private readonly IStreamProducer<string> _producer;
        private readonly IStreamProcessor<string> _processor;

        public StringProcessingFlowConfig(IStreamProducer<string> producer, IStreamProcessor<string> processor)
        {
            _producer = producer;
            _processor = processor;
        }

        public void Configure(DataFlowBuilder builder)
        {
            builder
                .AddProducer("source", sp => _producer)
                .AddProcessor<string, TestProcessor<string>>("processor",
                    sp => _processor)
                    .ReceiveFrom("source");


        }
    }

    private class SlowFlowConfig : IDataFlowConfiguration
    {
        public void Configure(DataFlowBuilder builder)
        {
            builder
                .AddProducer("source", sp => sp.GetRequiredService<TestProducer<int>>())
                .AddProcessor<int, TestProcessor<int>>("processor",
                    sp => sp.GetRequiredService<TestProcessor<int>>())
                    .ReceiveFrom("source");
        }
    }

    private class LargeDataFlowConfig : IDataFlowConfiguration
    {
        public void Configure(DataFlowBuilder builder)
        {
            var options = new BlockOptions
            {
                MaxConcurrency = 4,
                Capacity = 10,                
            };

            builder
                .AddProducer("source", sp => sp.GetRequiredService<TestProducer<int>>())
                    .ThenTransform("transform", sp => ActivatorUtilities.CreateInstance<PassthroughTransformer<int>>(sp))
                //.AddTransform<int, int, PassthroughTransformer<int>>("transform")
                //    .ReceiveFrom("source")
                .AddProcessor<int, TestProcessor<int>>("collector",
                    sp => sp.GetRequiredService<TestProcessor<int>>())
                    .ReceiveFrom("transform");
        }
    }
}
