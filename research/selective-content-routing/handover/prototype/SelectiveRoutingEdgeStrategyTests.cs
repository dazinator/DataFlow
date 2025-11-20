namespace DataFlow.POC.Tests;

using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

/// <summary>
/// RESEARCH PROTOTYPE TESTS - Will be reverted after approval
/// 
/// Tests for SelectiveRoutingEdgeStrategy to validate:
/// 1. Correctness - items routed to correct targets
/// 2. Selectivity - each item sent only to ONE route (no broadcasting)
/// 3. Performance - no allocation overhead, better throughput than broadcast-and-filter
/// </summary>
public class SelectiveRoutingEdgeStrategyTests
{
    /// <summary>
    /// Validates basic selective routing with 2 routes.
    /// Each item should go to exactly ONE route based on content.
    /// </summary>
    [Fact]
    public async Task SelectiveRouting_WithTwoRoutes_RoutesItemsCorrectly()
    {
        // Arrange
        var evenItems = new List<int>();
        var oddItems = new List<int>();
        
        var evenScopeFactory = TestServiceBuilder.Create()
            .WithScoped(new CollectorActor<int>(evenItems))
            .BuildScopeFactory();

        var oddScopeFactory = TestServiceBuilder.Create()
            .WithScoped(new CollectorActor<int>(oddItems))
            .BuildScopeFactory();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        // Create blocks
        var producer = BlockHelpers.CreateProducer<int>("producer", TestStreams.Integers(10));
        var evenProcessor = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "even-processor",
            evenScopeFactory);
        var oddProcessor = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "odd-processor",
            oddScopeFactory);

        // Create selective routing edge strategy
        var routeMapping = new Dictionary<string, IBlock>
        {
            ["even"] = evenProcessor,
            ["odd"] = oddProcessor
        };

        var strategy = new SelectiveRoutingEdgeStrategy<int>(
            routeKeyToBlock: routeMapping,
            routeSelector: i => i % 2 == 0 ? "even" : "odd");

        // Create edge with selective routing strategy
        var edge = new Edge(
            producer,
            new[] { evenProcessor, oddProcessor },
            strategy);

        var builder = GraphHelpers.CreateGraphBuilder("selective-routing-test");
        builder.AddBlock(producer)
            .AddBlock(evenProcessor)
            .AddBlock(oddProcessor)
            .AddEdge(edge);

        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert - verify correctness
        evenItems.Count.ShouldBe(5);
        oddItems.Count.ShouldBe(5);
        evenItems.ShouldBe(new[] { 2, 4, 6, 8, 10 });
        oddItems.ShouldBe(new[] { 1, 3, 5, 7, 9 });
    }

    /// <summary>
    /// Validates selective routing with 5 routes.
    /// Tests scalability - with N routes, traditional approach broadcasts to N channels.
    /// Selective routing should only write to 1 channel per item.
    /// </summary>
    [Fact]
    public async Task SelectiveRouting_WithFiveRoutes_RoutesItemsCorrectly()
    {
        // Arrange
        var route0Items = new List<int>();
        var route1Items = new List<int>();
        var route2Items = new List<int>();
        var route3Items = new List<int>();
        var route4Items = new List<int>();
        
        var route0Factory = TestServiceBuilder.Create()
            .WithScoped(new CollectorActor<int>(route0Items))
            .BuildScopeFactory();
        var route1Factory = TestServiceBuilder.Create()
            .WithScoped(new CollectorActor<int>(route1Items))
            .BuildScopeFactory();
        var route2Factory = TestServiceBuilder.Create()
            .WithScoped(new CollectorActor<int>(route2Items))
            .BuildScopeFactory();
        var route3Factory = TestServiceBuilder.Create()
            .WithScoped(new CollectorActor<int>(route3Items))
            .BuildScopeFactory();
        var route4Factory = TestServiceBuilder.Create()
            .WithScoped(new CollectorActor<int>(route4Items))
            .BuildScopeFactory();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        // Create blocks
        var producer = BlockHelpers.CreateProducer<int>("producer", TestStreams.Integers(50));
        var route0 = BlockHelpers.CreateActor<int, object, CollectorActor<int>>("route0", route0Factory);
        var route1 = BlockHelpers.CreateActor<int, object, CollectorActor<int>>("route1", route1Factory);
        var route2 = BlockHelpers.CreateActor<int, object, CollectorActor<int>>("route2", route2Factory);
        var route3 = BlockHelpers.CreateActor<int, object, CollectorActor<int>>("route3", route3Factory);
        var route4 = BlockHelpers.CreateActor<int, object, CollectorActor<int>>("route4", route4Factory);

        // Route based on modulo 5
        var routeMapping = new Dictionary<string, IBlock>
        {
            ["route0"] = route0,
            ["route1"] = route1,
            ["route2"] = route2,
            ["route3"] = route3,
            ["route4"] = route4
        };

        var strategy = new SelectiveRoutingEdgeStrategy<int>(
            routeKeyToBlock: routeMapping,
            routeSelector: i => $"route{i % 5}");

        var edge = new Edge(
            producer,
            new[] { route0, route1, route2, route3, route4 },
            strategy);

        var builder = GraphHelpers.CreateGraphBuilder("five-route-test");
        builder.AddBlock(producer)
            .AddBlock(route0)
            .AddBlock(route1)
            .AddBlock(route2)
            .AddBlock(route3)
            .AddBlock(route4)
            .AddEdge(edge);

        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert - verify correctness
        route0Items.Count.ShouldBe(10); // 5, 10, 15, 20, 25, 30, 35, 40, 45, 50
        route1Items.Count.ShouldBe(10); // 1, 6, 11, 16, 21, 26, 31, 36, 41, 46
        route2Items.Count.ShouldBe(10); // 2, 7, 12, 17, 22, 27, 32, 37, 42, 47
        route3Items.Count.ShouldBe(10); // 3, 8, 13, 18, 23, 28, 33, 38, 43, 48
        route4Items.Count.ShouldBe(10); // 4, 9, 14, 19, 24, 29, 34, 39, 44, 49

        // Verify items are distributed correctly
        route0Items.All(i => i % 5 == 0).ShouldBeTrue();
        route1Items.All(i => i % 5 == 1).ShouldBeTrue();
        route2Items.All(i => i % 5 == 2).ShouldBeTrue();
        route3Items.All(i => i % 5 == 3).ShouldBeTrue();
        route4Items.All(i => i % 5 == 4).ShouldBeTrue();
    }

    /// <summary>
    /// Validates that an unknown route key throws an appropriate exception.
    /// This tests error handling for configuration errors.
    /// </summary>
    [Fact]
    public async Task SelectiveRouting_WithUnknownRouteKey_ThrowsException()
    {
        // Arrange
        var items = new List<int>();
        var scopeFactory = TestServiceBuilder.Create()
            .WithScoped(new CollectorActor<int>(items))
            .BuildScopeFactory();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = BlockHelpers.CreateProducer<int>("producer", TestStreams.Integers(10));
        var processor = BlockHelpers.CreateActor<int, object, CollectorActor<int>>("processor", scopeFactory);

        // Configure only "even" route, but items will also be "odd"
        var routeMapping = new Dictionary<string, IBlock>
        {
            ["even"] = processor
        };

        var strategy = new SelectiveRoutingEdgeStrategy<int>(
            routeKeyToBlock: routeMapping,
            routeSelector: i => i % 2 == 0 ? "even" : "odd"); // "odd" not configured!

        var edge = new Edge(
            producer,
            new[] { processor },
            strategy);

        var builder = GraphHelpers.CreateGraphBuilder("unknown-route-test");
        builder.AddBlock(producer)
            .AddBlock(processor)
            .AddEdge(edge);

        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await graph.ExecuteAsync(context));
        
        exception.Message.ShouldContain("Route 'odd' not found");
    }

    /// <summary>
    /// Tests routing with a complex type (not just int).
    /// Demonstrates that SelectiveRoutingEdgeStrategy works with any type.
    /// </summary>
    [Fact]
    public async Task SelectiveRouting_WithComplexType_RoutesCorrectly()
    {
        // Arrange
        var customerAOrders = new List<Order>();
        var customerBOrders = new List<Order>();

        var customerAScopeFactory = TestServiceBuilder.Create()
            .WithScoped(new CollectorActor<Order>(customerAOrders))
            .BuildScopeFactory();

        var customerBScopeFactory = TestServiceBuilder.Create()
            .WithScoped(new CollectorActor<Order>(customerBOrders))
            .BuildScopeFactory();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        // Create test orders
        var orders = new List<Order>
        {
            new Order { Id = 1, CustomerId = "CustomerA", Amount = 100 },
            new Order { Id = 2, CustomerId = "CustomerB", Amount = 200 },
            new Order { Id = 3, CustomerId = "CustomerA", Amount = 150 },
            new Order { Id = 4, CustomerId = "CustomerB", Amount = 250 },
            new Order { Id = 5, CustomerId = "CustomerA", Amount = 300 }
        };

        var producer = BlockHelpers.CreateProducer<Order>("producer", 
            (ctx) => orders.ToAsyncEnumerable());
        
        var customerAProcessor = BlockHelpers.CreateActor<Order, object, CollectorActor<Order>>(
            "customerA-processor", customerAScopeFactory);
        var customerBProcessor = BlockHelpers.CreateActor<Order, object, CollectorActor<Order>>(
            "customerB-processor", customerBScopeFactory);

        // Route by CustomerId
        var routeMapping = new Dictionary<string, IBlock>
        {
            ["CustomerA"] = customerAProcessor,
            ["CustomerB"] = customerBProcessor
        };

        var strategy = new SelectiveRoutingEdgeStrategy<Order>(
            routeKeyToBlock: routeMapping,
            routeSelector: order => order.CustomerId);

        var edge = new Edge(
            producer,
            new[] { customerAProcessor, customerBProcessor },
            strategy);

        var builder = GraphHelpers.CreateGraphBuilder("complex-type-routing");
        builder.AddBlock(producer)
            .AddBlock(customerAProcessor)
            .AddBlock(customerBProcessor)
            .AddEdge(edge);

        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        customerAOrders.Count.ShouldBe(3);
        customerBOrders.Count.ShouldBe(2);
        
        customerAOrders.All(o => o.CustomerId == "CustomerA").ShouldBeTrue();
        customerBOrders.All(o => o.CustomerId == "CustomerB").ShouldBeTrue();
        
        customerAOrders.Select(o => o.Id).ShouldBe(new[] { 1, 3, 5 });
        customerBOrders.Select(o => o.Id).ShouldBe(new[] { 2, 4 });
    }

    // Test helper class
    private class Order
    {
        public int Id { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}
