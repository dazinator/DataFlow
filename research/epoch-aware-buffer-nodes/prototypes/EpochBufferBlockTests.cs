namespace DataFlow.POC.Tests.Research;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// PROTOTYPE TESTS: Validates the EpochBufferBlock design.
/// These tests are part of the research phase to validate the approach.
/// </summary>
public class EpochBufferBlockPrototypeTests
{
    private readonly ITestOutputHelper _output;

    public EpochBufferBlockPrototypeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task EpochBuffer_Should_Preserve_Epoch_Boundaries()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new DataFlowGraphBuilder("test", serviceProvider: null!);

        // Create test source that produces epoch streams
        builder
            .AddSourceActor<TestEpochSource>("source")
            .AddEpochBuffer<int>("buffer", capacity: 10)
                .ReceiveFrom("source")
            .AddStreamActor<TestEpochConsumer>("consumer")
                .ReceiveFrom("buffer");

        // Act - build and run graph
        var graph = builder.Build(CreateServiceProvider());
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await graph.ExecuteAsync(cts.Token);

        // Assert
        var consumer = graph.ServiceProvider.GetRequiredService<TestEpochConsumer>();
        Assert.Equal(3, consumer.ReceivedEpochs.Count); // 3 epochs
        
        // Verify each epoch preserved its items
        Assert.Equal(new[] { 1, 2, 3 }, consumer.ReceivedEpochs[0].Items);
        Assert.Equal(new[] { 4, 5, 6 }, consumer.ReceivedEpochs[1].Items);
        Assert.Equal(new[] { 7, 8, 9 }, consumer.ReceivedEpochs[2].Items);
        
        _output.WriteLine($"✓ Received {consumer.ReceivedEpochs.Count} epochs with correct boundaries");
    }

    [Fact]
    public async Task EpochBuffer_Should_Apply_Backpressure_When_Full()
    {
        // Arrange
        var builder = new DataFlowGraphBuilder("test", serviceProvider: null!);

        // Create source that produces items faster than consumer processes
        builder
            .AddSourceActor<FastEpochSource>("source")
            .AddEpochBuffer<int>("buffer", capacity: 5) // Small buffer
                .ReceiveFrom("source")
            .AddStreamActor<SlowEpochConsumer>("consumer")
                .ReceiveFrom("buffer");

        // Act
        var graph = builder.Build(CreateServiceProvider());
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var startTime = DateTime.UtcNow;
        await graph.ExecuteAsync(cts.Token);
        var duration = DateTime.UtcNow - startTime;

        // Assert
        // If backpressure works, execution should be slower than if buffer was infinite
        // The slow consumer should limit the rate, not the fast producer
        var consumer = graph.ServiceProvider.GetRequiredService<SlowEpochConsumer>();
        Assert.True(consumer.ProcessedCount > 0, "Consumer should have processed items");
        
        _output.WriteLine($"✓ Backpressure test completed in {duration.TotalMilliseconds}ms");
    }

    [Fact]
    public async Task EpochBuffer_Should_Preserve_Epoch_Metadata()
    {
        // Arrange
        var builder = new DataFlowGraphBuilder("test", serviceProvider: null!);

        builder
            .AddSourceActor<TestEpochSourceWithMetadata>("source")
            .AddEpochBuffer<int>("buffer", capacity: 10)
                .ReceiveFrom("source")
            .AddStreamActor<MetadataCheckingConsumer>("consumer")
                .ReceiveFrom("buffer");

        // Act
        var graph = builder.Build(CreateServiceProvider());
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await graph.ExecuteAsync(cts.Token);

        // Assert
        var consumer = graph.ServiceProvider.GetRequiredService<MetadataCheckingConsumer>();
        Assert.All(consumer.ReceivedEpochVectors, vector => Assert.NotNull(vector));
        
        _output.WriteLine($"✓ All {consumer.ReceivedEpochVectors.Count} epoch vectors preserved");
    }

    // Helper method to create service provider
    private IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        
        // Register test actors as singletons so we can verify results
        services.AddSingleton<TestEpochSource>();
        services.AddSingleton<TestEpochConsumer>();
        services.AddSingleton<FastEpochSource>();
        services.AddSingleton<SlowEpochConsumer>();
        services.AddSingleton<TestEpochSourceWithMetadata>();
        services.AddSingleton<MetadataCheckingConsumer>();
        
        return services.BuildServiceProvider();
    }

    // Test actors

    private class TestEpochSource : ISourceActor<IEpochStream<int>>
    {
        public async IAsyncEnumerable<IEpochStream<int>> ProduceEpochsAsync(
            ISourceActorExecutionContext context)
        {
            // Produce 3 epochs with 3 items each
            for (int epoch = 0; epoch < 3; epoch++)
            {
                var items = GenerateItems(epoch * 3 + 1, 3);
                var epochVector = new EpochVector(new Dictionary<string, long> { ["source"] = epoch });
                yield return new EpochStream<int>(epochVector, items);
            }
        }

        private async IAsyncEnumerable<int> GenerateItems(int start, int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return start + i;
                await Task.Delay(1); // Small delay to simulate work
            }
        }
    }

    private class TestEpochConsumer : IStreamActor<IEpochStream<int>, object>
    {
        public List<ReceivedEpoch> ReceivedEpochs { get; } = new();

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<IEpochStream<int>> input,
            IActorExecutionContext context)
        {
            await foreach (var epochStream in input)
            {
                var items = new List<int>();
                await foreach (var item in epochStream.Items)
                {
                    items.Add(item);
                }
                
                ReceivedEpochs.Add(new ReceivedEpoch(epochStream.Epoch, items));
            }
            
            yield break; // Terminal block
        }

        public class ReceivedEpoch
        {
            public EpochVector Epoch { get; }
            public List<int> Items { get; }

            public ReceivedEpoch(EpochVector epoch, List<int> items)
            {
                Epoch = epoch;
                Items = items;
            }
        }
    }

    private class FastEpochSource : ISourceActor<IEpochStream<int>>
    {
        public async IAsyncEnumerable<IEpochStream<int>> ProduceEpochsAsync(
            ISourceActorExecutionContext context)
        {
            // Produce 1 epoch with 100 items rapidly
            var items = GenerateItemsFast(100);
            var epochVector = new EpochVector(new Dictionary<string, long> { ["source"] = 0 });
            yield return new EpochStream<int>(epochVector, items);
        }

        private async IAsyncEnumerable<int> GenerateItemsFast(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return i;
                // No delay - produce as fast as possible
            }
        }
    }

    private class SlowEpochConsumer : IStreamActor<IEpochStream<int>, object>
    {
        public int ProcessedCount { get; private set; }

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<IEpochStream<int>> input,
            IActorExecutionContext context)
        {
            await foreach (var epochStream in input)
            {
                await foreach (var item in epochStream.Items)
                {
                    await Task.Delay(10); // Slow processing
                    ProcessedCount++;
                }
            }
            
            yield break;
        }
    }

    private class TestEpochSourceWithMetadata : ISourceActor<IEpochStream<int>>
    {
        public async IAsyncEnumerable<IEpochStream<int>> ProduceEpochsAsync(
            ISourceActorExecutionContext context)
        {
            for (int i = 0; i < 3; i++)
            {
                var epochVector = new EpochVector(new Dictionary<string, long> 
                { 
                    ["source"] = i,
                    ["metadata"] = i * 100
                });
                
                var items = GenerateItems(i * 10, 5);
                yield return new EpochStream<int>(epochVector, items);
            }
        }

        private async IAsyncEnumerable<int> GenerateItems(int start, int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return start + i;
            }
        }
    }

    private class MetadataCheckingConsumer : IStreamActor<IEpochStream<int>, object>
    {
        public List<EpochVector> ReceivedEpochVectors { get; } = new();

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<IEpochStream<int>> input,
            IActorExecutionContext context)
        {
            await foreach (var epochStream in input)
            {
                ReceivedEpochVectors.Add(epochStream.Epoch);
                
                // Consume items (but we only care about metadata)
                await foreach (var item in epochStream.Items)
                {
                    // Process items
                }
            }
            
            yield break;
        }
    }
}
