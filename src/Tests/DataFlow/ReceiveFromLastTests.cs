namespace Tests.DataFlow;

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

/// <summary>
/// Tests for the ReceiveFromLast() API that enables positional chaining
/// </summary>
[IntegrationTest]
public class ReceiveFromLastTests
{
    public ITestOutputHelper Output { get; }
    public ServiceCollection Services { get; }

    public ReceiveFromLastTests(ITestOutputHelper output)
    {
        Output = output;
        Services = new ServiceCollection();
        AddDefaultServices(Services);
    }

    private void AddDefaultServices(IServiceCollection services)
    {
        Services.AddLogging(builder => builder.AddXUnit(Output));
        Services.AddDataFlowMetrics();
        Services.AddDataFlows();
    }

    [Fact]
    public async Task ReceiveFromLast_ConnectsToLastAddedSourceBlock()
    {
        // Arrange
        var sp = Services.BuildServiceProvider();
        var items = new[] { "item1", "item2", "item3" };
        var processedItems = new List<string>();

        var builder = new DataFlowBuilder(sp);

        // Act
        builder
            .AddProducer("source", sp => new TestProducer<string>(items))
            .AddProcessor<string, TestProcessor<string>>("processor",
                sp => new TestProcessor<string>(onProcessItem: item => processedItems.Add(item)))
            .ReceiveFromLast(); // Connect to the last added source block (the producer)

        var flow = builder.Build();
        var context = DataFlowContextTestUtils.GetContext("test", Guid.NewGuid(), sp);

        await flow.ExecuteAsync(context);

        // Assert
        Assert.Equal(3, processedItems.Count);
        Assert.Equal(items, processedItems);
    }

    [Fact]
    public async Task ReceiveFromLast_WorksWithMultipleBlocks()
    {
        // Arrange
        var sp = Services.BuildServiceProvider();
        var items = new[] { 1, 2, 3, 4, 5 };
        var processedItems = new List<int>();

        var builder = new DataFlowBuilder(sp);

        // Act - Chain multiple blocks using ReceiveFromLast
        builder
            .AddProducer("source", sp => new TestProducer<int>(items))
            .AddTransform<int, int>("doubler", sp => new DoublerTransformer())
            .ReceiveFrom("source")  // Connect to producer
            .AddProcessor<int, TestProcessor<int>>("processor",
                sp => new TestProcessor<int>(onProcessItem: item => processedItems.Add(item)))
            .ReceiveFromLast();  // Connect to the last propagator (doubler)

        var flow = builder.Build();
        var context = DataFlowContextTestUtils.GetContext("test", Guid.NewGuid(), sp);

        await flow.ExecuteAsync(context);

        // Assert
        Assert.Equal(5, processedItems.Count);
        Assert.Equal(new[] { 2, 4, 6, 8, 10 }, processedItems);
    }

    [Fact]
    public async Task ReceiveFromLast_WorksWithBatchBlocks()
    {
        // Arrange
        var sp = Services.BuildServiceProvider();
        var items = Enumerable.Range(1, 10).Select(i => $"item{i}").ToArray();
        var processedBatches = new List<string[]>();

        var builder = new DataFlowBuilder(sp);

        // Act
        builder
            .AddProducer("source", sp => new TestProducer<string>(items))
            .AddBatch<string>("batcher", maxBatchSize: 3, windowPeriod: TimeSpan.FromSeconds(10))
            .ReceiveFrom("source")  // Explicitly connect to producer
            .AddProcessor<string[], TestProcessor<string[]>>("processor",
                sp => new TestProcessor<string[]>(onProcessItem: batch => processedBatches.Add(batch)))
            .ReceiveFromLast();  // Connect to batcher

        var flow = builder.Build();
        var context = DataFlowContextTestUtils.GetContext("test", Guid.NewGuid(), sp);

        await flow.ExecuteAsync(context);

        // Assert
        Assert.Equal(4, processedBatches.Count); // 3 full batches + 1 partial
    }

    [Fact]
    public void ReceiveFromLast_ThrowsWhenNoSourceBlockAdded()
    {
        // Arrange
        var sp = Services.BuildServiceProvider();
        var builder = new DataFlowBuilder(sp);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            builder
                .AddProcessor<string, TestProcessor<string>>("processor",
                    sp => new TestProcessor<string>())
                .ReceiveFromLast(); // Should throw - no source block added yet
        });

        Assert.Contains("No source block of type String has been added yet", exception.Message);
    }

    [Fact]
    public void ReceiveFromLast_ThrowsWhenLastBlockIsNotSourceOfCorrectType()
    {
        // Arrange
        var sp = Services.BuildServiceProvider();
        var builder = new DataFlowBuilder(sp);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            builder
                .AddProducer("intSource", sp => new TestProducer<int>(new[] { 1, 2, 3 }))
                .AddProcessor<string, TestProcessor<string>>("stringProcessor",
                    sp => new TestProcessor<string>())
                .ReceiveFromLast(); // Should throw - type mismatch
        });

        Assert.Contains("No source block of type String has been added yet", exception.Message);
    }

    /// <summary>
    /// Simple transformer that doubles integers
    /// </summary>
    private class DoublerTransformer : IStreamTransformer<int, int>
    {
        public async IAsyncEnumerable<int> TransformAsync(IDataFlowContext context, IAsyncEnumerable<int> input, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var item in input.WithCancellation(cancellationToken))
            {
                yield return item * 2;
            }
        }
    }
}
