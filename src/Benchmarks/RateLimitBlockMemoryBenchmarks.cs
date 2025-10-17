using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tests.DataFlow.Utils.Processors;
using Tests.DataFlow.Utils.Producers;
using Uniun.DataFlow.Blocks.RateLimiting;
using Uniun.DataFlow.Builder;

[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 1, iterationCount: 1)]
[MemoryDiagnoser]
public class RateLimitBlockMemoryBenchmarks
{
    private ServiceProvider _sp;
    private ILogger<RateLimitBlockMemoryBenchmarks> _logger;
    private ByteArrayProducer _producer;

    private MemoryCsvSampler _memorySampler;

    [Params(100)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        // Add logging (with null logger to minimize overhead)
        services.AddLogging(builder =>
        {
            // builder.AddConsole();
            builder.AddProvider(NullLoggerProvider.Instance);
        });
        services.AddDataFlowMetrics();
        services.AddDataFlows();
        _sp = services.BuildServiceProvider();
        _logger = _sp.GetRequiredService<ILogger<RateLimitBlockMemoryBenchmarks>>();
        // Each item is a 1MB byte array
        _producer = new ByteArrayProducer(ItemCount, 1024 * 1024);
        // Start memory sampling to CSV
        var outputPath = Path.Combine(
            Path.GetTempPath(),
            // Directory.GetParent(Environment.CurrentDirectory).FullName, // One level up from temp
            "artifacts",
            $"memory_{ItemCount}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        );
        Console.WriteLine($"Memory profile will be saved to: {outputPath}");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        _memorySampler = new MemoryCsvSampler(outputPath, _logger);
    }

    private byte[][] GenerateLargeBlobsStream(int itemCount) => throw new NotImplementedException();

    [Benchmark(Baseline = true)]
    public void UnrestrictedThroughput()
    {
        var processed = new ConcurrentBag<byte[]>();
        var builder = new DataFlowBuilder(_sp);

        builder.AddProducer("source", sp => _producer, a => a.Capacity = 1)
            .AddProcessor<byte[]>("processor", sp => new TestProcessor<byte[]>(onProcessItem: processed.Add))
            .ReceiveFrom("source");

        var flow = builder.Build();
        var context = new DataFlowContext(Guid.NewGuid()) { ServiceProvider = _sp, Name = "unrestricted" };
        flow.ExecuteAsync(context).GetAwaiter().GetResult();

        // Optionally record memory usage here
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var mem = GC.GetTotalMemory(false);
        Console.WriteLine($"Unrestricted memory: {mem / (1024 * 1024)} MB");
    }

    [Benchmark]
    public void RateLimitedThroughput()
    {
        var processed = new ConcurrentBag<byte[]>();
        var builder = new DataFlowBuilder(_sp);

        builder.AddProducer("source", sp => _producer, a => a.Capacity = 1)
            .AddRateLimit<byte[]>(
                "rateLimiter",
                () => new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 2,
                    TokensPerPeriod = 2,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                    QueueLimit = 100,
                    AutoReplenishment = true
                }))
            .ReceiveFrom("source")
            .AddProcessor<byte[]>("processor", sp => new TestProcessor<byte[]>(onProcessItem: processed.Add))
            .ReceiveFrom("rateLimiter");

        var flow = builder.Build();
        var context = new DataFlowContext(Guid.NewGuid()) { ServiceProvider = _sp, Name = "ratelimited" };
        flow.ExecuteAsync(context).GetAwaiter().GetResult();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var mem = GC.GetTotalMemory(false);
        Console.WriteLine($"Rate-limited memory: {mem / (1024 * 1024)} MB");
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _memorySampler.Dispose();
    }
}
