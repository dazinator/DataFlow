namespace Tests.DataFlow;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using SerilogTracing;
using SerilogTracing.Expressions;
using Uniun.DataFlow.Metrics;
using Xunit.Sdk;

[IntegrationTest]
public class DataFlowTracingTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceProvider _serviceProvider;
    private readonly List<LogEvent> _logEvents;

    public DataFlowTracingTests(ITestOutputHelper output)
    {
        _output = output;
        _logEvents = new List<LogEvent>();

        // Set up Serilog for testing with SerilogTracing

        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddUserSecrets<DataFlowTracingTests>();

        var config = configBuilder.Build();
        //var apiKey = config["SeqApiKey"];           
      

        // Configure and create the logger
        var loggerConfiguration = new LoggerConfiguration()
             .MinimumLevel.Debug()
            //.ReadFrom.Configuration(config)
            .Enrich.FromLogContext()
           // .WriteTo.Seq("", apiKey: apiKey)
                .WriteTo.Sink(new TestLogEventSink(_logEvents))
               .WriteTo.TestOutput(output, Formatters.CreateConsoleTextFormatter());// Capture logs for verification      

        var logger = loggerConfiguration.CreateLogger();

        // Set as static logger
        Log.Logger = logger;


        //  LoggerProviderContext.LogLevelSwitch.MinimumLevel = LogLevel.Trace;
        //  loggerProvider.LogLevelSwitch.MinimumLevel = LogLevel.Trace;
        // var logger = loggerProvider.LoggerProvider.CreateLogger(nameof(SeqTests));
        var testLogger = logger.ForContext<DataFlowTracingTests>();



        //Log.Logger = new LoggerConfiguration()
        //    .MinimumLevel.Debug()
        //    .Enrich.FromLogContext()
        //    .WriteTo.Sink(new TestLogEventSink(_logEvents)) // Capture logs for verification           
        //    .WriteTo.TestOutput(output, Formatters.CreateConsoleTextFormatter()) // Use SerilogTracing's formatter
        //    .CreateLogger();

        // Configure services
        var services = new ServiceCollection();
        ConfigureServices(services);
        services.AddLogging(builder =>
        {
            builder.AddSerilog(logger);
        });
        _serviceProvider = services.BuildServiceProvider();

        // Register Activity listener using SerilogTracing
        _activityListener = new ActivityListenerConfiguration()
            .InitialLevel.Override("Uniun.DataFlow", LogEventLevel.Debug)
            .TraceToSharedLogger();
    
    }

    private readonly IDisposable _activityListener;

    private void ConfigureServices(IServiceCollection services)
    {
        // Register your DataFlow services
        services.AddSingleton<IDataFlowMetrics, TestDataFlowMetrics>();

        // Register a simple test configuration
        services.AddTransient<TestFlowConfiguration>();

        // Register any other dependencies your DataFlow needs
        // ...
    }

    // The SerilogTracing ActivityListenerConfiguration replaces our custom listener setup

    [Fact]
    public async Task DataFlow_ExecuteAsync_ProducesExpectedTraces()
    {
        // Arrange
        // Create a test DataFlow with blocks that we can verify
        var blocks = new List<IBlock>
        {
            new TestBlock("block-1"),
            new TestBlock("block-2"),
            new TestBlock("block-3")
        };

        var metrics = _serviceProvider.GetRequiredService<IDataFlowMetrics>();
        var dataFlow = new Uniun.DataFlow.DataFlow("test-flow", blocks, metrics);

        var context = new TestDataFlowContext
        {
            InvocationId = Guid.NewGuid(),
            ServiceProvider = _serviceProvider,
            CancellationToken = CancellationToken.None
        };

        // Act
        using (TestCorrelator.CreateContext())
        {
            await dataFlow.ExecuteAsync(context);

            // Give the logger a moment to process everything
            await Task.Delay(100);
        }

        // Assert
        // Find all trace events (SerilogTracing maps activities to log events with TraceId/SpanId properties)
        var traceEvents = _logEvents.Where(e =>
            e.TraceId is not null &&
            e.SpanId is not null).ToList();

        _output.WriteLine($"Found {traceEvents.Count} trace events");

        // Verify we have spans for each block plus one for the flow
        Assert.True(traceEvents.Count >= 4, $"Expected at least 4 trace events, but found {traceEvents.Count}");


        foreach (var item in traceEvents)
        {
            var message = item.RenderMessage();
        }
      

        // Output all captured logs for debugging
        _output.WriteLine("--- All Captured Log Events ---");
        foreach (var logEvent in _logEvents)
        {
            _output.WriteLine($"[{logEvent.Level}] {logEvent.RenderMessage()}");
            if (logEvent.Properties.ContainsKey("TraceId"))
            {
                _output.WriteLine($"  TraceId: {logEvent.Properties["TraceId"]}");
                _output.WriteLine($"  SpanId: {logEvent.Properties["SpanId"]}");
                if (logEvent.Properties.ContainsKey("ParentSpanId"))
                {
                    _output.WriteLine($"  ParentSpanId: {logEvent.Properties["ParentSpanId"]}");
                }
            }
            _output.WriteLine($"  Properties: {string.Join(", ", logEvent.Properties.Select(p => $"{p.Key}={p.Value}"))}");
            _output.WriteLine("---");
        }

        var outputHelper = (TestOutputHelper)_output;
        var output = outputHelper.Output;

        await Task.Delay(2000); // give time for logs to be pushed to server in background by seq sink.      
       await Log.CloseAndFlushAsync(); // ensures the provider has a chance to flush log events before shutdown.

    }

    public void Dispose()
    {
        _activityListener?.Dispose();
        _serviceProvider?.Dispose();
        Log.CloseAndFlush();
    }

    // Test implementations

    private class TestLogEventSink : Serilog.Core.ILogEventSink
    {
        private readonly List<LogEvent> _logEvents;

        public TestLogEventSink(List<LogEvent> logEvents)
        {
            _logEvents = logEvents;
        }

        public void Emit(LogEvent logEvent)
        {
            _logEvents.Add(logEvent);
        }
    }

    private class TestBlock : IBlock
    {
        public TestBlock(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public BlockMetricsContext MetricsContext { get; set; }

        public Task ExecuteAsync(IDataFlowContext context)
        {
            // Simulate some work
            return Task.Delay(50);
        }
    }

    private class TestDataFlowMetrics : IDataFlowMetrics
    {
        public TagList GlobalTags { get; } = new TagList();

        public void RegisterChannel(IMonitoredChannel channel)
        {
            // No-op for testing
        }       

        public void FlowStarted(KeyValuePair<string, object?>[] tags)
        {
            // No-op for testing
        }
        public void FlowCompleted(double durationMs, KeyValuePair<string, object?>[] tags)
        {
            // No-op for testing
        }
        public void BlockStarted(KeyValuePair<string, object?>[] tags)
        {
            // No-op for testing
        }
        public void BlockCompleted(double durationTotalMs, KeyValuePair<string, object?>[] tags)
        {
            // No-op for testing
        }
    }

    private class TestDataFlowContext : IDataFlowContext
    {
        private readonly Dictionary<string, string> _dimensions = new Dictionary<string, string>();

        public Guid InvocationId { get; set; }
        public CancellationToken CancellationToken { get; set; }
        public IServiceProvider ServiceProvider { get; set; }
        public string Name { get; set; }

        public IDictionary<string, string> Dimensions => _dimensions;

        public DataFlowMetricsContext FlowMetricsContext { get; set; }
        public ConcurrentDictionary<string, object> Items { get; }
    }

    private class TestFlowConfiguration : IDataFlowConfiguration
    {
        public void Configure(DataFlowBuilder builder)
        {
            // Configure test flow
        }
    }

    // Define any interfaces/classes needed if your actual code is not accessible
    // You should replace these with your actual implementations

    public static class ActivityNames
    {
        public const string Flow = "Flow";
        public const string Block = "Block";

        public static class TagNames
        {
            public const string FlowInvocationId = "flow.invocationId";
            public const string FlowName = "flow.name";
            public const string BlockName = "block.name";
        }
    }
}
