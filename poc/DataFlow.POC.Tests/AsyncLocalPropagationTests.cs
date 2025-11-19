namespace DataFlow.POC.Tests;

using System.Collections.Concurrent;
using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using DataFlow.POC.Tests.TestHelpers;

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
    /// Actor that collects items and captures their execution context IDs.
    /// </summary>
    private class ContextCapturingCollectorActor<T> : IStreamActor<T, object>
    {
        private readonly ConcurrentBag<Guid> _capturedContextIds;

        public ContextCapturingCollectorActor(ConcurrentBag<Guid> capturedContextIds)
        {
            _capturedContextIds = capturedContextIds;
        }

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<T> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                var current = ExecutionContext.Current;
                if (current != null)
                {
                    _capturedContextIds.Add(current.InvocationId);
                }
            }
            yield break;
        }
    }

    /// <summary>
    /// Actor that transforms items and captures execution context IDs.
    /// </summary>
    private class ContextCapturingTransformerActor : IStreamActor<int, string>
    {
        private readonly ConcurrentBag<Guid> _capturedContextIds;

        public ContextCapturingTransformerActor(ConcurrentBag<Guid> capturedContextIds)
        {
            _capturedContextIds = capturedContextIds;
        }

        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                await foreach (var result in TransformWithContextCapture(item, _capturedContextIds))
                {
                    yield return result;
                }
            }
        }
    }

    /// <summary>
    /// Tests that AsyncLocal propagates in a simple producer-processor flow
    /// </summary>
    [Fact]
    public async Task AsyncLocal_Should_Propagate_In_Simple_Producer_Processor_Flow()
    {
        // Arrange
        var expectedContextId = Guid.NewGuid();
        var capturedContextIds = new ConcurrentBag<Guid>();
        
        // Create service provider for the processor
        var processorServices = new ServiceCollection();
        processorServices.AddScoped(_ => new ContextCapturingCollectorActor<int>(capturedContextIds));
        var processorSP = processorServices.BuildServiceProvider();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = BlockHelpers.CreateProducer<int>("producer", ctx => {
            return ProduceWithContextCapture(5, capturedContextIds);
        });

        var processor = BlockHelpers.CreateActor<int, object, ContextCapturingCollectorActor<int>>(
            "processor",
            processorSP.GetRequiredService<IServiceScopeFactory>());

        var builder = new DataFlowGraphBuilder("simple-flow");
        builder.AddBlock(producer)
            .AddBlock(processor)
            .Connect(producer, processor);

        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None, expectedContextId);

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
        
        // Create service provider for the processor
        var processorServices = new ServiceCollection();
        processorServices.AddScoped(_ => new ContextCapturingCollectorActor<int>(capturedContextIds));
        var processorSP = processorServices.BuildServiceProvider();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        var concurrentProducer = BlockHelpers.CreateConcurrentProducer<int>("concurrent-producer",
            ctx => new[]
            {
                ProduceWithContextCapture(5, capturedContextIds),
                ProduceWithContextCapture(5, capturedContextIds)
            },
            2);

        var processor = BlockHelpers.CreateActor<int, object, ContextCapturingCollectorActor<int>>(
            "processor",
            processorSP.GetRequiredService<IServiceScopeFactory>());

        var builder = new DataFlowGraphBuilder("concurrent-flow");
        builder.AddBlock(concurrentProducer)
            .AddBlock(processor)
            .Connect(concurrentProducer, processor);

        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None, expectedContextId);

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
        
        // Create service providers for transformer and processor
        var transformerServices = new ServiceCollection();
        transformerServices.AddScoped(_ => new ContextCapturingTransformerActor(transformerContextIds));
        var transformerSP = transformerServices.BuildServiceProvider();

        var processorServices = new ServiceCollection();
        processorServices.AddScoped(_ => new ContextCapturingCollectorActor<string>(processorContextIds));
        var processorSP = processorServices.BuildServiceProvider();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = BlockHelpers.CreateProducer<int>("producer", ctx => {
            return ProduceWithContextCapture(5, producerContextIds);
        });

        var transformer = BlockHelpers.CreateActor<int, string, ContextCapturingTransformerActor>("transformer", transformerSP.GetRequiredService<IServiceScopeFactory>());

        var processor = BlockHelpers.CreateActor<string, object, ContextCapturingCollectorActor<string>>(
            "processor",
            processorSP.GetRequiredService<IServiceScopeFactory>());

        var builder = new DataFlowGraphBuilder("pipeline-flow");
        builder.AddBlock(producer)
            .AddBlock(transformer)
            .AutoConnect()
            .AddBlock(processor)
            .AutoConnect();

        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None, expectedContextId);

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
        
        // Create separate service providers for each flow's processor
        var processor1Services = new ServiceCollection();
        processor1Services.AddScoped(_ => new ContextCapturingCollectorActor<int>(capturedIds1));
        var processor1SP = processor1Services.BuildServiceProvider();

        var processor2Services = new ServiceCollection();
        processor2Services.AddScoped(_ => new ContextCapturingCollectorActor<int>(capturedIds2));
        var processor2SP = processor2Services.BuildServiceProvider();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer1 = BlockHelpers.CreateProducer<int>("producer1", ctx => {
            return ProduceWithContextCapture(5, capturedIds1, delay: 10);
        });

        var processor1 = BlockHelpers.CreateActor<int, object, ContextCapturingCollectorActor<int>>(
            "processor1",
            processor1SP.GetRequiredService<IServiceScopeFactory>());

        var producer2 = BlockHelpers.CreateProducer<int>("producer2", ctx => {
            return ProduceWithContextCapture(5, capturedIds2, delay: 10);
        });

        var processor2 = BlockHelpers.CreateActor<int, object, ContextCapturingCollectorActor<int>>(
            "processor2",
            processor2SP.GetRequiredService<IServiceScopeFactory>());

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

        var context1 = new ExecutionContext(commonServices, CancellationToken.None, context1Id);
        var context2 = new ExecutionContext(commonServices, CancellationToken.None, context2Id);

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
        
        // Create separate service providers for each processor
        var processor1Services = new ServiceCollection();
        processor1Services.AddScoped(_ => new ContextCapturingCollectorActor<int>(processor1ContextIds));
        var processor1SP = processor1Services.BuildServiceProvider();

        var processor2Services = new ServiceCollection();
        processor2Services.AddScoped(_ => new ContextCapturingCollectorActor<int>(processor2ContextIds));
        var processor2SP = processor2Services.BuildServiceProvider();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = BlockHelpers.CreateProducer<int>("producer", ctx => {
            return ProduceWithContextCapture(5, producer1ContextIds);
        });

        var broadcast = BlockHelpers.CreateBroadcast<int>("broadcast");

        var processor1 = BlockHelpers.CreateActor<int, object, ContextCapturingCollectorActor<int>>(
            "processor1",
            processor1SP.GetRequiredService<IServiceScopeFactory>());

        var processor2 = BlockHelpers.CreateActor<int, object, ContextCapturingCollectorActor<int>>(
            "processor2",
            processor2SP.GetRequiredService<IServiceScopeFactory>());

        var builder = new DataFlowGraphBuilder("broadcast-flow");
        builder.AddBlock(producer)
            .AddBlock(broadcast)
            .Connect(producer, broadcast)
            .AddBlock(processor1)
            .Connect(broadcast, processor1)
            .AddBlock(processor2)
            .Connect(broadcast, processor2);

        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None, expectedContextId);

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
        
        // Create service provider for the processor
        var processorServices = new ServiceCollection();
        processorServices.AddScoped(_ => new ContextCapturingCollectorActor<int[]>(processorContextIds));
        var processorSP = processorServices.BuildServiceProvider();

        var commonServices = new ServiceCollection().BuildServiceProvider();

        var producer = BlockHelpers.CreateProducer<int>("producer", ctx => {
            return ProduceWithContextCapture(10, producerContextIds);
        });

        var batch = BlockHelpers.CreateBatch<int>("batch", 3);

        var processor = BlockHelpers.CreateActor<int[], object, ContextCapturingCollectorActor<int[]>>(
            "processor",
            processorSP.GetRequiredService<IServiceScopeFactory>());

        var builder = new DataFlowGraphBuilder("batch-flow");
        builder.AddBlock(producer)
            .AddBlock(batch)
            .Connect(producer, batch)
            .AddBlock(processor)
            .Connect(batch, processor);

        var graph = builder.Build();
        var context = new ExecutionContext(commonServices, CancellationToken.None, expectedContextId);

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
