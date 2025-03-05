using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry;

using Otel.Example.Flows;
using System.Collections.Concurrent;
using Uniun.DataFlow;
using Uniun.DataFlow.Metrics;
using System.Diagnostics.Metrics;

namespace Otel.Example;

public class Program
{
    public static void Main(string[] args)
    {
        // Add at the beginning of Main
        Environment.SetEnvironmentVariable("OTEL_SERVICE_NAME", "DataFlowExample");
        Environment.SetEnvironmentVariable("OTEL_RESOURCE_ATTRIBUTES", "service.instance.id=1,deployment.environment=development");
        Environment.SetEnvironmentVariable("OTEL_LOGS_EXPORTER", "otlp");
        Environment.SetEnvironmentVariable("OTEL_METRICS_EXPORTER", "otlp");
        Environment.SetEnvironmentVariable("OTEL_TRACES_EXPORTER", "otlp");
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", "grpc");
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317");

        // For debugging OTEL SDK issues
        System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.ConsoleTraceListener());
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        TestAspireEndpoint();

        // Create a shared resource builder for all telemetry
        var resourceBuilder = ResourceBuilder.CreateDefault()
       .AddService("DataFlowOtelExample")
       .AddAttributes(new Dictionary<string, object>
       {
           ["service.name"] = "DataFlowExample",
           ["service.instance.id"] = Guid.NewGuid().ToString(),
           ["deployment.environment"] = "development"
       });

        IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            })
            .ConfigureServices((builder, services) =>
            {
                // Add hosted service and DataFlow components
                services.AddHostedService<Worker>();

                services.AddDataFlowMetrics();
                services.AddDataFlows();
                services.AddDataFlow<ExampleFlowConfig>("test");
                services.AddDataFlow<OtherFlowConfig>("other");

                // Configure OpenTelemetry for the entire app
                var otelEndpoint = builder.Configuration.GetValue<string>("Otlp:Endpoint") ?? "http://localhost:4317";
                Console.WriteLine($"Configuring OTLP with endpoint: {otelEndpoint}");

                services.AddOpenTelemetry()
                    .ConfigureResource(r => r.AddService("DataFlowOtelExample"))
                    .WithMetrics(metrics =>
                    {
                        // Add custom meters and runtime metrics
                        metrics.AddMeter(MeterAccessor.MeterName);
                        metrics.AddRuntimeInstrumentation();
                        metrics.AddProcessInstrumentation();
                    })
                    .UseOtlpExporter(OpenTelemetry.Exporter.OtlpExportProtocol.Grpc, new Uri(otelEndpoint));

                // Add a test meter for verification
                var meter = new Meter("TestMeter", "1.0.0");
                var counter = meter.CreateCounter<int>("test_counter");

                // Background task to emit test metrics
               // services.AddHostedService<MetricEmitterService>();
            })
            .Build();

        Console.WriteLine("Application started. Emitting metrics to Aspire dashboard...");
        host.Run();
    }

    private static void TestAspireEndpoint()
    {

        // Test the Aspire OTLP endpoint
        Console.WriteLine("Testing connection to OTLP endpoint: http://localhost:4317");
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(2);
            try
            {
                var response = httpClient.GetAsync("http://localhost:4317").Result;
                Console.WriteLine($"Response: {response.StatusCode}");
            }
            catch (AggregateException ae)
            {
                if (ae.InnerException?.Message.Contains("404") == true ||
                    ae.InnerException?.Message.Contains("400") == true)
                {
                    Console.WriteLine("Endpoint is reachable (expected error for gRPC)");
                }
                else
                {
                    throw;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to connect to OTLP endpoint: {ex.Message}");
        }
    }
}

// Service to emit test metrics for verification
//public class MetricEmitterService : BackgroundService
//{
//    private readonly Meter _meter;
//    private readonly Counter<int> _counter;

//    public MetricEmitterService()
//    {
//        _meter = new Meter("TestMeter", "1.0.0");
//        _counter = _meter.CreateCounter<int>("test_counter", "items", "Test counter");
//    }

//    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//    {
//        while (!stoppingToken.IsCancellationRequested)
//        {
//            _counter.Add(1);
//            Console.WriteLine($"[{DateTime.Now}] Emitted metric: test_counter = 1");
//            await Task.Delay(5000, stoppingToken);
//        }
//    }
//}
