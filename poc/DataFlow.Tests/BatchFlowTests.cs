namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using DataFlow.POC.Registry;

public class BatchFlowTests
{
    // Refactored to use BlockHelpers for consistent block instantiation patterns.
    private static async IAsyncEnumerable<int> ProduceIntegersWithDelay(IExecutionContext ctx, int count, int delayMs)
    {
        for (int i = 1; i <= count; i++)
        {
            yield return i;
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, ctx.CancellationToken);
            }
        }
    }

    /// <summary>
    /// Produces items in two bursts separated by a configurable gap, simulating infrequent item arrival.
    /// </summary>
    private static async IAsyncEnumerable<int> ProduceInTwoBursts(
        IExecutionContext ctx,
        int firstBurstCount,
        int secondBurstCount,
        TimeSpan gapBetweenBursts)
    {
        for (int i = 1; i <= firstBurstCount; i++)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();
            yield return i;
        }

        await Task.Delay(gapBetweenBursts, ctx.CancellationToken);

        for (int i = firstBurstCount + 1; i <= firstBurstCount + secondBurstCount; i++)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();
            yield return i;
        }
    }

    private static (DataFlowGraph graph, IExecutionContext context) BuildGraph<T>(
        string name,
        IBlock<object, T> producer,
        IBlock<T, T[]> batcher,
        IBlock<T[], object> processor)
    {
        var builder = GraphHelpers.CreateGraphBuilder(name);
        builder.AddBlock(producer)
            .AddBlock(batcher)
            .AddBlock(processor)
            .Connect(producer, batcher)
            .Connect(batcher, processor);

        var sp = new ServiceCollection().BuildServiceProvider();
        var graph = builder.Build(sp, new BlockTypeRegistry());
        var context = new ExecutionContext(sp, CancellationToken.None);
        return (graph, context);
    }

    [Fact]
    public async Task Batch_Block_Should_Batch_Items_By_Size()
    {
        // Arrange
        var batches = new List<int[]>();

        var producer = BlockHelpers.CreateProducer("producer", TestStreams.Integers(10));
        var batcher = BlockHelpers.CreateBatch<int>("batcher", maxBatchSize: 3);
        var processor = BlockHelpers.CreateActor<int[], object, CollectorActor<int[]>>(
            "processor",
            new CollectorActor<int[]>(batches));

        var (graph, context) = BuildGraph("batch-flow", producer, batcher, processor);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        batches.Count.ShouldBe(4); // 3 full batches + 1 partial
        batches[0].ShouldBe(new[] { 1, 2, 3 });
        batches[1].ShouldBe(new[] { 4, 5, 6 });
        batches[2].ShouldBe(new[] { 7, 8, 9 });
        batches[3].ShouldBe(new[] { 10 }); // Final partial batch
    }

    [Fact]
    public async Task Batch_Block_Should_Batch_Items_By_Time_Window()
    {
        // Arrange
        var batches = new List<int[]>();

        var producer = BlockHelpers.CreateProducer("producer", ctx => ProduceIntegersWithDelay(ctx, 5, delayMs: 50));
        var batcher = BlockHelpers.CreateBatch<int>("batcher", maxBatchSize: 100, windowPeriod: TimeSpan.FromMilliseconds(120));
        var processor = BlockHelpers.CreateActor<int[], object, CollectorActor<int[]>>(
            "processor",
            new CollectorActor<int[]>(batches));

        var (graph, context) = BuildGraph("batch-window-flow", producer, batcher, processor);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        batches.Count.ShouldBeGreaterThan(1); // Should have multiple batches due to time window
        batches.SelectMany(b => b).Count().ShouldBe(5); // All items should be present
    }

    /// <summary>
    /// Verifies the core fix: after the window period elapses the block proactively emits any
    /// accumulated items WITHOUT waiting for the next item to arrive.
    ///
    /// Scenario:
    ///   - maxBatchSize = 1000 (will never be reached)
    ///   - windowPeriod = 100 ms
    ///   - 3 items arrive quickly, then a 300 ms gap (3× the window)
    ///   - 2 more items arrive, then the source ends
    ///
    /// Expected: the first 3 items are emitted during the gap as a proactive timer flush,
    /// the last 2 are flushed when the epoch ends — giving 2 batches total.
    /// Previously the implementation would only emit a single batch on epoch end.
    /// </summary>
    [Fact]
    public async Task Batch_Block_With_Window_Should_Proactively_Flush_Without_Waiting_For_Next_Item()
    {
        // Arrange
        var batches = new List<int[]>();
        var batchTimestamps = new List<DateTime>();

        var window = TimeSpan.FromMilliseconds(100);
        var gap = TimeSpan.FromMilliseconds(300); // 3× the window — timer must have fired

        var producer = BlockHelpers.CreateProducer("producer",
            ctx => ProduceInTwoBursts(ctx, firstBurstCount: 3, secondBurstCount: 2, gapBetweenBursts: gap));
        var batcher = BlockHelpers.CreateBatch<int>("batcher", maxBatchSize: 1000, windowPeriod: window);
        var processor = BlockHelpers.CreateActor<int[], object, CollectorActor<int[]>>(
            "processor",
            new CollectorActor<int[]>(batches, onCollect: _ => batchTimestamps.Add(DateTime.UtcNow)));

        var (graph, context) = BuildGraph("proactive-flush-flow", producer, batcher, processor);

        // Act
        await graph.ExecuteAsync(context);

        // Assert — proactive flush produced two batches, not one end-of-epoch flush
        batches.Count.ShouldBe(2, "timer flush should emit the first batch during the gap");
        batches[0].ShouldBe(new[] { 1, 2, 3 }, "first batch: items from the first burst");
        batches[1].ShouldBe(new[] { 4, 5 },    "second batch: items from the second burst, flushed on epoch end");

        // First batch timestamp should appear well before the second burst arrived
        // (i.e. it was emitted proactively, not triggered by item 4 or 5 arriving)
        var lag = batchTimestamps[1] - batchTimestamps[0];
        lag.TotalMilliseconds.ShouldBeGreaterThan(window.TotalMilliseconds,
            "the two batches should be separated by at least one window period");
    }

    /// <summary>
    /// Verifies that size-based emission still works correctly when a windowPeriod is also set.
    /// Full batches should be emitted immediately without waiting for the timer.
    /// </summary>
    [Fact]
    public async Task Batch_Block_Should_Emit_Full_Batch_Immediately_When_Size_Reached_With_Window()
    {
        // Arrange
        var batches = new List<int[]>();

        // Items arrive faster than the window; full batches of 3 should emit right away
        var producer = BlockHelpers.CreateProducer("producer", TestStreams.Integers(9));
        var batcher = BlockHelpers.CreateBatch<int>("batcher",
            maxBatchSize: 3,
            windowPeriod: TimeSpan.FromSeconds(10)); // very long window — should never fire
        var processor = BlockHelpers.CreateActor<int[], object, CollectorActor<int[]>>(
            "processor",
            new CollectorActor<int[]>(batches));

        var (graph, context) = BuildGraph("size-with-window-flow", producer, batcher, processor);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await graph.ExecuteAsync(context);
        sw.Stop();

        // Assert
        batches.Count.ShouldBe(3);                         // 3 full batches of 3
        batches[0].ShouldBe(new[] { 1, 2, 3 });
        batches[1].ShouldBe(new[] { 4, 5, 6 });
        batches[2].ShouldBe(new[] { 7, 8, 9 });
        sw.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5),
            "all full batches should be emitted immediately, not waiting for the 10-second window");
    }

    /// <summary>
    /// Verifies that a partial batch remaining after all windows have fired is still flushed
    /// when the epoch ends (source completes).
    /// </summary>
    [Fact]
    public async Task Batch_Block_With_Window_Should_Flush_Remaining_Items_On_Epoch_End()
    {
        // Arrange
        var batches = new List<int[]>();

        // 7 items with maxBatchSize=5 → one full batch of 5, then 2 remaining
        var producer = BlockHelpers.CreateProducer("producer", TestStreams.Integers(7));
        var batcher = BlockHelpers.CreateBatch<int>("batcher",
            maxBatchSize: 5,
            windowPeriod: TimeSpan.FromSeconds(10)); // long window; flush triggered by epoch end, not timer
        var processor = BlockHelpers.CreateActor<int[], object, CollectorActor<int[]>>(
            "processor",
            new CollectorActor<int[]>(batches));

        var (graph, context) = BuildGraph("epoch-end-flush-flow", producer, batcher, processor);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        batches.Count.ShouldBe(2);
        batches[0].Length.ShouldBe(5);
        batches[1].ShouldBe(new[] { 6, 7 }, "remaining 2 items should be flushed on epoch end");
    }

    /// <summary>
    /// Verifies that an empty source produces no batches when a window is set.
    /// </summary>
    [Fact]
    public async Task Batch_Block_With_Window_Should_Produce_No_Batches_For_Empty_Source()
    {
        // Arrange
        var batches = new List<int[]>();

        var producer = BlockHelpers.CreateProducer("producer", TestStreams.Empty<int>());
        var batcher = BlockHelpers.CreateBatch<int>("batcher",
            maxBatchSize: 10,
            windowPeriod: TimeSpan.FromMilliseconds(50));
        var processor = BlockHelpers.CreateActor<int[], object, CollectorActor<int[]>>(
            "processor",
            new CollectorActor<int[]>(batches));

        var (graph, context) = BuildGraph("empty-windowed-flow", producer, batcher, processor);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        batches.ShouldBeEmpty("no items means no batches, even with a window");
    }

    /// <summary>
    /// Verifies that cancellation stops the windowed batch block cleanly without hanging.
    /// </summary>
    [Fact]
    public async Task Batch_Block_With_Window_Should_Stop_Cleanly_On_Cancellation()
    {
        // Arrange
        var batches = new List<int[]>();
        using var cts = new CancellationTokenSource();

        // Produce items slowly so cancellation hits mid-stream
        var producer = BlockHelpers.CreateProducer("producer",
            ctx => ProduceIntegersWithDelay(ctx, count: 100, delayMs: 20));
        var batcher = BlockHelpers.CreateBatch<int>("batcher",
            maxBatchSize: 50,
            windowPeriod: TimeSpan.FromMilliseconds(200));
        var processor = BlockHelpers.CreateActor<int[], object, CollectorActor<int[]>>(
            "processor",
            new CollectorActor<int[]>(batches, onCollect: _ =>
            {
                if (batches.Count >= 1) cts.Cancel();
            }));

        var builder = GraphHelpers.CreateGraphBuilder("cancellation-windowed-flow");
        builder.AddBlock(producer)
            .AddBlock(batcher)
            .AddBlock(processor)
            .Connect(producer, batcher)
            .Connect(batcher, processor);

        var sp = new ServiceCollection().BuildServiceProvider();
        var graph = builder.Build(sp, new BlockTypeRegistry());
        var context = new ExecutionContext(sp, cts.Token);

        // Act & Assert — should throw OperationCanceledException, not hang
        await Should.ThrowAsync<OperationCanceledException>(() => graph.ExecuteAsync(context));
        batches.ShouldNotBeEmpty("at least one batch was processed before cancellation");
    }
}
