using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;
using Uniun.DataFlow.Blocks.RateLimiting;

[Category("Performance")]
public class RateLimitBlockMemoryProfileTests
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceProvider _sp;
    private readonly ILogger<RateLimitBlockMemoryProfileTests> _logger;

    public RateLimitBlockMemoryProfileTests(ITestOutputHelper output)
    {
        _output = output;
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddXUnit(_output));
        services.AddDataFlowMetrics();
        services.AddDataFlows();
        _sp = services.BuildServiceProvider();
        _logger = _sp.GetRequiredService<ILogger<RateLimitBlockMemoryProfileTests>>();
    }

    [Theory]
    [InlineData(10)] // 10 items for clear memory steps
    public async Task MemoryProfile_Unrestricted(int itemCount)
    {
        // Generate one large blob (10MB) per item
        var data = GenerateLargeBlobsStream(itemCount); // yields blobs one at a time

        var outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "artifacts");
        Directory.CreateDirectory(outputDir);

        // Unrestricted: 200ms delay between blobs
        var unrestrictedPath = Path.Combine(outputDir, $"memory_unrestricted_{itemCount}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        using (var memorySampler = new MemoryCsvSampler(unrestrictedPath, _logger))
        {
            var processed = new ConcurrentBag<byte[]>();
            var builder = new DataFlowBuilder(_sp);

            builder.AddProducer("source", sp => new TestProducer<byte[]>(data, delay: TimeSpan.FromMilliseconds(200)))
                .AddProcessor<byte[]>("processor", sp => new TestProcessor<byte[]>(onProcessItem: processed.Add))
                .ReceiveFrom("source");

            var flow = builder.Build();
            var context = new DataFlowContext(Guid.NewGuid()) { ServiceProvider = _sp, Name = "unrestricted" };
            await flow.ExecuteAsync(context);
        }
        _logger.LogInformation("Unrestricted memory profile written to: {Path}", unrestrictedPath);
    }

    [Theory]
    [InlineData(10)] // 10 items for clear memory steps
    public async Task MemoryProfile_RateLimited(int itemCount)
    {
        // Generate one large blob (10MB) per item
        var data = GenerateLargeBlobsStream(itemCount); // yields blobs one at a time

        var outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "artifacts");
        Directory.CreateDirectory(outputDir);

        // Rate-limited: 1 item per second
        var rateLimitedPath = Path.Combine(outputDir, $"memory_ratelimited_{itemCount}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        using (var memorySampler = new MemoryCsvSampler(rateLimitedPath, _logger))
        {
            var processed = new ConcurrentBag<byte[]>();
            var builder = new DataFlowBuilder(_sp);

            builder
                .AddProducer("source",
                sp => new TestProducer<byte[]>(data, delay: TimeSpan.FromMilliseconds(200)), configureOptions: (o) =>
                {
                    o.Capacity = 1;
                })
                .AddRateLimit<byte[]>(
                    "rateLimiter",
                    () => new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 1,
                        TokensPerPeriod = 1,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                        QueueLimit = 10,
                        AutoReplenishment = true
                    }), configureOptions: (o) =>
                    {
                        o.Capacity = 1;
                    })
                .ReceiveFrom("source")
                .AddProcessor<byte[]>("processor", sp => new TestProcessor<byte[]>(onProcessItem: processed.Add))
                .ReceiveFrom("rateLimiter");

            var flow = builder.Build();
            var context = new DataFlowContext(Guid.NewGuid()) { ServiceProvider = _sp, Name = "ratelimited" };
            await flow.ExecuteAsync(context);
            // Add a delay to allow profiler to sample memory
            await Task.Delay(2000);
        }
        _logger.LogInformation("Rate-limited memory profile written to: {Path}", rateLimitedPath);
    }

    private static IEnumerable<byte[]> GenerateLargeBlobsStream(int itemCount, int blobSizeBytes = 10 * 1024 * 1024)
    {
        for (var i = 0; i < itemCount; i++)
        {
            yield return new byte[blobSizeBytes];
        }
    }
}

public sealed class ProfilingEventSource : EventSource
{
    public static readonly ProfilingEventSource Log = new ProfilingEventSource();

    [Event(1, Message = "GC between executions", Level = EventLevel.Informational)]
    public void GCMarker() => WriteEvent(1);
}
