namespace DataFlow.POC.Tests.Core;

using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using DataFlow.POC.Registry;

/// <summary>
/// Tests for async readiness coordination in EpochCoordinator.
/// Validates async waiting, cancellation, timeouts, and error handling.
/// </summary>
public class AsyncReadinessTests : IAsyncDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceProvider _serviceProvider;
    private readonly EpochCoordinator _coordinator;

    public AsyncReadinessTests(ITestOutputHelper output)
    {
        _output = output;
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
    public async Task AsyncWait_SourceBlocksUntilOthersReady()
    {
        // Arrange
        var sourceA = "sourceA";
        var sourceB = "sourceB";
        
        var vectorA1 = EpochVector.FromSingleSource(sourceA, 1);
        var vectorB1 = EpochVector.FromSingleSource(sourceB, 1);
        
        _output.WriteLine("Creating epoch 1...");
        await _coordinator.GetOrCreateEpochAsync(sourceA, vectorA1);
        await _coordinator.GetOrCreateEpochAsync(sourceB, vectorB1);
        
        var vectorA2 = EpochVector.FromSingleSource(sourceA, 2);
        var vectorB2 = EpochVector.FromSingleSource(sourceB, 2);
        
        // Act - Source A tries to advance without Source B being ready
        _output.WriteLine("Source A requesting epoch 2 (should block)...");
        var getEpochTask = _coordinator.GetOrCreateEpochAsync(sourceA, vectorA2).AsTask();
        
        _output.WriteLine($"Task status after request: {getEpochTask.Status}");
        _output.WriteLine($"Task completed? {getEpochTask.IsCompleted}");
        
        // Wait a bit to ensure it's blocked
        await Task.Delay(100);
        
        _output.WriteLine($"Task status after delay: {getEpochTask.Status}");
        _output.WriteLine($"Task completed after delay? {getEpochTask.IsCompleted}");
        
        Assert.False(getEpochTask.IsCompleted, "Should be waiting for source B");
        
        // Source B signals readiness
        _output.WriteLine("Source B signaling readiness...");
        _coordinator.SignalReadyForNext(sourceB, vectorB1, vectorB2);
        
        // Source A should still be waiting
        await Task.Delay(100);
        Assert.False(getEpochTask.IsCompleted, "Source A not ready yet");
        
        // Source A signals readiness
        _output.WriteLine("Source A signaling readiness...");
        _coordinator.SignalReadyForNext(sourceA, vectorA1, vectorA2);
        
        // Now source A should complete
        _output.WriteLine("Waiting for source A to complete...");
        var epoch2A = await getEpochTask;
        
        Assert.NotNull(epoch2A);
        _output.WriteLine($"Source A got epoch: {epoch2A.Vector}");
    }

    [Fact]
    public async Task AsyncWait_Cancellation_CancelsWaitingSource()
    {
        // Arrange
        var sourceA = "sourceA";
        var sourceB = "sourceB";
        
        var vectorA1 = EpochVector.FromSingleSource(sourceA, 1);
        var vectorB1 = EpochVector.FromSingleSource(sourceB, 1);
        
        await _coordinator.GetOrCreateEpochAsync(sourceA, vectorA1);
        await _coordinator.GetOrCreateEpochAsync(sourceB, vectorB1);
        
        var vectorA2 = EpochVector.FromSingleSource(sourceA, 2);
        
        // Act - Source A waits with cancellation token
        var cts = new CancellationTokenSource();
        var getEpochTask = _coordinator.GetOrCreateEpochAsync(sourceA, vectorA2, cts.Token).AsTask();
        
        await Task.Delay(100);
        Assert.False(getEpochTask.IsCompleted);
        
        // Cancel the request
        cts.Cancel();
        
        // Assert - Should be canceled
        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await getEpochTask);
        Assert.True(ex is TaskCanceledException || ex is OperationCanceledException);
    }

    [Fact]
    public async Task AsyncWait_MultipleSourcesWaiting_AllUnblockedTogether()
    {
        // Arrange
        var sourceA = "sourceA";
        var sourceB = "sourceB";
        var sourceC = "sourceC";
        
        var vectorA1 = EpochVector.FromSingleSource(sourceA, 1);
        var vectorB1 = EpochVector.FromSingleSource(sourceB, 1);
        var vectorC1 = EpochVector.FromSingleSource(sourceC, 1);
        
        await _coordinator.GetOrCreateEpochAsync(sourceA, vectorA1);
        await _coordinator.GetOrCreateEpochAsync(sourceB, vectorB1);
        await _coordinator.GetOrCreateEpochAsync(sourceC, vectorC1);
        
        var vectorA2 = EpochVector.FromSingleSource(sourceA, 2);
        var vectorB2 = EpochVector.FromSingleSource(sourceB, 2);
        var vectorC2 = EpochVector.FromSingleSource(sourceC, 2);
        
        // Act - All sources try to advance (all will wait)
        var taskA = _coordinator.GetOrCreateEpochAsync(sourceA, vectorA2).AsTask();
        var taskB = _coordinator.GetOrCreateEpochAsync(sourceB, vectorB2).AsTask();
        
        await Task.Delay(100);
        Assert.False(taskA.IsCompleted);
        Assert.False(taskB.IsCompleted);
        
        // All sources signal readiness
        _coordinator.SignalReadyForNext(sourceA, vectorA1, vectorA2);
        _coordinator.SignalReadyForNext(sourceB, vectorB1, vectorB2);
        _coordinator.SignalReadyForNext(sourceC, vectorC1, vectorC2);
        
        // Assert - Both waiting tasks should complete
        var epoch2A = await taskA;
        var epoch2B = await taskB;
        
        Assert.Same(epoch2A, epoch2B);
        Assert.NotNull(epoch2A);
    }

    [Fact]
    public async Task AsyncWait_CoordinatorDisposed_PendingWaitsFail()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<TestService>();
        var sp = services.BuildServiceProvider();
        var coordinator = new EpochCoordinator(sp.GetRequiredService<IServiceScopeFactory>());
        
        var sourceA = "sourceA";
        var sourceB = "sourceB";
        
        var vectorA1 = EpochVector.FromSingleSource(sourceA, 1);
        var vectorB1 = EpochVector.FromSingleSource(sourceB, 1);
        
        await coordinator.GetOrCreateEpochAsync(sourceA, vectorA1);
        await coordinator.GetOrCreateEpochAsync(sourceB, vectorB1);
        
        var vectorA2 = EpochVector.FromSingleSource(sourceA, 2);
        
        // Act - Source A waits, then coordinator is disposed
        var getEpochTask = coordinator.GetOrCreateEpochAsync(sourceA, vectorA2).AsTask();
        
        await Task.Delay(100);
        Assert.False(getEpochTask.IsCompleted);
        
        // Dispose coordinator
        await coordinator.DisposeAsync();
        await sp.DisposeAsync();
        
        // Assert - Should throw ObjectDisposedException
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await getEpochTask);
    }

    [Fact]
    public async Task AsyncWait_SequentialAdvancement_MaintainsOrder()
    {
        // Arrange
        var sourceA = "sourceA";
        var sourceB = "sourceB";
        
        var vectorA1 = EpochVector.FromSingleSource(sourceA, 1);
        var vectorB1 = EpochVector.FromSingleSource(sourceB, 1);
        
        await _coordinator.GetOrCreateEpochAsync(sourceA, vectorA1);
        await _coordinator.GetOrCreateEpochAsync(sourceB, vectorB1);
        
        // Act - Advance through multiple epochs
        for (int i = 2; i <= 5; i++)
        {
            var vectorA = EpochVector.FromSingleSource(sourceA, i);
            var vectorB = EpochVector.FromSingleSource(sourceB, i);
            
            // Both sources signal readiness
            _coordinator.SignalReadyForNext(sourceA, EpochVector.FromSingleSource(sourceA, i - 1), vectorA);
            _coordinator.SignalReadyForNext(sourceB, EpochVector.FromSingleSource(sourceB, i - 1), vectorB);
            
            // Both can advance
            var epochA = await _coordinator.GetOrCreateEpochAsync(sourceA, vectorA);
            var epochB = await _coordinator.GetOrCreateEpochAsync(sourceB, vectorB);
            
            // Assert - Same epoch for both sources at each sequence
            Assert.Same(epochA, epochB);
            Assert.Equal(vectorA.Merge(vectorB), epochA.Vector);
        }
    }
}
