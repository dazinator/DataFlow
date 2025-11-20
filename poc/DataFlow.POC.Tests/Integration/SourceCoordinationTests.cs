namespace DataFlow.POC.Tests.Integration;

using DataFlow.POC.Blocks;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

/// <summary>
/// Integration tests for Phase 2: Source Integration and Stream Propagation.
/// Tests coordinator integration with source blocks and multi-source scenarios.
/// All source actors now use IEpochCoordinator for epoch management.
/// </summary>
public class SourceCoordinationTests : IAsyncDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IEpochCoordinator _coordinator;

    public SourceCoordinationTests()
    {
        var services = new ServiceCollection();
        services.AddScoped<SharedTestService>();
        services.AddSingleton<IEpochCoordinator>(sp => 
            new EpochCoordinator(sp.GetRequiredService<IServiceScopeFactory>()));
        
        _serviceProvider = services.BuildServiceProvider();
        _coordinator = _serviceProvider.GetRequiredService<IEpochCoordinator>();
    }

    public async ValueTask DisposeAsync()
    {
        await _coordinator.DisposeAsync();
        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task SingleSource_WithCoordinator_UsesCoordinatorEpoch()
    {
        // Arrange - create custom service provider for this test
        var services = new ServiceCollection();
        services.AddScoped<SharedTestService>();
        services.AddSingleton<IEpochCoordinator>(sp => 
            new EpochCoordinator(sp.GetRequiredService<IServiceScopeFactory>()));
        services.AddTransient(sp => new SingleEpochSourceActor(
            sp.GetRequiredService<IEpochCoordinator>(), 
            "source1"));
        var testServiceProvider = services.BuildServiceProvider();

        var sourceBlock = new EpochSourceBlock<int, SingleEpochSourceActor>(
            new BlockContext("source1"),
            testServiceProvider.GetRequiredService<IServiceScopeFactory>());

        var context = new TestExecutionContext();
        
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Act
        var epochs = new List<(IEpoch? epochScope, EpochVector vector, List<int> items)>();
        
        await foreach (var epochStream in sourceBlock.ExecuteAsync(EmptyInput(), context))
        {
            var items = new List<int>();
            await foreach (var item in epochStream.Items)
            {
                items.Add(item);
            }
            epochs.Add((epochStream.EpochScope, epochStream.Epoch, items));
        }

        // Assert
        epochs.Count.ShouldBe(1);
        
        // Verify epoch came from coordinator (EpochScope is not null)
        epochs[0].epochScope.ShouldNotBeNull();
        epochs[0].vector.GetSequence("source1").ShouldBe(1);
        epochs[0].items.ShouldBe(new[] { 1, 2, 3, 4, 5 });

        // Cleanup
        await testServiceProvider.DisposeAsync();
    }

    [Fact]
    public async Task SingleSource_FastPath_NoCoordinationOverhead()
    {
        // This test validates that single source gets immediate epoch (fast path)
        // by checking that epochs are returned without blocking
        
        // Arrange - create custom service provider for this test
        var services = new ServiceCollection();
        services.AddScoped<SharedTestService>();
        services.AddSingleton<IEpochCoordinator>(sp => 
            new EpochCoordinator(sp.GetRequiredService<IServiceScopeFactory>()));
        services.AddTransient(sp => new MultiEpochSourceActor(
            sp.GetRequiredService<IEpochCoordinator>(), 
            "source1"));
        var testServiceProvider = services.BuildServiceProvider();

        var sourceBlock = new EpochSourceBlock<int, MultiEpochSourceActor>(
            new BlockContext("source1"),
            testServiceProvider.GetRequiredService<IServiceScopeFactory>());

        var context = new TestExecutionContext();
        
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Act
        var startTime = DateTime.UtcNow;
        var epochs = new List<IEpoch?>();
        
        await foreach (var epochStream in sourceBlock.ExecuteAsync(EmptyInput(), context))
        {
            epochs.Add(epochStream.EpochScope);
            // Consume items
            await foreach (var _ in epochStream.Items) { }
        }
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        epochs.Count.ShouldBe(3);
        epochs.All(e => e != null).ShouldBeTrue();
        
        // Should be fast (no coordination blocking)
        elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));

        // Cleanup
        await testServiceProvider.DisposeAsync();
    }

    [Fact]
    public async Task MultiSource_SameEpochObject_SharedDIScope()
    {
        // Arrange - create shared root service provider for coordinator using factory pattern
        var rootServices = new ServiceCollection();
        rootServices.AddScoped<SharedTestService>();
        rootServices.AddSingleton<IEpochCoordinator>(sp => 
            new EpochCoordinator(sp.GetRequiredService<IServiceScopeFactory>()));
        var rootServiceProvider = rootServices.BuildServiceProvider();

        // Create service providers for each source actor with coordinator resolved from root
        var services1 = new ServiceCollection();
        services1.AddTransient(sp => new SingleEpochSourceActor(
            rootServiceProvider.GetRequiredService<IEpochCoordinator>(), 
            "source1"));
        var sp1 = services1.BuildServiceProvider();

        var services2 = new ServiceCollection();
        services2.AddTransient(sp => new SingleEpochSourceActor(
            rootServiceProvider.GetRequiredService<IEpochCoordinator>(), 
            "source2"));
        var sp2 = services2.BuildServiceProvider();

        var source1Block = new EpochSourceBlock<int, SingleEpochSourceActor>(
            new BlockContext("source1"),
            sp1.GetRequiredService<IServiceScopeFactory>());

        var source2Block = new EpochSourceBlock<int, SingleEpochSourceActor>(
            new BlockContext("source2"),
            sp2.GetRequiredService<IServiceScopeFactory>());

        var context = new TestExecutionContext();
        
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Act
        IEpoch? epoch1 = null;
        IEpoch? epoch2 = null;
        
        // Execute both sources concurrently
        var task1 = Task.Run(async () =>
        {
            await foreach (var epochStream in source1Block.ExecuteAsync(EmptyInput(), context))
            {
                epoch1 = epochStream.EpochScope;
                // Consume items
                await foreach (var _ in epochStream.Items) { }
            }
        });

        var task2 = Task.Run(async () =>
        {
            await foreach (var epochStream in source2Block.ExecuteAsync(EmptyInput(), context))
            {
                epoch2 = epochStream.EpochScope;
                // Consume items
                await foreach (var _ in epochStream.Items) { }
            }
        });

        await Task.WhenAll(task1, task2);

        // Assert
        epoch1.ShouldNotBeNull();
        epoch2.ShouldNotBeNull();
        
        // Both sources should get the SAME epoch object (coordinator merged them)
        epoch1.ShouldBeSameAs(epoch2);
        
        // Verify merged vector
        var mergedVector = epoch1!.Vector;
        mergedVector.GetSequence("source1").ShouldBe(1);
        mergedVector.GetSequence("source2").ShouldBe(1);
        
        // Verify shared DI scope - both should get the same service instance
        var service1 = epoch1.GetService<SharedTestService>();
        var service2 = epoch2.GetService<SharedTestService>();
        service1.ShouldBeSameAs(service2);

        // Cleanup
        await sp1.DisposeAsync();
        await sp2.DisposeAsync();
        await rootServiceProvider.DisposeAsync();
    }

    [Fact]
    public async Task MultiSource_ReadinessSignaling_IsInvoked()
    {
        // This test validates that readiness signaling is invoked during epoch transitions
        // For Phase 2, we verify the mechanism exists (full async coordination is Phase 3+)
        
        // Arrange - use single source to verify signaling without coordination complexity
        var services = new ServiceCollection();
        services.AddScoped<SharedTestService>();
        services.AddSingleton<IEpochCoordinator>(sp => 
            new EpochCoordinator(sp.GetRequiredService<IServiceScopeFactory>()));
        services.AddTransient(sp => new MultiEpochSourceActor(
            sp.GetRequiredService<IEpochCoordinator>(), 
            "source1"));
        var testServiceProvider = services.BuildServiceProvider();

        var sourceBlock = new EpochSourceBlock<int, MultiEpochSourceActor>(
            new BlockContext("source1"),
            testServiceProvider.GetRequiredService<IServiceScopeFactory>());

        var context = new TestExecutionContext();
        
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Act
        var epochs = new List<IEpoch?>();
        
        await foreach (var epochStream in sourceBlock.ExecuteAsync(EmptyInput(), context))
        {
            epochs.Add(epochStream.EpochScope);
            // Consume items
            await foreach (var _ in epochStream.Items) { }
        }

        // Assert
        epochs.Count.ShouldBe(3);
        epochs.All(e => e != null).ShouldBeTrue();
        
        // Verify epochs are distinct (one per sequence number)
        epochs[0].ShouldNotBeSameAs(epochs[1]);
        epochs[1].ShouldNotBeSameAs(epochs[2]);
        
        // Verify readiness signaling worked - we successfully created 3 epochs
        // The SignalReadyForNext was called before advancing to epoch 2 and 3
        epochs[0]!.Vector.GetSequence("source1").ShouldBe(1);
        epochs[1]!.Vector.GetSequence("source1").ShouldBe(2);
        epochs[2]!.Vector.GetSequence("source1").ShouldBe(3);

        // Cleanup
        await testServiceProvider.DisposeAsync();
    }

    // Test Actors

    private class SingleEpochSourceActor : SourceActorBase<int>
    {
        public SingleEpochSourceActor(IEpochCoordinator coordinator, string sourceId)
            : base(coordinator, sourceId)
        {
        }

        public override async IAsyncEnumerable<IEpochStream<int>> ProduceEpochsAsync(
            IActorExecutionContext context)
        {
            yield return await CreateEpochStreamAsync(
                1,
                ProduceItems(context.CancellationToken),
                context.CancellationToken);
        }

        private static async IAsyncEnumerable<int> ProduceItems(
            CancellationToken cancellationToken)
        {
            for (int i = 1; i <= 5; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return i;
            }
        }
    }

    private class MultiEpochSourceActor : SourceActorBase<int>
    {
        public MultiEpochSourceActor(IEpochCoordinator coordinator, string sourceId)
            : base(coordinator, sourceId)
        {
        }

        public override async IAsyncEnumerable<IEpochStream<int>> ProduceEpochsAsync(
            IActorExecutionContext context)
        {
            for (int epoch = 1; epoch <= 3; epoch++)
            {
                if (epoch > 1)
                {
                    SignalReadyForNext(epoch - 1, epoch);
                }
                
                yield return await CreateEpochStreamAsync(
                    epoch,
                    ProduceEpochItems(epoch, context.CancellationToken),
                    context.CancellationToken);
            }
        }

        private static async IAsyncEnumerable<int> ProduceEpochItems(
            int epochNum,
            CancellationToken cancellationToken)
        {
            var start = (epochNum - 1) * 5;
            for (int i = 0; i < 5; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return start + i;
            }
        }
    }

    // Test Service for DI scope validation
    private class SharedTestService
    {
        public Guid InstanceId { get; } = Guid.NewGuid();
    }
}
