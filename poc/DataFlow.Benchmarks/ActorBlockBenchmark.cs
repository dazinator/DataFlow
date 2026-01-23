namespace DataFlow.POC.Benchmarks;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using DataFlow.POC.Benchmarks.DeprecatedBlocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static BenchmarkActorHelpers;
using DataFlow.POC.Registry;

/// <summary>
/// ActorBlock benchmarks designed for external profiling with dotnet-counters.
/// Compares steady-state performance vs rotation at different frequencies.
/// Use with dotnet-counters to collect time-series metrics for memory and performance analysis.
/// </summary>
public class ActorBlockBenchmark
{
    /// <summary>
    /// Simple actor that processes items without rotation.
    /// Used for steady-state baseline performance.
    /// </summary>
    private class SteadyStateActor : IStreamActor<int, int>
    {
        private int _count;

        public async IAsyncEnumerable<int> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                _count++;
                // Simulate some processing work
                await Task.Delay(1, context.CancellationToken);
                yield return item * 2;
            }
        }
    }

    /// <summary>
    /// Actor that rotates after processing a specified number of items.
    /// Used to measure rotation overhead.
    /// </summary>
    private class RotatingActor : IStreamActor<int, int>
    {
        private readonly int _rotateAfter;
        private int _count;

        public RotatingActor(int rotateAfter = 100)
        {
            _rotateAfter = rotateAfter;
        }

        public async IAsyncEnumerable<int> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                _count++;
                
                // Simulate some processing work
                await Task.Delay(1, context.CancellationToken);
                yield return item * 2;

                if (_count >= _rotateAfter)
                {
                    context.RequestRotation();
                    yield break;
                }
            }
        }
    }

    /// <summary>
    /// Actor that simulates memory-intensive operations and rotates periodically
    /// to release memory. This demonstrates a realistic use case for rotation.
    /// </summary>
    private class MemoryIntensiveActor : IStreamActor<int, int>
    {
        private readonly int _rotateAfter;
        private readonly List<byte[]> _memoryBuffer = new();
        private int _count;

        public MemoryIntensiveActor(int rotateAfter = 50)
        {
            _rotateAfter = rotateAfter;
        }

        public async IAsyncEnumerable<int> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                _count++;
                
                // Simulate memory accumulation (e.g., caching, buffering)
                _memoryBuffer.Add(new byte[1024]); // 1 KB per item
                
                // Simulate some processing work
                await Task.Delay(1, context.CancellationToken);
                yield return item * 2;

                if (_count >= _rotateAfter)
                {
                    context.RequestRotation();
                    yield break;
                }
            }
        }
    }

    private static async IAsyncEnumerable<int> ProduceIntegers(int count)
    {
        for (int i = 1; i <= count; i++)
        {
            yield return i;
        }
    }

    /// <summary>
    /// Run steady-state benchmark (no rotation).
    /// Designed for external profiling with dotnet-counters.
    /// </summary>
    public static async Task RunSteadyStateAsync(int itemCount)
    {
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("ActorBlock Benchmark - Steady State (No Rotation)");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine($"Items: {itemCount:N0}");
        Console.WriteLine("Use dotnet-counters to monitor real-time performance metrics.");
        Console.WriteLine();

        var services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .AddScoped<SteadyStateActor>()
            .BuildServiceProvider();
        
        var sw = Stopwatch.StartNew();
        
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(itemCount));
        var actorBlock = new ActorBlock<int, int, SteadyStateActor>(
            "steady-actor",
            services.GetRequiredService<IServiceScopeFactory>());
        
        var processedCount = 0;
        var counterServices = new ServiceCollection();
        counterServices.AddScoped(_ => new NoOpProcessorActor<int>(() => Interlocked.Increment(ref processedCount)));
        var counterServiceProvider = counterServices.BuildServiceProvider();
        var processor = new ActorBlock<int, object, NoOpProcessorActor<int>>("processor", counterServiceProvider.GetRequiredService<IServiceScopeFactory>());
        
        var builder = GraphHelpers.CreateGraphBuilder("steady-state-flow");
        builder.AddBlock(producer)
            .AddBlock(actorBlock)
            .AddBlock(processor)
            .Connect(producer, actorBlock)
            .Connect(actorBlock, processor);
        
        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        var context = new ExecutionContext(services, CancellationToken.None);
        
        await graph.ExecuteAsync(context);
        
        sw.Stop();
        
        var throughput = itemCount / sw.Elapsed.TotalSeconds;
        Console.WriteLine();
        Console.WriteLine($"Completed: {processedCount:N0} items in {sw.ElapsedMilliseconds:N0} ms");
        Console.WriteLine($"Throughput: {throughput:N0} items/sec");
        Console.WriteLine($"Rotations: 0 (steady-state)");
    }

    /// <summary>
    /// Run rotation benchmark with specified rotation frequency.
    /// Designed for external profiling with dotnet-counters.
    /// </summary>
    public static async Task RunRotationAsync(int itemCount, int rotateAfter)
    {
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine($"ActorBlock Benchmark - Rotation (Every {rotateAfter} Items)");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine($"Items: {itemCount:N0}");
        Console.WriteLine($"Rotation Frequency: Every {rotateAfter} items");
        Console.WriteLine("Use dotnet-counters to monitor real-time performance metrics.");
        Console.WriteLine();
        
        var services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .AddScoped<RotatingActor>(sp => new RotatingActor(rotateAfter))
            .BuildServiceProvider();
        
        var sw = Stopwatch.StartNew();
        
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(itemCount));
        var actorBlock = new ActorBlock<int, int, RotatingActor>(
            "rotating-actor",
            services.GetRequiredService<IServiceScopeFactory>());
        
        var processedCount = 0;
        var counterServices = new ServiceCollection();
        counterServices.AddScoped(_ => new NoOpProcessorActor<int>(() => Interlocked.Increment(ref processedCount)));
        var counterServiceProvider = counterServices.BuildServiceProvider();
        var processor = new ActorBlock<int, object, NoOpProcessorActor<int>>("processor", counterServiceProvider.GetRequiredService<IServiceScopeFactory>());
        
        var builder = GraphHelpers.CreateGraphBuilder("rotation-flow");
        builder.AddBlock(producer)
            .AddBlock(actorBlock)
            .AddBlock(processor)
            .Connect(producer, actorBlock)
            .Connect(actorBlock, processor);
        
        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        var context = new ExecutionContext(services, CancellationToken.None);
        
        await graph.ExecuteAsync(context);
        
        sw.Stop();
        
        var throughput = itemCount / sw.Elapsed.TotalSeconds;
        var expectedRotations = (itemCount + rotateAfter - 1) / rotateAfter;
        
        Console.WriteLine();
        Console.WriteLine($"Completed: {processedCount:N0} items in {sw.ElapsedMilliseconds:N0} ms");
        Console.WriteLine($"Throughput: {throughput:N0} items/sec");
        Console.WriteLine($"Expected Rotations: ~{expectedRotations}");
    }

    /// <summary>
    /// Run memory-intensive benchmark with rotation.
    /// Designed for external profiling with dotnet-counters to observe memory patterns.
    /// </summary>
    public static async Task RunMemoryIntensiveAsync(int itemCount, int rotateAfter)
    {
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("ActorBlock Benchmark - Memory-Intensive with Rotation");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine($"Items: {itemCount:N0}");
        Console.WriteLine($"Rotation Frequency: Every {rotateAfter} items");
        Console.WriteLine($"Memory per item: ~1 KB (accumulated until rotation)");
        Console.WriteLine("Use dotnet-counters to monitor memory growth and GC patterns.");
        Console.WriteLine();
        
        var services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .AddScoped<MemoryIntensiveActor>(sp => new MemoryIntensiveActor(rotateAfter))
            .BuildServiceProvider();
        
        var sw = Stopwatch.StartNew();
        
        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(itemCount));
        var actorBlock = new ActorBlock<int, int, MemoryIntensiveActor>(
            "memory-intensive-actor",
            services.GetRequiredService<IServiceScopeFactory>());
        
        var processedCount = 0;
        var counterServices = new ServiceCollection();
        counterServices.AddScoped(_ => new NoOpProcessorActor<int>(() => Interlocked.Increment(ref processedCount)));
        var counterServiceProvider = counterServices.BuildServiceProvider();
        var processor = new ActorBlock<int, object, NoOpProcessorActor<int>>("processor", counterServiceProvider.GetRequiredService<IServiceScopeFactory>());
        
        var builder = GraphHelpers.CreateGraphBuilder("memory-intensive-flow");
        builder.AddBlock(producer)
            .AddBlock(actorBlock)
            .AddBlock(processor)
            .Connect(producer, actorBlock)
            .Connect(actorBlock, processor);
        
        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        var context = new ExecutionContext(services, CancellationToken.None);
        
        await graph.ExecuteAsync(context);
        
        sw.Stop();
        
        var throughput = itemCount / sw.Elapsed.TotalSeconds;
        var expectedRotations = (itemCount + rotateAfter - 1) / rotateAfter;
        
        Console.WriteLine();
        Console.WriteLine($"Completed: {processedCount:N0} items in {sw.ElapsedMilliseconds:N0} ms");
        Console.WriteLine($"Throughput: {throughput:N0} items/sec");
        Console.WriteLine($"Expected Rotations: ~{expectedRotations}");
        Console.WriteLine($"Expected Peak Memory: ~{rotateAfter} KB per rotation cycle");
    }
}
