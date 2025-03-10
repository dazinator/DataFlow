namespace Tests.DataFlow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[Category("Performance")]
public class RoutingBlockScopeDisposalTest
{
    private readonly ITestOutputHelper _output;

    public RoutingBlockScopeDisposalTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task RoutingBlock_WhenRouteExpires_ShouldNotUseDisposedServiceProvider()
    {
        // Use the standard test with few iterations
        await RunRoutingTest(iterations: 1, itemsPerRoute: 5, routeExpirationMs: 300, timeoutSeconds: 30);
    }

    // 
    //[Fact(Skip = "Long-running test for manual execution only")]
    [Fact()]
    public async Task RoutingBlock_LongRunning_ShouldDetectDisposedServiceProvider()
    {
        // This test runs much longer and puts more stress on the routing mechanism
        await RunRoutingTest(iterations: 50, itemsPerRoute: 20, routeExpirationMs: 200, timeoutSeconds: 300);
    }

    private async Task RunRoutingTest(int iterations, int itemsPerRoute, int routeExpirationMs, int timeoutSeconds)
    {
        // Arrange
        var services = new ServiceCollection();

        // Setup logging
        services.AddLogging(builder =>
        {

            builder.AddXUnit(_output, (o) =>
            {
                o.IncludeScopes = true;                
            });
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Add memory cache for routing
        services.AddMemoryCache();

        // Register our test service with scoped lifetime
        services.AddScoped<IScopedTestService, ScopedTestService>();

        // Add required data flow services
        services.AddDataFlowMetrics();
        services.AddDataFlows();

        // Register the test metrics before building the service provider
        var testMetrics = new TestMetrics();
        services.AddSingleton(testMetrics);

        // Pass the test parameters to our configuration
        services.AddSingleton(new TestParameters
        {
            Iterations = iterations,
            ItemsPerRoute = itemsPerRoute,
            RouteExpirationMs = routeExpirationMs
        });

        // Register our flow configuration
        services.AddDataFlow<RoutingTestFlowConfig>("RoutingTest");

        var serviceProvider = services.BuildServiceProvider();

        // Create context for flow execution with the specified timeout
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

        var context = new DataFlowContext()
        {
            InvocationId = Guid.NewGuid(),
            ServiceProvider = serviceProvider,
            CancellationToken = timeoutCts.Token
        };

        // Get the flow executor
        var executor = serviceProvider.GetRequiredService<FlowExecutor<RoutingTestFlowConfig>>();

        // Act & Assert
        try
        {
            await executor.ExecuteAsync(context);

            // Wait a bit more to ensure all cleanup tasks have run
            await Task.Delay(3000, timeoutCts.Token);
        }
        catch (System.AggregateException agg)
        {
            if(agg.InnerExceptions.All(e => e is TaskCanceledException))
            {
                _output.WriteLine("Test timed out after 30 seconds");
            }
            else
            {
                throw;
            }          
        }

        // Check the test metrics
        _output.WriteLine($"Items produced: {testMetrics.ItemsProduced}");
        _output.WriteLine($"Routes created: {testMetrics.RoutesCreated}");
        _output.WriteLine($"Items processed: {testMetrics.ItemsProcessed}");
        _output.WriteLine($"Service disposal attempts: {testMetrics.ServiceDisposalAttempts}");
        _output.WriteLine($"Exceptions caught: {testMetrics.ExceptionsCaught}");

        if (testMetrics.ExceptionDetails.Count > 0)
        {
            _output.WriteLine("\nException details:");
            foreach (var detail in testMetrics.ExceptionDetails)
            {
                _output.WriteLine(detail);
            }
        }

        // Verify no exceptions were caught during the test
        Assert.Equal(0, testMetrics.ExceptionsCaught);

        // Verify items were processed
        Assert.True(testMetrics.ItemsProcessed > 0);
    }

    /// <summary>
    /// Parameters for the test that can be adjusted
    /// </summary>
    public class TestParameters
    {
        public int Iterations { get; set; } = 1;
        public int ItemsPerRoute { get; set; } = 5;
        public int RouteExpirationMs { get; set; } = 300;
    }

    /// <summary>
    /// Test data flow configuration with a routing block
    /// </summary>
    public class RoutingTestFlowConfig : IDataFlowConfiguration
    {
        public void Configure(DataFlowBuilder builder)
        {
            var memoryCache = builder.ServiceProvider.GetRequiredService<IMemoryCache>();
            var logger = builder.ServiceProvider.GetRequiredService<ILogger<RoutingTestFlowConfig>>();
            var testMetrics = builder.ServiceProvider.GetRequiredService<TestMetrics>();
            var parameters = builder.ServiceProvider.GetRequiredService<TestParameters>();

            // Add producer of test items with different route keys
            builder.AddProducer<TestItem>("source", sp => new StressingTestItemProducer(
                testMetrics,
                iterations: parameters.Iterations,
                itemsPerRoute: parameters.ItemsPerRoute))
                // Route items by key to different processors
                .AddRouter<TestItem>("router",
                    item => item.RouteKey, // Routing key selector
                    context =>
                    {
                        // Create a new processor for each route
                        testMetrics.IncrementRoutesCreated();

                        // Create a new flow for this route
                        var routeBuilder = new DataFlowBuilder(context.ServiceProvider);

                        // Add processor that uses scoped service
                        routeBuilder.AddProcessor<TestItem, ScopedServiceProcessor>("processor",
                            new BlockOptions
                            {
                                MaxConcurrency = 3,  // Increased concurrency
                                UseSeperateScopes = true // This is important for the test
                            });

                        // Build the flow
                        var flow = routeBuilder.Build();
                        var targetBlock = routeBuilder.GetTargetBlock<TestItem>("processor");
                        return (flow, targetBlock);
                    },
                    options =>
                    {
                        options.RouteCache = memoryCache;
                        // Configurable expiration to trigger the issue
                        options.RouteExpiration = TimeSpan.FromMilliseconds(parameters.RouteExpirationMs);
                        options.MaxConcurrency = 3;  // Increased concurrency
                    })
                .ReceiveFrom("source");
        }
    }

    /// <summary>
    /// Test metrics to track what's happening in the test
    /// </summary>
    public class TestMetrics
    {
        private int _itemsProduced;
        private int _routesCreated;
        private int _itemsProcessed;
        private int _serviceDisposalAttempts;
        private int _exceptionsCaught;
        private readonly List<string> _exceptionDetails = new List<string>();
        private readonly object _lock = new object();

        public int ItemsProduced => _itemsProduced;
        public int RoutesCreated => _routesCreated;
        public int ItemsProcessed => _itemsProcessed;
        public int ServiceDisposalAttempts => _serviceDisposalAttempts;
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
        public void IncrementServiceDisposalAttempts() => Interlocked.Increment(ref _serviceDisposalAttempts);
        public void IncrementExceptionsCaught() => Interlocked.Increment(ref _exceptionsCaught);

        public void AddExceptionDetail(string detail)
        {
            lock (_lock)
            {
                _exceptionDetails.Add(detail);
            }
        }

        public void AddException(Exception ex)
        {
            IncrementExceptionsCaught();
            lock (_lock)
            {
                _exceptionDetails.Add($"Exception: {ex.GetType().Name}");
                _exceptionDetails.Add($"Message: {ex.Message}");
                _exceptionDetails.Add($"StackTrace: {ex.StackTrace}");

                if (ex is ObjectDisposedException ode)
                {
                    _exceptionDetails.Add($"ObjectName: {ode.ObjectName}");
                }

                if (ex.InnerException != null)
                {
                    _exceptionDetails.Add($"Inner Exception: {ex.InnerException.GetType().Name}");
                    _exceptionDetails.Add($"Inner Message: {ex.InnerException.Message}");
                }
            }
        }
    }

    /// <summary>
    /// A more demanding producer that stresses the routing system
    /// </summary>
    public class StressingTestItemProducer : IStreamProducer<TestItem>
    {
        private readonly TestMetrics _metrics;
        private readonly string[] _routeKeys = { "A", "B", "C", "D", "E" };
        private readonly Random _random = new Random();
        private readonly int _iterations;
        private readonly int _itemsPerRoute;
        private int _idCounter = 0;

        public StressingTestItemProducer(TestMetrics metrics, int iterations = 1, int itemsPerRoute = 5)
        {
            _metrics = metrics;
            _iterations = Math.Max(1, iterations);
            _itemsPerRoute = Math.Max(1, itemsPerRoute);
        }

        public async IAsyncEnumerable<TestItem> ProduceAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellation)
        {
            // Series of production phases to create stress on the routing system
            for (int iteration = 0; iteration < _iterations; iteration++)
            {
                if (cancellation.IsCancellationRequested)
                {
                    break;
                }

                // Phase 1: Send items to all routes in sequence
                foreach (var routeKey in _routeKeys)
                {
                    if (cancellation.IsCancellationRequested)
                    {
                        break;
                    }

                    // Send a batch to this route
                    for (int i = 0; i < _itemsPerRoute; i++)
                    {
                        if (cancellation.IsCancellationRequested)
                        {
                            break;
                        }

                        _metrics.IncrementItemsProduced();
                        yield return new TestItem
                        {
                            Id = _idCounter++,
                            RouteKey = routeKey,
                            Data = $"Data-{routeKey}-{_idCounter}"
                        };
                    }
                }

                // Wait for a random time to allow some routes to expire
                if (!cancellation.IsCancellationRequested)
                {
                    await Task.Delay(_random.Next(100, 400), cancellation);
                }

                // Phase 2: Alternate between routes rapidly to cause contention
                // Focus on two routes, alternating quickly
                string focusRoute1 = _routeKeys[iteration % _routeKeys.Length];
                string focusRoute2 = _routeKeys[(iteration + 2) % _routeKeys.Length];

                for (int i = 0; i < _itemsPerRoute * 2; i++)
                {
                    if (cancellation.IsCancellationRequested)
                    {
                        break;
                    }

                    string route = (i % 2 == 0) ? focusRoute1 : focusRoute2;
                    _metrics.IncrementItemsProduced();
                    yield return new TestItem
                    {
                        Id = _idCounter++,
                        RouteKey = route,
                        Data = $"Alt-{route}-{_idCounter}"
                    };
                }

                // Wait longer to ensure routes expire
                if (!cancellation.IsCancellationRequested)
                {
                    await Task.Delay(_random.Next(200, 500), cancellation);
                }

                // Phase 3: Revisit expired routes to force recreation
                // Also create periods of rapid bursts followed by inactivity
                foreach (var routeKey in _routeKeys)
                {
                    if (cancellation.IsCancellationRequested)
                    {
                        break;
                    }

                    // Burst of items
                    for (int i = 0; i < _random.Next(1, _itemsPerRoute); i++)
                    {
                        if (cancellation.IsCancellationRequested)
                        {
                            break;
                        }

                        _metrics.IncrementItemsProduced();
                        yield return new TestItem
                        {
                            Id = _idCounter++,
                            RouteKey = routeKey,
                            Data = $"Burst-{routeKey}-{_idCounter}"
                        };
                    }

                    // Small pause between bursts
                    if (!cancellation.IsCancellationRequested)
                    {
                        await Task.Delay(_random.Next(50, 150), cancellation);
                    }
                }

                // End of iteration delay
                if (!cancellation.IsCancellationRequested && iteration < _iterations - 1)
                {
                    await Task.Delay(_random.Next(300, 600), cancellation);
                }
            }
        }
    }

    /// <summary>
    /// Test item with a route key
    /// </summary>
    public class TestItem
    {
        public int Id { get; set; }
        public string RouteKey { get; set; }
        public string Data { get; set; }
    }

    /// <summary>
    /// Scoped service interface to simulate DI behavior
    /// </summary>
    public interface IScopedTestService
    {
        Task ProcessAsync(TestItem item);
        void RegisterForDisposal(TestMetrics metrics);
    }

    /// <summary>
    /// Implementation of scoped service that tracks disposal
    /// </summary>
    public class ScopedTestService : IScopedTestService, IDisposable
    {
        private TestMetrics _metrics;
        private bool _disposed = false;
        private readonly Random _random = new Random();
        private readonly string _instanceId = Guid.NewGuid().ToString().Substring(0, 8);

        public void RegisterForDisposal(TestMetrics metrics)
        {
            _metrics = metrics;
        }

        public async Task ProcessAsync(TestItem item)
        {
            // Check for disposed state
            if (_disposed)
            {
                _metrics.AddExceptionDetail($"Attempted to use disposed service {_instanceId} for item {item.Id}, route {item.RouteKey}");
                throw new ObjectDisposedException(nameof(ScopedTestService),
                    $"Service instance {_instanceId} was accessed after disposal for item {item.Id}");
            }

            // Simulate varying work durations to increase chance of timing issues
            int processingTime = _random.Next(10, 150);

            // Items for some routes take longer to process
            if (item.RouteKey == "A" || item.RouteKey == "C")
            {
                processingTime += _random.Next(50, 150);
            }

            // Do some "work"
            await Task.Delay(processingTime);

            // Occasionally create some memory pressure
            if (_random.Next(100) < 15)
            {
                var temp = new List<byte[]>();
                for (int i = 0; i < 5; i++)
                {
                    temp.Add(new byte[1024 * _random.Next(1, 20)]);
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed && _metrics != null)
            {
                _metrics.IncrementServiceDisposalAttempts();
            }
            _disposed = true;
        }
    }

    /// <summary>
    /// Processor that uses the scoped service to process items
    /// </summary>
    public class ScopedServiceProcessor : IStreamProcessor<TestItem>
    {
        private readonly IScopedTestService _scopedService;
        private readonly TestMetrics _metrics;
        private readonly Random _random = new Random();
        private readonly string _processorId = Guid.NewGuid().ToString().Substring(0, 8);

        public ScopedServiceProcessor(IScopedTestService scopedService, IServiceProvider serviceProvider)
        {
            _scopedService = scopedService;

            // Get metrics from service provider
            _metrics = serviceProvider.GetService<TestMetrics>() ?? new TestMetrics();

            // Register for disposal
            _scopedService.RegisterForDisposal(_metrics);
        }

        public async Task ProcessAsync(IAsyncEnumerable<TestItem> input, CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var item in input.WithCancellation(cancellationToken))
                {
                    try
                    {
                        // Occasionally add a delay before processing to increase timing sensitivity
                        if (_random.Next(100) < 20)
                        {
                            await Task.Delay(_random.Next(10, 80), cancellationToken);
                        }

                        await _scopedService.ProcessAsync(item);
                        _metrics.IncrementItemsProcessed();

                        // Occasionally add a delay after processing
                        if (_random.Next(100) < 20)
                        {
                            await Task.Delay(_random.Next(10, 50), cancellationToken);
                        }
                    }
                    catch (ObjectDisposedException ex)
                    {
                        // This is what we're trying to reproduce
                        _metrics.AddException(ex);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _metrics.AddException(ex);
                        throw;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during cancellation, don't log
                throw;
            }
            catch (Exception ex)
            {
                _metrics.AddException(ex);
                throw;
            }
        }
    }
}


