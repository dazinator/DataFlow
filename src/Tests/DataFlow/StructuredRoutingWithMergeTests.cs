namespace Tests.DataFlow;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Uniun.DataFlow;
using Uniun.DataFlow.Builder.Graph;
using Xunit.Abstractions;

/// <summary>
/// Tests for routing blocks with merge functionality using BufferBlock.
/// Demonstrates the new unified routing mechanism where routes can be merged.
/// </summary>
[IntegrationTest]
public class StructuredRoutingWithMergeTests
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceCollection _services;

    public StructuredRoutingWithMergeTests(ITestOutputHelper output)
    {
        _output = output;
        _services = new ServiceCollection();
        AddDefaultServices(_services);
    }

    private void AddDefaultServices(IServiceCollection services)
    {
        services.AddLogging(builder => builder.AddXUnit(_output));
        services.AddDataFlows();
        services.AddMetrics();
        services.AddDataFlowMetrics();
    }

    private IDataFlowContext CreateContext(string name, Guid guid, IServiceProvider provider, CancellationToken ct = default)
    {
        return DataFlowContextTestUtils.GetContext(name, guid, provider, ct);
    }

    [Fact]
    public void RoutingBlock_CanBe_ConfiguredWithMergeOption()
    {
        // Arrange
        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "MergeOptionTest");

        // Act - Build a routing block with merge configured
        builder.AddProducer("source", sp => new TestProducer<int>(new[] { 1, 2, 3 }));

        builder.AddRouter<int>("router", item => "route1")
            .RegisterRoute("route1", context =>
            {
                var routeBuilder = context.RouteBuilder;
                routeBuilder.AddProcessor<int>("proc", sp => new TestProcessor<int>())
                    .AsEntry();
                return routeBuilder;
            })
            .MergeInto("merger") // Configure merge target
            .ReceiveFrom("source");

        builder.AddBuffer<int>("merger");
        builder.AddProcessor<int>("final", sp => new TestProcessor<int>())
            .ReceiveFrom("merger");

        // Assert - Should build without errors
        var flow = builder.Build();
        flow.ShouldNotBeNull();
    }

    [Fact]
    public void RouteContext_HasAccessTo_ParentGraph()
    {
        // Arrange
        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "GraphAccessTest");
        DataFlowGraph? capturedGraph = null;

        // Act - Create a route that captures the parent graph
        builder.AddProducer("source", sp => new TestProducer<int>(new[] { 1 }));

        builder.AddRouter<int>("router", item => "route1")
            .RegisterRoute("route1", context =>
            {
                capturedGraph = context.ParentGraph;
                var routeBuilder = context.RouteBuilder;
                routeBuilder.AddProcessor<int>("proc", sp => new TestProcessor<int>())
                    .AsEntry();
                return routeBuilder;
            })
            .ReceiveFrom("source");

        var flow = builder.Build();

        // Assert - Parent graph should be available in route context
        // Note: The route factory isn't called until runtime, so we can't test this synchronously
        // This test just verifies the structure compiles
        flow.ShouldNotBeNull();
    }

    [Fact(Skip = "Merge functionality not yet implemented - test demonstrates intended API")]
    public async Task StaticRouting_CanMerge_RoutesIntoDownstreamBuffer()
    {
        // Arrange
        var processedItems = new ConcurrentBag<int>();
        var items = Enumerable.Range(1, 20).ToArray();

        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "RoutingWithMergeTest");
        var logger = sp.GetRequiredService<ILogger<StructuredRoutingWithMergeTests>>();

        // Act - Add producer
        builder.AddProducer("source", sp => new TestProducer<int>(items));

        // Add routing block with static routes
        builder.AddRouter<int>("router", item => item % 2 == 0 ? "even" : "odd")
            .RegisterRoute("even", context =>
            {
                logger.LogInformation("Building route for: {RouteName}", context.RouteName);
                var routeBuilder = context.RouteBuilder;
                
                // Process even numbers - multiply by 10
                routeBuilder.AddTransform<int, int>("even-processor", sp => 
                    new SimpleTestTransformer<int, int>(x => x * 10))
                    .AsEntry();

                return routeBuilder;
            })
            .RegisterRoute("odd", context =>
            {
                logger.LogInformation("Building route for: {RouteName}", context.RouteName);
                var routeBuilder = context.RouteBuilder;
                
                // Process odd numbers - add 1000
                routeBuilder.AddTransform<int, int>("odd-processor", sp => 
                    new SimpleTestTransformer<int, int>(x => x + 1000))
                    .AsEntry();

                return routeBuilder;
            })
            .MergeInto("merger")
            .ReceiveFrom("source");

        // Add buffer block to merge all route outputs
        builder.AddBuffer<int>("merger");

        // Add final processor to collect all merged results
        builder.AddProcessor<int>("collector", sp =>
            new TestProcessor<int>(onProcessItem: item =>
            {
                logger.LogInformation("Collected merged item: {Item}", item);
                processedItems.Add(item);
            }))
            .ReceiveFrom("merger");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context);

        // Assert - All items should be processed and merged
        var sortedItems = processedItems.OrderBy(x => x).ToList();
        logger.LogInformation("Merged items: {Items}", string.Join(", ", sortedItems));

        // Even numbers: 2*10=20, 4*10=40, 6*10=60, 8*10=80, 10*10=100, ...
        // Odd numbers: 1+1000=1001, 3+1000=1003, 5+1000=1005, ...
        var expectedEvens = items.Where(x => x % 2 == 0).Select(x => x * 10).ToList();
        var expectedOdds = items.Where(x => x % 2 != 0).Select(x => x + 1000).ToList();
        var expectedAll = expectedEvens.Concat(expectedOdds).OrderBy(x => x).ToList();

        sortedItems.ShouldBe(expectedAll);
        processedItems.Count.ShouldBe(items.Length);
    }

    [Fact(Skip = "Merge functionality not yet implemented - test demonstrates intended API")]
    public async Task DynamicRouting_CanMerge_RoutesIntoDownstreamBuffer()
    {
        // Arrange
        var processedItems = new ConcurrentBag<string>();
        var items = new[]
        {
            new RoutingTestItem { Category = "A", Value = "Item1" },
            new RoutingTestItem { Category = "B", Value = "Item2" },
            new RoutingTestItem { Category = "A", Value = "Item3" },
            new RoutingTestItem { Category = "C", Value = "Item4" },
            new RoutingTestItem { Category = "B", Value = "Item5" }
        };

        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "DynamicRoutingMergeTest");
        var logger = sp.GetRequiredService<ILogger<StructuredRoutingWithMergeTests>>();

        // Act - Register a template route and enable dynamic routing
        builder.AddProducer("source", sp => new TestProducer<RoutingTestItem>(items));

        builder.AddRouter<RoutingTestItem>("router", item => item.Category)
            .RegisterRoute("template", context =>
            {
                logger.LogInformation("Building dynamic route for: {RouteName}", context.RouteName);
                var routeBuilder = context.RouteBuilder;
                
                // Transform items by adding route name prefix
                routeBuilder.AddTransform<RoutingTestItem, string>("route-processor", sp =>
                    new SimpleTestTransformer<RoutingTestItem, string>(item => $"[{context.RouteName}] {item.Value}"))
                    .AsEntry();

                return routeBuilder;
            })
            .WithDynamicRouting("template")
            .MergeInto("merger")
            .ReceiveFrom("source");

        // Add buffer block to merge all route outputs
        builder.AddBuffer<string>("merger");

        // Add final processor to collect all merged results
        builder.AddProcessor<string>("collector", sp =>
            new TestProcessor<string>(onProcessItem: result =>
            {
                logger.LogInformation("Collected merged result: {Result}", result);
                processedItems.Add(result);
            }))
            .ReceiveFrom("merger");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context);

        // Assert - All items should be processed and merged with their route prefix
        logger.LogInformation("All merged results: {Results}", string.Join(", ", processedItems.OrderBy(x => x)));

        processedItems.ShouldContain("[A] Item1");
        processedItems.ShouldContain("[A] Item3");
        processedItems.ShouldContain("[B] Item2");
        processedItems.ShouldContain("[B] Item5");
        processedItems.ShouldContain("[C] Item4");
        processedItems.Count.ShouldBe(5);
    }

    [Fact(Skip = "Merge functionality not yet implemented - test demonstrates intended API")]
    public async Task Routing_CanLookup_ExistingBranchFromGraph()
    {
        // Arrange
        var processedItems = new ConcurrentBag<string>();
        var items = Enumerable.Range(1, 10).ToArray();

        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "RoutingWithBranchLookupTest");
        var logger = sp.GetRequiredService<ILogger<StructuredRoutingWithMergeTests>>();

        // Add a pre-defined branch in the main graph
        builder.AddProducer("dummy-source", sp => new TestProducer<int>(new int[0]));
        
        var sharedBranch = builder.AddBranch("shared-processing-branch");
        sharedBranch.AddTransform<int, string>("shared-transformer", sp =>
            new SimpleTestTransformer<int, string>(x => $"Shared: {x}"))
            .ReceiveFrom("dummy-source");

        // Act - Add producer for routing
        builder.AddProducer("source", sp => new TestProducer<int>(items));

        // Add routing block where routes can lookup existing branches
        builder.AddRouter<int>("router", item => item % 2 == 0 ? "even" : "odd")
            .RegisterRoute("even", context =>
            {
                // Create a new route for even numbers
                var routeBuilder = context.RouteBuilder;
                routeBuilder.AddTransform<int, string>("even-processor", sp =>
                    new SimpleTestTransformer<int, string>(x => $"Even: {x}"))
                    .AsEntry();
                return routeBuilder;
            })
            .RegisterRoute("odd", context =>
            {
                // Demonstrate that routes have access to the parent graph
                // The parent graph contains branch and route definitions that could be looked up
                logger.LogInformation("Route has access to parent graph with {BlockCount} blocks and {RouteCount} routes",
                    context.ParentGraph.BlockDefinitions.Count,
                    context.ParentGraph.RouteDefinitions.Count);

                var routeBuilder = context.RouteBuilder;
                routeBuilder.AddTransform<int, string>("odd-processor", sp =>
                    new SimpleTestTransformer<int, string>(x => $"Odd: {x}"))
                    .AsEntry();
                return routeBuilder;
            })
            .MergeInto("merger")
            .ReceiveFrom("source");

        // Add buffer block to merge all route outputs
        builder.AddBuffer<string>("merger");

        // Add final processor
        builder.AddProcessor<string>("collector", sp =>
            new TestProcessor<string>(onProcessItem: result => processedItems.Add(result)))
            .ReceiveFrom("merger");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context);

        // Assert - Verify all items were processed
        processedItems.Count.ShouldBe(10);
        processedItems.Where(x => x.StartsWith("Even:")).Count().ShouldBe(5);
        processedItems.Where(x => x.StartsWith("Odd:")).Count().ShouldBe(5);
    }
}

/// <summary>
/// Simple test helper for transforming items inline
/// </summary>
internal class SimpleTestTransformer<TIn, TOut> : IStreamTransformer<TIn, TOut>
{
    private readonly Func<TIn, TOut> _transform;

    public SimpleTestTransformer(Func<TIn, TOut> transform)
    {
        _transform = transform;
    }

    public async IAsyncEnumerable<TOut> TransformAsync(
        IDataFlowContext context,
        IAsyncEnumerable<TIn> input,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in input.WithCancellation(cancellationToken))
        {
            yield return _transform(item);
        }
    }
}
