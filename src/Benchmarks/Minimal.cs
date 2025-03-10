namespace Benchmarks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks.Dataflow;
using Uniun.DataFlow.Builder;
using Microsoft.Extensions.Logging;
using Uniun.DataFlow.Blocks.InputChannel;

/// <summary>
/// A minimal benchmark to identify potential hanging issues
/// </summary>
public partial class MinimalBenchmark
{
    private const int ItemCount = 100; // Using a small number for quick testing
    private readonly int[] _items;
    private ServiceProvider _serviceProvider;

    public MinimalBenchmark()
    {
        _items = Enumerable.Range(1, ItemCount).ToArray();

        // Set up DI for Uniun DataFlow with debug logging
        var services = new ServiceCollection();

        // Add logging with console output to see what's happening
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        services.AddDataFlowMetrics();
        services.AddDataFlows();
        _serviceProvider = services.BuildServiceProvider();
    }

    [Benchmark(Baseline = true)]
    public async Task TPL_DataFlow_BufferBlock()
    {
        Console.WriteLine("Starting TPL DataFlow benchmark with BufferBlock");

        var tcs = new TaskCompletionSource<bool>();
        var count = 0;

        var producer = new BufferBlock<int>(new DataflowBlockOptions { BoundedCapacity = 100 });

        var processor = new ActionBlock<int>(async x =>
        {
            Console.WriteLine($"TPL processing item {x}");
            await Task.Yield(); // Ensure async execution
            var processed = Interlocked.Increment(ref count);
            if (processed == ItemCount)
            {
                Console.WriteLine("TPL complete");
                tcs.SetResult(true);
            }
        }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });

        producer.LinkTo(processor, new DataflowLinkOptions { PropagateCompletion = true });

        // Push all items into the pipeline asynchronously
        foreach (var item in _items)
        {
            await producer.SendAsync(item);
        }

        Console.WriteLine("TPL producer complete");
        producer.Complete();

        // Wait for completion with timeout
        var completionTask = tcs.Task;
        if (await Task.WhenAny(completionTask, Task.Delay(TimeSpan.FromSeconds(10))) != completionTask)
        {
            Console.WriteLine("!!! TPL TIMEOUT !!!");
            throw new TimeoutException("Benchmark timed out after 10 seconds");
        }

        Console.WriteLine("TPL benchmark complete");
    }

    [Benchmark]
    public async Task Uniun_DataFlow_Minimal()
    {
        Console.WriteLine("Starting Uniun DataFlow benchmark");

        var processedCount = 0;
        var tcs = new TaskCompletionSource<bool>();

        var builder = new DataFlowBuilder(_serviceProvider);

        // Create the simplest possible pipeline
        builder.AddInputChannel<int>("source", null);

        var inputBlock = builder.GetSourceBlock<int>("source") as InputChannelBlock<int>;
        builder.AddProcessor<int>("processor", sp => new SimpleProcessor(
                   onProcess: item =>
                   {
                       Console.WriteLine($"Uniun processing item {item}");
                       var count = Interlocked.Increment(ref processedCount);
                       if (count == ItemCount)
                       {
                           Console.WriteLine("Uniun complete");
                           tcs.SetResult(true);
                       }
                   }))
               .ReceiveFrom("source");

        Console.WriteLine("Uniun pipeline built");

        var flow = builder.Build();

        var context = new DataFlowContext
        {
            InvocationId = Guid.NewGuid(),
            ServiceProvider = _serviceProvider,
            CancellationToken = CancellationToken.None,
            Name = "MinimalBenchmark"
        };

        // Execute flow with timeout
        Console.WriteLine("Uniun starting flow execution");
        var flowTask = flow.ExecuteAsync(context);

        // Push all items into the pipeline asynchronously
        foreach (var item in _items)
        {
            await inputBlock.Writer.WriteAsync(item);
        }

        var completionTask = tcs.Task;

        // Add timeout to catch hangs
        if (await Task.WhenAny(completionTask, Task.Delay(TimeSpan.FromSeconds(10))) != completionTask)
        {
            Console.WriteLine("!!! UNIUN TIMEOUT !!!");
            throw new TimeoutException("Benchmark timed out after 10 seconds");
        }

        Console.WriteLine("Uniun benchmark complete");
    }

    // Custom implementations for Uniun DataFlow
    private class SimpleProducer : IStreamProducer<int>
    {
        private readonly int[] _items;
        private readonly Action<int>? _onItem;

        public SimpleProducer(int[] items, Action<int>? onItem = null)
        {
            _items = items;
            _onItem = onItem;
        }

        public async IAsyncEnumerable<int> ProduceAsync([EnumeratorCancellation] CancellationToken cancellation)
        {
            foreach (var item in _items)
            {
                cancellation.ThrowIfCancellationRequested();
                _onItem?.Invoke(item);
                yield return item;
            }
        }
    }
}

