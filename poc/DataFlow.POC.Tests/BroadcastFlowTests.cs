namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public class BroadcastFlowTests
{
    /// <summary>
    /// Simple collector actor for integers.
    /// </summary>
    private class IntCollectorActor : IStreamActor<int, object>
    {
        private readonly List<int> _collected;

        public IntCollectorActor(List<int> collected)
        {
            _collected = collected;
        }

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                _collected.Add(item);
            }
            yield break;
        }
    }

    private static async IAsyncEnumerable<int> ProduceIntegers(IExecutionContext ctx, int count)
    {
        for (int i = 1; i <= count; i++)
        {
            yield return i;
        }
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Broadcast_Should_Send_Items_To_Multiple_Processors()
    {
        // Arrange
        var processor1Items = new List<int>();
        var processor2Items = new List<int>();
        
        // Create separate service providers for each processor to have isolated collectors
        var services1 = new ServiceCollection();
        services1.AddScoped(_ => new IntCollectorActor(processor1Items));
        var serviceProvider1 = services1.BuildServiceProvider();

        var services2 = new ServiceCollection();
        services2.AddScoped(_ => new IntCollectorActor(processor2Items));
        var serviceProvider2 = services2.BuildServiceProvider();

        // Use a common service provider for the execution context
        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(ctx, 5));

        var broadcast = new BroadcastBlock<int>("broadcast");

        var processor1 = new ActorBlock<int, object, IntCollectorActor>(
            "processor1",
            serviceProvider1.GetRequiredService<IServiceScopeFactory>());

        var processor2 = new ActorBlock<int, object, IntCollectorActor>(
            "processor2",
            serviceProvider2.GetRequiredService<IServiceScopeFactory>());

        var builder = new DataFlowGraphBuilder("broadcast-flow");
        builder.AddBlock(producer)
            .AddBlock(broadcast)
            .AddBlock(processor1)
            .AddBlock(processor2)
            .Connect(producer, broadcast)
            .Connect(broadcast, processor1) // Broadcast to processor1
            .Connect(broadcast, processor2); // Broadcast to processor2

        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        processor1Items.Count.ShouldBe(5);
        processor2Items.Count.ShouldBe(5);
        processor1Items.ShouldBe(Enumerable.Range(1, 5));
        processor2Items.ShouldBe(Enumerable.Range(1, 5));
    }
}
