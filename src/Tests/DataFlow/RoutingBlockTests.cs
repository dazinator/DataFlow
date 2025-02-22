namespace Tests.DataFlow;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

    public void AddDefaultServices(IServiceCollection services)
    {
        Services.AddLogging(builder => builder.AddXUnit(Output));
        Services.AddDataFlows(maxConcurrentFlows: 1);

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
                    logger.LogInformation("Creating processor for route {RouteKey}", context.RoutingKey);
                    var routeBuilder = new DataFlowBuilder(context.ServiceProvider);

                    return routeBuilder.AddProcessor("processor", sp => new TestProcessor<int>(
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
                        }))
                        .Current;
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


}
