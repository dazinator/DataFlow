namespace Tests.DataFlow;

using System.Collections.Concurrent;

[IntegrationTest]
public class ProducerBlockTests
{
    public ProducerBlockTests()
    {
        AddDefaultServices();
    }

    private void AddDefaultServices()
    {
        Services.AddDataFlows();
        Services.AddDataFlowMetrics();
    }

    public IServiceCollection Services { get; set; } = new ServiceCollection();

    [Fact]
    public async Task ProducerBlock_ProducesAllItems()
    {
        // Arrange
        var items = Enumerable.Range(0, 10);

        var producedItems = new ConcurrentBag<int>();
        var processedItems = new ConcurrentBag<int>();

        Services
             .AddDataFlow<TestProducerConfig>("test");

        // services.AddSingleton(producedItems);
        // services.AddSingleton(processedItems);
        // Register other dependencies needed by the producers
        Services.AddSingleton(new TestProducer<int>(
            items,
            onItemProduced: item => producedItems.Add(item),
            delay: TimeSpan.FromMilliseconds(10)));
        Services.AddSingleton(new TestProcessor<int>(
            onProcessItem: item => processedItems.Add(item)));

        await using var provider = Services.BuildServiceProvider();
        var executor = provider.GetRequiredService<FlowExecutor<TestProducerConfig>>();

        // Act
        var context = DataFlowContextTestUtils.GetContext("test", Guid.NewGuid(), provider);
        await executor.ExecuteAsync(context);

        // Assert
        Assert.Equal(10, producedItems.Count);
        Assert.Equal(10, processedItems.Count);
        Assert.Equal(producedItems.OrderBy(x => x), processedItems.OrderBy(x => x));
    }

    [Fact]
    public async Task ProducerBlock_RespectsMaxConcurrency()
    {
        // Arrange
        var concurrentExecutions = 0;
        var maxConcurrentExecutions = 0;
        var maxAllowedConcurrency = 2;
        var syncLock = new object();

        var tracker = new ConcurrencyTracker(
            onEnter: () =>
            {
                lock (syncLock)
                {
                    concurrentExecutions++;
                    maxConcurrentExecutions = Math.Max(maxConcurrentExecutions, concurrentExecutions);
                }
            },
            onExit: () =>
            {
                lock (syncLock)
                {
                    concurrentExecutions--;
                }
            });

        var items = Enumerable.Range(0, 10);

        Services
             .AddDataFlow<ConcurrencyTestConfig>("test");

        var producedItems = new ConcurrentBag<int>();
        var processedItems = new ConcurrentBag<int>();
        // services.AddSingleton(_producedItems);
        // services.AddSingleton(_processedItems);
        Services.AddSingleton(tracker);
        Services.AddSingleton(new ConcurrencyTestProducer<int>(
            items,
            tracker,
            onItemProduced: item => producedItems.Add(item),
            workDelay: TimeSpan.FromMilliseconds(50)));
        Services.AddSingleton(new TestProcessor<int>(
            onProcessItem: item => processedItems.Add(item)));

        await using var provider = Services.BuildServiceProvider();
        var executor = provider.GetRequiredService<FlowExecutor<ConcurrencyTestConfig>>();

        // Act
        var context = DataFlowContextTestUtils.GetContext("test", Guid.NewGuid(), provider);
        await executor.ExecuteAsync(context);

        // Assert
        Assert.True(maxConcurrentExecutions <= maxAllowedConcurrency);
        Assert.Equal(10, producedItems.Count);
        Assert.Equal(10, processedItems.Count);
    }

    [Fact]
    public async Task ProducerBlock_HandlesCancellation()
    {
        // Arrange
        var items = Enumerable.Range(0, 10);

        Services
            .AddDataFlow<SlowProducerConfig>("test");

        var producedItems = new ConcurrentBag<int>();
        var processedItems = new ConcurrentBag<int>();

        //  services.AddSingleton(_producedItems);
        //  services.AddSingleton(_processedItems);
        Services.AddSingleton(new TestProducer<int>(
            items,
            onItemProduced: item => producedItems.Add(item),
            delay: TimeSpan.FromSeconds(5)));
        Services.AddSingleton(new TestProcessor<int>(
            delay: TimeSpan.FromSeconds(5),
            onProcessItem: item => processedItems.Add(item)));

        await using var provider = Services.BuildServiceProvider();
        var executor = provider.GetRequiredService<FlowExecutor<SlowProducerConfig>>();

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(1);

        var context = DataFlowContextTestUtils.GetContext("test", Guid.NewGuid(), provider, cts.Token);

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await executor.ExecuteAsync(context));
        Assert.True(producedItems.Count < 10); // Should not complete all items
    }

    [Fact]
    public async Task ProducerBlock_HandlesExceptions()
    {
        // Arrange
        var items = Enumerable.Range(0, 5);

        Services
            .AddDataFlow<ErrorProducerConfig>("test");

        var producedItems = new ConcurrentBag<int>();
        var processedItems = new ConcurrentBag<int>();

        Services.AddSingleton(new ErrorProducer<int>(
            items,
            shouldError: item => item == 3,
            onItemProduced: item => producedItems.Add(item)));
        Services.AddSingleton(new TestProcessor<int>(
            onProcessItem: item => processedItems.Add(item)));

        await using var provider = Services.BuildServiceProvider();
        var executor = provider.GetRequiredService<FlowExecutor<ErrorProducerConfig>>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            var context = CreateContext("test", Guid.NewGuid(), provider);
            await executor.ExecuteAsync(context);
        });
    }

    private IDataFlowContext CreateContext(string v, Guid guid, ServiceProvider provider)
    {
        return DataFlowContextTestUtils.GetContext(v, guid, provider);
    }

    private class TestProducerConfig : IDataFlowConfiguration
    {
        public void Configure(DataFlowBuilder builder)
        {
            // Add producer block and link to processor block
            builder
                .AddProducer("source", sp => sp.GetRequiredService<TestProducer<int>>())
                .AddProcessor<int, TestProcessor<int>>("processor",
                    sp => sp.GetRequiredService<TestProcessor<int>>())
                .ReceiveFrom("source");
        }
    }

    private class ConcurrencyTestConfig : IDataFlowConfiguration
    {
        public void Configure(DataFlowBuilder builder)
        {
            var options = new BlockOptions { MaxConcurrency = 2 };

            // Add producer block and link to processor block
            builder
                .AddProducer("source", sp => sp.GetRequiredService<ConcurrencyTestProducer<int>>())
                .AddProcessor<int, TestProcessor<int>>("processor",
                    sp => sp.GetRequiredService<TestProcessor<int>>())
                .ReceiveFrom("source");
        }
    }

    private class SlowProducerConfig : IDataFlowConfiguration
    {
        public void Configure(DataFlowBuilder builder)
        {
            // Add producer block and link to processor block
            builder
                .AddProducer("source", sp => sp.GetRequiredService<TestProducer<int>>())
                .AddProcessor<int, TestProcessor<int>>("processor",
                    sp => sp.GetRequiredService<TestProcessor<int>>())
                .ReceiveFrom("source");
        }
    }

    private class ErrorProducerConfig : IDataFlowConfiguration
    {
        public void Configure(DataFlowBuilder builder)
        {
            builder
                .AddProducer("source", sp => sp.GetRequiredService<ErrorProducer<int>>())
                .AddProcessor<int, TestProcessor<int>>("processor",
                    sp => sp.GetRequiredService<TestProcessor<int>>())
                .ReceiveFrom("source");

        }
    }
}
