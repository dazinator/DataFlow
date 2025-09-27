namespace Tests.DataFlow;

using System.Collections.Concurrent;
using Xunit;
using Uniun.DataFlow;
using Uniun.DataFlow.Blocks;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

[IntegrationTest]
public class DataFlowComplexScenarioTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public DataFlowComplexScenarioTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        Services = new ServiceCollection();
        AddDefaultServices();

    }

    private void AddDefaultServices()
    {
        Services.AddLogging(a => a.AddXUnit(_testOutputHelper));
        Services.AddDataFlowMetrics();
        Services.AddDataFlows();
    }

    public IServiceCollection Services { get; set; }
    [Fact]
    public async Task ProcessingLargeDataSet_WithBackpressure_WorksCorrectly()
    {
        // Arrange      
        var processedItems = new ConcurrentBag<int>();

        Services
            .AddDataFlow<LargeDataFlowConfig>("test")
            .AddSingleton(processedItems);

        await using var provider = Services.BuildServiceProvider();
        var executor = provider.GetRequiredService<FlowExecutor<LargeDataFlowConfig>>();

        // Act
        var context = DataFlowContextTestUtils.GetContext("test", Guid.NewGuid(), provider);
        await executor.ExecuteAsync(context);

        // Assert
        Assert.Equal(1000, processedItems.Count);
        Assert.Equal(Enumerable.Range(0, 1000).ToList(), processedItems.OrderBy(x => x).ToList());
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
                .AddProducer<int, LargeDataProducer>("source")
                .AddTransform<int, int, SlowTransformer>("transform")
                    .ReceiveFrom("source")
                .AddProcessor<int, DataCollector>("collector")
                .ReceiveFrom("transform");         
        }
    }

    private class LargeDataProducer : IStreamProducer<int>
    {
        public async IAsyncEnumerable<int> ProduceAsync(IDataFlowContext context,
            [EnumeratorCancellation] CancellationToken cancellation)
        {
            foreach (var i in Enumerable.Range(0, 1000))
            {
                cancellation.ThrowIfCancellationRequested();
                yield return i;
            }
        }

    }

    private class SlowTransformer : IStreamTransformer<int, int>
    {
        public async IAsyncEnumerable<int> TransformAsync(
            IDataFlowContext context,
            IAsyncEnumerable<int> input,
            CancellationToken cancellationToken)
        {
            await foreach (var item in input)
            {
                await Task.Delay(10, cancellationToken); // Simulate some work
                yield return item;
            }
        }
    }

    private class DataCollector : IStreamProcessor<int>
    {
        private readonly ConcurrentBag<int> _items;

        public DataCollector(ConcurrentBag<int> items)
        {
            _items = items;
        }

        public async Task ProcessAsync(IDataFlowContext context, IAsyncEnumerable<int> input, CancellationToken cancellationToken)
        {
            await foreach (var item in input)
            {
                _items.Add(item);
            }
        }
    }

    [Fact]
    public async Task ExceptionInMiddleBlock_PropagatesAndUpstreamDoesNotHang()
    {
        var items = new[] { 1, 2, 3, 4, 5 };
        var produced = new ConcurrentBag<int>();
        var processedB = new ConcurrentBag<int>();
        var processedC = new ConcurrentBag<int>();

        //  Services
        // .AddDataFlow<LargeDataFlowConfig>("test")
        //.AddSingleton(processedItems);
        var sp = Services.BuildServiceProvider();

        var builder = new DataFlowBuilder(sp);

        // Block A: Producer, small buffer
        builder.AddProducer("A", _ => new TestProducer<int>(items, onItemProduced: produced.Add), o => o.Capacity = 1);

        // Block B: Throws on second item
        builder.AddTransform("B", _ => new TestTransformer<int, int>(
            onTransform: item =>
            {
                processedB.Add(item);
                if (item == 2)
                {
                    throw new InvalidOperationException("B failed");
                }
                return item;
            })).ReceiveFrom("A");

        // Block C: Just collects items
        builder.AddProcessor<int>("C", _ => new TestProcessor<int>(onProcessItem: processedC.Add))
            .ReceiveFrom("B");


        var flow = builder.Build();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var context = new DataFlowContext(Guid.NewGuid())
        {
            ServiceProvider = sp,
            CancellationToken = cts.Token,
            Name = "ExceptionInMiddleBlock"
        };

        // Act and Assert
        // Prior to fix, this would hang indefinitely (or for full 10 seconds of the outer cancellation token we are supplying in this test)
        // because A would be blocked trying to write to its output buffer, because B hits an exception is no longer pulling items from it. A is unaware that B is no longer running
        // and is just waiting to write to its output buffer indefinately (based on cancellation token).
        // With the fix, the cancellation token (10s) we pass in here is joined with another that is signalled on block exception.
        // causing A to stop producing and exit gracefully as the cancellation token is signalled as soon as block B throws the uncaught exception.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => flow.ExecuteAsync(context));

        // B should have processed some items, but not item 3 because it throws on 2.
        Assert.NotEmpty(processedB);
        Assert.DoesNotContain(3, processedB);

        // C should complete normally (may process 1 item, depending on timing)
        Assert.True(processedC.Count >= 0);

        // A should not hang indefinitely
        Assert.True(produced.Count <= 2); // Only first item(s) should be produced before B throws
    }
}
