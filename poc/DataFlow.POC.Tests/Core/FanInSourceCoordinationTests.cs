namespace DataFlow.POC.Tests.Core;

using DataFlow.POC.Blocks;
using DataFlow.POC.Checkpointing;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

/// <summary>
/// Phase 3: Fan-In Support and Dynamic Sources
/// 
/// Tests that validate fan-in scenarios work correctly because sources
/// already coordinate to get the same epoch object. Unlike traditional
/// stream merging, fan-in is trivial - sources are already coordinated!
/// 
/// Key Insight: Sources using IEpochCoordinator automatically get the
/// same epoch object when they coordinate, eliminating the need for
/// complex merge logic.
/// </summary>
public class FanInSourceCoordinationTests : IAsyncDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IEpochCoordinator _coordinator;

    public FanInSourceCoordinationTests()
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

    /// <summary>
    /// Test 1: Simple two-source merge (verify same epoch)
    /// 
    /// Validates that two sources coordinating via IEpochCoordinator
    /// receive the exact same epoch object (reference equality).
    /// </summary>
    [Fact]
    public async Task TwoSource_FanIn_SameEpochObject()
    {
        // Arrange - create shared coordinator for both sources
        var rootServices = new ServiceCollection();
        rootServices.AddScoped<SharedTestService>();
        rootServices.AddSingleton<IEpochCoordinator>(sp => 
            new EpochCoordinator(sp.GetRequiredService<IServiceScopeFactory>()));
        var rootServiceProvider = rootServices.BuildServiceProvider();

        var services1 = new ServiceCollection();
        services1.AddTransient(sp => new TestSourceActor(
            rootServiceProvider.GetRequiredService<IEpochCoordinator>(), 
            "source1", 
            new[] { 1, 2, 3 }));
        var sp1 = services1.BuildServiceProvider();

        var services2 = new ServiceCollection();
        services2.AddTransient(sp => new TestSourceActor(
            rootServiceProvider.GetRequiredService<IEpochCoordinator>(), 
            "source2", 
            new[] { 4, 5, 6 }));
        var sp2 = services2.BuildServiceProvider();

        var source1Block = new EpochSourceBlock<int, TestSourceActor>(
            "source1",
            sp1.GetRequiredService<IServiceScopeFactory>());

        var source2Block = new EpochSourceBlock<int, TestSourceActor>(
            "source2",
            sp2.GetRequiredService<IServiceScopeFactory>());

        var context = new TestExecutionContext();
        
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Act - execute both sources concurrently
        IEpoch? epoch1 = null;
        IEpoch? epoch2 = null;
        var items1 = new List<int>();
        var items2 = new List<int>();
        
        var task1 = Task.Run(async () =>
        {
            await foreach (var epochStream in source1Block.ExecuteAsync(EmptyInput(), context))
            {
                epoch1 = epochStream.EpochScope;
                await foreach (var item in epochStream.Items)
                {
                    items1.Add(item);
                }
            }
        });

        var task2 = Task.Run(async () =>
        {
            await foreach (var epochStream in source2Block.ExecuteAsync(EmptyInput(), context))
            {
                epoch2 = epochStream.EpochScope;
                await foreach (var item in epochStream.Items)
                {
                    items2.Add(item);
                }
            }
        });

        await Task.WhenAll(task1, task2);

        // Assert
        epoch1.ShouldNotBeNull();
        epoch2.ShouldNotBeNull();
        
        // KEY VALIDATION: Both sources get the SAME epoch object (fan-in is trivial!)
        epoch1.ShouldBeSameAs(epoch2);
        
        // Verify merged vector contains both sources
        var mergedVector = epoch1!.Vector;
        mergedVector.GetSequence("source1").ShouldBe(1);
        mergedVector.GetSequence("source2").ShouldBe(1);
        
        // Both sources produced their items
        items1.ShouldBe(new[] { 1, 2, 3 });
        items2.ShouldBe(new[] { 4, 5, 6 });

        // Cleanup
        await context.DisposeAsync();
        await sp1.DisposeAsync();
        await sp2.DisposeAsync();
        await rootServiceProvider.DisposeAsync();
    }

    /// <summary>
    /// Test 2: Service instance preservation (same DI scope)
    /// 
    /// Validates that services accessed from merged streams come from
    /// the same DI scope (proving epoch sharing at the DI level).
    /// </summary>
    [Fact]
    public async Task TwoSource_FanIn_SharedDIScope()
    {
        // Arrange - create shared coordinator
        var rootServices = new ServiceCollection();
        rootServices.AddScoped<SharedTestService>();
        rootServices.AddSingleton<IEpochCoordinator>(sp => 
            new EpochCoordinator(sp.GetRequiredService<IServiceScopeFactory>()));
        var rootServiceProvider = rootServices.BuildServiceProvider();

        var services1 = new ServiceCollection();
        services1.AddTransient(sp => new TestSourceActor(
            rootServiceProvider.GetRequiredService<IEpochCoordinator>(), 
            "source1", 
            new[] { 10, 20 }));
        var sp1 = services1.BuildServiceProvider();

        var services2 = new ServiceCollection();
        services2.AddTransient(sp => new TestSourceActor(
            rootServiceProvider.GetRequiredService<IEpochCoordinator>(), 
            "source2", 
            new[] { 30, 40 }));
        var sp2 = services2.BuildServiceProvider();

        var source1Block = new EpochSourceBlock<int, TestSourceActor>(
            "source1",
            sp1.GetRequiredService<IServiceScopeFactory>());

        var source2Block = new EpochSourceBlock<int, TestSourceActor>(
            "source2",
            sp2.GetRequiredService<IServiceScopeFactory>());

        var context = new TestExecutionContext();
        
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Act
        SharedTestService? service1 = null;
        SharedTestService? service2 = null;
        
        var task1 = Task.Run(async () =>
        {
            await foreach (var epochStream in source1Block.ExecuteAsync(EmptyInput(), context))
            {
                service1 = epochStream.EpochScope?.GetService<SharedTestService>();
                await foreach (var _ in epochStream.Items) { }
            }
        });

        var task2 = Task.Run(async () =>
        {
            await foreach (var epochStream in source2Block.ExecuteAsync(EmptyInput(), context))
            {
                service2 = epochStream.EpochScope?.GetService<SharedTestService>();
                await foreach (var _ in epochStream.Items) { }
            }
        });

        await Task.WhenAll(task1, task2);

        // Assert
        service1.ShouldNotBeNull();
        service2.ShouldNotBeNull();
        
        // KEY VALIDATION: Services from merged streams share the same instance (same DI scope)
        service1.ShouldBeSameAs(service2);
        service1!.InstanceId.ShouldBe(service2!.InstanceId);

        // Cleanup
        await context.DisposeAsync();
        await sp1.DisposeAsync();
        await sp2.DisposeAsync();
        await rootServiceProvider.DisposeAsync();
    }

    /// <summary>
    /// Test 3: Dynamic source joining (late subsume)
    /// 
    /// Validates that a source can join mid-flow and subsume into the
    /// active epoch without disrupting existing sources.
    /// </summary>
    [Fact]
    public async Task DynamicSource_LateJoin_SubsumesIntoActiveEpoch()
    {
        // Arrange - create shared coordinator
        var rootServices = new ServiceCollection();
        rootServices.AddScoped<SharedTestService>();
        rootServices.AddSingleton<IEpochCoordinator>(sp => 
            new EpochCoordinator(sp.GetRequiredService<IServiceScopeFactory>()));
        var rootServiceProvider = rootServices.BuildServiceProvider();

        // Start with source1
        var services1 = new ServiceCollection();
        services1.AddTransient(sp => new SlowSourceActor(
            rootServiceProvider.GetRequiredService<IEpochCoordinator>(), 
            "source1", 
            new[] { 1, 2, 3 },
            delayMs: 50));
        var sp1 = services1.BuildServiceProvider();

        var source1Block = new EpochSourceBlock<int, SlowSourceActor>(
            "source1",
            sp1.GetRequiredService<IServiceScopeFactory>());

        var context = new TestExecutionContext();
        
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        IEpoch? epoch1 = null;
        IEpoch? epoch2 = null;
        var items1 = new List<int>();
        var items2 = new List<int>();
        
        // Act - start source1 first
        var task1 = Task.Run(async () =>
        {
            await foreach (var epochStream in source1Block.ExecuteAsync(EmptyInput(), context))
            {
                epoch1 = epochStream.EpochScope;
                await foreach (var item in epochStream.Items)
                {
                    items1.Add(item);
                }
            }
        });

        // Wait a bit for source1 to start
        await Task.Delay(25);

        // Now add source2 dynamically (joins mid-flow)
        var services2 = new ServiceCollection();
        services2.AddTransient(sp => new TestSourceActor(
            rootServiceProvider.GetRequiredService<IEpochCoordinator>(), 
            "source2", 
            new[] { 100, 200 }));
        var sp2 = services2.BuildServiceProvider();

        var source2Block = new EpochSourceBlock<int, TestSourceActor>(
            "source2",
            sp2.GetRequiredService<IServiceScopeFactory>());

        var task2 = Task.Run(async () =>
        {
            await foreach (var epochStream in source2Block.ExecuteAsync(EmptyInput(), context))
            {
                epoch2 = epochStream.EpochScope;
                await foreach (var item in epochStream.Items)
                {
                    items2.Add(item);
                }
            }
        });

        await Task.WhenAll(task1, task2);

        // Assert
        epoch1.ShouldNotBeNull();
        epoch2.ShouldNotBeNull();
        
        // KEY VALIDATION: Late joining source2 subsumed into source1's active epoch
        epoch1.ShouldBeSameAs(epoch2);
        
        // Merged vector contains both sources
        var mergedVector = epoch1!.Vector;
        mergedVector.GetSequence("source1").ShouldBe(1);
        mergedVector.GetSequence("source2").ShouldBe(1);

        // Cleanup
        await context.DisposeAsync();
        await sp1.DisposeAsync();
        await sp2.DisposeAsync();
        await rootServiceProvider.DisposeAsync();
    }

    /// <summary>
    /// Test 4: Three-source fan-in (multiple merges)
    /// 
    /// Validates that multiple sources (>2) all coordinate to get
    /// the same epoch object, demonstrating scalability.
    /// </summary>
    [Fact]
    public async Task ThreeSource_FanIn_AllShareSameEpoch()
    {
        // Arrange - create shared coordinator for all sources
        var rootServices = new ServiceCollection();
        rootServices.AddScoped<SharedTestService>();
        rootServices.AddSingleton<IEpochCoordinator>(sp => 
            new EpochCoordinator(sp.GetRequiredService<IServiceScopeFactory>()));
        var rootServiceProvider = rootServices.BuildServiceProvider();

        var services1 = new ServiceCollection();
        services1.AddTransient(sp => new TestSourceActor(
            rootServiceProvider.GetRequiredService<IEpochCoordinator>(), 
            "source1", 
            new[] { 1, 2 }));
        var sp1 = services1.BuildServiceProvider();

        var services2 = new ServiceCollection();
        services2.AddTransient(sp => new TestSourceActor(
            rootServiceProvider.GetRequiredService<IEpochCoordinator>(), 
            "source2", 
            new[] { 3, 4 }));
        var sp2 = services2.BuildServiceProvider();

        var services3 = new ServiceCollection();
        services3.AddTransient(sp => new TestSourceActor(
            rootServiceProvider.GetRequiredService<IEpochCoordinator>(), 
            "source3", 
            new[] { 5, 6 }));
        var sp3 = services3.BuildServiceProvider();

        var source1Block = new EpochSourceBlock<int, TestSourceActor>(
            "source1",
            sp1.GetRequiredService<IServiceScopeFactory>());

        var source2Block = new EpochSourceBlock<int, TestSourceActor>(
            "source2",
            sp2.GetRequiredService<IServiceScopeFactory>());

        var source3Block = new EpochSourceBlock<int, TestSourceActor>(
            "source3",
            sp3.GetRequiredService<IServiceScopeFactory>());

        var context = new TestExecutionContext();
        
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Act - execute all three sources concurrently
        IEpoch? epoch1 = null;
        IEpoch? epoch2 = null;
        IEpoch? epoch3 = null;
        
        var task1 = Task.Run(async () =>
        {
            await foreach (var epochStream in source1Block.ExecuteAsync(EmptyInput(), context))
            {
                epoch1 = epochStream.EpochScope;
                await foreach (var _ in epochStream.Items) { }
            }
        });

        var task2 = Task.Run(async () =>
        {
            await foreach (var epochStream in source2Block.ExecuteAsync(EmptyInput(), context))
            {
                epoch2 = epochStream.EpochScope;
                await foreach (var _ in epochStream.Items) { }
            }
        });

        var task3 = Task.Run(async () =>
        {
            await foreach (var epochStream in source3Block.ExecuteAsync(EmptyInput(), context))
            {
                epoch3 = epochStream.EpochScope;
                await foreach (var _ in epochStream.Items) { }
            }
        });

        await Task.WhenAll(task1, task2, task3);

        // Assert
        epoch1.ShouldNotBeNull();
        epoch2.ShouldNotBeNull();
        epoch3.ShouldNotBeNull();
        
        // KEY VALIDATION: All three sources get the SAME epoch object
        epoch1.ShouldBeSameAs(epoch2);
        epoch2.ShouldBeSameAs(epoch3);
        epoch1.ShouldBeSameAs(epoch3);
        
        // Verify merged vector contains all three sources
        var mergedVector = epoch1!.Vector;
        mergedVector.GetSequence("source1").ShouldBe(1);
        mergedVector.GetSequence("source2").ShouldBe(1);
        mergedVector.GetSequence("source3").ShouldBe(1);

        // Cleanup
        await context.DisposeAsync();
        await sp1.DisposeAsync();
        await sp2.DisposeAsync();
        await sp3.DisposeAsync();
        await rootServiceProvider.DisposeAsync();
    }

    /// <summary>
    /// Test 5: Fast/slow source coordination (bounded growth)
    /// 
    /// Validates that when sources produce at different rates, the
    /// coordination ensures bounded epoch growth (one sequence per source).
    /// </summary>
    [Fact]
    public async Task FastSlowSources_BoundedEpochGrowth()
    {
        // Arrange - create shared coordinator
        var rootServices = new ServiceCollection();
        rootServices.AddScoped<SharedTestService>();
        rootServices.AddSingleton<IEpochCoordinator>(sp => 
            new EpochCoordinator(sp.GetRequiredService<IServiceScopeFactory>()));
        var rootServiceProvider = rootServices.BuildServiceProvider();

        // Fast source - produces quickly
        var services1 = new ServiceCollection();
        services1.AddTransient(sp => new TestSourceActor(
            rootServiceProvider.GetRequiredService<IEpochCoordinator>(), 
            "fast-source", 
            new[] { 1, 2, 3, 4, 5 }));
        var sp1 = services1.BuildServiceProvider();

        // Slow source - produces slowly
        var services2 = new ServiceCollection();
        services2.AddTransient(sp => new SlowSourceActor(
            rootServiceProvider.GetRequiredService<IEpochCoordinator>(), 
            "slow-source", 
            new[] { 10, 20 },
            delayMs: 100));
        var sp2 = services2.BuildServiceProvider();

        var fastBlock = new EpochSourceBlock<int, TestSourceActor>(
            "fast-source",
            sp1.GetRequiredService<IServiceScopeFactory>());

        var slowBlock = new EpochSourceBlock<int, SlowSourceActor>(
            "slow-source",
            sp2.GetRequiredService<IServiceScopeFactory>());

        var context = new TestExecutionContext();
        
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Act
        IEpoch? fastEpoch = null;
        IEpoch? slowEpoch = null;
        
        var task1 = Task.Run(async () =>
        {
            await foreach (var epochStream in fastBlock.ExecuteAsync(EmptyInput(), context))
            {
                fastEpoch = epochStream.EpochScope;
                await foreach (var _ in epochStream.Items) { }
            }
        });

        var task2 = Task.Run(async () =>
        {
            await foreach (var epochStream in slowBlock.ExecuteAsync(EmptyInput(), context))
            {
                slowEpoch = epochStream.EpochScope;
                await foreach (var _ in epochStream.Items) { }
            }
        });

        await Task.WhenAll(task1, task2);

        // Assert
        fastEpoch.ShouldNotBeNull();
        slowEpoch.ShouldNotBeNull();
        
        // Both sources get the same epoch
        fastEpoch.ShouldBeSameAs(slowEpoch);
        
        // KEY VALIDATION: Merged vector shows bounded growth (one sequence per source)
        var mergedVector = fastEpoch!.Vector;
        mergedVector.GetSequence("fast-source").ShouldBe(1);
        mergedVector.GetSequence("slow-source").ShouldBe(1);
        
        // Vector should only contain these two sources (bounded growth)
        mergedVector.Sequences.Count.ShouldBe(2);

        // Cleanup
        await context.DisposeAsync();
        await sp1.DisposeAsync();
        await sp2.DisposeAsync();
        await rootServiceProvider.DisposeAsync();
    }

    /// <summary>
    /// Test 6: Service sharing across merged streams
    /// 
    /// Validates that scoped services are shared across all streams
    /// merged into the same epoch, confirming single DI scope semantics.
    /// </summary>
    [Fact]
    public async Task MergedStreams_ShareScopedServices()
    {
        // Arrange - create shared coordinator
        var rootServices = new ServiceCollection();
        rootServices.AddScoped<SharedTestService>();
        rootServices.AddScoped<AnotherTestService>();
        rootServices.AddSingleton<IEpochCoordinator>(sp => 
            new EpochCoordinator(sp.GetRequiredService<IServiceScopeFactory>()));
        var rootServiceProvider = rootServices.BuildServiceProvider();

        var services1 = new ServiceCollection();
        services1.AddTransient(sp => new TestSourceActor(
            rootServiceProvider.GetRequiredService<IEpochCoordinator>(), 
            "source1", 
            new[] { 1 }));
        var sp1 = services1.BuildServiceProvider();

        var services2 = new ServiceCollection();
        services2.AddTransient(sp => new TestSourceActor(
            rootServiceProvider.GetRequiredService<IEpochCoordinator>(), 
            "source2", 
            new[] { 2 }));
        var sp2 = services2.BuildServiceProvider();

        var source1Block = new EpochSourceBlock<int, TestSourceActor>(
            "source1",
            sp1.GetRequiredService<IServiceScopeFactory>());

        var source2Block = new EpochSourceBlock<int, TestSourceActor>(
            "source2",
            sp2.GetRequiredService<IServiceScopeFactory>());

        var context = new TestExecutionContext();
        
        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Act
        SharedTestService? service1A = null;
        AnotherTestService? service1B = null;
        SharedTestService? service2A = null;
        AnotherTestService? service2B = null;
        
        var task1 = Task.Run(async () =>
        {
            await foreach (var epochStream in source1Block.ExecuteAsync(EmptyInput(), context))
            {
                service1A = epochStream.EpochScope?.GetService<SharedTestService>();
                service1B = epochStream.EpochScope?.GetService<AnotherTestService>();
                await foreach (var _ in epochStream.Items) { }
            }
        });

        var task2 = Task.Run(async () =>
        {
            await foreach (var epochStream in source2Block.ExecuteAsync(EmptyInput(), context))
            {
                service2A = epochStream.EpochScope?.GetService<SharedTestService>();
                service2B = epochStream.EpochScope?.GetService<AnotherTestService>();
                await foreach (var _ in epochStream.Items) { }
            }
        });

        await Task.WhenAll(task1, task2);

        // Assert
        service1A.ShouldNotBeNull();
        service1B.ShouldNotBeNull();
        service2A.ShouldNotBeNull();
        service2B.ShouldNotBeNull();
        
        // KEY VALIDATION: All services from merged streams are the same instances
        service1A.ShouldBeSameAs(service2A);
        service1B.ShouldBeSameAs(service2B);
        
        // Verify instance IDs match
        service1A!.InstanceId.ShouldBe(service2A!.InstanceId);
        service1B!.InstanceId.ShouldBe(service2B!.InstanceId);

        // Cleanup
        await context.DisposeAsync();
        await sp1.DisposeAsync();
        await sp2.DisposeAsync();
        await rootServiceProvider.DisposeAsync();
    }

    // Test Actors

    private class TestSourceActor : SourceActorBase<int>
    {
        private readonly int[] _items;

        public TestSourceActor(IEpochCoordinator coordinator, string sourceId, int[] items)
            : base(coordinator, sourceId)
        {
            _items = items;
        }

        public override async IAsyncEnumerable<IEpochStream<int>> ProduceEpochsAsync(
            IActorExecutionContext context)
        {
            yield return await CreateEpochStreamAsync(
                1,
                ProduceItems(context.CancellationToken),
                context.CancellationToken);
        }

        private async IAsyncEnumerable<int> ProduceItems(CancellationToken cancellationToken)
        {
            foreach (var item in _items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
            }
        }
    }

    private class SlowSourceActor : SourceActorBase<int>
    {
        private readonly int[] _items;
        private readonly int _delayMs;

        public SlowSourceActor(
            IEpochCoordinator coordinator, 
            string sourceId, 
            int[] items,
            int delayMs)
            : base(coordinator, sourceId)
        {
            _items = items;
            _delayMs = delayMs;
        }

        public override async IAsyncEnumerable<IEpochStream<int>> ProduceEpochsAsync(
            IActorExecutionContext context)
        {
            yield return await CreateEpochStreamAsync(
                1,
                ProduceItemsSlowly(context.CancellationToken),
                context.CancellationToken);
        }

        private async IAsyncEnumerable<int> ProduceItemsSlowly(CancellationToken cancellationToken)
        {
            foreach (var item in _items)
            {
                await Task.Delay(_delayMs, cancellationToken);
                yield return item;
            }
        }
    }

    // Test Services for DI scope validation
    
    private class SharedTestService
    {
        public Guid InstanceId { get; } = Guid.NewGuid();
    }

    private class AnotherTestService
    {
        public Guid InstanceId { get; } = Guid.NewGuid();
    }

    // Test Execution Context
    
    private class TestExecutionContext : IExecutionContext, IAsyncDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
        public IServiceProvider ServiceProvider => _serviceProvider;
        public Guid InvocationId { get; } = Guid.NewGuid();
        public ICheckpoint? RecoveryCheckpoint { get; } = null;

        public TestExecutionContext()
        {
            _serviceProvider = new ServiceCollection().BuildServiceProvider();
        }

        public async ValueTask DisposeAsync()
        {
            if (_serviceProvider is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else
            {
                _serviceProvider.Dispose();
            }
        }
    }
}
