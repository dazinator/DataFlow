namespace Benchmarks;
using Uniun.DataFlow.Builder;

using System.Runtime.CompilerServices;
using System.Threading.Tasks.Dataflow;
using System;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Uniun.DataFlow.Blocks.InputChannel;

[MemoryDiagnoser]  // Track memory allocations
[ThreadingDiagnoser]  // Track threading information
public class SimplePipelineBenchmarks
{
    private const int ItemCount = 10_000;
    private readonly int[] _items;
    private ServiceProvider _serviceProvider;

    public SimplePipelineBenchmarks()
    {
        _items = Enumerable.Range(1, ItemCount).ToArray();

        // Set up DI for Uniun DataFlow
        var services = new ServiceCollection();
        // Add logging (with null logger to minimize overhead)
        services.AddLogging(builder =>
        {
            // builder.AddConsole();
            builder.AddProvider(NullLoggerProvider.Instance);
        });
        services.AddDataFlowMetrics();
        services.AddDataFlows();
        _serviceProvider = services.BuildServiceProvider();
    }

    [Params(1, 4, 8)] // Test with different degrees of parallelism
    public int MaxDegreeOfParallelism { get; set; }

    [Params(100, 500, 5000)] // Test with different bounded capacity
    public int BoundedCapacity { get; set; }

    [Benchmark(Baseline = true)]
    public async Task TPL_DataFlow_SimplePipeline()
    {
        var tcs = new TaskCompletionSource<bool>();
        var count = 0;

        var options = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = MaxDegreeOfParallelism,
            BoundedCapacity = BoundedCapacity
        };

        var producer = new BufferBlock<int>(options);
        //   var producer = new TransformBlock<int, int>(x => x, options);
        var processor = new ActionBlock<int>(
            _ =>
            {
                var processed = Interlocked.Increment(ref count);
                if (processed == ItemCount)
                {
                    tcs.SetResult(true);
                }
            },
            options);

        producer.LinkTo(processor, new DataflowLinkOptions { PropagateCompletion = true });

        // Push all items into the pipeline
        foreach (var item in _items)
        {
            await producer.SendAsync(item);
        }
        producer.Complete();

        // Wait for all items to be processed
        await tcs.Task;
    }

    [Benchmark]
    public async Task Uniun_DataFlow_SimplePipeline()
    {
        var processedCount = 0;
        var tcs = new TaskCompletionSource<bool>();

        // Configure Uniun DataFlow
        var builder = new DataFlowBuilder(_serviceProvider);
        var blockOptions = new BlockOptions() { MaxConcurrency = MaxDegreeOfParallelism, Capacity = BoundedCapacity };
        builder
            //.AddProducer<int>("source", sp => new SimpleProducer(_items))
            // Create the simplest possible pipeline
            .AddInputChannel<int>("source", blockOptions);

        var inputBlock = builder.GetSourceBlock<int>("source") as InputChannelBlock<int>;

        builder.AddProcessor<int>("processor", sp => new SimpleProcessor(
                   onProcess: _ =>
                   {
                       var count = Interlocked.Increment(ref processedCount);
                       if (count == ItemCount)
                       {
                           tcs.SetResult(true);
                       }
                   }), blockOptions)
               .ReceiveFrom("source");

        var flow = builder.Build();
        var context = new DataFlowContext
        {
            InvocationId = Guid.NewGuid(),
            ServiceProvider = _serviceProvider,
            CancellationToken = CancellationToken.None,
            Name = "SimplePipelineBenchmark"
        };

        // Execute flow and wait for completion
        var flowTask = flow.ExecuteAsync(context);

        // Push all items into the pipeline asynchronously
        foreach (var item in _items)
        {
            await inputBlock.Writer.WriteAsync(item);
        }


        await tcs.Task;
    }

    // Custom simple producer for Uniun DataFlow
    //private class SimpleProducer : IStreamProducer<int>
    //{
    //    private readonly int[] _items;

    //    public SimpleProducer(int[] items)
    //    {
    //        _items = items;
    //    }

    //    public async IAsyncEnumerable<int> ProduceAsync([EnumeratorCancellation] CancellationToken cancellation)
    //    {
    //        foreach (var item in _items)
    //        {
    //            cancellation.ThrowIfCancellationRequested();
    //            yield return item;
    //        }
    //    }
    //}

    // Custom simple processor for Uniun DataFlow   
}

