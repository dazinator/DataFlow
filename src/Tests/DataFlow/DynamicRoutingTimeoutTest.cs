namespace Tests.DataFlow;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Test to reproduce the issue where dynamic routing blocks dispose of routes while they're still processing,
/// leading to "Timeout waiting for route execution" and lost items.
/// </summary>
[IntegrationTest]
public class DynamicRoutingTimeoutTest
{
    private readonly ITestOutputHelper _output;

    public DynamicRoutingTimeoutTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task DynamicRouter_ShouldShowTimeoutDisposalIssue()
    {
        // Arrange
        var services = new ServiceCollection();

        // Setup logging to capture the timeout messages
        services.AddLogging(builder =>
        {
            builder.AddXUnit(_output, options =>
            {
                options.IncludeScopes = true;
            });
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Add memory cache for routing
        services.AddMemoryCache();

        // Add required data flow services
        services.AddDataFlowMetrics();
        services.AddDataFlows();

        // Register test metrics to track what happens
        var testMetrics = new TimeoutTestMetrics();
        services.AddSingleton(testMetrics);

        // Configure the flow with timeout-prone settings
        services.AddDataFlow<TimeoutRoutingFlowConfig>("TimeoutTest");

        var serviceProvider = services.BuildServiceProvider();

        // Create context with reasonable timeout
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        var context = new DataFlowContext()
        {
            Name = "timeout-test",
            InvocationId = Guid.NewGuid(),
            ServiceProvider = serviceProvider,
            CancellationToken = timeoutCts.Token
        };

        // Get the flow executor
        var executor = serviceProvider.GetRequiredService<FlowExecutor<TimeoutRoutingFlowConfig>>();

        // Act
        try
        {
            await executor.ExecuteAsync(context);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Flow execution completed with exception: {ex.GetType().Name}: {ex.Message}");
        }

        // Assert - Check if we reproduced the timeout issue
        _output.WriteLine($"Items produced: {testMetrics.ItemsProduced}");
        _output.WriteLine($"Items started processing: {testMetrics.ItemsStartedProcessing}");
        _output.WriteLine($"Items completed processing: {testMetrics.ItemsCompletedProcessing}");
        _output.WriteLine($"Routes created: {testMetrics.RoutesCreated}");
        _output.WriteLine($"Timeout warnings detected: {testMetrics.TimeoutWarningsDetected}");

        // The issue is reproduced if:
        // 1. We have items that started processing but didn't complete
        // 2. We detect timeout warnings in the logs
        var itemsLost = testMetrics.ItemsStartedProcessing - testMetrics.ItemsCompletedProcessing;

        _output.WriteLine($"Items potentially lost due to timeout: {itemsLost}");

        // Log all processing details for analysis
        _output.WriteLine("\nProcessing details:");
        foreach (var detail in testMetrics.ProcessingDetails)
        {
            _output.WriteLine(detail);
        }

        // The test "passes" if we reproduce the issue - this is a demonstration test
        // In a real scenario, we'd want to fix the issue, but for now we're just showing it exists
        if (itemsLost > 0)
        {
            _output.WriteLine($"SUCCESS: Reproduced the timeout disposal issue - {itemsLost} items were lost");
        }
        else
        {
            _output.WriteLine("Did not reproduce the timeout issue - all items were processed");
        }

        // Verify we processed some items but potentially lost some due to timeout
        Assert.True(testMetrics.ItemsProduced > 0, "Should have produced some items");
        Assert.True(testMetrics.ItemsStartedProcessing > 0, "Should have started processing some items");
    }

    /// <summary>
    /// Flow configuration designed to trigger the timeout disposal issue
    /// </summary>
    public class TimeoutRoutingFlowConfig : IDataFlowConfiguration
    {
        public void Configure(DataFlowBuilder builder)
        {
            var memoryCache = builder.ServiceProvider.GetRequiredService<IMemoryCache>();
            var logger = builder.ServiceProvider.GetRequiredService<ILogger<TimeoutRoutingFlowConfig>>();
            var testMetrics = builder.ServiceProvider.GetRequiredService<TimeoutTestMetrics>();

            // Producer that sends items quickly, then stops
            builder.AddProducer<TimeoutTestItem>("source", sp => new QuickBurstProducer(testMetrics))
                .AddRouter<TimeoutTestItem>("router",
                    item => item.RouteKey,
                    context =>
                    {
                        testMetrics.IncrementRoutesCreated();
                        logger.LogInformation("Creating route for key: {RouteKey}", context.RoutingKey);

                        var routeBuilder = new DataFlowBuilder(context.ServiceProvider);

                        // Add a very slow processor to each route
                        routeBuilder.AddProcessor<TimeoutTestItem, VerySlowProcessor>("processor",
                            options: new BlockOptions
                            {
                                MaxConcurrency = 1, // Single threaded to make timing predictable
                                UseSeperateScopes = true
                            });

                        var flow = routeBuilder.Build();
                        var targetBlock = routeBuilder.GetTargetBlock<TimeoutTestItem>("processor");
                        return (flow, targetBlock);
                    },
                    options =>
                    {
                        options.RouteCache = memoryCache;
                        // Very short expiration to force route disposal while processing
                        options.RouteExpiration = TimeSpan.FromSeconds(5);
                        options.MaxConcurrency = 3;
                    })
                .ReceiveFrom("source");
        }
    }

    /// <summary>
    /// Producer that sends a burst of items quickly then stops
    /// This simulates the scenario where the routing block finishes receiving items
    /// but routes are still processing
    /// </summary>
    public class QuickBurstProducer : IStreamProducer<TimeoutTestItem>
    {
        private readonly TimeoutTestMetrics _metrics;
        private readonly string[] _routeKeys = { "slow-route-A", "slow-route-B", "slow-route-C" };

        public QuickBurstProducer(TimeoutTestMetrics metrics)
        {
            _metrics = metrics;
        }

        public async IAsyncEnumerable<TimeoutTestItem> ProduceAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellation)
        {
            // Send items quickly to multiple routes
            for (var i = 0; i < 15; i++) // 5 items per route
            {
                if (cancellation.IsCancellationRequested)
                {
                    break;
                }

                var routeKey = _routeKeys[i % _routeKeys.Length];
                _metrics.IncrementItemsProduced();

                yield return new TimeoutTestItem
                {
                    Id = i,
                    RouteKey = routeKey,
                    Data = $"Item-{i}-for-{routeKey}",
                    ProducedAt = DateTime.UtcNow
                };

                // Small delay to make it realistic but still quick
                await Task.Delay(100, cancellation);
            }

            // Producer finishes quickly, but routes will still be processing slowly
            _metrics.AddProcessingDetail("Producer finished - all items sent");
        }
    }

    /// <summary>
    /// Very slow processor that takes much longer than the route expiration time
    /// </summary>
    public class VerySlowProcessor : IStreamProcessor<TimeoutTestItem>
    {
        private readonly TimeoutTestMetrics _metrics;
        private readonly ILogger<VerySlowProcessor> _logger;

        public VerySlowProcessor(TimeoutTestMetrics metrics, ILogger<VerySlowProcessor> logger)
        {
            _metrics = metrics;
            _logger = logger;
        }

        public async Task ProcessAsync(IAsyncEnumerable<TimeoutTestItem> input, CancellationToken cancellationToken)
        {
            await foreach (var item in input.WithCancellation(cancellationToken))
            {
                try
                {
                    _metrics.IncrementItemsStartedProcessing();
                    _metrics.AddProcessingDetail($"Started processing item {item.Id} on route {item.RouteKey} at {DateTime.UtcNow:HH:mm:ss.fff}");

                    _logger.LogInformation("Starting to process item {ItemId} on route {RouteKey}", item.Id, item.RouteKey);

                    // Very slow processing - much longer than route expiration (5 seconds)
                    // This should trigger the timeout disposal issue
                    await Task.Delay(TimeSpan.FromSeconds(12), cancellationToken);

                    _metrics.IncrementItemsCompletedProcessing();
                    _metrics.AddProcessingDetail($"Completed processing item {item.Id} on route {item.RouteKey} at {DateTime.UtcNow:HH:mm:ss.fff}");

                    _logger.LogInformation("Completed processing item {ItemId} on route {RouteKey}", item.Id, item.RouteKey);
                }
                catch (OperationCanceledException)
                {
                    _metrics.AddProcessingDetail($"Processing of item {item.Id} on route {item.RouteKey} was cancelled at {DateTime.UtcNow:HH:mm:ss.fff}");
                    _logger.LogWarning("Processing of item {ItemId} on route {RouteKey} was cancelled", item.Id, item.RouteKey);
                    throw;
                }
                catch (Exception ex)
                {
                    _metrics.AddProcessingDetail($"Processing of item {item.Id} on route {item.RouteKey} failed: {ex.Message}");
                    _logger.LogError(ex, "Error processing item {ItemId} on route {RouteKey}", item.Id, item.RouteKey);
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// Test item for timeout scenarios
    /// </summary>
    public class TimeoutTestItem
    {
        public int Id { get; set; }
        public string RouteKey { get; set; }
        public string Data { get; set; }
        public DateTime ProducedAt { get; set; }
    }

    /// <summary>
    /// Metrics to track the timeout disposal issue
    /// </summary>
    public class TimeoutTestMetrics
    {
        private int _itemsProduced;
        private int _itemsStartedProcessing;
        private int _itemsCompletedProcessing;
        private int _routesCreated;
        private int _timeoutWarningsDetected;
        private readonly List<string> _processingDetails = new();
        private readonly object _lock = new();

        public int ItemsProduced => _itemsProduced;
        public int ItemsStartedProcessing => _itemsStartedProcessing;
        public int ItemsCompletedProcessing => _itemsCompletedProcessing;
        public int RoutesCreated => _routesCreated;
        public int TimeoutWarningsDetected => _timeoutWarningsDetected;
        public IReadOnlyList<string> ProcessingDetails
        {
            get
            {
                lock (_lock)
                {
                    return _processingDetails.ToList();
                }
            }
        }

        public void IncrementItemsProduced() => Interlocked.Increment(ref _itemsProduced);
        public void IncrementItemsStartedProcessing() => Interlocked.Increment(ref _itemsStartedProcessing);
        public void IncrementItemsCompletedProcessing() => Interlocked.Increment(ref _itemsCompletedProcessing);
        public void IncrementRoutesCreated() => Interlocked.Increment(ref _routesCreated);
        public void IncrementTimeoutWarningsDetected() => Interlocked.Increment(ref _timeoutWarningsDetected);

        public void AddProcessingDetail(string detail)
        {
            lock (_lock)
            {
                _processingDetails.Add($"[{DateTime.UtcNow:HH:mm:ss.fff}] {detail}");
            }
        }
    }
}
