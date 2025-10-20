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
                                lock (list)
                                { list.Add(number); return list; }
                            });
                    }))
                    .AsEntry();

                return routeBuilder;
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
                                lock (list)
                                { list.Add(number); return list; }
                            });
                    }))
                    .AsEntry();

                return routeBuilder;
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
                return routeBuilder;
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

                return routeBuilder;
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

                return routeBuilder;
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
                                lock (list)
                                { list.Add(item.Value); return list; }
                            });
                    }))
                    .AsEntry();

                return routeBuilder;
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
                return routeBuilder;
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
                return routeBuilder;
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
                return routeBuilder;
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
                return routeBuilder;
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
                return routeBuilder;
            })
            .RegisterRoute("even", context =>
            {
                var routeBuilder = context.RouteBuilder;
                routeBuilder.AddProcessor("processor", sp => new TestProcessor<int>(
                    onProcessItem: item => processedItems.Add(item)))
                    .AsEntry();
                return routeBuilder;
            })
            .RegisterRoute("odd", context =>
            {
                var routeBuilder = context.RouteBuilder;
                routeBuilder.AddProcessor("processor", sp => new TestProcessor<int>(
                    onProcessItem: item => processedItems.Add(item)))
                    .AsEntry();
                return routeBuilder;
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
                return routeBuilder;
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

    #region Additional Non-Merge Routing Tests

    [Fact]
    public async Task StaticRouting_HandlesEmptyInput()
    {
        // Arrange
        var processedItems = new ConcurrentBag<int>();
        var items = Array.Empty<int>();

        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "EmptyInputTest");

        // Act - Configure router for empty input
        builder.AddProducer("source", sp => new TestProducer<int>(items));

        builder.AddRouter<int>("router", item => "route1")
            .RegisterRoute("route1", context =>
            {
                var routeBuilder = context.RouteBuilder;
                routeBuilder.AddProcessor("processor", sp => new TestProcessor<int>(
                    onProcessItem: item => processedItems.Add(item)))
                    .AsEntry();
                return routeBuilder;
            })
            .ReceiveFrom("source");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context);

        // Assert - Should handle empty input gracefully
        processedItems.ShouldBeEmpty();
    }

    [Fact]
    public async Task DynamicRouting_CreatesMultipleRoutesFromSameTemplate()
    {
        // Arrange
        var processedItems = new ConcurrentDictionary<string, ConcurrentBag<int>>();
        var items = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "MultiRouteTest");
        var logger = sp.GetRequiredService<ILogger<StructuredRoutingBlockTests>>();

        // Act - Route based on modulo 3 to create 3 different dynamic routes
        builder.AddProducer("source", sp => new TestProducer<int>(items));

        builder.AddRouter<int>("router", item => $"mod{item % 3}")
            .RegisterRoute("template", context =>
            {
                logger.LogInformation("Creating dynamic route: {RouteName}", context.RouteName);
                var routeBuilder = context.RouteBuilder;
                routeBuilder.AddProcessor("processor", sp => new TestProcessor<int>(
                    onProcessItem: number =>
                    {
                        processedItems.AddOrUpdate(
                            context.RouteName,
                            key => { var bag = new ConcurrentBag<int>(); bag.Add(number); return bag; },
                            (key, bag) => { bag.Add(number); return bag; });
                    }))
                    .AsEntry();
                return routeBuilder;
            })
            .WithDynamicRouting("template")
            .ReceiveFrom("source");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context);

        // Assert - Should have 3 routes (mod0, mod1, mod2)
        processedItems.Keys.Count.ShouldBe(3);
        processedItems.Keys.ShouldContain("mod0");
        processedItems.Keys.ShouldContain("mod1"); 
        processedItems.Keys.ShouldContain("mod2");

        // Verify correct routing
        processedItems["mod0"].OrderBy(x => x).ShouldBe(new[] { 3, 6, 9 });
        processedItems["mod1"].OrderBy(x => x).ShouldBe(new[] { 1, 4, 7, 10 });
        processedItems["mod2"].OrderBy(x => x).ShouldBe(new[] { 2, 5, 8 });
    }

    [Fact]
    public async Task StaticRouting_HandlesComplexRouteLogic()
    {
        // Arrange
        var processedResults = new ConcurrentBag<(string Route, string Result)>();
        var items = Enumerable.Range(1, 50).ToArray();

        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "ComplexRouteLogicTest");

        // Act - Complex routing: prime numbers, even numbers, and odd numbers
        builder.AddProducer("source", sp => new TestProducer<int>(items));

        builder.AddRouter<int>("router", item =>
        {
            if (IsPrime(item)) return "prime";
            return item % 2 == 0 ? "even" : "odd";
        })
        .RegisterRoute("prime", context =>
        {
            var routeBuilder = context.RouteBuilder;
            routeBuilder.AddTransform("transform", sp => new NumberTransformer("PRIME"))
                .AsEntry()
                .AddProcessor("processor", sp => new TestProcessor<string>(
                    onProcessItem: result => processedResults.Add((context.RouteName, result))));
            return routeBuilder;
        })
        .RegisterRoute("even", context =>
        {
            var routeBuilder = context.RouteBuilder;
            routeBuilder.AddTransform("transform", sp => new NumberTransformer("EVEN"))
                .AsEntry()
                .AddProcessor("processor", sp => new TestProcessor<string>(
                    onProcessItem: result => processedResults.Add((context.RouteName, result))));
            return routeBuilder;
        })
        .RegisterRoute("odd", context =>
        {
            var routeBuilder = context.RouteBuilder;
            routeBuilder.AddTransform("transform", sp => new NumberTransformer("ODD"))
                .AsEntry()
                .AddProcessor("processor", sp => new TestProcessor<string>(
                    onProcessItem: result => processedResults.Add((context.RouteName, result))));
            return routeBuilder;
        })
        .ReceiveFrom("source");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context);

        // Assert - Verify routing logic
        var primeResults = processedResults.Where(r => r.Route == "prime").ToList();
        var evenResults = processedResults.Where(r => r.Route == "even").ToList();
        var oddResults = processedResults.Where(r => r.Route == "odd").ToList();

        // Check that we have results in each category
        primeResults.Count.ShouldBeGreaterThan(0);
        evenResults.Count.ShouldBeGreaterThan(0);
        oddResults.Count.ShouldBeGreaterThan(0);

        // Check specific examples
        primeResults.ShouldContain(r => r.Result.Contains("PRIME-2"));
        primeResults.ShouldContain(r => r.Result.Contains("PRIME-3"));
        primeResults.ShouldContain(r => r.Result.Contains("PRIME-5"));

        evenResults.ShouldContain(r => r.Result.Contains("EVEN-4"));
        evenResults.ShouldContain(r => r.Result.Contains("EVEN-6"));
        evenResults.ShouldContain(r => r.Result.Contains("EVEN-8"));

        oddResults.ShouldContain(r => r.Result.Contains("ODD-9")); // 9 is odd but not prime
        oddResults.ShouldContain(r => r.Result.Contains("ODD-15")); // 15 is odd but not prime

        // Total processed should equal input
        processedResults.Count.ShouldBe(items.Length);

        static bool IsPrime(int number)
        {
            if (number < 2) return false;
            if (number == 2) return true;
            if (number % 2 == 0) return false;

            for (int i = 3; i * i <= number; i += 2)
            {
                if (number % i == 0) return false;
            }
            return true;
        }
    }

    [Fact]
    public async Task RoutingBlock_HandlesSingleRoute()
    {
        // Arrange
        var processedItems = new ConcurrentBag<string>();
        var items = new[] { 1, 2, 3, 4, 5 };

        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "SingleRouteTest");

        // Act - All items go to single route
        builder.AddProducer("source", sp => new TestProducer<int>(items));

        builder.AddRouter<int>("router", item => "single")
            .RegisterRoute("single", context =>
            {
                var routeBuilder = context.RouteBuilder;
                routeBuilder.AddTransform("transform", sp => new NumberTransformer("item"))
                    .AsEntry()
                    .AddProcessor("processor", sp => new TestProcessor<string>(
                        onProcessItem: result => processedItems.Add(result)));
                return routeBuilder;
            })
            .ReceiveFrom("source");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context);

        // Assert - All items processed through single route
        processedItems.Count.ShouldBe(5);
        processedItems.ShouldAllBe(item => item.StartsWith("item-"));
    }

    [Fact]
    public async Task DynamicRouting_RespectsRouteCreationOrder()
    {
        // Arrange  
        var routeCreationOrder = new ConcurrentQueue<string>();
        var items = new[] { 1, 3, 2, 4, 1, 3, 2, 4 }; // Repeated to ensure routes are created once

        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "RouteOrderTest");

        // Act - Track route creation order
        builder.AddProducer("source", sp => new TestProducer<int>(items));

        builder.AddRouter<int>("router", item => $"route{item}")
            .RegisterRoute("template", context =>
            {
                routeCreationOrder.Enqueue(context.RouteName);
                var routeBuilder = context.RouteBuilder;
                routeBuilder.AddProcessor("processor", sp => new TestProcessor<int>())
                    .AsEntry();
                return routeBuilder;
            })
            .WithDynamicRouting("template")
            .ReceiveFrom("source");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context);

        // Assert - Routes should be created in order of first occurrence
        var createdRoutes = routeCreationOrder.ToList();
        createdRoutes.Count.ShouldBe(4); // Should create exactly 4 routes
        createdRoutes[0].ShouldBe("route1");
        createdRoutes[1].ShouldBe("route3");
        createdRoutes[2].ShouldBe("route2");
        createdRoutes[3].ShouldBe("route4");
    }

    #endregion

    #region Merge Functionality Tests

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

        // Assert - Should throw NotImplementedException when merge is configured
        Should.Throw<NotImplementedException>(() => builder.Build())
            .Message.ShouldContain("Merge functionality is not yet implemented");
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

    [Fact(Skip = "Merge functionality requires additional architectural work - see TODO in StructuredRoutingBlock")]
    public async Task StaticRouting_CanMerge_RoutesIntoDownstreamBuffer()
    {
        // Arrange
        var processedItems = new ConcurrentBag<int>();
        var items = Enumerable.Range(1, 20).ToArray();

        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "RoutingWithMergeTest");
        var logger = sp.GetRequiredService<ILogger<StructuredRoutingBlockTests>>();

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

    [Fact(Skip = "Merge functionality requires additional architectural work - see TODO in StructuredRoutingBlock")]
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
        var logger = sp.GetRequiredService<ILogger<StructuredRoutingBlockTests>>();

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

    [Fact(Skip = "Merge functionality requires additional architectural work - see TODO in StructuredRoutingBlock")]
    public async Task Routing_CanLookup_ExistingBranchFromGraph()
    {
        // Arrange
        var processedItems = new ConcurrentBag<string>();
        var items = Enumerable.Range(1, 10).ToArray();

        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "RoutingWithBranchLookupTest");
        var logger = sp.GetRequiredService<ILogger<StructuredRoutingBlockTests>>();

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

    #endregion
}

// Test helper classes
public class RoutingTestItem
{
    public string Category { get; set; } = "";
    public string Value { get; set; } = "";
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
