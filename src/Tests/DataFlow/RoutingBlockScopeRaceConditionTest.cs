namespace Tests.DataFlow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[Xunit.Categories.Expensive]
[Category("Regression")]
public class RoutingBlockScopeRaceConditionTest
{
    private readonly ITestOutputHelper _output;

    public RoutingBlockScopeRaceConditionTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task RoutingBlock_WithShortRouteExpiration_ShouldNotUseDisposedServiceProvider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Setup logging
        services.AddLogging(builder =>
        {

            builder.AddXUnit(_output)
                .SetMinimumLevel(LogLevel.Debug);
        });

        // Add memory cache for routing
        services.AddMemoryCache();

        // Register our test service with scoped lifetime - this will be what gets disposed
        services.AddScoped<IScopedTestService, ScopedTestService>();

        // Add required data flow services
        services.AddDataFlowMetrics();
        services.AddDataFlows();

        // Register the test metrics to track failures
        var testMetrics = new TestMetrics();
        services.AddSingleton(testMetrics);

        // Configure test parameters - the key is to have very short route expiration
        // and slow processing to create the race condition
        services.AddSingleton(new TestParameters
        {
            RouteExpirationMs = 200,        // Very short route expiration
            ProcessingDelayMs = 800,        // Longer processing time
            RouteProcessingCycles = 5,      // Number of cycles to process in each route
            MaxTestDurationSeconds = 30     // Overall test timeout
        });

        // Register our flow configuration
        services.AddDataFlow<RaceConditionFlowConfig>("RaceConditionTest");

        var serviceProvider = services.BuildServiceProvider();

        // Create context for flow execution with the specified timeout
        var parameters = serviceProvider.GetRequiredService<TestParameters>();
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(parameters.MaxTestDurationSeconds));

        var context = new DataFlowContext()
        {
            Name = "race-condition-test",
            InvocationId = Guid.NewGuid(),
            ServiceProvider = serviceProvider,
            CancellationToken = timeoutCts.Token
        };

        // Get the flow executor
        var executor = serviceProvider.GetRequiredService<FlowExecutor<RaceConditionFlowConfig>>();

        // Act & Assert
        try
        {
            await executor.ExecuteAsync(context);

            // The test passes if we don't get an ObjectDisposedException during execution
            _output.WriteLine("Flow completed successfully without exceptions");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Exception during execution: {ex.GetType().Name}: {ex.Message}");
            _output.WriteLine(ex.StackTrace);

            if (ex is AggregateException aggEx)
            {
                foreach (var innerEx in aggEx.InnerExceptions)
                {
                    _output.WriteLine($"Inner exception: {innerEx.GetType().Name}: {innerEx.Message}");
                    _output.WriteLine(innerEx.StackTrace);
                }
            }

            // If we got an ObjectDisposedException, the bug has been reproduced
            Assert.DoesNotContain("ObjectDisposedException", ex.ToString());
        }
        finally
        {
            // Output metrics for analysis
            _output.WriteLine($"Items produced: {testMetrics.ItemsProduced}");
            _output.WriteLine($"Routes created: {testMetrics.RoutesCreated}");
            _output.WriteLine($"Items processed: {testMetrics.ItemsProcessed}");
            _output.WriteLine($"Service disposal attempts: {testMetrics.ServiceDisposalAttempts}");
            _output.WriteLine($"Exceptions caught: {testMetrics.ExceptionsCaught}");

            if (testMetrics.ExceptionsCaught > 0)
            {
                foreach (var detail in testMetrics.ExceptionDetails)
                {
                    _output.WriteLine(detail);
                }
            }
        }

        Assert.Equal(testMetrics.ItemsProduced, testMetrics.ItemsProcessed);
    }

    /// <summary>
    /// Parameters for the test that can be adjusted
    /// </summary>
    public class TestParameters
    {
        public int RouteExpirationMs { get; set; } = 200;
        public int ProcessingDelayMs { get; set; } = 800;
        public int RouteProcessingCycles { get; set; } = 5;
        public int MaxTestDurationSeconds { get; set; } = 30;
    }

    /// <summary>
    /// Test data flow configuration specifically designed to reproduce the race condition
    /// </summary>
    public class RaceConditionFlowConfig : IDataFlowConfiguration
    {
        public void Configure(DataFlowBuilder builder)
        {
            var memoryCache = builder.ServiceProvider.GetRequiredService<IMemoryCache>();
            var logger = builder.ServiceProvider.GetRequiredService<ILogger<RaceConditionFlowConfig>>();
            var testMetrics = builder.ServiceProvider.GetRequiredService<TestMetrics>();
            var parameters = builder.ServiceProvider.GetRequiredService<TestParameters>();

            // Add producer of test items that will cycle between a few route keys
            // with processing times longer than route expiration
            builder.AddProducer<TestItem>("source", sp => new RaceConditionProducer(
                testMetrics,
                routeProcessingCycles: parameters.RouteProcessingCycles));


            builder.AddRouter<TestItem>("router",
                 routingKeySelector: item => item.RouteKey,
                 (context) =>
                 {
                     // Create a new processor for each route
                     testMetrics.IncrementRoutesCreated();
                     logger.LogInformation($"Creating route for key: {context.RoutingKey}");

                     // Create a new flow for this route
                     var routeBuilder = new DataFlowBuilder(context.ServiceProvider);

                     // Create a simple flow with just a processor
                     routeBuilder.AddProcessor<TestItem, ScopedServiceProcessor>("processor", new BlockOptions
                     {
                         MaxConcurrency = 3,  // Increased concurrency to create more race conditions
                         UseSeperateScopes = true // This triggers the use of scoped service providers
                     });

                     var flow = routeBuilder.Build();
                     var targetBlock = routeBuilder.GetTargetBlock<TestItem>("processor");
                     return (flow, targetBlock);
                 },
            options =>
            {
                options.RouteCache = memoryCache;
                // Very short expiration time to trigger the race condition
                options.RouteExpiration = TimeSpan.FromMilliseconds(parameters.RouteExpirationMs);
                options.MaxConcurrency = 3;  // Increased concurrency
            }
            )
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
    /// Producer specifically designed to create a race condition
    /// </summary>
    public class RaceConditionProducer : IStreamProducer<TestItem>
    {
        private readonly TestMetrics _metrics;
        private readonly string[] _routeKeys = { "quantum", "standard", "express" };
        private readonly int _routeProcessingCycles;
        private int _idCounter = 0;

        public RaceConditionProducer(TestMetrics metrics, int routeProcessingCycles)
        {
            _metrics = metrics;
            _routeProcessingCycles = routeProcessingCycles;
        }

        public async IAsyncEnumerable<TestItem> ProduceAsync(IDataFlowContext context,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellation)
        {
            // Send items in batches to each route
            for (var cycle = 0; cycle < _routeProcessingCycles; cycle++)
            {
                if (cancellation.IsCancellationRequested)
                {
                    break;
                }

                foreach (var routeKey in _routeKeys)
                {
                    if (cancellation.IsCancellationRequested)
                    {
                        break;
                    }

                    // Send a few items to this route
                    for (var i = 0; i < 3; i++)
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

                    // Wait a bit before sending to the next route
                    await Task.Delay(50, cancellation);
                }

                // Wait a bit between cycles - just long enough for some routes to expire
                // but not all processing to complete
                await Task.Delay(300, cancellation);
            }
        }
    }

    /// <summary>
    /// Test item with a route key
    /// </summary>
    public class TestItem
    {
        public int Id { get; set; }
        public required string RouteKey { get; set; }
        public required string Data { get; set; }
    }

    /// <summary>
    /// Scoped service interface to simulate DI behavior
    /// </summary>
    public interface IScopedTestService
    {
        Task ProcessAsync(TestItem item, int processingTimeMs);
        void RegisterForDisposal(TestMetrics metrics);
    }

    /// <summary>
    /// Implementation of scoped service that tracks disposal
    /// </summary>
    public class ScopedTestService : IScopedTestService, IDisposable
    {
        private TestMetrics? _metrics;
        private bool _disposed = false;
        private readonly string _instanceId = Guid.NewGuid().ToString()[..8];

        public void RegisterForDisposal(TestMetrics metrics)
        {
            _metrics = metrics;
        }

        public async Task ProcessAsync(TestItem item, int processingTimeMs)
        {
            // Check for disposed state
            if (_disposed)
            {
                _metrics?.AddExceptionDetail($"Attempted to use disposed service {_instanceId} for item {item.Id}, route {item.RouteKey}");
                throw new ObjectDisposedException(nameof(ScopedTestService),
                    $"Service instance {_instanceId} was accessed after disposal for item {item.Id}");
            }

            // Simulate slow processing - this is key to causing the race condition
            await Task.Delay(processingTimeMs);
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
        private readonly TestParameters _parameters;
        private readonly ILogger<ScopedServiceProcessor> _logger;

        public ScopedServiceProcessor(
            IScopedTestService scopedService,
            TestMetrics metrics,
            TestParameters parameters,
            ILogger<ScopedServiceProcessor> logger)
        {
            _scopedService = scopedService;
            _metrics = metrics;
            _parameters = parameters;
            _logger = logger;

            // Register for disposal tracking
            _scopedService.RegisterForDisposal(_metrics);
        }

        public async Task ProcessAsync(IDataFlowContext context, IAsyncEnumerable<TestItem> input, CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var item in input.WithCancellation(cancellationToken))
                {
                    try
                    {
                        _logger.LogInformation($"Processing item {item.Id} for route {item.RouteKey}");

                        // Use the scoped service - with a delay that's longer than the route expiration time
                        await _scopedService.ProcessAsync(item, _parameters.ProcessingDelayMs);

                        _metrics.IncrementItemsProcessed();
                        _logger.LogInformation($"Completed item {item.Id} for route {item.RouteKey}");
                    }
                    catch (ObjectDisposedException ex)
                    {
                        // This is what we're trying to reproduce
                        _metrics.AddException(ex);
                        _logger.LogError(ex, $"ObjectDisposedException while processing item {item.Id}");
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _metrics.AddException(ex);
                        _logger.LogError(ex, $"Error processing item {item.Id}");
                        throw;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during cancellation
                throw;
            }
            catch (Exception ex)
            {
                _metrics.AddException(ex);
                _logger.LogError(ex, "Error in processor");
                throw;
            }
        }
    }
}


