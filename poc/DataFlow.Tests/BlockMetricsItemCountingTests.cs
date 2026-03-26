namespace DataFlow.POC.Tests;

using DataFlow.Blazor.Events;
using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using DataFlow.POC.Registry;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

/// <summary>
/// Regression tests that verify BlockMetricsEvent.ItemsConsumed correctly reflects the number
/// of individual data items received by a block, not the number of epoch containers.
///
/// Root cause of the bug: WrapWithInputCounter wrapped the outer IAsyncEnumerable&lt;IEpochStream&lt;T&gt;&gt;
/// and incremented once per epoch container consumed.  The output-side counting done by
/// EnumerateAndRouteEpochStreamAsync counts inner items within each epoch.  This asymmetry meant
/// a batch block receiving 2 items bundled in one epoch showed "in 1" while the upstream source
/// showed "2 out" — an inconsistent and confusing diagram.
///
/// The fix makes WrapWithInputCounter detect IEpochStream&lt;T&gt; input types and use a wrapper
/// that counts inner items so both sides use the same unit of measure.
/// </summary>
public class BlockMetricsItemCountingTests
{
    // ── Capturing event sink ──────────────────────────────────────────────

    private sealed class CapturingEventSink : IFlowEventSink
    {
        private readonly List<IDataFlowEvent> _events = new();

        public IReadOnlyList<IDataFlowEvent> Events => _events;

        public Task AppendAsync(Guid flowRunId, IDataFlowEvent evt, CancellationToken cancellationToken = default)
        {
            lock (_events) _events.Add(evt);
            return Task.CompletedTask;
        }

        /// <summary>Returns all BlockMetricsEvents for the given block name.</summary>
        public IEnumerable<BlockMetricsEvent> MetricsFor(string blockName) =>
            _events.OfType<BlockMetricsEvent>().Where(e => e.BlockName == blockName);
    }

    // ── Helper ──────────────────────────────────────────────────────────

    private static (DataFlowGraph graph, ExecutionContext context, CapturingEventSink sink)
        BuildGraphWithSink(string name, Action<DataFlowGraphBuilder> configure)
    {
        var sink = new CapturingEventSink();

        var services = new ServiceCollection();
        services.AddScoped<IFlowEventSink>(_ => sink);
        var sp = services.BuildServiceProvider();

        var builder = GraphHelpers.CreateGraphBuilder(name);
        configure(builder);
        var graph = builder.Build(sp, new BlockTypeRegistry());
        var context = new ExecutionContext(sp, CancellationToken.None);
        return (graph, context, sink);
    }

    // ── Tests ────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies the fix: a batch block that receives N individual items bundled in one epoch
    /// should report ItemsConsumed = N (individual items), not 1 (epoch container count).
    /// </summary>
    [Fact]
    public async Task BatchBlock_ItemsConsumed_ReflectsIndividualItemCount_NotEpochContainerCount()
    {
        // Arrange: source emits 10 items; batch block groups them into batches of 3.
        var collected = new List<int[]>();
        const int itemCount = 10;

        var producer = BlockHelpers.CreateProducer("producer", TestStreams.Integers(itemCount));
        var batcher  = BlockHelpers.CreateBatch<int>("batcher", maxBatchSize: 3);
        var consumer = BlockHelpers.CreateActor<int[], object, CollectorActor<int[]>>(
            "consumer", new CollectorActor<int[]>(collected));

        var (graph, context, sink) = BuildGraphWithSink("metrics-batch-test", b =>
            b.AddBlock(producer)
             .AddBlock(batcher)
             .AddBlock(consumer)
             .Connect(producer, batcher)
             .Connect(batcher, consumer));

        // Act
        await graph.ExecuteAsync(context);

        // Assert: consumer received correct batches
        collected.SelectMany(b => b).Count().ShouldBe(itemCount);

        // Assert: source produced 10 individual items
        var producerMetrics = sink.MetricsFor("producer").LastOrDefault();
        producerMetrics.ShouldNotBeNull("source must emit a final BlockMetricsEvent");
        producerMetrics!.ItemsProduced.ShouldBe(itemCount,
            "source block should report the number of individual items it produced");

        // Assert: batcher consumed 10 individual items — THIS was the bug (showed 1 before the fix)
        var batcherMetrics = sink.MetricsFor("batcher").LastOrDefault();
        batcherMetrics.ShouldNotBeNull("batcher must emit a final BlockMetricsEvent");
        batcherMetrics!.ItemsConsumed.ShouldBe(itemCount,
            "batcher should report individual items consumed, not epoch container count");

        // The batcher's output is batch arrays; with batchSize=3 and 10 items we expect
        // 3 full batches + 1 partial = 4 batch arrays emitted.
        batcherMetrics.ItemsProduced.ShouldBe(4,
            "batcher should report the number of batch arrays it produced");
    }

    /// <summary>
    /// Verifies that the output count of the upstream block matches the input count of the
    /// downstream block (the core invariant broken by the original bug).
    /// </summary>
    [Fact]
    public async Task Source_ItemsProduced_Matches_BatchBlock_ItemsConsumed()
    {
        // Arrange: 7 items, batch size 10 (one partial batch)
        const int itemCount = 7;

        var producer = BlockHelpers.CreateProducer("producer", TestStreams.Integers(itemCount));
        var batcher  = BlockHelpers.CreateBatch<int>("batcher", maxBatchSize: 10);
        var consumer = BlockHelpers.CreateActor<int[], object, CollectorActor<int[]>>(
            "consumer", new CollectorActor<int[]>(new List<int[]>()));

        var (graph, context, sink) = BuildGraphWithSink("metrics-match-test", b =>
            b.AddBlock(producer)
             .AddBlock(batcher)
             .AddBlock(consumer)
             .Connect(producer, batcher)
             .Connect(batcher, consumer));

        // Act
        await graph.ExecuteAsync(context);

        // Assert: produced == consumed across the producer → batcher edge
        var producerProduced = sink.MetricsFor("producer").LastOrDefault()?.ItemsProduced ?? 0;
        var batcherConsumed  = sink.MetricsFor("batcher").LastOrDefault()?.ItemsConsumed  ?? 0;

        producerProduced.ShouldBe(itemCount);
        batcherConsumed.ShouldBe(producerProduced,
            "items consumed by batcher must equal items produced by source — mismatch was the original bug");
    }

    /// <summary>
    /// Verifies the actor block (EpochActorBlock) also counts individual items consumed,
    /// not epoch containers.
    /// </summary>
    [Fact]
    public async Task ActorBlock_ItemsConsumed_ReflectsIndividualItemCount()
    {
        // Arrange: source emits 5 items; actor block does a 1:1 pass-through.
        var collected = new List<int>();
        const int itemCount = 5;

        var producer = BlockHelpers.CreateProducer("producer", TestStreams.Integers(itemCount));
        var actor    = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "actor", new CollectorActor<int>(collected));

        var (graph, context, sink) = BuildGraphWithSink("metrics-actor-test", b =>
            b.AddBlock(producer)
             .AddBlock(actor)
             .Connect(producer, actor));

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        collected.Count.ShouldBe(itemCount);

        var actorMetrics = sink.MetricsFor("actor").LastOrDefault();
        actorMetrics.ShouldNotBeNull("actor block must emit a final BlockMetricsEvent");
        actorMetrics!.ItemsConsumed.ShouldBe(itemCount,
            "actor block should report individual items consumed, not epoch container count");
    }
}
