namespace Tests.DataFlow;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tests.DataFlow.Utils.Transformers;

[IntegrationTest]
public class RoutingBlockTests
{

    public ITestOutputHelper Output { get; }
    public ServiceCollection Services { get; }

    private readonly ServiceProvider _serviceProvider;

    public RoutingBlockTests(ITestOutputHelper output)
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
        Services.AddMemoryCache();
    }

    [Fact]
    public async Task Routes_Items_To_Correct_Blocks()
    {
        // Arrange
        var processedItems = new ConcurrentDictionary<string, List<int>>();
        var items = Enumerable.Range(1, 10).ToArray();

        var sp = Services.BuildServiceProvider();
        var builder = new DataFlowBuilder(sp);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var logger = sp.GetRequiredService<ILogger<RoutingBlockTests>>();

        // Act
        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddRouter<int>("router",
                item =>
                {
                    var routeKey = item % 2 == 0 ? "route1" : "route2";
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
                },
                options =>
                {
                    options.RouteCache = memoryCache;
                    options.RouteExpiration = TimeSpan.FromMinutes(5);
                })
            .ReceiveFrom("source");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context);

        // Let's log what we got
        logger.LogInformation("Route1 items: {Items}", string.Join(",", processedItems.GetValueOrDefault("route1", new List<int>())));
        logger.LogInformation("Route2 items: {Items}", string.Join(",", processedItems.GetValueOrDefault("route2", new List<int>())));

        // Assert
        var route1Items = processedItems.GetValueOrDefault("route1", new List<int>());
        var route2Items = processedItems.GetValueOrDefault("route2", new List<int>());

        Assert.Equal(new[] { 2, 4, 6, 8, 10 }, route1Items.OrderBy(x => x));
        Assert.Equal(new[] { 1, 3, 5, 7, 9 }, route2Items.OrderBy(x => x));
    }

    private IDataFlowContext CreateContext(string v, Guid guid, ServiceProvider provider, CancellationToken ct = default)
    {
        return DataFlowContextTestUtils.GetContext(v, guid, provider, ct);
    }

    [Fact]
    public async Task Routes_Create_And_Dispose_Scopes_Correctly()
    {
        // Arrange
        // Add our scoped service for tracking
        Services.AddScoped<IScopedProcessor, ScopedProcessor>();
        Services.AddSingleton<ScopeTracker>();

        var items = Enumerable.Range(1, 10).ToArray();
        var scopeTracker = new ScopeTracker();

        Services.AddSingleton(scopeTracker);
        var sp = Services.BuildServiceProvider();

        var builder = new DataFlowBuilder(sp);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var logger = sp.GetRequiredService<ILogger<RoutingBlockTests>>();

        // Act
        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddRouter<int>("router",
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
                },
                options =>
                {
                    options.RouteCache = memoryCache;
                    options.RouteExpiration = TimeSpan.FromSeconds(2); // Short expiration for testing
                })
            .ReceiveFrom("source");

        var flow = builder.Build();
        var context = new DataFlowContext() { CancellationToken = default, ServiceProvider = sp };

        // Execute flow and wait for completion
        await flow.ExecuteAsync(context);

        // Wait a bit to ensure routes expire and are disposed
        await Task.Delay(TimeSpan.FromSeconds(3));

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

        // Verify scopes were disposed
        Assert.True(evenProcessor.WasDisposed);
        Assert.True(oddProcessor.WasDisposed);
    }

    [Fact]
    public async Task Routes_To_Complex_DataFlow_With_Multiple_Blocks()
    {
        // Arrange
        var transformedItems = new ConcurrentDictionary<string, List<string>>();
        var processedItems = new ConcurrentDictionary<string, List<string>>();
        var items = Enumerable.Range(1, 10).ToArray();

        var sp = Services.BuildServiceProvider();
        var builder = new DataFlowBuilder(sp);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var logger = sp.GetRequiredService<ILogger<RoutingBlockTests>>();

        // Act
        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddRouter<int>("router",
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
                        sp =>
                        new NumberTransformer(
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
                },
                options =>
                {
                    options.RouteCache = memoryCache;
                    options.RouteExpiration = TimeSpan.FromMinutes(5);
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


}




public interface IScopedProcessor : IDisposable
{
    void ProcessItem(int item, string routeKey);
    bool WasDisposed { get; }
    IReadOnlyList<int> ProcessedItems { get; }
}

public class ScopedProcessor : IScopedProcessor
{
    private readonly List<int> _processedItems = new();
    private readonly string _instanceId = Guid.NewGuid().ToString();
    private readonly ScopeTracker _tracker;

    public ScopedProcessor(ScopeTracker tracker)
    {
        _tracker = tracker;
        _tracker.RegisterProcessor(this);
    }

    public void ProcessItem(int item, string routeKey)
    {
        if (WasDisposed)
        {
            throw new ObjectDisposedException(_instanceId);
        }

        _processedItems.Add(item);
        _tracker.TrackProcessing(_instanceId, routeKey, this);
    }

    public bool WasDisposed { get; private set; }
    public IReadOnlyList<int> ProcessedItems => _processedItems;

    public void Dispose()
    {
        if (!WasDisposed)
        {
            WasDisposed = true;
            _tracker.TrackDisposal(_instanceId);
        }
    }
}

public class ScopeTracker
{
    private readonly ConcurrentDictionary<string, IScopedProcessor> _routeProcessors = new();
    private readonly ConcurrentDictionary<string, string> _processorRoutes = new();
    private readonly ConcurrentDictionary<string, bool> _disposedProcessors = new();

    public void RegisterProcessor(IScopedProcessor processor)
    {
        // Initial registration
    }

    public void TrackProcessing(string processorId, string routeKey, IScopedProcessor processor)
    {
        _routeProcessors.TryAdd(routeKey, processor);
        _processorRoutes.TryAdd(processorId, routeKey);
    }

    public void TrackDisposal(string processorId)
    {
        _disposedProcessors.TryAdd(processorId, true);
    }

    public IScopedProcessor GetProcessorForRoute(string routeKey)
    {
        return _routeProcessors.TryGetValue(routeKey, out var processor) ? processor : null;
    }
}
