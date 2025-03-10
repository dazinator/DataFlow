namespace Tests.DataFlow;

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tests.DataFlow.Utils.Transformers;

[IntegrationTest]
public class ProjectorBlockTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public class ProcessedItems : ConcurrentBag<string> { }

    public ProjectorBlockTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        AddDefaultServices();
       
    }

    private void AddDefaultServices()
    {
        Services.AddLogging(a => a.AddXUnit(_testOutputHelper));
        Services.AddDataFlows();
        Services.AddDataFlowMetrics();
    }

    public IServiceCollection Services { get; set; } = new ServiceCollection();


    [Fact]
    public async Task ProjectorBlock_ProjectsItemsCorrectly()
    {
        // Arrange
        var items = Enumerable.Range(1, 5);

        Services
             .AddDataFlow<TestProjectorConfig>("test");

        var producedItems = new ConcurrentBag<int>();
        var processedItems = new ConcurrentBag<string>();

        Services.AddSingleton(new TestProducer<int>(items));
        Services.AddSingleton(new TestProjector<int, string>(
            input => new[] { $"{input}_A", $"{input}_B", $"{input}_C" }));
        Services.AddSingleton(new TestProcessor<string>(
            onProcessItem: item => processedItems.Add(item)));

        await using var provider = Services.BuildServiceProvider();
        var executor = provider.GetRequiredService<FlowExecutor<TestProjectorConfig>>();

        // Act
        var context = CreateContext("test", Guid.NewGuid(), provider);
        await executor.ExecuteAsync(context);

        // Assert
        Assert.Equal(15, processedItems.Count);
        Assert.Equal(5, processedItems.Count(x => x.EndsWith("_A")));
        Assert.Equal(5, processedItems.Count(x => x.EndsWith("_B")));
        Assert.Equal(5, processedItems.Count(x => x.EndsWith("_C")));
    }

    private IDataFlowContext CreateContext(string v, Guid guid, ServiceProvider provider, CancellationToken ct = default)
    {
        return DataFlowContextTestUtils.GetContext(v, guid, provider, ct);
    }

    [Fact]
    public async Task ProjectorBlock_HandlesEmptyProjections()
    {
        // Arrange
        var items = Enumerable.Range(1, 5);

        Services
           .AddDataFlow<TestProjectorConfig>("test");

        var producedItems = new ConcurrentBag<int>();
        var processedItems = new ConcurrentBag<string>();

        //services.AddSingleton(_processedItems);
        Services.AddSingleton(new TestProducer<int>(items));
        Services.AddSingleton(new TestProjector<int, string>(
            input => Enumerable.Empty<string>()));
        Services.AddSingleton(new TestProcessor<string>(
            onProcessItem: item => processedItems.Add(item)));

        await using var provider = Services.BuildServiceProvider();
        var executor = provider.GetRequiredService<FlowExecutor<TestProjectorConfig>>();

        // Act
        var context = CreateContext("test", Guid.NewGuid(), provider);
        await executor.ExecuteAsync(context);

        // Assert
        Assert.Empty(processedItems);
    }

    [Fact]
    public async Task ProjectorBlock_HandlesCancellation()
    {
        // Arrange
        var items = Enumerable.Range(1, 5);


        Services
           .AddDataFlow<TestProjectorConfig>("test");

        var producedItems = new ConcurrentBag<int>();
        var processedItems = new ConcurrentBag<string>();

        Services.AddSingleton(new TestProducer<int>(items));
        Services.AddSingleton(new TestProjector<int, string>(
            input => new[] { $"{input}_A", $"{input}_B", $"{input}_C" },
            delay: TimeSpan.FromMilliseconds(100)));
        Services.AddSingleton(new TestProcessor<string>(
            onProcessItem: item => processedItems.Add(item)));


        await using var provider = Services.BuildServiceProvider();
        var executor = provider.GetRequiredService<FlowExecutor<TestProjectorConfig>>();

        using var cts = new CancellationTokenSource();
        var context = CreateContext("test", Guid.NewGuid(), provider, cts.Token);

        // Act & Assert
        var executionTask = executor.ExecuteAsync(context);
        await Task.Delay(100); // Let it start
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executionTask);
    }




    [Fact]
    public async Task ProjectorBlock_RespectsMaxConcurrency()
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

        var items = Enumerable.Range(1, 5);

        Services
            .AddDataFlow<TestProjectorConfig>("test");

        var producedItems = new ConcurrentBag<int>();
        var processedItems = new ConcurrentBag<string>();

        Services.AddSingleton(tracker);
        Services.AddSingleton(new TestProducer<int>(items));
        Services.AddSingleton(new TestProjector<int, string>(
            input => new[] { $"{input}_A", $"{input}_B", $"{input}_C" },
            delay: TimeSpan.FromMilliseconds(50),
            tracker: tracker));
        Services.AddSingleton(new TestProcessor<string>(
            onProcessItem: item => processedItems.Add(item)));

        await using var provider = Services.BuildServiceProvider();
        var executor = provider.GetRequiredService<FlowExecutor<TestProjectorConfig>>();

        // Act
        var context = CreateContext("test", Guid.NewGuid(), provider);
        await executor.ExecuteAsync(context);

        // Assert
        Assert.True(maxConcurrentExecutions <= maxAllowedConcurrency);
        Assert.Equal(15, processedItems.Count);
    }

    // Test Configurations
    private class TestProjectorConfig : IDataFlowConfiguration
    {
        public void Configure(DataFlowBuilder builder)
        {
            builder
                .AddProducer("source", sp => sp.GetRequiredService<TestProducer<int>>())
                .AddTransform<int, string>("projector", sp => sp.GetRequiredService<TestProjector<int, string>>())
                    .ReceiveFrom("source")
                .AddProcessor<string, TestProcessor<string>>("processor",
                    sp => sp.GetRequiredService<TestProcessor<string>>())
                    .ReceiveFrom("projector");
        }
    }

}
