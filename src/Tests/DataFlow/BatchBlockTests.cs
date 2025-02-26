namespace Tests.DataFlow;
using Microsoft.Extensions.Logging;

[IntegrationTest]
public class BatchBlockTests
{

    public ITestOutputHelper Output { get; }
    public ServiceCollection Services { get; }

    private readonly ServiceProvider _serviceProvider;

    public BatchBlockTests(ITestOutputHelper output)
    {
        Output = output;
        Services = new ServiceCollection();
        AddDefaultServices(Services);

    }

    public void AddDefaultServices(IServiceCollection services)
    {
        Services.AddLogging(builder => builder.AddXUnit(Output));
        Services.AddDataFlows(maxConcurrentFlows: 1);
    }

    /// <summary>
    /// Verifies that
    /// - items are correctly batched when reaching maxBatchSize
    /// - the final partial batch is also emitted
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task BatchesBySize_WhenMaxBatchSizeReached()
    {
        // Arrange


        // .AddDataFlow<TestProducerConfig>();

        var sp = Services.BuildServiceProvider();

        var items = Enumerable.Range(1, 10).Select(i => $"item{i}").ToArray();
        var processedBatches = new List<string[]>();

        var builder = new DataFlowBuilder(sp);

        // Act
        builder.AddProducer("source", sp => new TestProducer<string>(items))
            .AddBatch<string>("batcher", maxBatchSize: 3, windowPeriod: TimeSpan.FromSeconds(10))
                 .ReceiveFrom("source")

            // .AddBatch<string>("batcher", maxBatchSize: 3, windowPeriod: TimeSpan.FromSeconds(10))           
            .AddProcessor<string[], TestProcessor<string[]>>("processor", sp => new TestProcessor<string[]>(
                onProcessItem: batch => processedBatches.Add(batch)))
            .ReceiveFrom("batcher");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);
        // / var context = new PipelineContext("test", Guid.NewGuid(), _serviceProvider);
        await flow.ExecuteAsync(context, "myblock");

        // Assert
        Assert.Equal(4, processedBatches.Count); // Should create 3 full batches and 1 partial
        Assert.Collection(processedBatches,
            batch => Assert.Equal(new[] { "item1", "item2", "item3" }, batch),
            batch => Assert.Equal(new[] { "item4", "item5", "item6" }, batch),
            batch => Assert.Equal(new[] { "item7", "item8", "item9" }, batch),
            batch => Assert.Equal(new[] { "item10" }, batch)
        );
    }

    /// <summary>
    /// Ensures batches are emitted after the time window, even if not full
    /// Uses a slow producer to force time-based batching
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task BatchesByTime_WhenWindowPeriodElapsed()
    {
        // Arrange
        var items = Enumerable.Range(1, 5).Select(i => $"item{i}").ToArray();
        var processedBatches = new List<string[]>();



        var sp = Services.BuildServiceProvider();
        var builder = new DataFlowBuilder(sp);
        // Act
        builder.AddProducer("source", sp => new TestProducer<string>(
                items,
                delay: TimeSpan.FromMilliseconds(200))) // Produce items slowly
            .AddBatch<string>("batcher",
                maxBatchSize: 10, // Large enough to not trigger size-based batching
                windowPeriod: TimeSpan.FromMilliseconds(500)) // Short window to force time-based batching
            .ReceiveFrom("source")
            .AddProcessor<string[], TestProcessor<string[]>>("processor", sp => new TestProcessor<string[]>(
                onProcessItem: batch => processedBatches.Add(batch)))
            .ReceiveFrom("batcher");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context, "testflow");

        // Assert
        Assert.True(processedBatches.Count > 1, "Should have created multiple batches based on time");
        Assert.True(processedBatches.All(batch => batch.Length < 5), "Each batch should contain fewer than all items");
        Assert.Equal(items.Length, processedBatches.SelectMany(b => b).Count()); // All items should be processed
    }

    /// <summary>
    /// Verifies proper behavior when no items are provided
    /// Ensures clean completion with no batches emitted
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task HandlesEmptySource_GracefullyCompletes()
    {
        // Arrange
        var items = Array.Empty<string>();
        var processedBatches = new List<string[]>();

        var sp = Services.BuildServiceProvider();
        var builder = new DataFlowBuilder(sp);
        // Act
        builder.AddProducer("source", sp => new TestProducer<string>(items))
            .AddBatch<string>("batcher", maxBatchSize: 3, windowPeriod: TimeSpan.FromSeconds(1))
            .ReceiveFrom("source")
            .AddProcessor<string[], TestProcessor<string[]>>("processor", sp => new TestProcessor<string[]>(
                onProcessItem: batch => processedBatches.Add(batch)))
            .ReceiveFrom("batcher");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context, "testflow");

        // Assert
        Assert.Empty(processedBatches);
    }


    /// <summary>
    /// Tests error propagation from the source
    /// Verifies that batches processed before the error are still emitted
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task HandlesSourceError_PropagatesException()
    {
        // Arrange
        var items = Enumerable.Range(1, 5).Select(i => $"item{i}").ToArray();
        var processedBatches = new List<string[]>();

        var sp = Services.BuildServiceProvider();
        var builder = new DataFlowBuilder(sp);
        // Act & Assert
        builder.AddProducer("source", sp => new ErrorProducer<string>(
                items,
                shouldError: item => item == "item3",
                errorMessage: "Test error"))
            .AddBatch<string>("batcher", maxBatchSize: 2, windowPeriod: TimeSpan.FromSeconds(1))
            .ReceiveFrom("source")
            .AddProcessor<string[], TestProcessor<string[]>>("processor", sp => new TestProcessor<string[]>(
                onProcessItem: batch => processedBatches.Add(batch)))
            .ReceiveFrom("batcher");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            flow.ExecuteAsync(context, "testflow"));

        // Should have processed some batches before error
        Assert.True(processedBatches.Count > 0);
    }

    [Fact]
    public async Task HandlesCancellation_StopsCleanly()
    {
        // Arrange
        var items = Enumerable.Range(1, 100).Select(i => $"item{i}").ToArray();
        var processedBatches = new List<string[]>();
        using var cts = new CancellationTokenSource();

        var sp = Services.BuildServiceProvider();
        var builder = new DataFlowBuilder(sp);
        // Act
        builder.AddProducer("source", sp => new TestProducer<string>(
                items,
                delay: TimeSpan.FromMilliseconds(50))) // Slow producer
            .AddBatch<string>("batcher", maxBatchSize: 10, windowPeriod: TimeSpan.FromSeconds(1))
            .ReceiveFrom("source")
            .AddProcessor<string[], TestProcessor<string[]>>("processor", sp => new TestProcessor<string[]>(
                onProcessItem: batch =>
                {
                    processedBatches.Add(batch);
                    if (processedBatches.Count >= 3)
                    {
                        cts.Cancel(); // Cancel after processing 3 batches
                    }
                }))
            .ReceiveFrom("batcher");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp, cts.Token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            flow.ExecuteAsync(context, "testflow"));

        Assert.True(processedBatches.Count >= 3);
        Assert.True(processedBatches.All(batch => batch.Length > 0));
    }

    /// <summary>
    /// Verifies that BatchBlock processes items serially with a single reader.
    /// 
    /// The BatchBlock is designed to:
    /// 1. Read items one at a time from the source to maintain order
    /// 2. Form batches based on size/time window criteria
    /// 3. Emit batches immediately when they're ready
    /// 
    /// Concurrency is not applicable here as:
    /// - Items must be read serially to maintain order
    /// - Batches are emitted as soon as they're formed
    /// - Downstream blocks handle their own concurrency for processing batches
    /// </summary>
    [Fact]
    public async Task VerifiesSerialBatchFormation()
    {
        // Arrange
        var items = Enumerable.Range(1, 20).Select(i => $"item{i}").ToArray();
        var maxConcurrentOperations = 0;
        var currentConcurrentOperations = 0;
        var concurrencyTracker = new ConcurrencyTracker(
            onEnter: () =>
            {
                var current = Interlocked.Increment(ref currentConcurrentOperations);
                var max = Interlocked.CompareExchange(ref maxConcurrentOperations, current, current - 1);
                if (current > max)
                {
                    Interlocked.Exchange(ref maxConcurrentOperations, current);
                }
            },
            onExit: () => Interlocked.Decrement(ref currentConcurrentOperations));

        var sp = Services.BuildServiceProvider();
        var builder = new DataFlowBuilder(sp);

        // Act
        builder.AddProducer("source", sp => new ConcurrencyTestProducer<string>(
                items,
                concurrencyTracker,
                workDelay: TimeSpan.FromMilliseconds(100)))
            .AddBatch<string>("batcher",
                maxBatchSize: 5,
                windowPeriod: TimeSpan.FromSeconds(1))  // No BlockOptions needed as concurrency doesn't apply
            .ReceiveFrom("source")
            .AddProcessor<string[], TestProcessor<string[]>>("processor", sp => new TestProcessor<string[]>())
            .ReceiveFrom("batcher");

        var flow = builder.Build();
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context, "testflow");

        // Assert
        Assert.Equal(1, maxConcurrentOperations); // Verifies serial processing of items
    }
    private IDataFlowContext CreateContext(string v, Guid guid, ServiceProvider provider, CancellationToken ct = default)
    {
        return DataFlowContextTestUtils.GetContext(v, guid, provider, ct);
    }
}
