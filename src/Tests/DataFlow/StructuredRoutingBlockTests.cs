namespace Tests.DataFlow;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Uniun.DataFlow.Builder.Graph;
using Xunit.Abstractions;

/// <summary>
/// Tests for the structured routing block with static and dynamic routing.
/// </summary>
[IntegrationTest]
public class StructuredRoutingBlockTests
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceCollection _services;

    public StructuredRoutingBlockTests(ITestOutputHelper output)
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

    #region Static Routing Tests

    [Fact]
    public async Task StaticRouting_Routes_Items_To_PreRegistered_Routes()
    {
        // Arrange
        var processedItems = new ConcurrentDictionary<string, List<int>>();
        var items = Enumerable.Range(1, 10).ToArray();

        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "StaticRoutingTest");
        var logger = sp.GetRequiredService<ILogger<StructuredRoutingBlockTests>>();

        // Act - Add producer and routing block with static routes
        builder.AddProducer("source", sp => new TestProducer<int>(items));
        
        builder.AddRouter<int>("router",
                item => item % 2 == 0 ? "even" : "odd")
            .RegisterRoute("even", context =>
            {
                logger.LogInformation("Building route for: {RouteName}", context.RouteName);
                
                var routeBuilder = context.RouteBuilder;
                routeBuilder.AddProcessor("processor", sp => new TestProcessor<int>(
                    onProcessItem: number =>
                    {
                        logger.LogInformation("Processing {Number} on route {Route}", number, context.RouteName);
                        processedItems.AddOrUpdate(
                            context.RouteName,
                            key => new List<int> { number },
                            (key, list) =>
                            {
                                lock (list) { list.Add(number); return list; }
                            });
                    }))
                    .AsEntry();

                return routeBuilder.Build();
            })
            .RegisterRoute("odd", context =>
            {
                logger.LogInformation("Building route for: {RouteName}", context.RouteName);
                
                var routeBuilder = context.RouteBuilder;
                routeBuilder.AddProcessor("processor", sp => new TestProcessor<int>(
                    onProcessItem: number =>
                    {
                        logger.LogInformation("Processing {Number} on route {Route}", number, context.RouteName);
                        processedItems.AddOrUpdate(
                            context.RouteName,
                            key => new List<int> { number },
                            (key, list) =>
                            {
                                lock (list) { list.Add(number); return list; }
                            });
                    }))
                    .AsEntry();

                return routeBuilder.Build();
            })
            .ReceiveFrom("source");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context);

        // Assert
        var evenItems = processedItems.GetValueOrDefault("even", new List<int>());
        var oddItems = processedItems.GetValueOrDefault("odd", new List<int>());

        logger.LogInformation("Even items: {Items}", string.Join(",", evenItems));
        logger.LogInformation("Odd items: {Items}", string.Join(",", oddItems));

        evenItems.OrderBy(x => x).ShouldBe(new[] { 2, 4, 6, 8, 10 });
        oddItems.OrderBy(x => x).ShouldBe(new[] { 1, 3, 5, 7, 9 });
    }

    [Fact]
    public async Task StaticRouting_ThrowsException_When_RouteNotFound()
    {
        // Arrange
        var items = new[] { 1, 2, 3 };
        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "StaticRoutingMissingRouteTest");

        // Act - Only register "even" route, but data will have "odd" items too
        builder.AddProducer("source", sp => new TestProducer<int>(items));
        
        builder.AddRouter<int>("router", item => item % 2 == 0 ? "even" : "odd")
            .RegisterRoute("even", context =>
            {
                var routeBuilder = context.RouteBuilder;
                routeBuilder.AddProcessor("processor", sp => new TestProcessor<int>())
                    .AsEntry();
                return routeBuilder.Build();
            })
            .ReceiveFrom("source");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);

        // Assert - Should throw when trying to route to non-existent "odd" route
        var ex = await Should.ThrowAsync<InvalidOperationException>(
            async () => await flow.ExecuteAsync(context));
        
        ex.Message.ShouldContain("odd");
        ex.Message.ShouldContain("not found");
    }

    [Fact]
    public async Task StaticRouting_Routes_To_Complex_DataFlow_With_Multiple_Blocks()
    {
        // Arrange
        var processedBatches = new ConcurrentBag<(string Route, int[] Batch)>();
        var items = Enumerable.Range(1, 20).ToArray();

        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "ComplexRouteTest");
        var logger = sp.GetRequiredService<ILogger<StructuredRoutingBlockTests>>();

        // Act - Create routes with transform and batch blocks
        builder.AddProducer("source", sp => new TestProducer<int>(items));
        
        builder.AddRouter<int>("router", item => item % 2 == 0 ? "even" : "odd")
            .RegisterRoute("even", context =>
            {
                var routeBuilder = context.RouteBuilder;
                
                // Build a route with transform -> batch -> processor
                routeBuilder.AddTransform("transform", sp => new NumberTransformer("num"))
                    .AsEntry()
                    .AddBatch("batcher", maxBatchSize: 3, windowPeriod: TimeSpan.FromSeconds(1))
                    .AddProcessor("processor", sp => new TestProcessor<string[]>(
                        onProcessItem: batch =>
                        {
                            var numbers = batch.Select(s => int.Parse(s.Split('-')[1])).ToArray();
                            processedBatches.Add((context.RouteName, numbers));
                            logger.LogInformation("Processed batch on {Route}: [{Items}]",
                                context.RouteName, string.Join(", ", numbers));
                        }));

                return routeBuilder.Build();
            })
            .RegisterRoute("odd", context =>
            {
                var routeBuilder = context.RouteBuilder;
                
                routeBuilder.AddTransform("transform", sp => new NumberTransformer("num"))
                    .AsEntry()
                    .AddBatch("batcher", maxBatchSize: 3, windowPeriod: TimeSpan.FromSeconds(1))
                    .AddProcessor("processor", sp => new TestProcessor<string[]>(
                        onProcessItem: batch =>
                        {
                            var numbers = batch.Select(s => int.Parse(s.Split('-')[1])).ToArray();
                            processedBatches.Add((context.RouteName, numbers));
                            logger.LogInformation("Processed batch on {Route}: [{Items}]",
                                context.RouteName, string.Join(", ", numbers));
                        }));

                return routeBuilder.Build();
            })
            .ReceiveFrom("source");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context);

        // Assert
        var evenBatches = processedBatches.Where(b => b.Route == "even").SelectMany(b => b.Batch).ToList();
        var oddBatches = processedBatches.Where(b => b.Route == "odd").SelectMany(b => b.Batch).ToList();

        evenBatches.OrderBy(x => x).ShouldBe(new[] { 2, 4, 6, 8, 10, 12, 14, 16, 18, 20 });
        oddBatches.OrderBy(x => x).ShouldBe(new[] { 1, 3, 5, 7, 9, 11, 13, 15, 17, 19 });
    }

    #endregion

    #region Dynamic Routing Tests

    [Fact]
    public async Task DynamicRouting_Creates_Routes_OnDemand_Using_Template()
    {
        // Arrange
        var processedItems = new ConcurrentDictionary<string, List<string>>();
        var items = new[] 
        { 
            new RoutingTestItem { Category = "A", Value = "Item1" },
            new RoutingTestItem { Category = "B", Value = "Item2" },
            new RoutingTestItem { Category = "A", Value = "Item3" },
            new RoutingTestItem { Category = "C", Value = "Item4" },
            new RoutingTestItem { Category = "B", Value = "Item5" }
        };

        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "DynamicRoutingTest");
        var logger = sp.GetRequiredService<ILogger<StructuredRoutingBlockTests>>();

        // Act - Register a template route and enable dynamic routing
        builder.AddProducer("source", sp => new TestProducer<RoutingTestItem>(items));
        
        builder.AddRouter<RoutingTestItem>("router", item => item.Category)
            .RegisterRoute("template", context =>
            {
                logger.LogInformation("Building dynamic route for: {RouteName} using template", context.RouteName);
                
                var routeBuilder = context.RouteBuilder;
                routeBuilder.AddProcessor("processor", sp => new TestProcessor<RoutingTestItem>(
                    onProcessItem: item =>
                    {
                        logger.LogInformation("Processing {Value} on route {Route}", item.Value, context.RouteName);
                        processedItems.AddOrUpdate(
                            context.RouteName,
                            key => new List<string> { item.Value },
                            (key, list) =>
                            {
                                lock (list) { list.Add(item.Value); return list; }
                            });
                    }))
                    .AsEntry();

                return routeBuilder.Build();
            })
            .WithDynamicRouting("template")
            .ReceiveFrom("source");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context);

        // Assert - Routes for A, B, and C should be created dynamically
        processedItems.Keys.ShouldBe(new[] { "A", "B", "C" }, ignoreOrder: true);
        processedItems["A"].ShouldBe(new[] { "Item1", "Item3" }, ignoreOrder: true);
        processedItems["B"].ShouldBe(new[] { "Item2", "Item5" }, ignoreOrder: true);
        processedItems["C"].ShouldBe(new[] { "Item4" });
    }

    [Fact]
    public async Task DynamicRouting_RespectsDynamicRouteLimit()
    {
        // Arrange
        var items = Enumerable.Range(1, 100).Select(i => new RoutingTestItem 
        { 
            Category = $"Cat{i}", 
            Value = $"Item{i}" 
        }).ToArray();

        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "DynamicRouteLimitTest");

        // Act - Set max dynamic routes to 5
        builder.AddProducer("source", sp => new TestProducer<RoutingTestItem>(items));
        
        builder.AddRouter<RoutingTestItem>("router", item => item.Category)
            .RegisterRoute("template", context =>
            {
                var routeBuilder = context.RouteBuilder;
                routeBuilder.AddProcessor("processor", sp => new TestProcessor<RoutingTestItem>())
                    .AsEntry();
                return routeBuilder.Build();
            })
            .WithDynamicRouting("template", maxDynamicRoutes: 5)
            .ReceiveFrom("source");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);

        // Assert - Should throw when limit is exceeded
        var ex = await Should.ThrowAsync<InvalidOperationException>(
            async () => await flow.ExecuteAsync(context));
        
        ex.Message.ShouldContain("Maximum dynamic routes limit");
        ex.Message.ShouldContain("5");
    }

    [Fact]
    public async Task DynamicRouting_ThrowsException_When_TemplateNotFound()
    {
        // Arrange
        var items = new[] { new RoutingTestItem { Category = "A", Value = "Item1" } };
        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "MissingTemplateTest");

        // Act & Assert - Try to enable dynamic routing without registering template first
        var ex = Should.Throw<InvalidOperationException>(() =>
        {
            builder.AddProducer("source", sp => new TestProducer<RoutingTestItem>(items));
        
        builder.AddRouter<RoutingTestItem>("router", item => item.Category)
                .WithDynamicRouting("nonexistent");
        });

        ex.Message.ShouldContain("nonexistent");
        ex.Message.ShouldContain("must be registered");
    }

    [Fact]
    public async Task DynamicRouting_Uses_TriggeringItem_In_RouteContext()
    {
        // Arrange
        var firstItemPerRoute = new ConcurrentDictionary<string, RoutingTestItem>();
        var items = new[]
        {
            new RoutingTestItem { Category = "A", Value = "FirstA" },
            new RoutingTestItem { Category = "A", Value = "SecondA" },
            new RoutingTestItem { Category = "B", Value = "FirstB" }
        };

        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "TriggeringItemTest");

        // Act - Capture the triggering item in the route factory
        builder.AddProducer("source", sp => new TestProducer<RoutingTestItem>(items));
        
        builder.AddRouter<RoutingTestItem>("router", item => item.Category)
            .RegisterRoute("template", context =>
            {
                // The triggering item should be the first item that causes the route to be created
                if (context.TriggeringItem is RoutingTestItem triggeringItem)
                {
                    firstItemPerRoute[context.RouteName] = triggeringItem;
                }

                var routeBuilder = context.RouteBuilder;
                routeBuilder.AddProcessor("processor", sp => new TestProcessor<RoutingTestItem>())
                    .AsEntry();
                return routeBuilder.Build();
            })
            .WithDynamicRouting("template")
            .ReceiveFrom("source");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context);

        // Assert - First items that triggered route creation should be captured
        firstItemPerRoute["A"].Value.ShouldBe("FirstA");
        firstItemPerRoute["B"].Value.ShouldBe("FirstB");
    }

    #endregion

    #region Route Entry Block Tests

    [Fact]
    public async Task RouteBuilder_AutomaticallySelectsFirstTargetBlock_AsEntryBlock()
    {
        // Arrange
        var items = new[] { 1 };
        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "AutoEntryBlockTest");

        // Act - Register a route WITHOUT calling AsEntry()
        // The first target block should automatically become the entry block
        builder.AddProducer("source", sp => new TestProducer<int>(items));
        
        builder.AddRouter<int>("router", item => "route1")
            .RegisterRoute("route1", context =>
            {
                var routeBuilder = context.RouteBuilder;
                routeBuilder.AddProcessor("processor", sp => new TestProcessor<int>());
                // No need to call .AsEntry() - first target block is automatically the entry
                return routeBuilder.Build();
            })
            .ReceiveFrom("source");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);

        // Assert - Should execute successfully with automatic entry block
        await flow.ExecuteAsync(context);
    }

    [Fact]
    public async Task RouteBuilder_ValidatesEntryBlock_IsTargetBlock()
    {
        // This test verifies that the entry block must be a target block.
        // In practice, all our blocks (Processor, Transform, Batch) implement ITargetBlock,
        // so this is mainly documenting the requirement.
        
        // Arrange
        var items = new[] { 1 };
        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "EntryBlockValidationTest");

        // Act - Build a route with a processor as entry (which is valid)
        builder.AddProducer("source", sp => new TestProducer<int>(items));
        
        builder.AddRouter<int>("router", item => "route1")
            .RegisterRoute("route1", context =>
            {
                var routeBuilder = context.RouteBuilder;
                routeBuilder.AddProcessor("processor", sp => new TestProcessor<int>())
                    .AsEntry();
                return routeBuilder.Build();
            })
            .ReceiveFrom("source");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);

        // Assert - Should execute successfully
        await flow.ExecuteAsync(context);
    }

    #endregion

    #region Concurrent Processing Tests

    [Fact]
    public async Task StaticRouting_HandlesMultipleItemsConcurrently()
    {
        // Arrange
        var processedItems = new ConcurrentBag<int>();
        var itemCount = 100;
        var items = Enumerable.Range(1, itemCount).ToArray();

        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "ConcurrentRoutingTest");

        // Act - Configure router with higher concurrency
        builder.AddProducer("source", sp => new TestProducer<int>(items));
        
        builder.AddRouter<int>("router", item => item % 3 == 0 ? "div3" : item % 2 == 0 ? "even" : "odd",
                options => options.MaxConcurrency = 5)
            .RegisterRoute("div3", context =>
            {
                var routeBuilder = context.RouteBuilder;
                routeBuilder.AddProcessor("processor", sp => new TestProcessor<int>(
                    onProcessItem: item => processedItems.Add(item)))
                    .AsEntry();
                return routeBuilder.Build();
            })
            .RegisterRoute("even", context =>
            {
                var routeBuilder = context.RouteBuilder;
                routeBuilder.AddProcessor("processor", sp => new TestProcessor<int>(
                    onProcessItem: item => processedItems.Add(item)))
                    .AsEntry();
                return routeBuilder.Build();
            })
            .RegisterRoute("odd", context =>
            {
                var routeBuilder = context.RouteBuilder;
                routeBuilder.AddProcessor("processor", sp => new TestProcessor<int>(
                    onProcessItem: item => processedItems.Add(item)))
                    .AsEntry();
                return routeBuilder.Build();
            })
            .ReceiveFrom("source");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context);

        // Assert - All items should be processed
        processedItems.Count.ShouldBe(itemCount);
        processedItems.OrderBy(x => x).ShouldBe(items);
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public async Task RouteCreation_DisposesScope_WhenFactoryThrowsException()
    {
        // Arrange
        var items = new[] { 1, 2 };
        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "RouteExceptionTest");
        var logger = sp.GetRequiredService<ILogger<StructuredRoutingBlockTests>>();

        // Act - Register a route that throws during creation
        builder.AddProducer("source", sp => new TestProducer<int>(items));
        
        builder.AddRouter<int>("router", item => item % 2 == 0 ? "even" : "odd")
            .RegisterRoute("even", context =>
            {
                // This route will throw during creation
                throw new InvalidOperationException("Simulated route creation error");
            })
            .RegisterRoute("odd", context =>
            {
                var routeBuilder = context.RouteBuilder;
                routeBuilder.AddProcessor("processor", sp => new TestProcessor<int>());
                return routeBuilder.Build();
            })
            .ReceiveFrom("source");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);

        // Assert - Should throw when trying to create "even" route
        var ex = await Should.ThrowAsync<InvalidOperationException>(
            async () => await flow.ExecuteAsync(context));
        
        ex.Message.ShouldContain("Simulated route creation error");
        
        // The scope should have been disposed (we can't directly verify this, 
        // but the test ensures the exception is properly propagated)
    }

    #endregion
}

// Test helper class
public class RoutingTestItem
{
    public string Category { get; set; } = "";
    public string Value { get; set; } = "";
}
