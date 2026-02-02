namespace Tests.DataFlow;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tests.DataFlow.Utils.Transformers;
using Tests.Shared;
using Uniun.DataFlow.Blocks.Routing;

/// <summary>
/// Tests specifically for the scope disposal fix in PersistentRoutingBlock.
/// The bug was that route blocks were using the parent context's service provider
/// instead of the route's scoped service provider. This caused ObjectDisposedExceptions
/// when parent workers completed before route execution finished.
/// </summary>
[IntegrationTest]
public class PersistentRoutingBlockScopeFixTest : DataFlowTestBase
{
    public PersistentRoutingBlockScopeFixTest(ITestOutputHelper output) : base(output)
    {
    }

    protected override void AddDefaultServices()
    {
        base.AddDefaultServices();
        Services.AddMetrics();
    }

    /// <summary>
    /// This test specifically reproduces the scenario where route blocks would fail
    /// when trying to create scopes from a disposed parent service provider.
    /// 
    /// The fix ensures that routes use their own scoped context with the route's
    /// service provider, not the parent's.
    /// </summary>
    [Fact]
    public async Task PersistentRouter_Routes_Use_Their_Own_Scoped_Context()
    {
        // Arrange
        Services.AddScoped<IScopedProcessor, ScopedProcessor>();
        Services.AddSingleton<ScopeTracker>();

        var items = Enumerable.Range(1, 20).ToArray();

        var sp = Services.BuildServiceProvider();
        var scopeTracker = sp.GetRequiredService<ScopeTracker>();

        var builder = new DataFlowBuilder(sp);
        var logger = sp.GetRequiredService<ILogger<PersistentRoutingBlockScopeFixTest>>();

        // Act - Create a flow with routing and blocks that use ExecuteParallelActivities
        builder.AddProducer("source", sp => new TestProducer<int>(items))
            .AddPersistentRouter<int>("router",
                item => (item % 2 == 0) ? "even" : "odd",
                context =>
                {
                    var processor = context.ServiceProvider.GetRequiredService<IScopedProcessor>();
                    var routeBuilder = new DataFlowBuilder(context.ServiceProvider);

                    // Create a transform block with MaxConcurrency > 1
                    // This causes ExecuteParallelActivities to be called, which creates new scopes
                    routeBuilder.AddTransform<int, int>("transformer",
                        sp => new NumberToNumberTransformer(
                            number =>
                            {
                                // This simulates work that happens in parallel activities
                                processor.ProcessItem(number, context.RoutingKey);
                                return number;
                            }),
                        options => { options.MaxConcurrency = 3; options.UseSeperateScopes = true; });

                    // Add a processor to consume the transformed items
                    routeBuilder.AddProcessor<int>("processor", sp => new TestProcessor<int>(
                        onProcessItem: number =>
                        {
                            logger.LogInformation("Processing {Number} on route {Route}", number, context.RoutingKey);
                        }),
                        options: new BlockOptions { MaxConcurrency = 2 })
                    .ReceiveFrom("transformer");

                    var flow = routeBuilder.Build();
                    var targetBlock = routeBuilder.GetTargetBlock<int>("transformer");
                    return (flow, targetBlock);
                })
            .ReceiveFrom("source");

        var flow = builder.Build();
        var flowContext = DataFlowContextTestUtils.GetContext("test", Guid.NewGuid(), sp);

        // Execute flow - this should complete without ObjectDisposedException
        await flow.ExecuteAsync(flowContext);

        // Assert - Verify that scoped processors were created and used correctly
        var evenProcessor = scopeTracker.GetProcessorForRoute("even");
        var oddProcessor = scopeTracker.GetProcessorForRoute("odd");

        Assert.NotNull(evenProcessor);
        Assert.NotNull(oddProcessor);
        Assert.NotEqual(evenProcessor, oddProcessor);

        // Verify all items were processed
        Assert.Equal(new[] { 2, 4, 6, 8, 10, 12, 14, 16, 18, 20 }, evenProcessor.ProcessedItems.OrderBy(x => x));
        Assert.Equal(new[] { 1, 3, 5, 7, 9, 11, 13, 15, 17, 19 }, oddProcessor.ProcessedItems.OrderBy(x => x));

        // Verify scopes were eventually disposed (at end of route execution)
        Assert.True(evenProcessor.WasDisposed);
        Assert.True(oddProcessor.WasDisposed);
    }

    /// <summary>
    /// Helper transformer for the test that transforms int to int
    /// </summary>
    private class NumberToNumberTransformer : IStreamTransformer<int, int>
    {
        private readonly Func<int, int> _transform;

        public NumberToNumberTransformer(Func<int, int> transform)
        {
            _transform = transform;
        }

        public async IAsyncEnumerable<int> TransformAsync(
            IDataFlowContext context,
            IAsyncEnumerable<int> input,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var item in input.WithCancellation(cancellationToken))
            {
                yield return _transform(item);
            }
        }
    }
}
