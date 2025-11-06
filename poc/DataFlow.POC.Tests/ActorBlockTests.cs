namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public class ActorBlockTests
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

    /// <summary>
    /// Simple test actor that counts processed items.
    /// </summary>
    private class CountingActor : IStreamActor<int, int>
    {
        public int ProcessedCount { get; private set; }

        public async IAsyncEnumerable<int> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                ProcessedCount++;
                yield return item;
            }
        }
    }

    /// <summary>
    /// Actor that requests rotation after processing a certain number of items.
    /// </summary>
    private class RotatingActor : IStreamActor<int, int>
    {
        private readonly int _rotateAfter;
        private int _count;

        public RotatingActor(int rotateAfter = 10)
        {
            _rotateAfter = rotateAfter;
        }

        public int ProcessedCount => _count;

        public async IAsyncEnumerable<int> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                _count++;
                yield return item;

                if (_count >= _rotateAfter)
                {
                    context.RequestRotation();
                    yield break;
                }
            }
        }
    }

    /// <summary>
    /// Actor that tracks how many times it has been created (via static counter).
    /// </summary>
    private class InstanceTrackingActor : IStreamActor<int, int>
    {
        private static int _instanceCounter;
        private readonly int _instanceId;
        private int _count;

        public InstanceTrackingActor()
        {
            _instanceId = Interlocked.Increment(ref _instanceCounter);
        }

        public int InstanceId => _instanceId;
        public int ProcessedCount => _count;

        public static void ResetCounter() => _instanceCounter = 0;

        public async IAsyncEnumerable<int> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                _count++;
                yield return item * _instanceId; // Multiply by instance ID for verification

                if (_count >= 5)
                {
                    context.RequestRotation();
                    yield break;
                }
            }
        }
    }

    /// <summary>
    /// Actor that uses a scoped service to verify DI scope rotation.
    /// </summary>
    private class ScopedServiceActor : IStreamActor<int, string>
    {
        private readonly ScopedTestService _scopedService;
        private int _count;

        public ScopedServiceActor(ScopedTestService scopedService)
        {
            _scopedService = scopedService ?? throw new ArgumentNullException(nameof(scopedService));
        }

        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                _count++;
                yield return $"{item}-{_scopedService.InstanceId}";

                if (_count >= 3)
                {
                    context.RequestRotation();
                    yield break;
                }
            }
        }
    }

    /// <summary>
    /// Test scoped service to verify DI scope rotation.
    /// </summary>
    private class ScopedTestService
    {
        private static int _instanceCounter;

        public ScopedTestService()
        {
            InstanceId = Interlocked.Increment(ref _instanceCounter);
        }

        public int InstanceId { get; }

        public static void ResetCounter() => _instanceCounter = 0;
    }

    private static async IAsyncEnumerable<int> ProduceIntegers(int count)
    {
        for (int i = 1; i <= count; i++)
        {
            yield return i;
        }
    }

    [Fact]
    public async Task ActorBlock_Should_Process_All_Items_Without_Rotation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<CountingActor>();
        var serviceProvider = services.BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var actorBlock = new ActorBlock<int, int, CountingActor>("actor", scopeFactory);
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);

        var input = ProduceIntegers(10);

        // Act
        var results = new List<int>();
        await foreach (var item in actorBlock.ExecuteAsync(input, context))
        {
            results.Add(item);
        }

        // Assert
        results.Count.ShouldBe(10);
        results.ShouldBe(Enumerable.Range(1, 10));
    }

    [Fact]
    public async Task ActorBlock_Should_Rotate_Actor_When_Requested()
    {
        // Arrange
        InstanceTrackingActor.ResetCounter();

        var services = new ServiceCollection();
        services.AddTransient<InstanceTrackingActor>();
        var serviceProvider = services.BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var actorBlock = new ActorBlock<int, int, InstanceTrackingActor>("actor", scopeFactory);
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);

        var input = ProduceIntegers(15);

        // Act
        var results = new List<int>();
        await foreach (var item in actorBlock.ExecuteAsync(input, context))
        {
            results.Add(item);
        }

        // Assert
        results.Count.ShouldBe(15);
        
        // First 5 items should be multiplied by 1 (first instance)
        results.Take(5).ShouldBe(new[] { 1, 2, 3, 4, 5 });
        
        // Next 5 items should be multiplied by 2 (second instance after rotation)
        results.Skip(5).Take(5).ShouldBe(new[] { 12, 14, 16, 18, 20 }); // 6*2, 7*2, 8*2, 9*2, 10*2
        
        // Last 5 items should be multiplied by 3 (third instance after rotation)
        results.Skip(10).Take(5).ShouldBe(new[] { 33, 36, 39, 42, 45 }); // 11*3, 12*3, 13*3, 14*3, 15*3
    }

    [Fact]
    public async Task ActorBlock_Should_Create_New_DI_Scope_On_Rotation()
    {
        // Arrange
        ScopedTestService.ResetCounter();

        var services = new ServiceCollection();
        services.AddTransient<ScopedServiceActor>();
        services.AddScoped<ScopedTestService>();
        var serviceProvider = services.BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var actorBlock = new ActorBlock<int, string, ScopedServiceActor>("actor", scopeFactory);
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);

        var input = ProduceIntegers(9);

        // Act
        var results = new List<string>();
        await foreach (var item in actorBlock.ExecuteAsync(input, context))
        {
            results.Add(item);
        }

        // Assert
        results.Count.ShouldBe(9);
        
        // First 3 items should have scope instance 1
        results.Take(3).ShouldBe(new[] { "1-1", "2-1", "3-1" });
        
        // Next 3 items should have scope instance 2 (after rotation)
        results.Skip(3).Take(3).ShouldBe(new[] { "4-2", "5-2", "6-2" });
        
        // Last 3 items should have scope instance 3 (after rotation)
        results.Skip(6).Take(3).ShouldBe(new[] { "7-3", "8-3", "9-3" });
    }

    [Fact]
    public async Task ActorBlock_Should_Respect_Cancellation_Token()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<CountingActor>();
        var serviceProvider = services.BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var actorBlock = new ActorBlock<int, int, CountingActor>("actor", scopeFactory);

        using var cts = new CancellationTokenSource();
        var context = new ExecutionContext(serviceProvider, cts.Token);

        var input = ProduceIntegers(100);

        // Act & Assert
        var results = new List<int>();
        var processedCount = 0;
        
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await foreach (var item in actorBlock.ExecuteAsync(input, context))
            {
                results.Add(item);
                processedCount++;
                
                if (processedCount == 5)
                {
                    cts.Cancel();
                }
            }
        });

        // Verify we processed 5 items before cancellation
        results.Count.ShouldBe(5);
    }

    [Fact]
    public async Task ActorBlock_Should_Work_In_DataFlow_Pipeline()
    {
        // Arrange
        InstanceTrackingActor.ResetCounter();

        var services = new ServiceCollection();
        services.AddTransient<InstanceTrackingActor>();
        var serviceProvider = services.BuildServiceProvider();

        var processedItems = new List<int>();
        var processorServices = new ServiceCollection();
        processorServices.AddScoped(_ => new IntCollectorActor(processedItems));
        var processorServiceProvider = processorServices.BuildServiceProvider();
        
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var producer = new ProducerBlock<int>("producer", ctx => ProduceIntegers(12));
        var actorBlock = new ActorBlock<int, int, InstanceTrackingActor>("actor", scopeFactory);
        var processor = new ActorBlock<int, object, IntCollectorActor>(
            "processor",
            processorServiceProvider.GetRequiredService<IServiceScopeFactory>());

        var builder = new DataFlowGraphBuilder("actor-flow");
        builder.AddBlock(producer)
            .AddBlock(actorBlock)
            .Connect(producer, actorBlock)
            .AddBlock(processor)
            .Connect(actorBlock, processor);

        var graph = builder.Build();
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        processedItems.Count.ShouldBe(12);
        
        // First 5 items from instance 1, next 5 from instance 2, last 2 from instance 3
        processedItems.Take(5).ShouldBe(new[] { 1, 2, 3, 4, 5 });
        processedItems.Skip(5).Take(5).ShouldBe(new[] { 12, 14, 16, 18, 20 });
        processedItems.Skip(10).Take(2).ShouldBe(new[] { 33, 36 });
    }

    [Fact]
    public async Task ActorBlock_Should_Handle_Empty_Input_Stream()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<CountingActor>();
        var serviceProvider = services.BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var actorBlock = new ActorBlock<int, int, CountingActor>("actor", scopeFactory);
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);

        var input = ProduceIntegers(0);

        // Act
        var results = new List<int>();
        await foreach (var item in actorBlock.ExecuteAsync(input, context))
        {
            results.Add(item);
        }

        // Assert
        results.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ActorBlock_Should_Handle_Actor_That_Never_Rotates()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<CountingActor>();
        var serviceProvider = services.BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var actorBlock = new ActorBlock<int, int, CountingActor>("actor", scopeFactory);
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);

        var input = ProduceIntegers(100);

        // Act
        var results = new List<int>();
        await foreach (var item in actorBlock.ExecuteAsync(input, context))
        {
            results.Add(item);
        }

        // Assert
        results.Count.ShouldBe(100);
        results.ShouldBe(Enumerable.Range(1, 100));
    }

    [Fact]
    public async Task ActorBlock_Should_Handle_Rotation_At_Stream_End()
    {
        // Arrange
        InstanceTrackingActor.ResetCounter();

        var services = new ServiceCollection();
        services.AddTransient<InstanceTrackingActor>();
        var serviceProvider = services.BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var actorBlock = new ActorBlock<int, int, InstanceTrackingActor>("actor", scopeFactory);
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);

        // Actor will process 5 items before rotating, but input only has 5 items
        // So rotation is requested on last item, but no second actor is needed
        var input = ProduceIntegers(5);

        // Act
        var results = new List<int>();
        await foreach (var item in actorBlock.ExecuteAsync(input, context))
        {
            results.Add(item);
        }

        // Assert
        results.Count.ShouldBe(5);
        results.ShouldBe(new[] { 1, 2, 3, 4, 5 }); // All from first instance
    }
}
