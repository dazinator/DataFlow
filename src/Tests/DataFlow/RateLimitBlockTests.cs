namespace Tests.DataFlow;

using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;
using Uniun.DataFlow.Blocks.RateLimiting;

public class RateLimitBlockTests
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceCollection _services;

    public RateLimitBlockTests(ITestOutputHelper output)
    {
        _output = output;
        _services = new ServiceCollection();
        _services.AddLogging(builder => builder.AddXUnit(_output));
        _services.AddDataFlowMetrics();
        _services.AddDataFlows();
    }

    /// <summary>
    /// Creates a dataflow pipeline with a TokenBucketRateLimiter block.
    /// Inputs 5 items and verifies that they are processed at the expected rate.
    /// 
    /// Rate limiter configuration:
    ///   - TokenLimit = 2 (initial burst of 2 items)
    ///   - TokensPerPeriod = 2 (2 tokens replenished every second)
    ///   - ReplenishmentPeriod = 1s
    /// 
    /// Timeline:
    ///   t=0s: 2 items processed immediately (initial tokens)
    ///   t=1s: 2 more items processed (replenished tokens)
    ///   t=2s: 1 final item processed (replenished token)
    ///   Total: ~2s for 5 items
    /// 
    /// The test asserts that all items are processed and the elapsed time is at least 2 seconds.
    /// </summary>
    [IntegrationTest]
    [Fact]
    public async Task EnforcesTokenBucketRateLimit()
    {
        // Arrange
        var items = Enumerable.Range(1, 5).ToArray();
        var processedItems = new ConcurrentBag<int>();
        var sp = _services.BuildServiceProvider();
        var builder = new DataFlowBuilder(sp);

        // TokenBucket: 2 tokens per second, so 5 items should take at least 3 seconds
        builder.AddProducer("source", sp => new TestProducer<int>(items, delay: TimeSpan.Zero))
            .AddRateLimit<int>(
                "rateLimiter",
                () => new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 2,
                    TokensPerPeriod = 2,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                    QueueLimit = 10,
                    AutoReplenishment = true
                }))
            .ReceiveFrom("source")
            .AddProcessor("processor", sp => new TestProcessor<int>(onProcessItem: processedItems.Add))
            .ReceiveFrom("rateLimiter");

        var flow = builder.Build();
        var context = new DataFlowContext(Guid.NewGuid())
        {
            ServiceProvider = sp,
            CancellationToken = default,
            Name = "test"
        };

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await flow.ExecuteAsync(context);
        sw.Stop();

        // Assert
        Assert.Equal(items.Length, processedItems.Count);
        Assert.True(sw.Elapsed >= TimeSpan.FromSeconds(2), $"Elapsed: {sw.Elapsed}"); // Should take at least 2s for 5 items at 2/s
    }
}
