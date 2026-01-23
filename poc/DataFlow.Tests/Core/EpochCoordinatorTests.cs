namespace DataFlow.POC.Tests.Core;

using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DataFlow.POC.Registry;

/// <summary>
/// Tests for source-level epoch coordination approach.
/// Validates key scenarios: single source, multi-source, fan-in, bounded growth, and DI scope sharing.
/// </summary>
public class EpochCoordinatorTests : IAsyncDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly EpochCoordinator _coordinator;

    public EpochCoordinatorTests()
    {
        var services = new ServiceCollection();
        services.AddScoped<TestService>();
        _serviceProvider = services.BuildServiceProvider();
        
        _coordinator = new EpochCoordinator(_serviceProvider.GetRequiredService<IServiceScopeFactory>());
    }

    public async ValueTask DisposeAsync()
    {
        await _coordinator.DisposeAsync();
        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task SingleSource_NoCoordinationOverhead()
    {
        // Arrange
        var sourceA = "sourceA";
        var vector1 = EpochVector.FromSingleSource(sourceA, 1);
        var vector2 = EpochVector.FromSingleSource(sourceA, 2);

        // Act - Single source should get epochs immediately (fast path)
        var epoch1 = await _coordinator.GetOrCreateEpochAsync(sourceA, vector1);
        var epoch2 = await _coordinator.GetOrCreateEpochAsync(sourceA, vector2);

        // Assert
        Assert.NotNull(epoch1);
        Assert.NotNull(epoch2);
        Assert.NotSame(epoch1, epoch2); // Different epochs for different sequences
        Assert.Equal(vector1, epoch1.Vector);
        Assert.Equal(vector2, epoch2.Vector);
    }

    [Fact]
    public async Task SingleSource_SameVector_ReturnsSameEpoch()
    {
        // Arrange
        var sourceA = "sourceA";
        var vector1 = EpochVector.FromSingleSource(sourceA, 1);

        // Act - Request same vector multiple times
        var epoch1 = await _coordinator.GetOrCreateEpochAsync(sourceA, vector1);
        var epoch2 = await _coordinator.GetOrCreateEpochAsync(sourceA, vector1);

        // Assert - Should get the same epoch instance
        Assert.Same(epoch1, epoch2);
    }

    [Fact]
    public async Task MultiSource_SameEpochObject_WhenSubsumed()
    {
        // Arrange
        var sourceA = "sourceA";
        var sourceB = "sourceB";
        var vectorA = EpochVector.FromSingleSource(sourceA, 1);
        var vectorB = EpochVector.FromSingleSource(sourceB, 1);

        // Act
        var epochA = await _coordinator.GetOrCreateEpochAsync(sourceA, vectorA);
        var epochB = await _coordinator.GetOrCreateEpochAsync(sourceB, vectorB);

        // Assert - Both sources get SAME epoch object
        Assert.Same(epochA, epochB);
        
        // Epoch vector should be merged
        var mergedVector = vectorA.Merge(vectorB);
        Assert.Equal(mergedVector, epochB.Vector);
    }

    [Fact]
    public async Task MultiSource_SameScope_SharedServices()
    {
        // Arrange
        var sourceA = "sourceA";
        var sourceB = "sourceB";
        var vectorA = EpochVector.FromSingleSource(sourceA, 1);
        var vectorB = EpochVector.FromSingleSource(sourceB, 1);

        // Act
        var epochA = await _coordinator.GetOrCreateEpochAsync(sourceA, vectorA);
        var epochB = await _coordinator.GetOrCreateEpochAsync(sourceB, vectorB);

        var serviceA = epochA.GetService<TestService>();
        var serviceB = epochB.GetService<TestService>();

        // Assert - Same epoch = same DI scope = same service instance
        Assert.Same(serviceA, serviceB);
    }

    [Fact]
    public async Task MultiSource_BoundedGrowth_WaitsWithoutReadiness()
    {
        // Arrange
        var sourceA = "sourceA";
        var sourceB = "sourceB";
        
        // Both sources start with epoch 1
        var vectorA1 = EpochVector.FromSingleSource(sourceA, 1);
        var vectorB1 = EpochVector.FromSingleSource(sourceB, 1);
        
        var epoch1A = await _coordinator.GetOrCreateEpochAsync(sourceA, vectorA1);
        var epoch1B = await _coordinator.GetOrCreateEpochAsync(sourceB, vectorB1);
        
        Assert.Same(epoch1A, epoch1B); // Same epoch for sequence 1

        // Act - Source A tries to advance to epoch 2 without Source B ready
        var vectorA2 = EpochVector.FromSingleSource(sourceA, 2);
        
        // Assert - Should wait (not complete immediately) because Source B not ready
        var getEpochTask = _coordinator.GetOrCreateEpochAsync(sourceA, vectorA2).AsTask();
        
        await Task.Delay(100); // Give it time to potentially complete
        Assert.False(getEpochTask.IsCompleted, "Source A should be waiting for Source B");
        
        // Now have Source B signal readiness and advance - this should unblock Source A
        var vectorB2 = EpochVector.FromSingleSource(sourceB, 2);
        _coordinator.SignalReadyForNext(sourceB, vectorB1, vectorB2);
        _coordinator.SignalReadyForNext(sourceA, vectorA1, vectorA2);
        
        // Now Source A should complete
        var epoch2A = await getEpochTask;
        Assert.NotNull(epoch2A);
    }

    [Fact]
    public async Task MultiSource_ReadinessSignaling_AllowsAdvancement()
    {
        // Arrange
        var sourceA = "sourceA";
        var sourceB = "sourceB";
        
        // Both sources start with epoch 1
        var vectorA1 = EpochVector.FromSingleSource(sourceA, 1);
        var vectorB1 = EpochVector.FromSingleSource(sourceB, 1);
        
        await _coordinator.GetOrCreateEpochAsync(sourceA, vectorA1);
        await _coordinator.GetOrCreateEpochAsync(sourceB, vectorB1);

        // Act - Both sources signal readiness for epoch 2
        var vectorA2 = EpochVector.FromSingleSource(sourceA, 2);
        var vectorB2 = EpochVector.FromSingleSource(sourceB, 2);
        
        _coordinator.SignalReadyForNext(sourceA, vectorA1, vectorA2);
        _coordinator.SignalReadyForNext(sourceB, vectorB1, vectorB2);
        
        // Now both sources can advance
        var epoch2A = await _coordinator.GetOrCreateEpochAsync(sourceA, vectorA2);
        var epoch2B = await _coordinator.GetOrCreateEpochAsync(sourceB, vectorB2);

        // Assert - Both get same NEW epoch (different from epoch 1)
        Assert.Same(epoch2A, epoch2B);
        Assert.NotNull(epoch2A);
        
        var mergedVector2 = vectorA2.Merge(vectorB2);
        Assert.Equal(mergedVector2, epoch2A.Vector);
    }

    [Fact]
    public async Task FanIn_StreamsCarrySameEpoch_NoMergingNeeded()
    {
        // Arrange - Simulate two sources creating streams
        var sourceA = "sourceA";
        var sourceB = "sourceB";
        var vectorA = EpochVector.FromSingleSource(sourceA, 1);
        var vectorB = EpochVector.FromSingleSource(sourceB, 1);

        var epochA = await _coordinator.GetOrCreateEpochAsync(sourceA, vectorA);
        var epochB = await _coordinator.GetOrCreateEpochAsync(sourceB, vectorB);

        // Both sources get same epoch
        Assert.Same(epochA, epochB);

        // Act - Create streams that carry this epoch
        var streamA = new EpochStream<int>(epochA, CreateItems(1, 2, 3));
        var streamB = new EpochStream<int>(epochB, CreateItems(4, 5, 6));

        // Simulate fan-in (BufferNode merging streams)
        var mergedEpoch = streamA.EpochScope; // Could use either - they're the same!
        
        // Assert - No scope merging problem!
        Assert.Same(streamA.EpochScope, streamB.EpochScope);
        Assert.Same(streamA.EpochScope!.ServiceProvider, streamB.EpochScope!.ServiceProvider);
        
        // Services resolved from merged streams are from same scope
        var serviceFromA = streamA.EpochScope!.GetService<TestService>();
        var serviceFromB = streamB.EpochScope!.GetService<TestService>();
        Assert.Same(serviceFromA, serviceFromB);
    }

    [Fact]
    public async Task DynamicSource_JoiningMidFlow_SubsumesIntoActiveEpoch()
    {
        // Arrange - Source A starts first
        var sourceA = "sourceA";
        var vectorA = EpochVector.FromSingleSource(sourceA, 1);
        var epochA = await _coordinator.GetOrCreateEpochAsync(sourceA, vectorA);

        // Act - Source B joins later with its own vector
        var sourceB = "sourceB";
        var vectorB = EpochVector.FromSingleSource(sourceB, 1);
        var epochB = await _coordinator.GetOrCreateEpochAsync(sourceB, vectorB);

        // Assert - Source B gets SAME epoch as A (subsumed)
        Assert.Same(epochA, epochB);
        
        // Epoch vector expanded to include both sources
        var mergedVector = vectorA.Merge(vectorB);
        Assert.Equal(mergedVector, epochB.Vector);
    }

    [Fact]
    public async Task ThreeSources_CoordinatedAdvancement()
    {
        // Arrange - Three sources
        var sourceA = "sourceA";
        var sourceB = "sourceB";
        var sourceC = "sourceC";
        
        var vectorA1 = EpochVector.FromSingleSource(sourceA, 1);
        var vectorB1 = EpochVector.FromSingleSource(sourceB, 1);
        var vectorC1 = EpochVector.FromSingleSource(sourceC, 1);
        
        // All sources get epoch 1
        var epoch1A = await _coordinator.GetOrCreateEpochAsync(sourceA, vectorA1);
        var epoch1B = await _coordinator.GetOrCreateEpochAsync(sourceB, vectorB1);
        var epoch1C = await _coordinator.GetOrCreateEpochAsync(sourceC, vectorC1);
        
        Assert.Same(epoch1A, epoch1B);
        Assert.Same(epoch1B, epoch1C);

        // Act - All three signal readiness for epoch 2
        var vectorA2 = EpochVector.FromSingleSource(sourceA, 2);
        var vectorB2 = EpochVector.FromSingleSource(sourceB, 2);
        var vectorC2 = EpochVector.FromSingleSource(sourceC, 2);
        
        _coordinator.SignalReadyForNext(sourceA, vectorA1, vectorA2);
        _coordinator.SignalReadyForNext(sourceB, vectorB1, vectorB2);
        _coordinator.SignalReadyForNext(sourceC, vectorC1, vectorC2);
        
        // All sources can now advance
        var epoch2A = await _coordinator.GetOrCreateEpochAsync(sourceA, vectorA2);
        var epoch2B = await _coordinator.GetOrCreateEpochAsync(sourceB, vectorB2);
        var epoch2C = await _coordinator.GetOrCreateEpochAsync(sourceC, vectorC2);

        // Assert - All get same new epoch
        Assert.Same(epoch2A, epoch2B);
        Assert.Same(epoch2B, epoch2C);
    }

    [Fact]
    public async Task EpochDisposal_DisposesScope()
    {
        // Arrange
        var sourceA = "sourceA";
        var vector1 = EpochVector.FromSingleSource(sourceA, 1);
        var epoch = await _coordinator.GetOrCreateEpochAsync(sourceA, vector1);
        
        var service = epoch.GetService<TestService>();
        Assert.NotNull(service);

        // Act - Dispose epoch
        await epoch.DisposeAsync();

        // Assert - Should throw when accessing disposed epoch
        Assert.Throws<ObjectDisposedException>(() => epoch.GetService<TestService>());
    }

    [Fact]
    public async Task CoordinatorDisposal_DisposesAllEpochs()
    {
        // Arrange
        var sourceA = "sourceA";
        var sourceB = "sourceB";
        var vectorA = EpochVector.FromSingleSource(sourceA, 1);
        var vectorB = EpochVector.FromSingleSource(sourceB, 1);
        
        var epochA = await _coordinator.GetOrCreateEpochAsync(sourceA, vectorA);
        var epochB = await _coordinator.GetOrCreateEpochAsync(sourceB, vectorB);

        // Act - Dispose coordinator
        await _coordinator.DisposeAsync();

        // Assert - Should throw when using disposed coordinator
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            await _coordinator.GetOrCreateEpochAsync(sourceA, vectorA);
        });
    }

    [Fact]
    public void SignalReadyForNext_UnknownSource_ThrowsException()
    {
        // Arrange
        var unknownSource = "unknownSource";
        var currentVector = EpochVector.FromSingleSource(unknownSource, 1);
        var nextVector = EpochVector.FromSingleSource(unknownSource, 2);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            _coordinator.SignalReadyForNext(unknownSource, currentVector, nextVector);
        });
        
        Assert.Contains("Unknown source", ex.Message);
    }

    [Fact]
    public async Task EpochStream_DisposalDoesNotDisposeEpoch()
    {
        // Arrange
        var sourceA = "sourceA";
        var vector = EpochVector.FromSingleSource(sourceA, 1);
        var epoch = await _coordinator.GetOrCreateEpochAsync(sourceA, vector);
        
        var stream = new EpochStream<int>(epoch, CreateItems(1, 2, 3));

        // Act - Dispose stream
        await stream.DisposeAsync();

        // Assert - Epoch should still be usable (stream doesn't own it)
        var service = epoch.GetService<TestService>();
        Assert.NotNull(service);
    }

    private static async IAsyncEnumerable<T> CreateItems<T>(params T[] items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }
}

/// <summary>
/// Test service for verifying DI scope behavior.
/// Each scoped instance gets a unique ID.
/// </summary>
public class TestService
{
    public Guid Id { get; } = Guid.NewGuid();
}
