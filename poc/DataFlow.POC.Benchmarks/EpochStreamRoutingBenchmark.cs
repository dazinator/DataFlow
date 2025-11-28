using BenchmarkDotNet.Attributes;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DataFlow.POC.Benchmarks;

/// <summary>
/// Benchmarks epoch stream routing performance focusing on build-time optimization improvements.
/// Tests scenarios with large numbers of epochs to measure impact of eliminating runtime type checks
/// and dynamic casts in the container routing hot path.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class EpochStreamRoutingBenchmark
{
    private ServiceProvider _serviceProvider = null!;
    private ILogger<DataFlowGraph> _logger = null!;

    [Params(1000, 10000, 50000)]
    public int EpochCount { get; set; }

    [Params(10, 100)]
    public int ItemsPerEpoch { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Warning); // Reduce noise
        });
        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<DataFlowGraph>>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _serviceProvider?.Dispose();
    }

    /// <summary>
    /// Baseline: Tests epoch stream routing through broadcast strategy.
    /// This exercises the container routing code path with type checking and dynamic cast.
    /// </summary>
    [Benchmark(Baseline = true)]
    public async Task BroadcastStrategy_ManyEpochs()
    {
        var graph = new DataFlowGraph("broadcast-test", _logger);

        // Create source that produces epoch streams
        var sourceContext = new SimpleBlockContext("source");
        var source = new TestEpochSource(EpochCount, ItemsPerEpoch, sourceContext);
        graph.AddBlock(source);

        // Add two downstream consumers (broadcast to both)
        var consumer1Context = new SimpleBlockContext("consumer1");
        var consumer2Context = new SimpleBlockContext("consumer2");
        var consumer1 = new TestEpochConsumer(consumer1Context);
        var consumer2 = new TestEpochConsumer(consumer2Context);
        graph.AddBlock(consumer1);
        graph.AddBlock(consumer2);

        // Connect with broadcast strategy (default)
        var edge1 = new Edge(source, consumer1, source.OutputType, new EdgeStrategy(EdgeType.Broadcast));
        var edge2 = new Edge(source, consumer2, source.OutputType, new EdgeStrategy(EdgeType.Broadcast));
        graph.AddEdge(edge1);
        graph.AddEdge(edge2);

        // Execute the graph
        await graph.ExecuteAsync(CancellationToken.None);
    }

    /// <summary>
    /// Tests epoch stream routing through competing strategy.
    /// Different code path (shared containers) but still uses dynamic cast.
    /// </summary>
    [Benchmark]
    public async Task CompetingStrategy_ManyEpochs()
    {
        var graph = new DataFlowGraph("competing-test", _logger);

        var sourceContext = new SimpleBlockContext("source");
        var source = new TestEpochSource(EpochCount, ItemsPerEpoch, sourceContext);
        graph.AddBlock(source);

        var consumer1Context = new SimpleBlockContext("consumer1");
        var consumer2Context = new SimpleBlockContext("consumer2");
        var consumer1 = new TestEpochConsumer(consumer1Context);
        var consumer2 = new TestEpochConsumer(consumer2Context);
        graph.AddBlock(consumer1);
        graph.AddBlock(consumer2);

        var edge1 = new Edge(source, consumer1, source.OutputType, new EdgeStrategy(EdgeType.Competing));
        var edge2 = new Edge(source, consumer2, source.OutputType, new EdgeStrategy(EdgeType.Competing));
        graph.AddEdge(edge1);
        graph.AddEdge(edge2);

        await graph.ExecuteAsync(CancellationToken.None);
    }

    /// <summary>
    /// Tests epoch stream routing with selective routing strategy.
    /// Routes to specific targets based on criteria.
    /// </summary>
    [Benchmark]
    public async Task SelectiveStrategy_ManyEpochs()
    {
        var graph = new DataFlowGraph("selective-test", _logger);

        var sourceContext = new SimpleBlockContext("source");
        var source = new TestEpochSource(EpochCount, ItemsPerEpoch, sourceContext);
        graph.AddBlock(source);

        var consumer1Context = new SimpleBlockContext("consumer1");
        var consumer2Context = new SimpleBlockContext("consumer2");
        var consumer1 = new TestEpochConsumer(consumer1Context);
        var consumer2 = new TestEpochConsumer(consumer2Context);
        graph.AddBlock(consumer1);
        graph.AddBlock(consumer2);

        // Route even epochs to consumer1, odd epochs to consumer2
        var strategy = new SelectiveRoutingEdgeStrategy<int>(
            (item, targets) =>
            {
                return item % 2 == 0 ? new[] { consumer1 } : new[] { consumer2 };
            });

        var edge1 = new Edge(source, consumer1, source.OutputType, strategy);
        var edge2 = new Edge(source, consumer2, source.OutputType, strategy);
        graph.AddEdge(edge1);
        graph.AddEdge(edge2);

        await graph.ExecuteAsync(CancellationToken.None);
    }

    /// <summary>
    /// Simple block context for testing.
    /// </summary>
    private class SimpleBlockContext : IBlockContext
    {
        public SimpleBlockContext(string blockName)
        {
            BlockName = blockName;
        }

        public string BlockName { get; }
        public IReadOnlyDictionary<string, object>? Metadata => null;
    }

    /// <summary>
    /// Test block that produces epoch streams.
    /// </summary>
    private class TestEpochSource : BlockBase<object, IEpochStream<int>>
    {
        private readonly int _epochCount;
        private readonly int _itemsPerEpoch;

        public TestEpochSource(int epochCount, int itemsPerEpoch, IBlockContext context) 
            : base(context)
        {
            _epochCount = epochCount;
            _itemsPerEpoch = itemsPerEpoch;
        }

        public override async IAsyncEnumerable<IEpochStream<int>> ExecuteAsync(
            IAsyncEnumerable<object> input,
            IExecutionContext context)
        {
            for (int epochIndex = 0; epochIndex < _epochCount; epochIndex++)
            {
                var epochVector = new EpochVector([epochIndex]);
                yield return new EpochStream<int>(epochVector, ProduceItems(epochIndex), null);
                
                // Small delay to simulate realistic async behavior
                if (epochIndex % 100 == 0)
                {
                    await Task.Yield();
                }
            }
        }

        private async IAsyncEnumerable<int> ProduceItems(int epochIndex)
        {
            int baseValue = epochIndex * _itemsPerEpoch;
            for (int i = 0; i < _itemsPerEpoch; i++)
            {
                yield return baseValue + i;
                
                // Simulate some async work occasionally
                if (i % 10 == 0)
                {
                    await Task.Yield();
                }
            }
        }
    }

    /// <summary>
    /// Test block that consumes epoch streams.
    /// </summary>
    private class TestEpochConsumer : BlockBase<IEpochStream<int>, object>
    {
        private int _processedEpochs = 0;
        private int _processedItems = 0;

        public TestEpochConsumer(IBlockContext context) 
            : base(context)
        {
        }

        public override async IAsyncEnumerable<object> ExecuteAsync(
            IAsyncEnumerable<IEpochStream<int>> input,
            IExecutionContext context)
        {
            await foreach (var epoch in input)
            {
                _processedEpochs++;
                
                await foreach (var item in epoch.Items)
                {
                    _processedItems++;
                    // Minimal processing to focus benchmark on routing overhead
                }
            }

            // Return empty - this is a terminal block
            yield break;
        }
    }
}
