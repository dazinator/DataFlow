namespace DataFlow.POC.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using DataFlow.POC.Benchmarks.DeprecatedBlocks;
using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static BenchmarkActorHelpers;

/// <summary>
/// Benchmarks for comparing typed channels vs object channels in EdgeStrategy implementations.
/// Measures throughput, allocations, and latency for value types (which box with object channels).
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class TypedChannelBenchmarks
{
    private const int ItemCount = 10000;
    
    [Params(1, 2, 4)]
    public int Concurrency { get; set; }
    
    [Params(100, 1000)]
    public int BufferCapacity { get; set; }
    
    /// <summary>
    /// Benchmark: BroadcastEdgeStrategy with value types (int).
    /// Value types benefit most from typed channels (no boxing).
    /// </summary>
    [Benchmark(Description = "Broadcast - ValueType (int)")]
    [BenchmarkCategory("Broadcast", "ValueType")]
    public async Task BroadcastValueType()
    {
        var services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .BuildServiceProvider();
        
        var itemsReceived = 0;
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(ItemCount));
        
        var consumers = CreateActorBlocks<int, object, NoOpProcessorActor<int>>(
            "processor",
            Concurrency,
            _ => new NoOpProcessorActor<int>(() => Interlocked.Increment(ref itemsReceived)))
            .Cast<IBlock>()
            .ToList();
        
        var builder = GraphHelpers.CreateGraphBuilder("broadcast-flow");
        builder.AddBlock(producer);
        foreach (var consumer in consumers)
        {
            builder.AddBlock(consumer);
        }
        
        var broadcastEdge = new Edge(
            producer,
            consumers.ToArray(),
            new BroadcastEdgeStrategy(BufferMode.Bounded, BufferCapacity));
        
        builder.AddEdge(broadcastEdge);
        
        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);
        
        await graph.ExecuteAsync(context);
    }
    
    /// <summary>
    /// Benchmark: BroadcastEdgeStrategy with reference types (string).
    /// Reference types don't benefit from typed channels (no boxing anyway).
    /// Useful as a baseline comparison.
    /// </summary>
    [Benchmark(Description = "Broadcast - ReferenceType (string)")]
    [BenchmarkCategory("Broadcast", "ReferenceType")]
    public async Task BroadcastReferenceType()
    {
        var services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .BuildServiceProvider();
        
        var itemsReceived = 0;
        var producer = new ProducerBlock<string>("producer", ctx => ProduceStrings(ItemCount));
        
        var consumers = CreateActorBlocks<string, object, NoOpProcessorActor<string>>(
            "processor",
            Concurrency,
            _ => new NoOpProcessorActor<string>(() => Interlocked.Increment(ref itemsReceived)))
            .Cast<IBlock>()
            .ToList();
        
        var builder = GraphHelpers.CreateGraphBuilder("broadcast-flow");
        builder.AddBlock(producer);
        foreach (var consumer in consumers)
        {
            builder.AddBlock(consumer);
        }
        
        var broadcastEdge = new Edge(
            producer,
            consumers.ToArray(),
            new BroadcastEdgeStrategy(BufferMode.Bounded, BufferCapacity));
        
        builder.AddEdge(broadcastEdge);
        
        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);
        
        await graph.ExecuteAsync(context);
    }
    
    /// <summary>
    /// Benchmark: CompetingEdgeStrategy with value types (int).
    /// Tests concurrent processing with value types.
    /// </summary>
    [Benchmark(Description = "Competing - ValueType (int)")]
    [BenchmarkCategory("Competing", "ValueType")]
    public async Task CompetingValueType()
    {
        var services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .BuildServiceProvider();
        
        var itemsReceived = 0;
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(ItemCount));
        
        var consumers = CreateActorBlocks<int, object, NoOpProcessorActor<int>>(
            "processor",
            Concurrency,
            _ => new NoOpProcessorActor<int>(async () =>
            {
                Interlocked.Increment(ref itemsReceived);
                await Task.Delay(1); // Simulate work
            }))
            .Cast<IBlock>()
            .ToList();
        
        var builder = GraphHelpers.CreateGraphBuilder("competing-flow");
        builder.AddBlock(producer);
        foreach (var consumer in consumers)
        {
            builder.AddBlock(consumer);
        }
        
        var competingEdge = new Edge(
            producer,
            consumers.ToArray(),
            new CompetingEdgeStrategy(BufferMode.Bounded, BufferCapacity));
        
        builder.AddEdge(competingEdge);
        
        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);
        
        await graph.ExecuteAsync(context);
    }
    
    /// <summary>
    /// Benchmark: CompetingEdgeStrategy with reference types (string).
    /// </summary>
    [Benchmark(Description = "Competing - ReferenceType (string)")]
    [BenchmarkCategory("Competing", "ReferenceType")]
    public async Task CompetingReferenceType()
    {
        var services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .BuildServiceProvider();
        
        var itemsReceived = 0;
        var producer = new ProducerBlock<string>("producer", ctx => ProduceStrings(ItemCount));
        
        var consumers = CreateActorBlocks<string, object, NoOpProcessorActor<string>>(
            "processor",
            Concurrency,
            _ => new NoOpProcessorActor<string>(async () =>
            {
                Interlocked.Increment(ref itemsReceived);
                await Task.Delay(1); // Simulate work
            }))
            .Cast<IBlock>()
            .ToList();
        
        var builder = GraphHelpers.CreateGraphBuilder("competing-flow");
        builder.AddBlock(producer);
        foreach (var consumer in consumers)
        {
            builder.AddBlock(consumer);
        }
        
        var competingEdge = new Edge(
            producer,
            consumers.ToArray(),
            new CompetingEdgeStrategy(BufferMode.Bounded, BufferCapacity));
        
        builder.AddEdge(competingEdge);
        
        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);
        
        await graph.ExecuteAsync(context);
    }
    
    /// <summary>
    /// Benchmark: Large struct (64 bytes) - shows maximum boxing overhead.
    /// </summary>
    [Benchmark(Description = "Broadcast - LargeStruct")]
    [BenchmarkCategory("Broadcast", "LargeStruct")]
    public async Task BroadcastLargeStruct()
    {
        var services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .BuildServiceProvider();
        
        var producer = new ProducerBlock<LargeStruct>("producer", ctx => ProduceLargeStructs(ItemCount));
        
        var consumers = CreateActorBlocks<LargeStruct, object, NoOpProcessorActor<LargeStruct>>(
            "processor",
            Concurrency,
            _ => new NoOpProcessorActor<LargeStruct>())
            .Cast<IBlock>()
            .ToList();
        
        var builder = GraphHelpers.CreateGraphBuilder("broadcast-flow");
        builder.AddBlock(producer);
        foreach (var consumer in consumers)
        {
            builder.AddBlock(consumer);
        }
        
        var broadcastEdge = new Edge(
            producer,
            consumers.ToArray(),
            new BroadcastEdgeStrategy(BufferMode.Bounded, BufferCapacity));
        
        builder.AddEdge(broadcastEdge);
        
        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);
        
        await graph.ExecuteAsync(context);
    }
    
    // Helper methods to generate test data
    private static async IAsyncEnumerable<int> ProduceIntegers(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return i;
        }
    }
    
    private static async IAsyncEnumerable<string> ProduceStrings(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return $"Item-{i}";
        }
    }
    
    private static async IAsyncEnumerable<LargeStruct> ProduceLargeStructs(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return new LargeStruct
            {
                Value1 = i,
                Value2 = i * 2,
                Value3 = i * 3,
                Value4 = i * 4,
                Value5 = i * 5,
                Value6 = i * 6,
                Value7 = i * 7,
                Value8 = i * 8
            };
        }
    }
    
    // 64-byte struct to test large value type performance
    private struct LargeStruct
    {
        public long Value1;
        public long Value2;
        public long Value3;
        public long Value4;
        public long Value5;
        public long Value6;
        public long Value7;
        public long Value8;
    }
}
