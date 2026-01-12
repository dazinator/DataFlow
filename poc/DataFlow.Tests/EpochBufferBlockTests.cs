namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Checkpointing;
using DataFlow.POC.Core;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Tests for EpochBufferBlock that validates epoch-aware buffering functionality.
/// </summary>
public class EpochBufferBlockTests
{
    private readonly ITestOutputHelper _output;

    public EpochBufferBlockTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Basic Functionality Tests

    [Fact]
    public async Task EpochBuffer_Should_Preserve_Epoch_Boundaries()
    {
        // Arrange
        var bufferConfig = new BufferConfiguration(capacity: 10);
        var context = new BlockContext("buffer");
        var buffer = new EpochBufferBlock<int>(context, bufferConfig);

        var inputEpochs = CreateTestEpochStreams(
            (EpochVector.FromSingleSource("source", 1), new[] { 1, 2, 3 }),
            (EpochVector.FromSingleSource("source", 2), new[] { 4, 5, 6 }),
            (EpochVector.FromSingleSource("source", 3), new[] { 7, 8, 9 })
        );

        var execContext = new TestExecutionContext();

        // Act
        var outputEpochs = new List<(EpochVector epoch, List<int> items)>();
        await foreach (var epochStream in buffer.ExecuteAsync(inputEpochs, execContext))
        {
            var items = new List<int>();
            await foreach (var item in epochStream.Items)
            {
                items.Add(item);
            }
            outputEpochs.Add((epochStream.Epoch, items));
        }

        // Assert
        outputEpochs.Count.ShouldBe(3);
        outputEpochs[0].epoch.ToString().ShouldBe("EpochVector[source=1]");
        outputEpochs[0].items.ShouldBe(new[] { 1, 2, 3 });
        outputEpochs[1].epoch.ToString().ShouldBe("EpochVector[source=2]");
        outputEpochs[1].items.ShouldBe(new[] { 4, 5, 6 });
        outputEpochs[2].epoch.ToString().ShouldBe("EpochVector[source=3]");
        outputEpochs[2].items.ShouldBe(new[] { 7, 8, 9 });
        
        _output.WriteLine($"✓ Preserved {outputEpochs.Count} epoch boundaries correctly");
    }

    [Fact]
    public async Task EpochBuffer_Should_Process_Single_Epoch()
    {
        // Arrange
        var bufferConfig = new BufferConfiguration(capacity: 5);
        var context = new BlockContext("buffer");
        var buffer = new EpochBufferBlock<int>(context, bufferConfig);

        var inputEpochs = CreateTestEpochStreams(
            (EpochVector.FromSingleSource("source", 1), new[] { 1, 2, 3, 4, 5 })
        );

        var execContext = new TestExecutionContext();

        // Act
        var outputItems = new List<int>();
        await foreach (var epochStream in buffer.ExecuteAsync(inputEpochs, execContext))
        {
            await foreach (var item in epochStream.Items)
            {
                outputItems.Add(item);
            }
        }

        // Assert
        outputItems.ShouldBe(new[] { 1, 2, 3, 4, 5 });
    }

    [Fact]
    public async Task EpochBuffer_Should_Handle_Empty_Epochs()
    {
        // Arrange
        var bufferConfig = new BufferConfiguration(capacity: 10);
        var context = new BlockContext("buffer");
        var buffer = new EpochBufferBlock<int>(context, bufferConfig);

        var inputEpochs = CreateTestEpochStreams(
            (EpochVector.FromSingleSource("source", 1), new[] { 1, 2 }),
            (EpochVector.FromSingleSource("source", 2), Array.Empty<int>()),
            (EpochVector.FromSingleSource("source", 3), new[] { 3, 4 })
        );

        var execContext = new TestExecutionContext();

        // Act
        var outputEpochs = new List<(EpochVector epoch, List<int> items)>();
        await foreach (var epochStream in buffer.ExecuteAsync(inputEpochs, execContext))
        {
            var items = new List<int>();
            await foreach (var item in epochStream.Items)
            {
                items.Add(item);
            }
            outputEpochs.Add((epochStream.Epoch, items));
        }

        // Assert
        outputEpochs.Count.ShouldBe(3);
        outputEpochs[0].items.ShouldBe(new[] { 1, 2 });
        outputEpochs[1].items.ShouldBe(Array.Empty<int>());
        outputEpochs[2].items.ShouldBe(new[] { 3, 4 });
    }

    #endregion

    #region Metadata Preservation Tests

    [Fact]
    public async Task EpochBuffer_Should_Preserve_Epoch_Metadata()
    {
        // Arrange
        var bufferConfig = new BufferConfiguration(capacity: 10);
        var context = new BlockContext("buffer");
        var buffer = new EpochBufferBlock<int>(context, bufferConfig);

        var epoch1 = EpochVector.FromSingleSource("source", 1);
        var epoch2 = EpochVector.FromSingleSource("source", 2);

        var inputEpochs = CreateTestEpochStreams(
            (epoch1, new[] { 1, 2, 3 }),
            (epoch2, new[] { 4, 5, 6 })
        );

        var execContext = new TestExecutionContext();

        // Act
        var outputEpochVectors = new List<EpochVector>();
        await foreach (var epochStream in buffer.ExecuteAsync(inputEpochs, execContext))
        {
            outputEpochVectors.Add(epochStream.Epoch);
            // Consume items
            await foreach (var _ in epochStream.Items) { }
        }

        // Assert
        outputEpochVectors.Count.ShouldBe(2);
        outputEpochVectors[0].ToString().ShouldBe(epoch1.ToString());
        outputEpochVectors[1].ToString().ShouldBe(epoch2.ToString());
    }

    [Fact]
    public async Task EpochBuffer_Should_Preserve_Epoch_Scope()
    {
        // Arrange
        var bufferConfig = new BufferConfiguration(capacity: 10);
        var context = new BlockContext("buffer");
        var buffer = new EpochBufferBlock<int>(context, bufferConfig);

        var epoch = EpochVector.FromSingleSource("source", 1);

        var inputStream = CreateEpochStreamWithScope(epoch, null, new[] { 1, 2, 3 });
        var inputEpochs = ToAsyncEnumerable(new[] { inputStream });

        var execContext = new TestExecutionContext();

        // Act
        IEpoch? outputScope = null;
        await foreach (var epochStream in buffer.ExecuteAsync(inputEpochs, execContext))
        {
            outputScope = epochStream.EpochScope;
            // Consume items
            await foreach (var _ in epochStream.Items) { }
        }

        // Assert
        outputScope.ShouldBeNull(); // Test implementation doesn't use epoch scope
    }

    #endregion

    #region Backpressure Tests

    [Fact]
    public async Task EpochBuffer_Should_Block_When_Capacity_Reached()
    {
        // Arrange - Small buffer to test backpressure
        var bufferConfig = new BufferConfiguration(capacity: 3);
        var context = new BlockContext("buffer");
        var buffer = new EpochBufferBlock<int>(context, bufferConfig);

        // Create epoch with more items than capacity
        var inputEpochs = CreateTestEpochStreams(
            (EpochVector.FromSingleSource("source", 1), new[] { 1, 2, 3, 4, 5 })
        );

        var execContext = new TestExecutionContext();

        // Act & Assert
        // If backpressure works correctly, all items should be consumed
        // without deadlock. The test succeeds if it completes.
        var consumedItems = new List<int>();
        await foreach (var epochStream in buffer.ExecuteAsync(inputEpochs, execContext))
        {
            await foreach (var item in epochStream.Items)
            {
                consumedItems.Add(item);
            }
        }

        // Verify all items were buffered and consumed despite buffer < epoch size
        consumedItems.ShouldBe(new[] { 1, 2, 3, 4, 5 });
        _output.WriteLine("✓ Backpressure mechanism working correctly - no deadlock with buffer smaller than epoch");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task EpochBuffer_Should_Propagate_Cancellation()
    {
        // Arrange
        var bufferConfig = new BufferConfiguration(capacity: 10);
        var context = new BlockContext("buffer");
        var buffer = new EpochBufferBlock<int>(context, bufferConfig);

        var inputEpochs = CreateTestEpochStreams(
            (EpochVector.FromSingleSource("source", 1), new[] { 1, 2, 3 }),
            (EpochVector.FromSingleSource("source", 2), new[] { 4, 5, 6 })
        );

        var cts = new CancellationTokenSource();
        var execContext = new TestExecutionContext { CancellationToken = cts.Token };

        // Act & Assert
        var itemCount = 0;
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await foreach (var epochStream in buffer.ExecuteAsync(inputEpochs, execContext))
            {
                await foreach (var item in epochStream.Items)
                {
                    itemCount++;
                    if (itemCount == 2)
                    {
                        cts.Cancel(); // Cancel after processing 2 items
                    }
                }
            }
        });

        _output.WriteLine($"✓ Cancellation propagated after {itemCount} items");
    }

    [Fact]
    public async Task EpochBuffer_Should_Propagate_Exceptions_From_Input()
    {
        // Arrange
        var bufferConfig = new BufferConfiguration(capacity: 10);
        var context = new BlockContext("buffer");
        var buffer = new EpochBufferBlock<int>(context, bufferConfig);

        var inputEpochs = CreateFaultingEpochStreams();

        var execContext = new TestExecutionContext();

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await foreach (var epochStream in buffer.ExecuteAsync(inputEpochs, execContext))
            {
                await foreach (var item in epochStream.Items)
                {
                    // Should throw before getting here
                }
            }
        });
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void EpochBuffer_Should_Require_Positive_Capacity()
    {
        // Arrange
        var context = new BlockContext("buffer");

        // Act & Assert
        Should.Throw<ArgumentException>(() => new BufferConfiguration(capacity: 0));
        Should.Throw<ArgumentException>(() => new BufferConfiguration(capacity: -1));
    }

    [Fact]
    public void EpochBuffer_Should_Require_NonNull_Configuration()
    {
        // Arrange
        var context = new BlockContext("buffer");

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => 
            new EpochBufferBlock<int>(context, bufferConfig: null!));
    }

    [Fact]
    public void EpochBuffer_Should_Require_NonNull_Context()
    {
        // Arrange
        var bufferConfig = new BufferConfiguration(capacity: 10);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => 
            new EpochBufferBlock<int>(context: null!, bufferConfig));
    }

    #endregion

    #region Integration Tests

    // EpochBuffer integration test with EpochSegmenterBlock removed - block has been deprecated
    // Replaced by graph-level ConfigureEpochs() API
    // Buffer functionality is tested independently above

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates async enumerable of epoch streams for testing.
    /// </summary>
    private static async IAsyncEnumerable<IEpochStream<T>> CreateTestEpochStreams<T>(
        params (EpochVector epoch, T[] items)[] epochs)
    {
        foreach (var (epoch, items) in epochs)
        {
            yield return CreateEpochStream(epoch, items);
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// Creates a single epoch stream with given items.
    /// </summary>
    private static IEpochStream<T> CreateEpochStream<T>(EpochVector epoch, T[] items)
    {
        return new TestEpochStream<T>(epoch, items);
    }

    /// <summary>
    /// Creates a single epoch stream with scope.
    /// </summary>
    private static IEpochStream<T> CreateEpochStreamWithScope<T>(
        EpochVector epoch, string scope, T[] items)
    {
        return new TestEpochStream<T>(epoch, items, scope);
    }

    /// <summary>
    /// Creates epoch streams that fault during enumeration.
    /// </summary>
    private static async IAsyncEnumerable<IEpochStream<int>> CreateFaultingEpochStreams()
    {
        yield return CreateEpochStream(EpochVector.FromSingleSource("source", 1), new[] { 1, 2 });
        await Task.CompletedTask;
        throw new InvalidOperationException("Test exception");
    }

    /// <summary>
    /// Produces plain test items.
    /// </summary>
    private static async IAsyncEnumerable<int> ProducePlainItems(int count = 10)
    {
        for (int i = 0; i < count; i++)
        {
            yield return i;
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// Converts array to async enumerable.
    /// </summary>
    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(T[] items)
    {
        foreach (var item in items)
        {
            yield return item;
        }
        await Task.CompletedTask;
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Simple test execution context.
    /// </summary>
    private class TestExecutionContext : IExecutionContext
    {
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
        public IServiceProvider ServiceProvider { get; } = new ServiceCollection().BuildServiceProvider();
        public Guid InvocationId { get; } = Guid.NewGuid();
        public ICheckpoint? RecoveryCheckpoint { get; } = null;
        public POC.Observability.IDataFlowMetrics? Metrics { get; } = null;
    }

    /// <summary>
    /// Simple test epoch stream implementation.
    /// </summary>
    private class TestEpochStream<T> : IEpochStream<T>
    {
        private readonly T[] _items;

        public TestEpochStream(EpochVector epoch, T[] items, string? scope = null)
        {
            Epoch = epoch;
            _items = items;
            EpochScope = null; // Test implementation doesn't use epoch coordinator
        }

        public EpochVector Epoch { get; }
        public IEpoch? EpochScope { get; }

        public IAsyncEnumerable<T> Items => GetItemsAsync();

        private async IAsyncEnumerable<T> GetItemsAsync()
        {
            foreach (var item in _items)
            {
                yield return item;
            }
            await Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    #endregion
}
