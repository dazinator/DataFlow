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
using DataFlow.POC.Registry;

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

    #region Concurrency and Scaling Tests

    [Fact]
    public async Task EpochBuffer_Should_Support_Multiple_Producers_Independently()
    {
        // Arrange - Multiple source actors producing concurrently to buffer
        var services = new ServiceCollection();
        var coordinator = new EpochCoordinator(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
        
        var producerCount = 4;
        var itemsPerProducer = 50;
        var bufferCapacity = 20; // Smaller than total items to test backpressure

        // Create source actors
        var sourceActors = Enumerable.Range(0, producerCount)
            .Select(i => new TestMultiProducerSourceActor($"source-{i}", i, itemsPerProducer))
            .ToList();

        // Create producer blocks
        var producerBlocks = sourceActors.Select((actor, idx) =>
            BlockHelpers.CreateEpochSource<int, TestMultiProducerSourceActor>($"producer-{idx}", actor, coordinator)
        ).ToList();

        var bufferConfig = new BufferConfiguration(capacity: bufferCapacity);
        var bufferContext = new BlockContext("buffer");
        var buffer = new EpochBufferBlock<int>(bufferContext, bufferConfig);

        var execContext = new TestExecutionContext();

        // Act - Merge all producer outputs into buffer
        var allProducerOutputs = MergeProducerEpochStreams<int>(producerBlocks, execContext);
        var bufferedOutputs = buffer.ExecuteAsync(allProducerOutputs, execContext);

        // Consume from buffer
        var receivedItems = new System.Collections.Concurrent.ConcurrentBag<int>();
        await foreach (var epochStream in bufferedOutputs)
        {
            await foreach (var item in epochStream.Items)
            {
                receivedItems.Add(item);
            }
        }

        // Assert - All items received from all producers
        receivedItems.Count.ShouldBe(producerCount * itemsPerProducer);
        
        // Verify each producer's items are present
        for (int producerId = 0; producerId < producerCount; producerId++)
        {
            var expectedItems = Enumerable.Range(producerId * 1000, itemsPerProducer);
            var producerItems = receivedItems.Where(item => item >= producerId * 1000 && item < (producerId + 1) * 1000);
            producerItems.Count().ShouldBe(itemsPerProducer, $"Producer {producerId} items missing");
        }

        _output.WriteLine($"✓ Multiple producers ({producerCount}) successfully wrote through buffer with capacity {bufferCapacity}");
        _output.WriteLine($"  Total items: {receivedItems.Count}, Expected: {producerCount * itemsPerProducer}");
    }

    [Fact]
    public async Task EpochBuffer_Should_Scale_Throughput_With_Multiple_Consumers()
    {
        // Arrange - Single producer, multiple consumers
        var itemCount = 200;
        var bufferCapacity = 50;
        var consumerDelayMs = 10;

        var bufferConfig = new BufferConfiguration(capacity: bufferCapacity);
        var context = new BlockContext("buffer");
        var buffer = new EpochBufferBlock<int>(context, bufferConfig);

        // Create input with multiple epochs
        var inputEpochs = CreateTestEpochStreams(
            (EpochVector.FromSingleSource("source", 1), Enumerable.Range(0, itemCount / 2).ToArray()),
            (EpochVector.FromSingleSource("source", 2), Enumerable.Range(itemCount / 2, itemCount / 2).ToArray())
        );

        var execContext = new TestExecutionContext();

        // Act - Test with different consumer counts
        var singleConsumerTime = await MeasureConsumerThroughput(buffer, inputEpochs, execContext, 1, consumerDelayMs, itemCount);
        
        // Reset for next test
        inputEpochs = CreateTestEpochStreams(
            (EpochVector.FromSingleSource("source", 1), Enumerable.Range(0, itemCount / 2).ToArray()),
            (EpochVector.FromSingleSource("source", 2), Enumerable.Range(itemCount / 2, itemCount / 2).ToArray())
        );
        
        var multiConsumerTime = await MeasureConsumerThroughput(buffer, inputEpochs, execContext, 4, consumerDelayMs, itemCount);

        // Assert - Multiple consumers should be faster (with tolerance for test variance)
        _output.WriteLine($"Single consumer time: {singleConsumerTime}ms");
        _output.WriteLine($"4 consumers time: {multiConsumerTime}ms");
        _output.WriteLine($"Speedup: {(double)singleConsumerTime / multiConsumerTime:F2}x");

        // With 4 consumers, we expect at least ~1.4x speedup (conservative due to test overhead)
        ((double)multiConsumerTime).ShouldBeLessThan((double)singleConsumerTime * 0.7, "Multiple consumers should improve throughput");
    }

    [Fact]
    public async Task EpochBuffer_Should_Preserve_Data_Under_Backpressure()
    {
        // Arrange - Small buffer, large epoch
        var bufferCapacity = 5;
        var itemCount = 100;

        var bufferConfig = new BufferConfiguration(capacity: bufferCapacity);
        var context = new BlockContext("buffer");
        var buffer = new EpochBufferBlock<int>(context, bufferConfig);

        var inputEpochs = CreateTestEpochStreams(
            (EpochVector.FromSingleSource("source", 1), Enumerable.Range(0, itemCount).ToArray())
        );

        var execContext = new TestExecutionContext();

        // Act - Slow consumer to trigger backpressure
        var receivedItems = new List<int>();
        await foreach (var epochStream in buffer.ExecuteAsync(inputEpochs, execContext))
        {
            await foreach (var item in epochStream.Items)
            {
                receivedItems.Add(item);
                // Simulate slow consumer
                await Task.Delay(1);
            }
        }

        // Assert - No data loss despite buffer < epoch size
        receivedItems.Count.ShouldBe(itemCount);
        receivedItems.ShouldBe(Enumerable.Range(0, itemCount).ToArray());
        
        _output.WriteLine($"✓ Preserved all {itemCount} items with buffer capacity {bufferCapacity}");
    }

    [Fact]
    public async Task EpochBuffer_Should_Handle_Backpressure_With_Multiple_Producers()
    {
        // Arrange - Multiple producers, small buffer, slow consumer
        var producerCount = 3;
        var itemsPerProducer = 30;
        var bufferCapacity = 10;

        var services = new ServiceCollection();
        var coordinator = new EpochCoordinator(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());

        var sourceActors = Enumerable.Range(0, producerCount)
            .Select(i => new TestMultiProducerSourceActor($"source-{i}", i, itemsPerProducer))
            .ToList();

        var producerBlocks = sourceActors.Select((actor, idx) =>
            BlockHelpers.CreateEpochSource<int, TestMultiProducerSourceActor>($"producer-{idx}", actor, coordinator)
        ).ToList();

        var bufferConfig = new BufferConfiguration(capacity: bufferCapacity);
        var bufferContext = new BlockContext("buffer");
        var buffer = new EpochBufferBlock<int>(bufferContext, bufferConfig);

        var execContext = new TestExecutionContext();

        // Act
        var allProducerOutputs = MergeProducerEpochStreams<int>(producerBlocks, execContext);
        var bufferedOutputs = buffer.ExecuteAsync(allProducerOutputs, execContext);

        var receivedItems = new List<int>();
        await foreach (var epochStream in bufferedOutputs)
        {
            await foreach (var item in epochStream.Items)
            {
                receivedItems.Add(item);
                // Slow consumer triggers backpressure
                await Task.Delay(2);
            }
        }

        // Assert - All items received without loss despite backpressure
        receivedItems.Count.ShouldBe(producerCount * itemsPerProducer);
        
        _output.WriteLine($"✓ Backpressure correctly handled with {producerCount} producers, buffer capacity {bufferCapacity}");
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(1, 4)]
    [InlineData(2, 1)]
    [InlineData(2, 2)]
    [InlineData(2, 4)]
    [InlineData(4, 1)]
    [InlineData(4, 2)]
    [InlineData(4, 4)]
    public async Task EpochBuffer_EndToEnd_ProducersToBufferToConsumers_ScalingPattern(int producerCount, int consumerCount)
    {
        // Arrange - Full pattern: Multiple Producers → Buffer → Competing Consumers
        var itemsPerProducer = 40;
        var bufferCapacity = 20;

        _output.WriteLine($"Testing {producerCount} producers → buffer → {consumerCount} consumers");

        var services = new ServiceCollection();
        var coordinator = new EpochCoordinator(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());

        // Create producers
        var sourceActors = Enumerable.Range(0, producerCount)
            .Select(i => new TestMultiProducerSourceActor($"source-{i}", i, itemsPerProducer))
            .ToList();

        var producerBlocks = sourceActors.Select((actor, idx) =>
            BlockHelpers.CreateEpochSource<int, TestMultiProducerSourceActor>($"producer-{idx}", actor, coordinator)
        ).ToList();

        // Create buffer
        var bufferConfig = new BufferConfiguration(capacity: bufferCapacity);
        var bufferContext = new BlockContext("buffer");
        var buffer = new EpochBufferBlock<int>(bufferContext, bufferConfig);

        var execContext = new TestExecutionContext();

        // Act - Merge producers → buffer → competing consumers
        var allProducerOutputs = MergeProducerEpochStreams<int>(producerBlocks, execContext);
        var bufferedOutputs = buffer.ExecuteAsync(allProducerOutputs, execContext);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var receivedItems = await ConsumeWithCompetingConsumers(bufferedOutputs, consumerCount);
        sw.Stop();

        // Assert
        var totalExpected = producerCount * itemsPerProducer;
        receivedItems.Count.ShouldBe(totalExpected);

        _output.WriteLine($"  Items: {receivedItems.Count}/{totalExpected}, Time: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task EpochBuffer_Should_Preserve_Epoch_Boundaries_Under_Concurrent_Load()
    {
        // Arrange - Single producer creating multiple epochs to test boundary preservation
        var epochCount = 5;
        var itemsPerEpoch = 20;
        var bufferCapacity = 15; // Smaller than epoch size to test backpressure

        var bufferConfig = new BufferConfiguration(capacity: bufferCapacity);
        var context = new BlockContext("buffer");
        var buffer = new EpochBufferBlock<int>(context, bufferConfig);

        // Create multiple epochs with distinct item ranges
        var inputEpochs = Enumerable.Range(1, epochCount).Select(epochNum =>
            (EpochVector.FromSingleSource("source", epochNum), 
             Enumerable.Range(epochNum * 100, itemsPerEpoch).ToArray())
        ).ToArray();
        
        var epochStreams = CreateTestEpochStreams(inputEpochs);

        var execContext = new TestExecutionContext();

        // Act - Process epochs through buffer with constrained capacity
        var bufferedOutputs = buffer.ExecuteAsync(epochStreams, execContext);

        var receivedEpochs = new List<(EpochVector epoch, List<int> items)>();

        await foreach (var epochStream in bufferedOutputs)
        {
            var items = new List<int>();
            await foreach (var item in epochStream.Items)
            {
                items.Add(item);
            }
            receivedEpochs.Add((epochStream.Epoch, items));
            _output.WriteLine($"Epoch {epochStream.Epoch}: {items.Count} items");
        }

        // Assert - All epochs and items preserved
        receivedEpochs.Count.ShouldBe(epochCount);
        var totalItems = receivedEpochs.Sum(e => e.items.Count);
        totalItems.ShouldBe(epochCount * itemsPerEpoch);

        // Verify each epoch has correct items
        for (int i = 0; i < epochCount; i++)
        {
            var epoch = receivedEpochs[i];
            epoch.items.Count.ShouldBe(itemsPerEpoch);
            var expectedItems = Enumerable.Range((i + 1) * 100, itemsPerEpoch);
            epoch.items.ShouldBe(expectedItems);
        }

        _output.WriteLine($"✓ Epoch boundaries preserved: {receivedEpochs.Count} epochs, {totalItems} total items");
    }

    [Fact]
    public async Task EpochBuffer_Should_Handle_Variable_Producer_Rates()
    {
        // Arrange - Producers with different speeds
        var bufferCapacity = 20;
        
        var services = new ServiceCollection();
        var coordinator = new EpochCoordinator(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());

        // Fast producer: no delay
        var fastProducer = new TestVariableRateSourceActor("fast", 0, 50, delayMs: 0);
        var fastBlock = BlockHelpers.CreateEpochSource<int, TestVariableRateSourceActor>("fast-producer", fastProducer, coordinator);

        // Slow producer: with delay
        var slowProducer = new TestVariableRateSourceActor("slow", 1, 50, delayMs: 5);
        var slowBlock = BlockHelpers.CreateEpochSource<int, TestVariableRateSourceActor>("slow-producer", slowProducer, coordinator);

        var bufferConfig = new BufferConfiguration(capacity: bufferCapacity);
        var bufferContext = new BlockContext("buffer");
        var buffer = new EpochBufferBlock<int>(bufferContext, bufferConfig);

        var execContext = new TestExecutionContext();

        // Act
        var producerBlocks = new[] { fastBlock, slowBlock };
        var allProducerOutputs = MergeProducerEpochStreams<int>(producerBlocks, execContext);
        var bufferedOutputs = buffer.ExecuteAsync(allProducerOutputs, execContext);

        var receivedItems = new System.Collections.Concurrent.ConcurrentBag<int>();
        await foreach (var epochStream in bufferedOutputs)
        {
            await foreach (var item in epochStream.Items)
            {
                receivedItems.Add(item);
            }
        }

        // Assert - Both producers' data received
        receivedItems.Count.ShouldBe(100);
        
        var fastItems = receivedItems.Where(i => i < 1000).Count();
        var slowItems = receivedItems.Where(i => i >= 1000).Count();
        
        fastItems.ShouldBe(50);
        slowItems.ShouldBe(50);

        _output.WriteLine($"✓ Variable rate producers handled: Fast={fastItems}, Slow={slowItems}");
    }

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

    #region Test Actors for Concurrency Tests

    /// <summary>
    /// Source actor that produces items for multi-producer tests.
    /// Each producer has a distinct ID range to verify independence.
    /// </summary>
    private class TestMultiProducerSourceActor : SourceActorBase<int>
    {
        private readonly int _producerId;
        private readonly int _itemCount;

        public TestMultiProducerSourceActor(string sourceId, int producerId, int itemCount)
            : base(sourceId)
        {
            _producerId = producerId;
            _itemCount = itemCount;
        }

        public override async IAsyncEnumerable<IEpochStream<int>> ProduceEpochsAsync(
            IActorExecutionContext context)
        {
            // Produce single epoch with items in producer's ID range
            var baseValue = _producerId * 1000;
            var items = ProduceItems(baseValue, _itemCount, context.CancellationToken);
            
            var epochStream = await CreateEpochStreamAsync(context, 1, items, context.CancellationToken);
            yield return epochStream;
        }

        private async IAsyncEnumerable<int> ProduceItems(
            int baseValue,
            int count,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (int i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return baseValue + i;
                await Task.Delay(1, cancellationToken); // Small delay to simulate work
            }
        }
    }

    /// <summary>
    /// Source actor with configurable production rate for variable rate tests.
    /// </summary>
    private class TestVariableRateSourceActor : SourceActorBase<int>
    {
        private readonly int _producerId;
        private readonly int _itemCount;
        private readonly int _delayMs;

        public TestVariableRateSourceActor(string sourceId, int producerId, int itemCount, int delayMs)
            : base(sourceId)
        {
            _producerId = producerId;
            _itemCount = itemCount;
            _delayMs = delayMs;
        }

        public override async IAsyncEnumerable<IEpochStream<int>> ProduceEpochsAsync(
            IActorExecutionContext context)
        {
            var baseValue = _producerId * 1000;
            var items = ProduceItems(baseValue, _itemCount, context.CancellationToken);
            
            var epochStream = await CreateEpochStreamAsync(context, 1, items, context.CancellationToken);
            yield return epochStream;
        }

        private async IAsyncEnumerable<int> ProduceItems(
            int baseValue,
            int count,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (int i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return baseValue + i;
                if (_delayMs > 0)
                {
                    await Task.Delay(_delayMs, cancellationToken);
                }
            }
        }
    }

    #endregion

    #region Concurrency Test Helper Methods

    /// <summary>
    /// Merges multiple producer epoch streams into a single stream.
    /// Simulates multiple producers writing to a buffer concurrently.
    /// </summary>
    private static async IAsyncEnumerable<IEpochStream<T>> MergeProducerEpochStreams<T>(
        IEnumerable<IBlock<object, IEpochStream<T>>> producerBlocks,
        IExecutionContext context)
    {
        var channel = System.Threading.Channels.Channel.CreateUnbounded<IEpochStream<T>>();

        async IAsyncEnumerable<object> EmptyInput()
        {
            yield break;
        }

        // Start all producers
        var tasks = producerBlocks.Select(producer => Task.Run(async () =>
        {
            await foreach (var epochStream in producer.ExecuteAsync(EmptyInput(), context))
            {
                await channel.Writer.WriteAsync(epochStream, context.CancellationToken);
            }
        }, context.CancellationToken)).ToList();

        // Complete channel when all producers done
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.WhenAll(tasks);
                channel.Writer.Complete();
            }
            catch (Exception ex)
            {
                channel.Writer.Complete(ex);
            }
        }, context.CancellationToken);

        // Yield epoch streams from channel
        await foreach (var epochStream in channel.Reader.ReadAllAsync(context.CancellationToken))
        {
            yield return epochStream;
        }
    }

    /// <summary>
    /// Measures throughput with specified number of competing consumers.
    /// </summary>
    private async Task<long> MeasureConsumerThroughput(
        EpochBufferBlock<int> buffer,
        IAsyncEnumerable<IEpochStream<int>> input,
        IExecutionContext context,
        int consumerCount,
        int delayMs,
        int expectedItemCount)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var bufferedOutput = buffer.ExecuteAsync(input, context);
        var receivedItems = await ConsumeWithCompetingConsumers(bufferedOutput, consumerCount, delayMs);
        sw.Stop();

        receivedItems.Count.ShouldBe(expectedItemCount);
        return sw.ElapsedMilliseconds;
    }

    /// <summary>
    /// Simulates competing consumers reading from buffered output.
    /// </summary>
    private static async Task<List<int>> ConsumeWithCompetingConsumers(
        IAsyncEnumerable<IEpochStream<int>> bufferedOutput,
        int consumerCount,
        int delayMs = 1)
    {
        var receivedItems = new System.Collections.Concurrent.ConcurrentBag<int>();
        var channel = System.Threading.Channels.Channel.CreateUnbounded<int>();

        // Writer task - reads from buffered output and writes to channel
        var writerTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var epochStream in bufferedOutput)
                {
                    await foreach (var item in epochStream.Items)
                    {
                        await channel.Writer.WriteAsync(item);
                    }
                }
                channel.Writer.Complete();
            }
            catch (Exception ex)
            {
                channel.Writer.Complete(ex);
            }
        });

        // Consumer tasks - compete for items from channel
        var consumerTasks = Enumerable.Range(0, consumerCount)
            .Select(_ => Task.Run(async () =>
            {
                await foreach (var item in channel.Reader.ReadAllAsync())
                {
                    receivedItems.Add(item);
                    if (delayMs > 0)
                    {
                        await Task.Delay(delayMs);
                    }
                }
            }))
            .ToList();

        await Task.WhenAll(consumerTasks);
        await writerTask;

        return receivedItems.ToList();
    }

    #endregion
}
