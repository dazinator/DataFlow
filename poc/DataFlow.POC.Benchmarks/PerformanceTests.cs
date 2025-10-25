namespace DataFlow.POC.Benchmarks;

using System.Diagnostics;
using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Simple performance tests to quickly measure typed channel improvements.
/// </summary>
public class PerformanceTests
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== Typed Channel Performance Tests ===\n");
        
        await RunBroadcastValueTypeTest();
        await RunBroadcastReferenceTypeTest();
        await RunCompetingValueTypeTest();
        await RunLargeStructTest();
        
        Console.WriteLine("\n=== All Tests Complete ===");
    }
    
    private static async Task RunBroadcastValueTypeTest()
    {
        Console.WriteLine("Test: Broadcast with Value Types (int)");
        Console.WriteLine("---------------------------------------");
        
        const int itemCount = 10000;
        const int concurrency = 4;
        const int bufferCapacity = 1000;
        
        var sw = Stopwatch.StartNew();
        var initialMemory = GC.GetTotalMemory(true);
        
        var services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .BuildServiceProvider();
        
        var itemsReceived = 0;
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(itemCount));
        
        var consumers = new List<ProcessorBlock<int>>();
        for (int i = 0; i < concurrency; i++)
        {
            var processor = new ProcessorBlock<int>($"processor{i}", async (item, ctx) =>
            {
                Interlocked.Increment(ref itemsReceived);
                await Task.CompletedTask;
            });
            consumers.Add(processor);
        }
        
        var builder = new DataFlowGraphBuilder("broadcast-flow");
        builder.AddBlock(producer);
        foreach (var consumer in consumers)
        {
            builder.AddBlock(consumer);
        }
        
        var broadcastEdge = new Edge(
            producer,
            consumers.ToArray(),
            new BroadcastEdgeStrategy(BufferMode.Bounded, bufferCapacity));
        
        builder.AddEdge(broadcastEdge);
        
        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);
        
        await graph.ExecuteAsync(context);
        
        sw.Stop();
        var finalMemory = GC.GetTotalMemory(true);
        var allocatedMemory = finalMemory - initialMemory;
        
        var throughput = itemCount * concurrency / sw.Elapsed.TotalSeconds;
        
        Console.WriteLine($"  Items: {itemCount:N0} x {concurrency} consumers = {itemCount * concurrency:N0} total");
        Console.WriteLine($"  Time: {sw.ElapsedMilliseconds:N0} ms");
        Console.WriteLine($"  Throughput: {throughput:N0} items/sec");
        Console.WriteLine($"  Memory: {allocatedMemory / 1024.0:N2} KB");
        Console.WriteLine($"  Avg per item: {allocatedMemory / (double)(itemCount * concurrency):N2} bytes");
        Console.WriteLine();
    }
    
    private static async Task RunBroadcastReferenceTypeTest()
    {
        Console.WriteLine("Test: Broadcast with Reference Types (string)");
        Console.WriteLine("---------------------------------------------");
        
        const int itemCount = 10000;
        const int concurrency = 4;
        const int bufferCapacity = 1000;
        
        var sw = Stopwatch.StartNew();
        var initialMemory = GC.GetTotalMemory(true);
        
        var services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .BuildServiceProvider();
        
        var itemsReceived = 0;
        var producer = new ProducerBlock<string>("producer", ctx => ProduceStrings(itemCount));
        
        var consumers = new List<ProcessorBlock<string>>();
        for (int i = 0; i < concurrency; i++)
        {
            var processor = new ProcessorBlock<string>($"processor{i}", async (item, ctx) =>
            {
                Interlocked.Increment(ref itemsReceived);
                await Task.CompletedTask;
            });
            consumers.Add(processor);
        }
        
        var builder = new DataFlowGraphBuilder("broadcast-flow");
        builder.AddBlock(producer);
        foreach (var consumer in consumers)
        {
            builder.AddBlock(consumer);
        }
        
        var broadcastEdge = new Edge(
            producer,
            consumers.ToArray(),
            new BroadcastEdgeStrategy(BufferMode.Bounded, bufferCapacity));
        
        builder.AddEdge(broadcastEdge);
        
        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);
        
        await graph.ExecuteAsync(context);
        
        sw.Stop();
        var finalMemory = GC.GetTotalMemory(true);
        var allocatedMemory = finalMemory - initialMemory;
        
        var throughput = itemCount * concurrency / sw.Elapsed.TotalSeconds;
        
        Console.WriteLine($"  Items: {itemCount:N0} x {concurrency} consumers = {itemCount * concurrency:N0} total");
        Console.WriteLine($"  Time: {sw.ElapsedMilliseconds:N0} ms");
        Console.WriteLine($"  Throughput: {throughput:N0} items/sec");
        Console.WriteLine($"  Memory: {allocatedMemory / 1024.0:N2} KB");
        Console.WriteLine($"  Avg per item: {allocatedMemory / (double)(itemCount * concurrency):N2} bytes");
        Console.WriteLine();
    }
    
    private static async Task RunCompetingValueTypeTest()
    {
        Console.WriteLine("Test: Competing with Value Types (int)");
        Console.WriteLine("--------------------------------------");
        
        const int itemCount = 10000;
        const int concurrency = 4;
        const int bufferCapacity = 1000;
        
        var sw = Stopwatch.StartNew();
        var initialMemory = GC.GetTotalMemory(true);
        
        var services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .BuildServiceProvider();
        
        var itemsReceived = 0;
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(itemCount));
        
        var consumers = new List<ProcessorBlock<int>>();
        for (int i = 0; i < concurrency; i++)
        {
            var processor = new ProcessorBlock<int>($"processor{i}", async (item, ctx) =>
            {
                Interlocked.Increment(ref itemsReceived);
                await Task.CompletedTask;
            });
            consumers.Add(processor);
        }
        
        var builder = new DataFlowGraphBuilder("competing-flow");
        builder.AddBlock(producer);
        foreach (var consumer in consumers)
        {
            builder.AddBlock(consumer);
        }
        
        var competingEdge = new Edge(
            producer,
            consumers.ToArray(),
            new CompetingEdgeStrategy(BufferMode.Bounded, bufferCapacity));
        
        builder.AddEdge(competingEdge);
        
        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);
        
        await graph.ExecuteAsync(context);
        
        sw.Stop();
        var finalMemory = GC.GetTotalMemory(true);
        var allocatedMemory = finalMemory - initialMemory;
        
        var throughput = itemCount / sw.Elapsed.TotalSeconds;
        
        Console.WriteLine($"  Items: {itemCount:N0} (competing across {concurrency} consumers)");
        Console.WriteLine($"  Time: {sw.ElapsedMilliseconds:N0} ms");
        Console.WriteLine($"  Throughput: {throughput:N0} items/sec");
        Console.WriteLine($"  Memory: {allocatedMemory / 1024.0:N2} KB");
        Console.WriteLine($"  Avg per item: {allocatedMemory / (double)itemCount:N2} bytes");
        Console.WriteLine();
    }
    
    private static async Task RunLargeStructTest()
    {
        Console.WriteLine("Test: Broadcast with Large Structs (64 bytes)");
        Console.WriteLine("---------------------------------------------");
        
        const int itemCount = 5000; // Fewer items due to size
        const int concurrency = 2;
        const int bufferCapacity = 500;
        
        var sw = Stopwatch.StartNew();
        var initialMemory = GC.GetTotalMemory(true);
        
        var services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .BuildServiceProvider();
        
        var producer = new ProducerBlock<LargeStruct>("producer", ctx => ProduceLargeStructs(itemCount));
        
        var consumers = new List<ProcessorBlock<LargeStruct>>();
        for (int i = 0; i < concurrency; i++)
        {
            var processor = new ProcessorBlock<LargeStruct>($"processor{i}", async (item, ctx) =>
            {
                // Access field to prevent optimization
                _ = item.Value1;
                await Task.CompletedTask;
            });
            consumers.Add(processor);
        }
        
        var builder = new DataFlowGraphBuilder("broadcast-flow");
        builder.AddBlock(producer);
        foreach (var consumer in consumers)
        {
            builder.AddBlock(consumer);
        }
        
        var broadcastEdge = new Edge(
            producer,
            consumers.ToArray(),
            new BroadcastEdgeStrategy(BufferMode.Bounded, bufferCapacity));
        
        builder.AddEdge(broadcastEdge);
        
        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None);
        
        await graph.ExecuteAsync(context);
        
        sw.Stop();
        var finalMemory = GC.GetTotalMemory(true);
        var allocatedMemory = finalMemory - initialMemory;
        
        var throughput = itemCount * concurrency / sw.Elapsed.TotalSeconds;
        
        Console.WriteLine($"  Items: {itemCount:N0} x {concurrency} consumers = {itemCount * concurrency:N0} total");
        Console.WriteLine($"  Time: {sw.ElapsedMilliseconds:N0} ms");
        Console.WriteLine($"  Throughput: {throughput:N0} items/sec");
        Console.WriteLine($"  Memory: {allocatedMemory / 1024.0:N2} KB");
        Console.WriteLine($"  Avg per item: {allocatedMemory / (double)(itemCount * concurrency):N2} bytes");
        Console.WriteLine($"  Expected size: 64 bytes per struct (typed) vs ~72-96 bytes (boxed)");
        Console.WriteLine();
    }
    
    // Helper methods
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
