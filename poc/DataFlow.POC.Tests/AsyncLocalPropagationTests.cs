namespace DataFlow.POC.Tests;

using System.Collections.Concurrent;
using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

/// <summary>
/// Tests to verify that AsyncLocal values (specifically ExecutionContext.Current) 
/// propagate correctly across POC DataFlow execution patterns, including:
/// - Task.Run() execution in ConcurrentProducerBlock
/// - Multiple concurrent producers
/// - Multi-block pipelines
/// - Complex flows with transformers and processors
/// </summary>
public class AsyncLocalPropagationTests
{
    /// <summary>
    /// Tests that AsyncLocal propagates in a simple producer-processor flow
    /// </summary>
    [Fact]
    public async Task AsyncLocal_Should_Propagate_In_Simple_Producer_Processor_Flow()
    {
        // Arrange
        var expectedContextId = Guid.NewGuid();
        var capturedContextIds = new ConcurrentBag<Guid>();
        var services = new ServiceCollection().BuildServiceProvider();

        var producer = new ProducerBlock<int>("producer", ctx =>
        {
            return ProduceWithContextCapture(5, capturedContextIds);
        });

        var processor = new ProcessorBlock<int>("processor", async (item, ctx) =>
        {
            var current = ExecutionContext.Current;
            if (current != null)
            {
                capturedContextIds.Add(current.InvocationId);
            }
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("simple-flow");
        builder.AddBlock(producer)
            .AddBlock(processor)
            .Connect(producer, processor);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None, expectedContextId);

        // Act
        ExecutionContext.Current = context;
        await graph.ExecuteAsync(context);

        // Assert
        capturedContextIds.Count.ShouldBe(10); // 5 from producer, 5 from processor
        capturedContextIds.ShouldAllBe(id => id == expectedContextId,
            "All captured context IDs should match the expected context ID");
    }

    /// <summary>
    /// Tests that AsyncLocal propagates across Task.Run() in ConcurrentProducerBlock
    /// This is the critical test since the POC uses Task.Run() for concurrent producers
    /// </summary>
    [Fact]
    public async Task AsyncLocal_Should_Propagate_In_ConcurrentProducerBlock_WithTaskRun()
    {
        // Arrange
        var expectedContextId = Guid.NewGuid();
        var capturedContextIds = new ConcurrentBag<Guid>();
        var services = new ServiceCollection().BuildServiceProvider();

        var concurrentProducer = new ConcurrentProducerBlock<int>("concurrent-producer",
            ctx => new[]
            {
                ProduceWithContextCapture(5, capturedContextIds),
                ProduceWithContextCapture(5, capturedContextIds)
            },
            maxConcurrency: 2);

        var processor = new ProcessorBlock<int>("processor", async (item, ctx) =>
        {
            var current = ExecutionContext.Current;
            if (current != null)
            {
                capturedContextIds.Add(current.InvocationId);
            }
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("concurrent-flow");
        builder.AddBlock(concurrentProducer)
            .AddBlock(processor)
            .Connect(concurrentProducer, processor);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None, expectedContextId);

        // Act
        ExecutionContext.Current = context;
        await graph.ExecuteAsync(context);

        // Assert
        capturedContextIds.Count.ShouldBe(20); // 10 from producers (2x5), 10 from processor
        capturedContextIds.ShouldAllBe(id => id == expectedContextId,
            "AsyncLocal should propagate across Task.Run() boundaries in ConcurrentProducerBlock");
    }

    /// <summary>
    /// Tests AsyncLocal propagation in a multi-block pipeline with transformer
    /// </summary>
    [Fact]
    public async Task AsyncLocal_Should_Propagate_Through_MultiBlock_Pipeline()
    {
        // Arrange
        var expectedContextId = Guid.NewGuid();
        var producerContextIds = new ConcurrentBag<Guid>();
        var transformerContextIds = new ConcurrentBag<Guid>();
        var processorContextIds = new ConcurrentBag<Guid>();
        var services = new ServiceCollection().BuildServiceProvider();

        var producer = new ProducerBlock<int>("producer", ctx =>
        {
            return ProduceWithContextCapture(5, producerContextIds);
        });

        var transformer = new TransformerBlock<int, string>("transformer",
            (item, ctx) =>
            {
                return TransformWithContextCapture(item, transformerContextIds);
            });

        var processor = new ProcessorBlock<string>("processor", async (item, ctx) =>
        {
            var current = ExecutionContext.Current;
            if (current != null)
            {
                processorContextIds.Add(current.InvocationId);
            }
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("pipeline-flow");
        builder.AddBlock(producer)
            .AddBlock(transformer)
            .AutoConnect()
            .AddBlock(processor)
            .AutoConnect();

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None, expectedContextId);

        // Act
        ExecutionContext.Current = context;
        await graph.ExecuteAsync(context);

        // Assert
        producerContextIds.Count.ShouldBe(5);
        transformerContextIds.Count.ShouldBe(5);
        processorContextIds.Count.ShouldBe(5);

        producerContextIds.ShouldAllBe(id => id == expectedContextId);
        transformerContextIds.ShouldAllBe(id => id == expectedContextId);
        processorContextIds.ShouldAllBe(id => id == expectedContextId);
    }

    /// <summary>
    /// Tests that AsyncLocal contexts are isolated between concurrent flow executions
    /// </summary>
    [Fact]
    public async Task AsyncLocal_Should_Be_Isolated_Between_Concurrent_Flows()
    {
        // Arrange
        var context1Id = Guid.NewGuid();
        var context2Id = Guid.NewGuid();
        var capturedIds1 = new ConcurrentBag<Guid>();
        var capturedIds2 = new ConcurrentBag<Guid>();
        var services = new ServiceCollection().BuildServiceProvider();

        var producer1 = new ProducerBlock<int>("producer1", ctx =>
        {
            return ProduceWithContextCapture(5, capturedIds1, delay: 10);
        });

        var processor1 = new ProcessorBlock<int>("processor1", async (item, ctx) =>
        {
            var current = ExecutionContext.Current;
            if (current != null)
            {
                capturedIds1.Add(current.InvocationId);
            }
            await Task.Delay(5); // Add some delay to ensure overlap
        });

        var producer2 = new ProducerBlock<int>("producer2", ctx =>
        {
            return ProduceWithContextCapture(5, capturedIds2, delay: 10);
        });

        var processor2 = new ProcessorBlock<int>("processor2", async (item, ctx) =>
        {
            var current = ExecutionContext.Current;
            if (current != null)
            {
                capturedIds2.Add(current.InvocationId);
            }
            await Task.Delay(5); // Add some delay to ensure overlap
        });

        var builder1 = new DataFlowGraphBuilder("flow1");
        builder1.AddBlock(producer1)
            .AddBlock(processor1)
            .Connect(producer1, processor1);

        var builder2 = new DataFlowGraphBuilder("flow2");
        builder2.AddBlock(producer2)
            .AddBlock(processor2)
            .Connect(producer2, processor2);

        var graph1 = builder1.Build();
        var graph2 = builder2.Build();

        var context1 = new ExecutionContext(services, CancellationToken.None, context1Id);
        var context2 = new ExecutionContext(services, CancellationToken.None, context2Id);

        // Act - Execute both flows concurrently
        var task1 = Task.Run(async () =>
        {
            ExecutionContext.Current = context1;
            await graph1.ExecuteAsync(context1);
        });

        var task2 = Task.Run(async () =>
        {
            ExecutionContext.Current = context2;
            await graph2.ExecuteAsync(context2);
        });

        await Task.WhenAll(task1, task2);

        // Assert
        capturedIds1.Count.ShouldBe(10); // 5 from producer, 5 from processor
        capturedIds2.Count.ShouldBe(10);

        capturedIds1.ShouldAllBe(id => id == context1Id,
            "Flow 1 should only see its own context");
        capturedIds2.ShouldAllBe(id => id == context2Id,
            "Flow 2 should only see its own context");

        // Ensure no cross-contamination
        capturedIds1.ShouldNotContain(context2Id);
        capturedIds2.ShouldNotContain(context1Id);
    }

    /// <summary>
    /// Tests AsyncLocal propagation with broadcast block (multiple consumers)
    /// </summary>
    [Fact]
    public async Task AsyncLocal_Should_Propagate_Through_BroadcastBlock()
    {
        // Arrange
        var expectedContextId = Guid.NewGuid();
        var producer1ContextIds = new ConcurrentBag<Guid>();
        var processor1ContextIds = new ConcurrentBag<Guid>();
        var processor2ContextIds = new ConcurrentBag<Guid>();
        var services = new ServiceCollection().BuildServiceProvider();

        var producer = new ProducerBlock<int>("producer", ctx =>
        {
            return ProduceWithContextCapture(5, producer1ContextIds);
        });

        var broadcast = new BroadcastBlock<int>("broadcast");

        var processor1 = new ProcessorBlock<int>("processor1", async (item, ctx) =>
        {
            var current = ExecutionContext.Current;
            if (current != null)
            {
                processor1ContextIds.Add(current.InvocationId);
            }
            await Task.CompletedTask;
        });

        var processor2 = new ProcessorBlock<int>("processor2", async (item, ctx) =>
        {
            var current = ExecutionContext.Current;
            if (current != null)
            {
                processor2ContextIds.Add(current.InvocationId);
            }
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("broadcast-flow");
        builder.AddBlock(producer)
            .AddBlock(broadcast)
            .Connect(producer, broadcast)
            .AddBlock(processor1)
            .Connect(broadcast, processor1)
            .AddBlock(processor2)
            .Connect(broadcast, processor2);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None, expectedContextId);

        // Act
        ExecutionContext.Current = context;
        await graph.ExecuteAsync(context);

        // Assert
        producer1ContextIds.Count.ShouldBe(5);
        processor1ContextIds.Count.ShouldBe(5);
        processor2ContextIds.Count.ShouldBe(5);

        producer1ContextIds.ShouldAllBe(id => id == expectedContextId);
        processor1ContextIds.ShouldAllBe(id => id == expectedContextId);
        processor2ContextIds.ShouldAllBe(id => id == expectedContextId);
    }

    /// <summary>
    /// Tests AsyncLocal propagation with batch block
    /// </summary>
    [Fact]
    public async Task AsyncLocal_Should_Propagate_Through_BatchBlock()
    {
        // Arrange
        var expectedContextId = Guid.NewGuid();
        var producerContextIds = new ConcurrentBag<Guid>();
        var processorContextIds = new ConcurrentBag<Guid>();
        var services = new ServiceCollection().BuildServiceProvider();

        var producer = new ProducerBlock<int>("producer", ctx =>
        {
            return ProduceWithContextCapture(10, producerContextIds);
        });

        var batch = new BatchBlock<int>("batch", maxBatchSize: 3);

        var processor = new ProcessorBlock<int[]>("processor", async (batch, ctx) =>
        {
            var current = ExecutionContext.Current;
            if (current != null)
            {
                processorContextIds.Add(current.InvocationId);
            }
            await Task.CompletedTask;
        });

        var builder = new DataFlowGraphBuilder("batch-flow");
        builder.AddBlock(producer)
            .AddBlock(batch)
            .Connect(producer, batch)
            .AddBlock(processor)
            .Connect(batch, processor);

        var graph = builder.Build();
        var context = new ExecutionContext(services, CancellationToken.None, expectedContextId);

        // Act
        ExecutionContext.Current = context;
        await graph.ExecuteAsync(context);

        // Assert
        producerContextIds.Count.ShouldBe(10);
        processorContextIds.Count.ShouldBeGreaterThan(0); // At least one batch

        producerContextIds.ShouldAllBe(id => id == expectedContextId);
        processorContextIds.ShouldAllBe(id => id == expectedContextId);
    }

    #region Helper Methods

    private static async IAsyncEnumerable<int> ProduceWithContextCapture(
        int count,
        ConcurrentBag<Guid> capturedContextIds,
        int delay = 1)
    {
        for (int i = 1; i <= count; i++)
        {
            // Capture the AsyncLocal context
            var current = ExecutionContext.Current;
            if (current != null)
            {
                capturedContextIds.Add(current.InvocationId);
            }

            await Task.Delay(delay);
            yield return i;
        }
    }

    private static async IAsyncEnumerable<string> TransformWithContextCapture(
        int item,
        ConcurrentBag<Guid> capturedContextIds)
    {
        // Capture the AsyncLocal context
        var current = ExecutionContext.Current;
        if (current != null)
        {
            capturedContextIds.Add(current.InvocationId);
        }

        await Task.Delay(1);
        yield return $"Item-{item}";
    }

    #endregion
}
