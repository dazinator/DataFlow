namespace DataFlow.POC.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

/// <summary>
/// RESEARCH PROTOTYPE BENCHMARK - Will be reverted after approval
/// 
/// Compares three routing approaches:
/// 1. Broadcast-and-Filter (current): RouterBlock + RouteFilterBlock
/// 2. SelectiveRoutingEdgeStrategy (Option 1): Edge-level selective routing
/// 
/// Key Metrics:
/// - Throughput (items/sec)
/// - Memory allocations
/// - CPU time
/// 
/// Scenarios:
/// - 2 routes (baseline)
/// - 5 routes (moderate)
/// - 10 routes (high)
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class SelectiveRoutingBenchmark
{
    private const int ItemCount = 10_000;

    [Params(2, 5, 10)]
    public int RouteCount { get; set; }

    /// <summary>
    /// BASELINE: Current broadcast-and-filter approach.
    /// RouterBlock broadcasts to ALL routes, each RouteFilterBlock filters items.
    /// Performance degrades with N routes because:
    /// - Broadcasts to N channels concurrently
    /// - Allocates 1 RoutedItem record per item
    /// - Each filter processes 100% of items, drops (N-1)/N items
    /// </summary>
    [Benchmark(Baseline = true)]
    public async Task BroadcastAndFilter()
    {
        // Create collectors for each route
        var collectors = new List<CollectorActor<int>>();
        var scopeFactories = new List<IServiceScopeFactory>();

        for (int i = 0; i < RouteCount; i++)
        {
            var collector = new CollectorActor<int>(new List<int>());
            collectors.Add(collector);
            
            var services = new ServiceCollection();
            services.AddScoped(_ => collector);
            scopeFactories.Add(services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IServiceScopeFactory>());
        }

        var commonServices = new ServiceCollection().BuildServiceProvider();

        // Producer
        var producer = CreateProducer("producer", Enumerable.Range(1, ItemCount));

        // Router + Filters
        var router = CreateRouter<int>("router", i => $"route{i % RouteCount}");

        var filters = new List<IBlock>();
        var processors = new List<IBlock>();

        for (int i = 0; i < RouteCount; i++)
        {
            var filter = CreateRouteFilter<int>($"filter{i}", $"route{i}");
            var processor = CreateActor<int, object, CollectorActor<int>>($"processor{i}", scopeFactories[i]);
            
            filters.Add(filter);
            processors.Add(processor);
        }

        // Build graph
        var builder = CreateGraphBuilder("broadcast-filter");
        builder.AddBlock(producer)
            .AddBlock(router)
            .AutoConnect();

        foreach (var filter in filters)
        {
            builder.AddBlock(filter);
        }

        foreach (var processor in processors)
        {
            builder.AddBlock(processor);
        }

        // Connect: producer -> router -> [all filters] -> [all processors]
        builder.ConnectMany(router, filters.ToArray());
        for (int i = 0; i < RouteCount; i++)
        {
            builder.Connect(filters[i], processors[i]);
        }

        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Execute
        await graph.ExecuteAsync(context);
    }

    /// <summary>
    /// OPTION 1: SelectiveRoutingEdgeStrategy.
    /// Routes items based on content WITHOUT broadcasting.
    /// Performance scales better with N routes because:
    /// - Sends each item to ONE route only (O(1) lookup)
    /// - No RoutedItem allocation
    /// - No filtering overhead
    /// </summary>
    [Benchmark]
    public async Task SelectiveRouting()
    {
        // Create collectors for each route
        var collectors = new List<CollectorActor<int>>();
        var scopeFactories = new List<IServiceScopeFactory>();
        var processors = new List<IBlock>();

        for (int i = 0; i < RouteCount; i++)
        {
            var collector = new CollectorActor<int>(new List<int>());
            collectors.Add(collector);
            
            var services = new ServiceCollection();
            services.AddScoped(_ => collector);
            var scopeFactory = services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IServiceScopeFactory>();
            scopeFactories.Add(scopeFactory);
            
            var processor = CreateActor<int, object, CollectorActor<int>>($"processor{i}", scopeFactory);
            processors.Add(processor);
        }

        var commonServices = new ServiceCollection().BuildServiceProvider();

        // Producer
        var producer = CreateProducer("producer", Enumerable.Range(1, ItemCount));

        // Create route mapping
        var routeMapping = new Dictionary<string, IBlock>();
        for (int i = 0; i < RouteCount; i++)
        {
            routeMapping[$"route{i}"] = processors[i];
        }

        // SelectiveRoutingEdgeStrategy
        var strategy = new SelectiveRoutingEdgeStrategy<int>(
            routeKeyToBlock: routeMapping,
            routeSelector: i => $"route{i % RouteCount}");

        var edge = new Edge(
            producer,
            processors.ToArray(),
            strategy);

        // Build graph
        var builder = CreateGraphBuilder("selective-routing");
        builder.AddBlock(producer);

        foreach (var processor in processors)
        {
            builder.AddBlock(processor);
        }

        builder.AddEdge(edge);

        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Execute
        await graph.ExecuteAsync(context);
    }

    // Helper methods (simplified versions from test helpers)

    private static IBlock<object, T> CreateProducer<T>(string name, IEnumerable<T> items)
    {
        return new PlainProducerWrapper<T>(name, _ => items.ToAsyncEnumerable());
    }

    private static IBlock<T, RoutedItem<T>> CreateRouter<T>(string name, Func<T, string> routeSelector)
    {
        return new RouterBlock<T>(new BlockContext(name), routeSelector);
    }

    private static IBlock<RoutedItem<T>, T> CreateRouteFilter<T>(string name, string routeKey)
    {
        return new RouteFilterBlock<T>(new BlockContext(name), routeKey);
    }

    private static IBlock<TInput, TOutput> CreateActor<TInput, TOutput, TActor>(
        string name,
        IServiceScopeFactory scopeFactory)
        where TActor : class, IStreamActor<TInput, TOutput>
    {
        return new EpochActorBlock<TInput, TOutput, TActor>(new BlockContext(name), scopeFactory);
    }

    private static DataFlowGraphBuilder CreateGraphBuilder(string name)
    {
        return new DataFlowGraphBuilder(name);
    }

    private class PlainProducerWrapper<T> : BlockBase<object, T>
    {
        private readonly Func<IExecutionContext, IAsyncEnumerable<T>> _producer;

        public PlainProducerWrapper(string name, Func<IExecutionContext, IAsyncEnumerable<T>> producer)
            : base(new BlockContext(name))
        {
            _producer = producer;
        }

        public override async IAsyncEnumerable<T> ExecuteAsync(
            IAsyncEnumerable<object> input,
            IExecutionContext context)
        {
            await foreach (var item in _producer(context).WithCancellation(context.CancellationToken))
            {
                yield return item;
            }
        }
    }

    private class CollectorActor<T> : IStreamActor<T, object>
    {
        private readonly List<T> _items;

        public CollectorActor(List<T> items)
        {
            _items = items;
        }

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<T> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                _items.Add(item);
            }
            yield break;
        }
    }
}
