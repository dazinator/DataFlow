namespace DataFlow.POC.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DataFlow.POC.Core;
using DataFlow.POC.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Runtime.CompilerServices;
using DataFlow.POC.Registry;

/// <summary>
/// Benchmarks epoch stream routing performance across high epoch volumes.
/// Tests the optimization of build-time delegate compilation for container routing
/// to eliminate dynamic casts and type checks during execution.
/// 
/// Scenarios tested:
/// - Small scale: 1,000 epochs
/// - Medium scale: 10,000 epochs
/// - Large scale: 50,000 epochs
/// 
/// Routing strategies:
/// - Broadcast: Each target gets its own unique container
/// - Competing: All targets share the same container
/// - Selective: Each target gets its own unique container based on route key
/// 
/// Measurements:
/// - Graph build time (one-time compilation cost)
/// - Execution time (runtime routing overhead)
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, warmupCount: 3, iterationCount: 5)]
public class EpochStreamRoutingBenchmark
{
    private const int ItemsPerEpoch = 100;
    
    [Params(1_000, 10_000, 50_000)]
    public int EpochCount { get; set; }
    
    private IServiceProvider _serviceProvider = null!;
    private ILogger<DataFlowGraph> _logger = null!;
    
    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        _serviceProvider = services.BuildServiceProvider();
        _logger = NullLogger<DataFlowGraph>.Instance;
    }
    
    // ========================================
    // Broadcast Strategy
    // ========================================
    
    [Benchmark(Description = "Broadcast: 2 targets")]
    public async Task<int> BroadcastStrategy_TwoTargets()
    {
        return await ExecuteBroadcastGraph(targetCount: 2);
    }
    
    [Benchmark(Description = "Broadcast: 5 targets")]
    public async Task<int> BroadcastStrategy_FiveTargets()
    {
        return await ExecuteBroadcastGraph(targetCount: 5);
    }
    
    // ========================================
    // Competing Strategy
    // ========================================
    
    [Benchmark(Description = "Competing: 2 consumers")]
    public async Task<int> CompetingStrategy_TwoConsumers()
    {
        return await ExecuteCompetingGraph(consumerCount: 2);
    }
    
    [Benchmark(Description = "Competing: 5 consumers")]
    public async Task<int> CompetingStrategy_FiveConsumers()
    {
        return await ExecuteCompetingGraph(consumerCount: 5);
    }
    
    // ========================================
    // Selective Routing Strategy
    // ========================================
    
    [Benchmark(Description = "Selective: 3 routes")]
    public async Task<int> SelectiveStrategy_ThreeRoutes()
    {
        return await ExecuteSelectiveGraph(routeCount: 3);
    }
    
    [Benchmark(Description = "Selective: 5 routes")]
    public async Task<int> SelectiveStrategy_FiveRoutes()
    {
        return await ExecuteSelectiveGraph(routeCount: 5);
    }
    
    // ========================================
    // Helper Methods
    // ========================================
    
    private async Task<int> ExecuteBroadcastGraph(int targetCount)
    {
        var graph = new DataFlowGraph("broadcast-test", _logger);
        
        var source = new EpochStreamSourceBlock(EpochCount, ItemsPerEpoch);
        graph.AddBlock(source);
        
        var targets = new List<IBlock>();
        for (int i = 0; i < targetCount; i++)
        {
            var target = new EpochStreamConsumerBlock($"consumer-{i}");
            graph.AddBlock(target);
            targets.Add(target);
        }
        
        var edge = new Edge(
            source,
            targets,
            new BroadcastEdgeStrategy(BufferMode.Bounded, bufferCapacity: 1000));
        
        graph.AddEdge(edge);
        
        var context = new ExecutionContext(_serviceProvider, CancellationToken.None);
        await graph.ExecuteAsync(context);
        
        return targets.Sum(t => ((EpochStreamConsumerBlock)t).ItemsProcessed);
    }
    
    private async Task<int> ExecuteCompetingGraph(int consumerCount)
    {
        var graph = new DataFlowGraph("competing-test", _logger);
        
        var source = new EpochStreamSourceBlock(EpochCount, ItemsPerEpoch);
        graph.AddBlock(source);
        
        var consumers = new List<IBlock>();
        for (int i = 0; i < consumerCount; i++)
        {
            var consumer = new EpochStreamConsumerBlock($"consumer-{i}");
            graph.AddBlock(consumer);
            consumers.Add(consumer);
        }
        
        var edge = new Edge(
            source,
            consumers,
            new CompetingEdgeStrategy(BufferMode.Bounded, bufferCapacity: 1000));
        
        graph.AddEdge(edge);
        
        var context = new ExecutionContext(_serviceProvider, CancellationToken.None);
        await graph.ExecuteAsync(context);
        
        return consumers.Sum(c => ((EpochStreamConsumerBlock)c).ItemsProcessed);
    }
    
    private async Task<int> ExecuteSelectiveGraph(int routeCount)
    {
        var graph = new DataFlowGraph("selective-test", _logger);
        
        var source = new EpochStreamSourceBlock(EpochCount, ItemsPerEpoch);
        graph.AddBlock(source);
        
        var routeMap = new Dictionary<string, IBlock>();
        var targets = new List<IBlock>();
        
        for (int i = 0; i < routeCount; i++)
        {
            var routeKey = $"route-{i}";
            var target = new EpochStreamConsumerBlock($"consumer-{i}");
            graph.AddBlock(target);
            routeMap[routeKey] = target;
            targets.Add(target);
        }
        
        // Route based on epoch vector
        var edge = new Edge(
            source,
            targets,
            new SelectiveRoutingEdgeStrategy<IEpochStream<int>>(
                routeMap,
                epochStream => $"route-{GetSequenceNumber(epochStream.Epoch) % routeCount}",
                BufferMode.Bounded,
                bufferCapacity: 1000));
        
        graph.AddEdge(edge);
        
        var context = new ExecutionContext(_serviceProvider, CancellationToken.None);
        await graph.ExecuteAsync(context);
        
        return targets.Sum(t => ((EpochStreamConsumerBlock)t).ItemsProcessed);
    }
    
    private static long GetSequenceNumber(EpochVector epoch)
    {
        // Get first sequence number from the epoch vector
        return epoch.Sequences.Values.FirstOrDefault();
    }
}

/// <summary>
/// Test block that produces epoch streams with configurable count and items per epoch.
/// </summary>
internal class EpochStreamSourceBlock : BlockBase<object, IEpochStream<int>>
{
    private readonly int _epochCount;
    private readonly int _itemsPerEpoch;
    
    public EpochStreamSourceBlock(int epochCount, int itemsPerEpoch)
        : base(new BlockContext("epoch-stream-source"))
    {
        _epochCount = epochCount;
        _itemsPerEpoch = itemsPerEpoch;
    }
    
    public override async IAsyncEnumerable<IEpochStream<int>> ExecuteAsync(
        IAsyncEnumerable<object> input,
        IExecutionContext context)
    {
        for (int epochNum = 0; epochNum < _epochCount; epochNum++)
        {
            var epochVector = EpochVector.FromSingleSource("benchmark", epochNum);
            var items = GenerateItems(epochNum);
            yield return new EpochStream<int>(epochVector, items);
        }
    }
    
    private async IAsyncEnumerable<int> GenerateItems(int epochNum)
    {
        var startValue = epochNum * _itemsPerEpoch;
        for (int i = 0; i < _itemsPerEpoch; i++)
        {
            yield return startValue + i;
        }
    }
}

/// <summary>
/// Test block that consumes epoch streams and counts items processed.
/// </summary>
internal class EpochStreamConsumerBlock : BlockBase<IEpochStream<int>, object>
{
    private int _itemsProcessed;
    
    public EpochStreamConsumerBlock(string name)
        : base(new BlockContext(name))
    {
    }
    
    public int ItemsProcessed => _itemsProcessed;
    
    public override async IAsyncEnumerable<object> ExecuteAsync(
        IAsyncEnumerable<IEpochStream<int>> input,
        IExecutionContext context)
    {
        await foreach (var epochStream in input.WithCancellation(context.CancellationToken))
        {
            await foreach (var item in epochStream.Items.WithCancellation(context.CancellationToken))
            {
                Interlocked.Increment(ref _itemsProcessed);
            }
        }
        
        yield break;
    }
}
