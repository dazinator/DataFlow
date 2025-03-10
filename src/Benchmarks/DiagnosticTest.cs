namespace Benchmarks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Uniun.DataFlow.Builder;
using Microsoft.Extensions.Logging;

public static class DiagnosticTest
{
    public static async Task RunTest()
    {
        Console.WriteLine("===============================================");
        Console.WriteLine("Starting Diagnostic Test");
        Console.WriteLine("===============================================");

        // Create very small test (only 10 items)
        const int ItemCount = 10;
        var items = Enumerable.Range(1, ItemCount).ToArray();

        Console.WriteLine($"Created test data with {ItemCount} items");

        // Setup services with console logging
        var services = new ServiceCollection();
        services.AddConsoleLogging(LogLevel.Debug);
        services.AddDataFlowMetrics();
        services.AddDataFlows();

        Console.WriteLine("Services configured");

        // Build service provider
        var serviceProvider = services.BuildServiceProvider();
        Console.WriteLine("Service provider built");

        // Configure manual completion signaling
        var completionSource = new TaskCompletionSource<bool>();
        var count = 0;

        Console.WriteLine("Building DataFlow pipeline...");

        // Build a very simple pipeline
        var builder = new DataFlowBuilder(serviceProvider);
        builder.AddProducer<int>("source", sp => new TestProducer(items))
               .AddProcessor<int>("processor", sp => new TestProcessor(item => {
                   var processed = Interlocked.Increment(ref count);
                   Console.WriteLine($"Processed item {item} ({processed}/{ItemCount})");

                   if (processed >= ItemCount)
                   {
                       Console.WriteLine("All items processed, signaling completion");
                       completionSource.TrySetResult(true);
                   }
               }))
               .ReceiveFrom("source");

        var flow = builder.Build();
        Console.WriteLine("Pipeline built successfully");

        // Create execution context with 10-second timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var context = new DataFlowContext
        {
            InvocationId = Guid.NewGuid(),
            ServiceProvider = serviceProvider,
            CancellationToken = cts.Token,
            Name = "DiagnosticTest"
        };

        Console.WriteLine("Starting flow execution");

        try
        {
            // Two different approaches to handle completion
            var executionTask = flow.ExecuteAsync(context);
            var completionTask = completionSource.Task;

            Console.WriteLine("Waiting for completion (timeout: 10 seconds)...");

            // Wait for either completion or timeout
            if (await Task.WhenAny(completionTask, Task.Delay(TimeSpan.FromSeconds(10))) == completionTask)
            {
                Console.WriteLine("Completion signaled successfully");
            }
            else
            {
                Console.WriteLine("WARNING: Timed out waiting for completion signal");
            }

            // Check if the flow execution has completed
            var executionStatus = executionTask.IsCompleted ? "completed" : "not completed";
            Console.WriteLine($"Flow execution is {executionStatus}");

            if (!executionTask.IsCompleted)
            {
                Console.WriteLine("Canceling flow execution...");
                cts.Cancel();

                try
                {
                    // Wait with short timeout for cancellation to be processed
                    await Task.WhenAny(executionTask, Task.Delay(TimeSpan.FromSeconds(2)));
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Flow execution was canceled successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during cancellation: {ex.Message}");
                }
            }
            else
            {
                // Wait for the flow to fully complete
                await executionTask;
                Console.WriteLine("Flow execution completed successfully");
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Operation was canceled");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }

        Console.WriteLine("===============================================");
        Console.WriteLine("Diagnostic Test Complete");
        Console.WriteLine("===============================================");
    }

    private class TestProducer : IStreamProducer<int>
    {
        private readonly int[] _items;

        public TestProducer(int[] items)
        {
            _items = items;
        }

        public async IAsyncEnumerable<int> ProduceAsync([EnumeratorCancellation] CancellationToken cancellation)
        {
            Console.WriteLine("Producer starting");

            foreach (var item in _items)
            {
                cancellation.ThrowIfCancellationRequested();
                Console.WriteLine($"Producing item {item}");
                yield return item;

                // Small delay to make output easier to follow
                await Task.Delay(100, cancellation);
            }

            Console.WriteLine("Producer completed");
        }
    }

    private class TestProcessor : IStreamProcessor<int>
    {
        private readonly Action<int> _onProcess;

        public TestProcessor(Action<int> onProcess)
        {
            _onProcess = onProcess;
        }

        public async Task ProcessAsync(IAsyncEnumerable<int> input, CancellationToken cancellationToken)
        {
            Console.WriteLine("Processor starting");

            try
            {
                await foreach (var item in input.WithCancellation(cancellationToken))
                {
                    Console.WriteLine($"Processing item {item}");
                    _onProcess(item);
                }

                Console.WriteLine("Processor completed normally");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Processor was canceled");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Processor error: {ex.Message}");
                throw;
            }
        }
    }
}

