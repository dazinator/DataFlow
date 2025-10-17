namespace Tests.DataFlow;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tests.DataFlow.Utils.Transformers;
using Uniun.DataFlow.Blocks.Routing;

[IntegrationTest]
public class PersistentRoutingBlockTests
{
    public ITestOutputHelper Output { get; }
    public ServiceCollection Services { get; }

    public PersistentRoutingBlockTests(ITestOutputHelper output)
    {
        Output = output;
        Services = new ServiceCollection();
        AddDefaultServices(Services);
    }

    private void AddDefaultServices(IServiceCollection services)
    {
        Services.AddLogging(builder => builder.AddXUnit(Output));
        services.AddDataFlows();
        Services.AddMetrics();
        Services.AddDataFlowMetrics();
    }

    [Fact]
    public async Task PersistentRouter_Routes_Items_To_Correct_Blocks()
    {
        // Arrange
        var processedItems = new ConcurrentDictionary<string, List<int>>();
        var items = Enumerable.Range(1, 10).ToArray();

        var sp = Services.BuildServiceProvider();
        var builder = new DataFlowBuilder(sp);
        var logger = sp.GetRequiredService<ILogger<PersistentRoutingBlockTests>>();

        // Act
        builder.AddProducer("source", ctx => new TestProducer<int>(items))
            .AddPersistentRouter<int>("router",
                item =>
                {
                    var routeKey = item % 2 == 0 ? "even" : "odd";
                    logger.LogInformation("Routing item {Item} to {RouteKey}", item, routeKey);
                    return routeKey;
                },
                context =>
                {
                    logger.LogInformation("Creating flow for route {RouteKey}", context.RoutingKey);
                    var routeBuilder = new DataFlowBuilder(context.ServiceProvider);

                    // Create a simple flow with just a processor
                    routeBuilder.AddProcessor<int>("processor", sp => new TestProcessor<int>(
                        onProcessItem: number =>
                        {
                            logger.LogInformation("Processing {Number} on route {Route}", number, context.RoutingKey);
                            processedItems.AddOrUpdate(
                                context.RoutingKey,
                                key => new List<int> { number },
                                (key, list) =>
                                {
                                    lock (list)
                                    {
                                        list.Add(number);
                                        return list;
                                    }
                                });
                        }));

                    var flow = routeBuilder.Build();
                    var targetBlock = routeBuilder.GetTargetBlock<int>("processor");
                    return (flow, targetBlock);
                })
            .ReceiveFrom("source");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context);

        // Let's log what we got
        logger.LogInformation("Even items: {Items}", string.Join(",", processedItems.GetValueOrDefault("even", new List<int>())));
        logger.LogInformation("Odd items: {Items}", string.Join(",", processedItems.GetValueOrDefault("odd", new List<int>())));

        // Assert
        var evenItems = processedItems.GetValueOrDefault("even", new List<int>());
        var oddItems = processedItems.GetValueOrDefault("odd", new List<int>());

        Assert.Equal(new[] { 2, 4, 6, 8, 10 }, evenItems.OrderBy(x => x));
        Assert.Equal(new[] { 1, 3, 5, 7, 9 }, oddItems.OrderBy(x => x));
    }

    [Fact]
    public async Task PersistentRouter_Creates_And_Manages_Scopes_Correctly()
    {
        // Arrange
        Services.AddScoped<IScopedProcessor, ScopedProcessor>();
        Services.AddSingleton<ScopeTracker>();

        var items = Enumerable.Range(1, 10).ToArray();
        var scopeTracker = new ScopeTracker();

        Services.AddSingleton(scopeTracker);
        var sp = Services.BuildServiceProvider();

        var builder = new DataFlowBuilder(sp);
        var logger = sp.GetRequiredService<ILogger<PersistentRoutingBlockTests>>();

        // Act
        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddPersistentRouter<int>("router",
                item => (item % 2 == 0) ? "even" : "odd",
                context =>
                {
                    // Get scoped processor for this route
                    var processor = context.ServiceProvider.GetRequiredService<IScopedProcessor>();
                    var routeBuilder = new DataFlowBuilder(context.ServiceProvider);

                    // Create a simple flow with just a processor
                    routeBuilder.AddProcessor<int>("processor", sp => new TestProcessor<int>(
                        onProcessItem: number =>
                        {
                            // Process using the scoped processor
                            processor.ProcessItem(number, context.RoutingKey);
                        }));

                    var flow = routeBuilder.Build();
                    var targetBlock = routeBuilder.GetTargetBlock<int>("processor");
                    return (flow, targetBlock);
                })
            .ReceiveFrom("source");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);

        // Execute flow and wait for completion
        await flow.ExecuteAsync(context);

        // Assert
        var evenProcessor = scopeTracker.GetProcessorForRoute("even");
        var oddProcessor = scopeTracker.GetProcessorForRoute("odd");

        // Verify different scopes were created
        Assert.NotNull(evenProcessor);
        Assert.NotNull(oddProcessor);
        Assert.NotEqual(evenProcessor, oddProcessor);

        // Verify items were processed by correct processors
        Assert.Equal(new[] { 2, 4, 6, 8, 10 }, evenProcessor.ProcessedItems.OrderBy(x => x));
        Assert.Equal(new[] { 1, 3, 5, 7, 9 }, oddProcessor.ProcessedItems.OrderBy(x => x));

        // Verify scopes were disposed (should happen at end of block execution)
        Assert.True(evenProcessor.WasDisposed);
        Assert.True(oddProcessor.WasDisposed);
    }

    [Fact]
    public async Task PersistentRouter_Routes_To_Complex_DataFlow_With_Multiple_Blocks()
    {
        // Arrange
        var transformedItems = new ConcurrentDictionary<string, List<string>>();
        var processedItems = new ConcurrentDictionary<string, List<string>>();
        var items = Enumerable.Range(1, 10).ToArray();

        var sp = Services.BuildServiceProvider();
        var builder = new DataFlowBuilder(sp);
        var logger = sp.GetRequiredService<ILogger<PersistentRoutingBlockTests>>();

        // Act
        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddPersistentRouter<int>("router",
                item =>
                {
                    var routeKey = item % 2 == 0 ? "even" : "odd";
                    logger.LogInformation("Routing item {Item} to {RouteKey}", item, routeKey);
                    return routeKey;
                },
                context =>
                {
                    // Create a more complex flow with multiple blocks
                    logger.LogInformation("Creating complex flow for route {RouteKey}", context.RoutingKey);
                    var routeBuilder = new DataFlowBuilder(context.ServiceProvider);

                    // First block: Transform int to string
                    routeBuilder.AddTransform<int, string>("transformer",
                        sp => new NumberTransformer(
                            prefix: context.RoutingKey,
                            onTransform: result =>
                            {
                                transformedItems.AddOrUpdate(
                                    context.RoutingKey,
                                    key => new List<string> { result },
                                    (key, list) =>
                                    {
                                        lock (list)
                                        {
                                            list.Add(result);
                                            return list;
                                        }
                                    });
                                logger.LogInformation("Transformed to {Result} on route {Route}",
                                    result,
                                    context.RoutingKey);
                            }),
                        (o) => { o.MaxConcurrency = 1; });

                    // Second block: Process the transformed strings
                    routeBuilder.AddProcessor<string>("processor", sp =>
                        new TestProcessor<string>(
                            onProcessItem: str =>
                            {
                                processedItems.AddOrUpdate(
                                    context.RoutingKey,
                                    key => new List<string> { str },
                                    (key, list) =>
                                    {
                                        lock (list)
                                        {
                                            list.Add(str);
                                            return list;
                                        }
                                    });
                                logger.LogInformation("Processing {String} on route {Route}", str, context.RoutingKey);
                            }),
                            options: new BlockOptions { MaxConcurrency = 1 })
                        .ReceiveFrom("transformer");

                    var flow = routeBuilder.Build();
                    // Returning the first block in the flow (the transformer) as the target
                    var targetBlock = routeBuilder.GetTargetBlock<int>("transformer");
                    return (flow, targetBlock);
                })
            .ReceiveFrom("source");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);

        try
        {
            await flow.ExecuteAsync(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Flow execution error");
            throw; // Rethrow to fail the test
        }

        // Assert
        // Verify transformations
        var evenTransformations = transformedItems.GetValueOrDefault("even", new List<string>());
        var oddTransformations = transformedItems.GetValueOrDefault("odd", new List<string>());

        Assert.Equal(5, evenTransformations.Count);
        Assert.Equal(5, oddTransformations.Count);

        foreach (var num in new[] { 2, 4, 6, 8, 10 })
        {
            Assert.Contains(evenTransformations, s => s == $"even-{num}");
        }

        foreach (var num in new[] { 1, 3, 5, 7, 9 })
        {
            Assert.Contains(oddTransformations, s => s == $"odd-{num}");
        }

        // Verify processing
        var evenProcessed = processedItems.GetValueOrDefault("even", new List<string>());
        var oddProcessed = processedItems.GetValueOrDefault("odd", new List<string>());

        Assert.Equal(5, evenProcessed.Count);
        Assert.Equal(5, oddProcessed.Count);
        Assert.Equal(evenTransformations.OrderBy(x => x), evenProcessed.OrderBy(x => x));
        Assert.Equal(oddTransformations.OrderBy(x => x), oddProcessed.OrderBy(x => x));
    }

    [Fact]
    public async Task PersistentRouter_Handles_Many_Routes_Efficiently()
    {
        // Arrange
        var processedItems = new ConcurrentDictionary<string, List<int>>();
        var items = Enumerable.Range(1, 100).ToArray();

        var sp = Services.BuildServiceProvider();
        var builder = new DataFlowBuilder(sp);
        var logger = sp.GetRequiredService<ILogger<PersistentRoutingBlockTests>>();

        // Act
        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddPersistentRouter<int>("router",
                item =>
                {
                    // Create 10 different routes (0-9)
                    var routeKey = $"route-{item % 10}";
                    return routeKey;
                },
                context =>
                {
                    logger.LogInformation("Creating flow for route {RouteKey}", context.RoutingKey);
                    var routeBuilder = new DataFlowBuilder(context.ServiceProvider);

                    // Create a simple flow with just a processor
                    routeBuilder.AddProcessor<int>("processor", sp => new TestProcessor<int>(
                        onProcessItem: number =>
                        {
                            processedItems.AddOrUpdate(
                                context.RoutingKey,
                                key => new List<int> { number },
                                (key, list) =>
                                {
                                    lock (list)
                                    {
                                        list.Add(number);
                                        return list;
                                    }
                                });
                        }));

                    var flow = routeBuilder.Build();
                    var targetBlock = routeBuilder.GetTargetBlock<int>("processor");
                    return (flow, targetBlock);
                })
            .ReceiveFrom("source");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context);

        // Assert
        // Should have created 10 routes
        Assert.Equal(10, processedItems.Count);

        // Each route should have processed 10 items
        foreach (var routeKey in processedItems.Keys)
        {
            var items_for_route = processedItems[routeKey];
            Assert.Equal(10, items_for_route.Count);
        }

        // Verify all items were processed
        var totalProcessed = processedItems.Values.SelectMany(x => x).Count();
        Assert.Equal(100, totalProcessed);
    }

    [Fact]
    public async Task PersistentRouter_Handles_Concurrent_Processing()
    {
        // Arrange
        var processedItems = new ConcurrentDictionary<string, List<int>>();
        var items = Enumerable.Range(1, 20).ToArray(); // Reduce items for more predictable timing

        var sp = Services.BuildServiceProvider();
        var builder = new DataFlowBuilder(sp);
        var logger = sp.GetRequiredService<ILogger<PersistentRoutingBlockTests>>();

        // Act
        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddPersistentRouter<int>("router",
                item => item % 2 == 0 ? "even" : "odd",
                context =>
                {
                    var routeBuilder = new DataFlowBuilder(context.ServiceProvider);

                    // Create a processor with some processing delay to test concurrency
                    routeBuilder.AddProcessor<int>("processor", sp => new TestProcessor<int>(
                        onProcessItem: number =>
                        {
                            processedItems.AddOrUpdate(
                                context.RoutingKey,
                                key => new List<int> { number },
                                (key, list) =>
                                {
                                    lock (list)
                                    {
                                        list.Add(number);
                                        return list;
                                    }
                                });
                        },
                        delay: TimeSpan.FromMilliseconds(50)), // Larger delay to make concurrency more visible
                        options: new BlockOptions { MaxConcurrency = 3 }); // Allow concurrent processing within each route

                    var flow = routeBuilder.Build();
                    var targetBlock = routeBuilder.GetTargetBlock<int>("processor");
                    return (flow, targetBlock);
                },
                options =>
                {
                    options.MaxConcurrency = 2; // Allow concurrent route processing
                })
            .ReceiveFrom("source");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);

        // Measure execution time
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await flow.ExecuteAsync(context);
        stopwatch.Stop();

        // Assert
        var evenItems = processedItems.GetValueOrDefault("even", new List<int>());
        var oddItems = processedItems.GetValueOrDefault("odd", new List<int>());

        Assert.Equal(10, evenItems.Count);
        Assert.Equal(10, oddItems.Count);

        // Verify all items were processed
        Assert.Equal(20, evenItems.Count + oddItems.Count);

        logger.LogInformation("Concurrent processing completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

        // Test that we have reasonable concurrency - not perfect timing, but better than sequential
        // Sequential would be: 20 items × 50ms = 1000ms
        // With concurrency: Should be significantly less
        // Let's just verify it's working reasonably well (under 600ms shows some concurrency)
        Assert.True(stopwatch.ElapsedMilliseconds < 600,
            $"Expected some concurrency benefit, sequential would be ~1000ms, took {stopwatch.ElapsedMilliseconds}ms");

        // Also verify it's not too fast (which would indicate the delay isn't working)
        Assert.True(stopwatch.ElapsedMilliseconds > 100,
            $"Expected processing to take some time due to delays, took {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task PersistentRouter_Handles_Route_Creation_Exceptions()
    {
        // Arrange
        var items = Enumerable.Range(1, 5).ToArray();

        var sp = Services.BuildServiceProvider();
        var builder = new DataFlowBuilder(sp);
        var logger = sp.GetRequiredService<ILogger<PersistentRoutingBlockTests>>();

        // Act & Assert
        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddPersistentRouter<int>("router",
                item => item % 2 == 0 ? "even" : "odd",
                context =>
                {
                    // Simulate an error during route creation for "even" routes
                    if (context.RoutingKey == "even")
                    {
                        throw new InvalidOperationException("Simulated route creation error");
                    }

                    var routeBuilder = new DataFlowBuilder(context.ServiceProvider);
                    routeBuilder.AddProcessor<int>("processor", sp => new TestProcessor<int>());

                    var flow = routeBuilder.Build();
                    var targetBlock = routeBuilder.GetTargetBlock<int>("processor");
                    return (flow, targetBlock);
                })
            .ReceiveFrom("source");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);

        // Should throw the route creation exception directly
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => flow.ExecuteAsync(context));
        Assert.Contains("Simulated route creation error", exception.Message);
    }

    [Fact]
    public async Task PersistentRouter_Handles_Empty_Source()
    {
        // Arrange
        var processedItems = new ConcurrentDictionary<string, List<int>>();
        var items = Array.Empty<int>();

        var sp = Services.BuildServiceProvider();
        var builder = new DataFlowBuilder(sp);
        var logger = sp.GetRequiredService<ILogger<PersistentRoutingBlockTests>>();

        // Act
        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddPersistentRouter<int>("router",
                item => item % 2 == 0 ? "even" : "odd",
                context =>
                {
                    var routeBuilder = new DataFlowBuilder(context.ServiceProvider);

                    routeBuilder.AddProcessor<int>("processor", sp => new TestProcessor<int>(
                        onProcessItem: number =>
                        {
                            processedItems.AddOrUpdate(
                                context.RoutingKey,
                                key => new List<int> { number },
                                (key, list) =>
                                {
                                    lock (list)
                                    {
                                        list.Add(number);
                                        return list;
                                    }
                                });
                        }));

                    var flow = routeBuilder.Build();
                    var targetBlock = routeBuilder.GetTargetBlock<int>("processor");
                    return (flow, targetBlock);
                })
            .ReceiveFrom("source");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context);

        // Assert
        Assert.Empty(processedItems);
    }

    [Fact]
    public async Task PersistentRouter_Handles_Cancellation()
    {
        // Arrange
        var processedItems = new ConcurrentDictionary<string, List<int>>();
        var items = Enumerable.Range(1, 100).ToArray();

        var sp = Services.BuildServiceProvider();
        var builder = new DataFlowBuilder(sp);
        var logger = sp.GetRequiredService<ILogger<PersistentRoutingBlockTests>>();

        using var cts = new CancellationTokenSource();

        // Act
        builder.AddProducer("source", sp => new TestProducer<int>(items, delay: TimeSpan.FromMilliseconds(50)))
            .AddPersistentRouter<int>("router",
                item => item % 2 == 0 ? "even" : "odd",
                context =>
                {
                    var routeBuilder = new DataFlowBuilder(context.ServiceProvider);

                    routeBuilder.AddProcessor<int>("processor", sp => new TestProcessor<int>(
                        onProcessItem: number =>
                        {
                            processedItems.AddOrUpdate(
                                context.RoutingKey,
                                key => new List<int> { number },
                                (key, list) =>
                                {
                                    lock (list)
                                    {
                                        list.Add(number);
                                        return list;
                                    }
                                });

                            // Cancel after processing some items
                            if (processedItems.Values.SelectMany(x => x).Count() >= 10)
                            {
                                cts.Cancel();
                            }
                        },
                        delay: TimeSpan.FromMilliseconds(10)));

                    var flow = routeBuilder.Build();
                    var targetBlock = routeBuilder.GetTargetBlock<int>("processor");
                    return (flow, targetBlock);
                })
            .ReceiveFrom("source");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp, cts.Token);

        // Assert
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => flow.ExecuteAsync(context));

        // Should be either OperationCanceledException or AggregateException containing cancellation exceptions
        Assert.True(
            exception is OperationCanceledException ||
            exception is TaskCanceledException ||
            (exception is AggregateException aggEx &&
             aggEx.InnerExceptions.All(e => e is OperationCanceledException || e is TaskCanceledException)),
            $"Expected cancellation exception, but got: {exception.GetType().Name}");

        // Should have processed some items before cancellation
        var totalProcessed = processedItems.Values.SelectMany(x => x).Count();
        Assert.True(totalProcessed >= 10, $"Expected at least 10 items processed, got {totalProcessed}");
        Assert.True(totalProcessed < 100, $"Expected cancellation before all items processed, got {totalProcessed}");
    }

    private IDataFlowContext CreateContext(string v, Guid guid, ServiceProvider provider, CancellationToken ct = default)
    {
        return DataFlowContextTestUtils.GetContext(v, guid, provider, ct);
    }
}

// Test classes to support the persistent routing tests
public class PersistentRoutingTestMetrics
{
    private int _itemsProduced;
    private int _routesCreated;
    private int _itemsProcessed;
    private int _exceptionsCaught;
    private readonly List<string> _exceptionDetails = new List<string>();
    private readonly object _lock = new object();

    public int ItemsProduced => _itemsProduced;
    public int RoutesCreated => _routesCreated;
    public int ItemsProcessed => _itemsProcessed;
    public int ExceptionsCaught => _exceptionsCaught;
    public IReadOnlyList<string> ExceptionDetails
    {
        get
        {
            lock (_lock)
            {
                return _exceptionDetails.ToList();
            }
        }
    }

    public void IncrementItemsProduced() => Interlocked.Increment(ref _itemsProduced);
    public void IncrementRoutesCreated() => Interlocked.Increment(ref _routesCreated);
    public void IncrementItemsProcessed() => Interlocked.Increment(ref _itemsProcessed);
    public void IncrementExceptionsCaught() => Interlocked.Increment(ref _exceptionsCaught);

    public void AddException(Exception ex)
    {
        IncrementExceptionsCaught();
        lock (_lock)
        {
            _exceptionDetails.Add($"Exception: {ex.GetType().Name}");
            _exceptionDetails.Add($"Message: {ex.Message}");
        }
    }
}

// Test configuration for persistent routing stress test
public class PersistentRoutingStressTestConfig : IDataFlowConfiguration
{
    public void Configure(DataFlowBuilder builder)
    {
        var testMetrics = builder.ServiceProvider.GetRequiredService<PersistentRoutingTestMetrics>();
        var logger = builder.ServiceProvider.GetRequiredService<ILogger<PersistentRoutingStressTestConfig>>();

        // Add producer of test items with different route keys
        builder.AddProducer<TestItem>("source", sp => new StressTestProducer(testMetrics, 100, 5))
            .AddPersistentRouter<TestItem>("router",
                item => item.RouteKey,
                context =>
                {
                    testMetrics.IncrementRoutesCreated();
                    logger.LogInformation("Creating route for key: {RouteKey}", context.RoutingKey);

                    var routeBuilder = new DataFlowBuilder(context.ServiceProvider);

                    routeBuilder.AddProcessor<TestItem>("processor", sp => new TestItemProcessor(testMetrics),
                        options: new BlockOptions
                        {
                            MaxConcurrency = 3,
                            UseSeperateScopes = true
                        });

                    var flow = routeBuilder.Build();
                    var targetBlock = routeBuilder.GetTargetBlock<TestItem>("processor");
                    return (flow, targetBlock);
                },
                options =>
                {
                    options.MaxConcurrency = 5;
                })
            .ReceiveFrom("source");
    }
}

public class StressTestProducer : IStreamProducer<TestItem>
{
    private readonly PersistentRoutingTestMetrics _metrics;
    private readonly string[] _routeKeys = { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J" };
    private readonly int _totalItems;
    private readonly int _itemsPerRoute;
    private int _idCounter = 0;

    public StressTestProducer(PersistentRoutingTestMetrics metrics, int totalItems, int itemsPerRoute)
    {
        _metrics = metrics;
        _totalItems = totalItems;
        _itemsPerRoute = itemsPerRoute;
    }

    public async IAsyncEnumerable<TestItem> ProduceAsync(IDataFlowContext context,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellation)
    {
        for (var i = 0; i < _totalItems; i++)
        {
            if (cancellation.IsCancellationRequested)
            {
                break;
            }

            var routeKey = _routeKeys[i % _routeKeys.Length];
            _metrics.IncrementItemsProduced();

            yield return new TestItem
            {
                Id = _idCounter++,
                RouteKey = routeKey,
                Data = $"Data-{routeKey}-{_idCounter}"
            };

            // Small delay to simulate realistic production rates
            await Task.Delay(10, cancellation);
        }
    }
}

public class TestItem
{
    public int Id { get; set; }
    public string RouteKey { get; set; }
    public string Data { get; set; }
}

public class TestItemProcessor : IStreamProcessor<TestItem>
{
    private readonly PersistentRoutingTestMetrics _metrics;

    public TestItemProcessor(PersistentRoutingTestMetrics metrics)
    {
        _metrics = metrics;
    }

    public async Task ProcessAsync(IDataFlowContext context, IAsyncEnumerable<TestItem> input, CancellationToken cancellationToken)
    {
        await foreach (var item in input.WithCancellation(cancellationToken))
        {
            try
            {
                // Simulate some processing work
                await Task.Delay(20, cancellationToken);
                _metrics.IncrementItemsProcessed();
            }
            catch (Exception ex)
            {
                _metrics.AddException(ex);
                throw;
            }
        }
    }
}
