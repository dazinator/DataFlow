namespace Tests.DataFlow;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Uniun.DataFlow;
using Uniun.DataFlow.Actor;
using Uniun.DataFlow.Blocks;
using Uniun.DataFlow.Builder.Graph;
using Uniun.DataFlow.Metrics;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Tests demonstrating the semantic difference between competing consumers (old MaxConcurrency)
/// and broadcast consumers (new branch approach).
/// 
/// IMPORTANT: BroadcastBlock-based branches create BROADCAST semantics where each branch
/// receives ALL items. This is fundamentally different from MaxConcurrency which creates
/// COMPETING CONSUMER semantics where items are distributed across workers.
/// </summary>
[IntegrationTest]
public class CompetingVsBroadcastConsumersTests
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceCollection _services;

    public CompetingVsBroadcastConsumersTests(ITestOutputHelper output)
    {
        _output = output;
        _services = new ServiceCollection();
        AddDefaultServices(_services);
    }

    private void AddDefaultServices(IServiceCollection services)
    {
        services.AddLogging(builder => builder.AddXUnit(_output));
        services.AddDataFlows();
        services.AddMetrics();
        services.AddDataFlowMetrics();
    }

    private IDataFlowContext CreateContext(string name, Guid guid, IServiceProvider provider, CancellationToken ct = default)
    {
        return DataFlowContextTestUtils.GetContext(name, guid, provider, ct);
    }

    [Fact]
    public async Task TransformBlock_WithMaxConcurrency_CreatesCompetingConsumers()
    {
        // Arrange - Multiple actors compete for items from the same input stream
        var items = Enumerable.Range(1, 10).ToArray();
        var processedItems = new ConcurrentBag<(int originalItem, int actorId)>();

        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "CompetingConsumers");

        builder.AddProducer<int>("source", (IServiceProvider sp) => new TestProducer<int>(items));

        builder.AddTransform<int, string>("transform",
            sp => new ActorTrackingTransformer(processedItems),
            options: new BlockOptions { MaxConcurrency = 3 })
            .ReceiveFrom("source");

        var stringProcessorBuilder = StructuredDataFlowBuilderExtensions.AddProcessor<string>(builder, "processor",
            (IServiceProvider sp) => new TestProcessor<string>());
        stringProcessorBuilder.ReceiveFrom("transform");

        var flow = builder.Build();

        // Act
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context);

        // Assert
        _output.WriteLine("Competing Consumers Pattern (Old MaxConcurrency):");
        _output.WriteLine($"Total items processed: {processedItems.Count}");
        
        // Each item should be processed by exactly ONE actor (competing consumers)
        processedItems.Count.ShouldBe(10, "Each item should be processed by exactly ONE actor");
        
        // With MaxConcurrency=3, up to 3 actors can process items (competing consumers)
        var actorIds = processedItems.Select(x => x.actorId).Distinct().ToArray();
        _output.WriteLine($"Number of actors that processed items: {actorIds.Length}");
        actorIds.Length.ShouldBeGreaterThanOrEqualTo(1, "At least one actor should process items");
        actorIds.Length.ShouldBeLessThanOrEqualTo(3, "No more than MaxConcurrency actors should be used");
        
        // Each original item should appear exactly once
        var originalItems = processedItems.Select(x => x.originalItem).OrderBy(x => x).ToArray();
        originalItems.ShouldBe(items, "Each item should appear exactly once");

        _output.WriteLine("Distribution:");
        foreach (var actorId in actorIds.OrderBy(x => x))
        {
            var count = processedItems.Count(x => x.actorId == actorId);
            _output.WriteLine($"  Actor {actorId}: {count} items");
        }
    }

    [Fact]
    public async Task BroadcastBranches_CreateBroadcastConsumers()
    {
        // Arrange - Each branch receives ALL items (broadcast semantics)
        var items = Enumerable.Range(1, 10).ToArray();
        var processedItems = new ConcurrentBag<(int originalItem, int branchId)>();

        var sp = _services.BuildServiceProvider();
        var builder = new StructuredDataFlowBuilder(sp, "BroadcastConsumers");

        builder.AddProducer<int>("source", (IServiceProvider sp) => new TestProducer<int>(items));

        builder.AddBroadcast<int>("fanout")
            .ReceiveFrom("source");

        // Create 3 branches - each will receive ALL items
        for (int i = 0; i < 3; i++)
        {
            var branchId = i;
            var branch = builder.AddBranch($"branch-{i}");
            
            branch.AddTransform<int, string>($"transform-{i}",
                sp => new BranchTrackingTransformer(branchId, processedItems))
                .ReceiveFrom("fanout");

            var processorBuilder = StructuredDataFlowBuilderExtensions.AddProcessor<string>(branch, $"processor-{i}",
                (IServiceProvider sp) => new TestProcessor<string>());
            processorBuilder.ReceiveFrom($"transform-{i}");
        }

        var flow = builder.Build();

        // Act
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context);

        // Assert
        _output.WriteLine("Broadcast Consumers Pattern (New Branch Approach):");
        _output.WriteLine($"Total items processed: {processedItems.Count}");
        
        // Each item should be processed by ALL branches (broadcast semantics)
        processedItems.Count.ShouldBe(30, "Each of 3 branches should process all 10 items");
        
        // Each branch should process all items
        for (int branchId = 0; branchId < 3; branchId++)
        {
            var branchItems = processedItems.Where(x => x.branchId == branchId).Select(x => x.originalItem).OrderBy(x => x).ToArray();
            branchItems.ShouldBe(items, $"Branch {branchId} should receive all items");
            _output.WriteLine($"  Branch {branchId}: {branchItems.Length} items");
        }
    }


    #region Helper Classes

    private class ActorTrackingTransformer : IStreamTransformer<int, string>
    {
        private static int _nextActorId = 0;
        private readonly int _actorId;
        private readonly ConcurrentBag<(int originalItem, int actorId)> _processedItems;

        public ActorTrackingTransformer(ConcurrentBag<(int originalItem, int actorId)> processedItems)
        {
            _actorId = Interlocked.Increment(ref _nextActorId);
            _processedItems = processedItems;
        }

        public async IAsyncEnumerable<string> TransformAsync(
            IDataFlowContext context,
            IAsyncEnumerable<int> input,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var item in input.WithCancellation(cancellationToken))
            {
                _processedItems.Add((item, _actorId));
                yield return $"Item-{item}-Actor-{_actorId}";
            }
        }
    }

    private class BranchTrackingTransformer : IStreamTransformer<int, string>
    {
        private readonly int _branchId;
        private readonly ConcurrentBag<(int originalItem, int branchId)> _processedItems;

        public BranchTrackingTransformer(int branchId, ConcurrentBag<(int originalItem, int branchId)> processedItems)
        {
            _branchId = branchId;
            _processedItems = processedItems;
        }

        public async IAsyncEnumerable<string> TransformAsync(
            IDataFlowContext context,
            IAsyncEnumerable<int> input,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var item in input.WithCancellation(cancellationToken))
            {
                _processedItems.Add((item, _branchId));
                yield return $"Item-{item}-Branch-{_branchId}";
            }
        }
    }

    #endregion
}
