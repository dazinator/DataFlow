namespace Tests.DataFlow;

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Tests.DataFlow.Utils;

[Xunit.Categories.IntegrationTest]
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

            // Using the fluent API with type-safe linking
            builder
                .AddProducer<int, LargeDataProducer>("source")
                .AddTransform<int, int, SlowTransformer>("transform")
                    .ReceiveFrom("source")
                .AddProcessor<int, DataCollector>("collector")
                .ReceiveFrom("transform");

            //(builder.AddTransform<int, int, SlowTransformer>("transform"))
            //  .LinkTo();

            // Or if you prefer step by step:
            /*
            var source = builder.AddSource<int, LargeDataProducer>("source");
            var transform = builder.AddTransform<int, int, SlowTransformer>("transform");
            var collector = builder.AddProcessor<int, DataCollector>("collector");

            source.LinkTo(transform);
            transform.LinkTo(collector);
            */
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
}
