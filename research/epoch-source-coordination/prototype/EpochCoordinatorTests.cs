namespace DataFlow.Research.EpochSourceCoordination.Tests;

using Xunit;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Tests for source-level epoch coordination approach.
/// Validates key scenarios: single source, multi-source, fan-in.
/// </summary>
public class EpochCoordinatorTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly EpochCoordinator _coordinator;

    public EpochCoordinatorTests()
    {
        var services = new ServiceCollection();
        services.AddScoped<TestService>();
        _serviceProvider = services.BuildServiceProvider();
        
        _coordinator = new EpochCoordinator(_serviceProvider);
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
    public async Task MultiSource_BoundedGrowth_BlocksWithoutReadiness()
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
        
        // Assert - Should throw because Source B not ready
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _coordinator.GetOrCreateEpochAsync(sourceA, vectorA2);
        });
        
        Assert.Contains("not ready", ex.Message);
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
        var mergedEpoch = streamA.Epoch; // Could use either - they're the same!
        
        // Assert - No scope merging problem!
        Assert.Same(streamA.Epoch, streamB.Epoch);
        Assert.Same(streamA.Epoch.ServiceProvider, streamB.Epoch.ServiceProvider);
        
        // Services resolved from merged streams are from same scope
        var serviceFromA = streamA.Epoch.GetService<TestService>();
        var serviceFromB = streamB.Epoch.GetService<TestService>();
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
/// </summary>
public class TestService
{
    public Guid Id { get; } = Guid.NewGuid();
}

/// <summary>
/// Placeholder for EpochVector - would use real implementation.
/// </summary>
public class EpochVector
{
    private readonly Dictionary<string, long> _sequences;

    private EpochVector(Dictionary<string, long> sequences)
    {
        _sequences = sequences;
    }

    public static EpochVector None => new(new Dictionary<string, long>());

    public static EpochVector FromSingleSource(string sourceId, long sequence)
    {
        return new EpochVector(new Dictionary<string, long> { [sourceId] = sequence });
    }

    public EpochVector Merge(EpochVector other)
    {
        var merged = new Dictionary<string, long>(_sequences);
        foreach (var kvp in other._sequences)
        {
            if (!merged.ContainsKey(kvp.Key) || merged[kvp.Key] < kvp.Value)
            {
                merged[kvp.Key] = kvp.Value;
            }
        }
        return new EpochVector(merged);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not EpochVector other) return false;
        if (_sequences.Count != other._sequences.Count) return false;
        return _sequences.All(kvp => 
            other._sequences.TryGetValue(kvp.Key, out var otherVal) && kvp.Value == otherVal);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var kvp in _sequences.OrderBy(x => x.Key))
        {
            hash.Add(kvp.Key);
            hash.Add(kvp.Value);
        }
        return hash.ToHashCode();
    }

    public override string ToString()
    {
        return "{" + string.Join(", ", _sequences.Select(kvp => $"{kvp.Key}={kvp.Value}")) + "}";
    }
}
