namespace DataFlow.POC.Tests;

using DataFlow.POC.Core;
using Shouldly;
using Xunit;
using System.Threading.Channels;

/// <summary>
/// Tests for SingleEpochExtensions - the key pattern enabling mandatory epochs.
/// </summary>
public class SingleEpochExtensionsTests
{
    /// <summary>
    /// Test that WrapInSingleEpoch produces exactly one epoch stream.
    /// </summary>
    [Fact]
    public async Task WrapInSingleEpoch_ShouldProduceOneEpochStream()
    {
        // Arrange
        var plainStream = CreatePlainStream(count: 10);
        var sourceName = "test-source";

        // Act
        var epochStreams = plainStream.WrapInSingleEpoch(sourceName);
        var epochs = new List<IEpochStream<int>>();
        await foreach (var epoch in epochStreams)
        {
            epochs.Add(epoch);
        }

        // Assert
        epochs.Count.ShouldBe(1, "Should produce exactly one epoch stream");
    }

    /// <summary>
    /// Test that the single epoch contains all items from the plain stream.
    /// </summary>
    [Fact]
    public async Task WrapInSingleEpoch_ShouldContainAllItems()
    {
        // Arrange
        var expectedItems = Enumerable.Range(1, 100).ToList();
        var plainStream = CreatePlainStream(count: 100);
        var sourceName = "test-source";

        // Act
        var epochStreams = plainStream.WrapInSingleEpoch(sourceName);
        IEpochStream<int>? epoch = null;
        await foreach (var e in epochStreams)
        {
            epoch = e;
        }

        var items = new List<int>();
        await foreach (var item in epoch!.Items)
        {
            items.Add(item);
        }

        // Assert
        items.ShouldBe(expectedItems, "All items should be preserved in the single epoch");
    }

    /// <summary>
    /// Test that the epoch vector has the correct source name and sequence.
    /// </summary>
    [Fact]
    public async Task WrapInSingleEpoch_ShouldHaveCorrectEpochVector()
    {
        // Arrange
        var plainStream = CreatePlainStream(count: 10);
        var sourceName = "my-source";

        // Act
        var epochStreams = plainStream.WrapInSingleEpoch(sourceName);
        IEpochStream<int>? epoch = null;
        await foreach (var e in epochStreams)
        {
            epoch = e;
        }

        // Assert
        epoch.ShouldNotBeNull();
        epoch.Epoch.ShouldNotBeNull();
        epoch.Epoch.GetSequence(sourceName).ShouldBe(1L, "Should have sequence number 1");
        epoch.Epoch.Sequences.Count.ShouldBe(1, "Should have exactly one source");
    }

    /// <summary>
    /// Test that null source throws ArgumentNullException.
    /// </summary>
    [Fact]
    public async Task WrapInSingleEpoch_NullSource_ShouldThrow()
    {
        // Arrange
        IAsyncEnumerable<int>? nullSource = null;

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in nullSource!.WrapInSingleEpoch("test"))
            {
            }
        });
    }

    /// <summary>
    /// Test that null source name throws ArgumentNullException.
    /// </summary>
    [Fact]
    public async Task WrapInSingleEpoch_NullSourceName_ShouldThrow()
    {
        // Arrange
        var plainStream = CreatePlainStream(count: 10);

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in plainStream.WrapInSingleEpoch(null!))
            {
            }
        });
    }

    /// <summary>
    /// Test that cancellation is properly propagated.
    /// </summary>
    [Fact]
    public async Task WrapInSingleEpoch_Cancellation_ShouldPropagate()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var plainStream = CreateInfiniteStream();
        var sourceName = "test-source";

        // Act
        var epochStreams = plainStream.WrapInSingleEpoch(sourceName, cts.Token);
        IEpochStream<int>? epoch = null;
        await foreach (var e in epochStreams)
        {
            epoch = e;
        }
        
        var itemCount = 0;
        cts.CancelAfter(100); // Cancel after 100ms

        // Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await foreach (var item in epoch!.Items.WithCancellation(cts.Token))
            {
                itemCount++;
                await Task.Delay(10, cts.Token); // Simulate slow processing
            }
        });

        itemCount.ShouldBeGreaterThan(0, "Should have processed some items before cancellation");
    }

    /// <summary>
    /// Test that empty stream produces one epoch with no items.
    /// </summary>
    [Fact]
    public async Task WrapInSingleEpoch_EmptyStream_ShouldProduceEmptyEpoch()
    {
        // Arrange
        var emptyStream = CreatePlainStream(count: 0);
        var sourceName = "empty-source";

        // Act
        var epochStreams = emptyStream.WrapInSingleEpoch(sourceName);
        IEpochStream<int>? epoch = null;
        await foreach (var e in epochStreams)
        {
            epoch = e;
        }

        var items = new List<int>();
        await foreach (var item in epoch!.Items)
        {
            items.Add(item);
        }

        // Assert
        items.ShouldBeEmpty("Empty stream should produce epoch with no items");
        epoch.Epoch.GetSequence(sourceName).ShouldBe(1L, "Epoch vector should still be valid");
    }

    /// <summary>
    /// Test WrapInEpoch with IEpoch parameter.
    /// </summary>
    [Fact]
    public async Task WrapInEpoch_ShouldUseProvidedEpoch()
    {
        // Arrange
        var plainStream = CreatePlainStream(count: 10);
        var epochVector = EpochVector.FromSingleSource("custom-source", 42);
        var epoch = new TestEpoch(epochVector);

        // Act
        var epochStreams = plainStream.WrapInEpoch(epoch);
        IEpochStream<int>? result = null;
        await foreach (var e in epochStreams)
        {
            result = e;
        }

        // Assert
        result.ShouldNotBeNull();
        result.EpochScope.ShouldBeSameAs(epoch, "Should use the provided epoch object");
        result.Epoch.ShouldBeSameAs(epochVector, "Should use epoch vector from IEpoch");
        result.Epoch.GetSequence("custom-source").ShouldBe(42L);
    }

    /// <summary>
    /// Test that WrapInEpoch with null epoch throws.
    /// </summary>
    [Fact]
    public async Task WrapInEpoch_NullEpoch_ShouldThrow()
    {
        // Arrange
        var plainStream = CreatePlainStream(count: 10);

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in plainStream.WrapInEpoch(null!))
            {
            }
        });
    }

    /// <summary>
    /// Test that multiple epochs can be created with different source names.
    /// </summary>
    [Fact]
    public async Task WrapInSingleEpoch_DifferentSourceNames_ShouldHaveDifferentVectors()
    {
        // Arrange
        var stream1 = CreatePlainStream(count: 5);
        var stream2 = CreatePlainStream(count: 5);

        // Act
        IEpochStream<int>? epoch1 = null;
        await foreach (var e in stream1.WrapInSingleEpoch("source1"))
        {
            epoch1 = e;
        }

        IEpochStream<int>? epoch2 = null;
        await foreach (var e in stream2.WrapInSingleEpoch("source2"))
        {
            epoch2 = e;
        }

        // Assert
        epoch1.ShouldNotBeNull();
        epoch2.ShouldNotBeNull();
        epoch1.Epoch.GetSequence("source1").ShouldBe(1L);
        epoch1.Epoch.GetSequence("source2").ShouldBe(-1L, "Should not have source2");
        
        epoch2.Epoch.GetSequence("source2").ShouldBe(1L);
        epoch2.Epoch.GetSequence("source1").ShouldBe(-1L, "Should not have source1");
    }

    // Helper methods

    private static async IAsyncEnumerable<int> CreatePlainStream(int count)
    {
        for (int i = 1; i <= count; i++)
        {
            yield return i;
        }
    }

    private static async IAsyncEnumerable<int> CreateInfiniteStream()
    {
        int i = 0;
        while (true)
        {
            yield return i++;
            await Task.Delay(1); // Small delay to avoid tight loop
        }
    }

    /// <summary>
    /// Simple test implementation of IEpoch for testing.
    /// </summary>
    private class TestEpoch : IEpoch
    {
        public TestEpoch(EpochVector vector)
        {
            Vector = vector;
            ServiceProvider = null!; // Not needed for this test
        }

        public EpochVector Vector { get; }
        public IServiceProvider ServiceProvider { get; }
        public bool IsCheckpointing => false;
        public ChannelReader<IEpochOperation> OperationsReader => null!;

        public T GetService<T>() where T : notnull => throw new NotImplementedException();
        public Task QueueSerializedOperationAsync<TService>(Func<TService, Task> operation, CancellationToken cancellationToken = default) where TService : notnull 
            => throw new NotImplementedException();
        public Task QueueSerializedOperationAsync<TService>(Func<TService, DataFlow.POC.Checkpointing.IEpochOperationContext, Task> operation, CancellationToken cancellationToken = default) where TService : notnull 
            => throw new NotImplementedException();
        public void CompleteOperations() { }
        public Task WhenAllOperationsCompletedAsync() => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
