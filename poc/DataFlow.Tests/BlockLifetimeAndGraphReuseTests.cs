namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using DataFlow.POC.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using DataFlow.POC.Tests.TestHelpers;
using System.Collections.Concurrent;

/// <summary>
/// Tests for block lifetime management and dataflow graph reuse patterns.
/// Validates that blocks registered globally can be safely reused across multiple graph executions.
/// </summary>
public class BlockLifetimeAndGraphReuseTests
{
    #region Test Scenario 1: Two Different Graphs Using Same Global Block - Concurrent Execution

    [Fact]
    public async Task TwoGraphs_UsingSameGlobalBlock_ExecuteConcurrently_CreateSeparateBlockInstances()
    {
        // Arrange - Register ONE block globally, but TWO different graphs
        var services = new ServiceCollection();
        var instanceTracker = new BlockInstanceTracker();
        
        services.AddSingleton(instanceTracker);
        services.AddDataFlows("global", df =>
        {
            // ONE shared block registration
            df.AddActorBlock<int, string, InstanceTrackingActor>("shared-transformer");
            
            // TWO different graph registrations
            df.AddGraph("graph-a", g =>
            {
                g.UseBlock("shared-transformer");
            });
            
            df.AddGraph("graph-b", g =>
            {
                g.UseBlock("shared-transformer");
            });
        });

        var serviceProvider = services.BuildServiceProvider();
        
        // Act - Execute both graphs concurrently
        var graphA = serviceProvider.GetRequiredKeyedService<DataFlowGraph>("global:graph-a");
        var graphB = serviceProvider.GetRequiredKeyedService<DataFlowGraph>("global:graph-b");
        
        var contextA = new ExecutionContext(serviceProvider, CancellationToken.None);
        var contextB = new ExecutionContext(serviceProvider, CancellationToken.None);
        
        var taskA = graphA.ExecuteAsync(contextA);
        var taskB = graphB.ExecuteAsync(contextB);
        
        await Task.WhenAll(taskA, taskB);
        
        // Assert - Two different block instances were created (one per execution scope)
        instanceTracker.InstanceIds.Count.ShouldBe(2);
        instanceTracker.InstanceIds[0].ShouldNotBe(instanceTracker.InstanceIds[1]);
    }

    [Fact]
    public async Task TwoGraphs_UsingSameGlobalBlock_WithDifferentInputs_ProcessIndependently()
    {
        // Arrange
        var services = new ServiceCollection();
        var resultsA = new ConcurrentBag<string>();
        var resultsB = new ConcurrentBag<string>();
        
        services.AddDataFlows("global", df =>
        {
            // Shared transformer block
            df.AddActorBlock<int, string, TransformActor<int, string>>("transformer");
            
            // Graph A: processes 1-5
            df.AddGraph("graph-a", g =>
            {
                // Note: Graph configuration doesn't include blocks - they're resolved when graph executes
                g.UseBlock("transformer");
            });
            
            // Graph B: processes 6-10
            df.AddGraph("graph-b", g =>
            {
                g.UseBlock("transformer");
            });
        });

        var serviceProvider = services.BuildServiceProvider();
        
        // Create separate input sources for each graph execution
        var producerA = BlockHelpers.CreateProducer("producer-a", TestStreams.Range(1, 5));
        var collectorAList = new List<string>();
        var collectorA = BlockHelpers.CreateActor<string, object, CollectorActor<string>>(
            "collector-a",
            new CollectorActor<string>(collectorAList));
        
        var producerB = BlockHelpers.CreateProducer("producer-b", TestStreams.Range(6, 10));
        var collectorBList = new List<string>();
        var collectorB = BlockHelpers.CreateActor<string, object, CollectorActor<string>>(
            "collector-b",
            new CollectorActor<string>(collectorBList));
        
        // Build complete graphs with inputs/outputs
        var builderA = GraphHelpers.CreateGraphBuilder("test-a", serviceProvider);
        builderA.AddBlock(producerA);
        builderA.UseBlock("transformer");
        var transformerA = builderA.Build().Blocks.First(b => b.Name == "global:transformer");
        
        builderA = GraphHelpers.CreateGraphBuilder("test-a", serviceProvider);
        builderA.AddBlock(producerA)
            .AddBlock(transformerA)
            .AddBlock(collectorA)
            .Connect(producerA, transformerA)
            .Connect(transformerA, collectorA);
        var graphA = builderA.Build();
        
        var builderB = GraphHelpers.CreateGraphBuilder("test-b", serviceProvider);
        builderB.AddBlock(producerB);
        builderB.UseBlock("transformer");
        var transformerB = builderB.Build().Blocks.First(b => b.Name == "global:transformer");
        
        builderB = GraphHelpers.CreateGraphBuilder("test-b", serviceProvider);
        builderB.AddBlock(producerB)
            .AddBlock(transformerB)
            .AddBlock(collectorB)
            .Connect(producerB, transformerB)
            .Connect(transformerB, collectorB);
        var graphB = builderB.Build();
        
        // Act - Execute concurrently
        var contextA = new ExecutionContext(serviceProvider, CancellationToken.None);
        var contextB = new ExecutionContext(serviceProvider, CancellationToken.None);
        
        var taskA = graphA.ExecuteAsync(contextA);
        var taskB = graphB.ExecuteAsync(contextB);
        
        await Task.WhenAll(taskA, taskB);
        
        // Assert - Each graph processed its own data independently
        collectorAList.Count.ShouldBe(5);
        collectorBList.Count.ShouldBe(5);
        
        // Results should contain transformed values from their respective inputs
        collectorAList.ShouldContain(s => s.Contains("1"));
        collectorAList.ShouldContain(s => s.Contains("5"));
        
        collectorBList.ShouldContain(s => s.Contains("6"));
        collectorBList.ShouldContain(s => s.Contains("10"));
    }

    #endregion

    #region Test Scenario 2: Same Graph Executed Multiple Times Concurrently

    [Fact]
    public async Task SameGraph_ExecutedTwiceConcurrently_CreatesSeparateBlockInstances()
    {
        // Arrange
        var services = new ServiceCollection();
        var instanceTracker = new BlockInstanceTracker();
        
        services.AddSingleton(instanceTracker);
        services.AddDataFlows("global", df =>
        {
            df.AddActorBlock<int, string, InstanceTrackingActor>("transformer");
            
            df.AddGraph("main", g =>
            {
                g.UseBlock("transformer");
            });
        });

        var serviceProvider = services.BuildServiceProvider();
        var graph = serviceProvider.GetRequiredKeyedService<DataFlowGraph>("global:main");
        
        // Act - Execute the SAME graph twice concurrently in different scopes
        using var scope1 = serviceProvider.CreateScope();
        using var scope2 = serviceProvider.CreateScope();
        
        var context1 = new ExecutionContext(scope1.ServiceProvider, CancellationToken.None);
        var context2 = new ExecutionContext(scope2.ServiceProvider, CancellationToken.None);
        
        var task1 = graph.ExecuteAsync(context1);
        var task2 = graph.ExecuteAsync(context2);
        
        await Task.WhenAll(task1, task2);
        
        // Assert - Two different instances created (one per scope)
        instanceTracker.InstanceIds.Count.ShouldBe(2);
        instanceTracker.InstanceIds[0].ShouldNotBe(instanceTracker.InstanceIds[1]);
    }

    #endregion

    #region Test Scenario 3: Sequential Execution Should Also Create New Instances Per Scope

    [Fact]
    public async Task SameGraph_ExecutedSequentiallyInDifferentScopes_CreatesNewInstancesPerScope()
    {
        // Arrange
        var services = new ServiceCollection();
        var instanceTracker = new BlockInstanceTracker();
        
        services.AddSingleton(instanceTracker);
        services.AddDataFlows("global", df =>
        {
            df.AddActorBlock<int, string, InstanceTrackingActor>("transformer");
            
            df.AddGraph("main", g =>
            {
                g.UseBlock("transformer");
            });
        });

        var serviceProvider = services.BuildServiceProvider();
        var graph = serviceProvider.GetRequiredKeyedService<DataFlowGraph>("global:main");
        
        // Act - Execute sequentially in different scopes
        using (var scope1 = serviceProvider.CreateScope())
        {
            var context1 = new ExecutionContext(scope1.ServiceProvider, CancellationToken.None);
            await graph.ExecuteAsync(context1);
        }
        
        using (var scope2 = serviceProvider.CreateScope())
        {
            var context2 = new ExecutionContext(scope2.ServiceProvider, CancellationToken.None);
            await graph.ExecuteAsync(context2);
        }
        
        // Assert - Two different instances (one per scope)
        instanceTracker.InstanceIds.Count.ShouldBe(2);
        instanceTracker.InstanceIds[0].ShouldNotBe(instanceTracker.InstanceIds[1]);
    }

    #endregion

    #region Test Scenario 4: Scoped Services Within Blocks Are Isolated

    [Fact]
    public async Task TwoGraphs_WithScopedDependencies_HaveIsolatedServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var scopedServiceTracker = new ScopedServiceTracker();
        
        services.AddScoped<TrackedScopedService>();
        services.AddSingleton(scopedServiceTracker);
        
        services.AddDataFlows("global", df =>
        {
            df.AddActorBlock<int, string, ScopedServiceUsingActor>("transformer");
            
            df.AddGraph("graph-a", g => g.UseBlock("transformer"));
            df.AddGraph("graph-b", g => g.UseBlock("transformer"));
        });

        var serviceProvider = services.BuildServiceProvider();
        
        // Act - Execute both graphs concurrently
        var graphA = serviceProvider.GetRequiredKeyedService<DataFlowGraph>("global:graph-a");
        var graphB = serviceProvider.GetRequiredKeyedService<DataFlowGraph>("global:graph-b");
        
        using var scopeA = serviceProvider.CreateScope();
        using var scopeB = serviceProvider.CreateScope();
        
        var contextA = new ExecutionContext(scopeA.ServiceProvider, CancellationToken.None);
        var contextB = new ExecutionContext(scopeB.ServiceProvider, CancellationToken.None);
        
        var taskA = graphA.ExecuteAsync(contextA);
        var taskB = graphB.ExecuteAsync(contextB);
        
        await Task.WhenAll(taskA, taskB);
        
        // Assert - Two different scoped service instances were used
        scopedServiceTracker.ServiceIds.Count.ShouldBe(2);
        scopedServiceTracker.ServiceIds[0].ShouldNotBe(scopedServiceTracker.ServiceIds[1]);
    }

    #endregion

    #region Test Scenario 5: Stateful Behavior Within A Single Execution

    [Fact]
    public async Task SingleGraph_SingleExecution_BlockInstanceMaintainsState()
    {
        // Arrange
        var services = new ServiceCollection();
        var stateTracker = new StateTracker();
        
        services.AddSingleton(stateTracker);
        services.AddDataFlows("global", df =>
        {
            df.AddActorBlock<int, string, StatefulActor>("transformer");
            
            df.AddGraph("main", g => g.UseBlock("transformer"));
        });

        var serviceProvider = services.BuildServiceProvider();
        
        var producer = BlockHelpers.CreateProducer("producer", TestStreams.Range(1, 5));
        var results = new List<string>();
        var collector = BlockHelpers.CreateActor<string, object, CollectorActor<string>>(
            "collector",
            new CollectorActor<string>(results));
        
        // Get the transformer block first
        var builderTemp = GraphHelpers.CreateGraphBuilder("temp", serviceProvider);
        builderTemp.UseBlock("transformer");
        var transformer = builderTemp.Build().Blocks.First(b => b.Name == "global:transformer");
        
        var builder = GraphHelpers.CreateGraphBuilder("test", serviceProvider);
        builder.AddBlock(producer)
            .AddBlock(transformer)
            .AddBlock(collector)
            .Connect(producer, transformer)
            .Connect(transformer, collector);
        
        var graph = builder.Build();
        
        // Act
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);
        await graph.ExecuteAsync(context);
        
        // Assert - State accumulated within single execution
        results.Count.ShouldBe(5);
        stateTracker.TotalItemsProcessed.ShouldBe(5);
    }

    #endregion

    #region Helper Classes

    /// <summary>
    /// Tracks block instances created across executions.
    /// </summary>
    private class BlockInstanceTracker
    {
        public List<int> InstanceIds { get; } = new();
        
        public void RecordInstance(int instanceId)
        {
            lock (InstanceIds)
            {
                InstanceIds.Add(instanceId);
            }
        }
    }

    /// <summary>
    /// Actor that reports its instance ID to a tracker.
    /// </summary>
    private class InstanceTrackingActor : IStreamActor<int, string>
    {
        private static int _instanceCounter;
        private readonly int _instanceId;
        private readonly BlockInstanceTracker _tracker;

        public InstanceTrackingActor(BlockInstanceTracker tracker)
        {
            _instanceId = Interlocked.Increment(ref _instanceCounter);
            _tracker = tracker;
            _tracker.RecordInstance(_instanceId);
        }

        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                yield return $"Instance{_instanceId}-Item{item}";
            }
        }
    }

    /// <summary>
    /// Tracks scoped service instances.
    /// </summary>
    private class ScopedServiceTracker
    {
        public List<int> ServiceIds { get; } = new();
        
        public void RecordService(int serviceId)
        {
            lock (ServiceIds)
            {
                ServiceIds.Add(serviceId);
            }
        }
    }

    /// <summary>
    /// A scoped service used by actors.
    /// </summary>
    private class TrackedScopedService
    {
        private static int _instanceCounter;
        private readonly int _instanceId;
        
        public TrackedScopedService()
        {
            _instanceId = Interlocked.Increment(ref _instanceCounter);
        }
        
        public int InstanceId => _instanceId;
    }

    /// <summary>
    /// Actor that uses a scoped service.
    /// </summary>
    private class ScopedServiceUsingActor : IStreamActor<int, string>
    {
        private readonly TrackedScopedService _service;
        private readonly ScopedServiceTracker _tracker;

        public ScopedServiceUsingActor(
            TrackedScopedService service,
            ScopedServiceTracker tracker)
        {
            _service = service;
            _tracker = tracker;
            _tracker.RecordService(_service.InstanceId);
        }

        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                yield return $"Service{_service.InstanceId}-Item{item}";
            }
        }
    }

    /// <summary>
    /// Tracks state accumulation across a single execution.
    /// </summary>
    private class StateTracker
    {
        private int _totalItemsProcessed;
        
        public int TotalItemsProcessed => _totalItemsProcessed;
        
        public void IncrementProcessed()
        {
            Interlocked.Increment(ref _totalItemsProcessed);
        }
    }

    /// <summary>
    /// Actor that maintains state within an execution.
    /// </summary>
    private class StatefulActor : IStreamActor<int, string>
    {
        private int _processedCount;
        private readonly StateTracker _tracker;

        public StatefulActor(StateTracker tracker)
        {
            _tracker = tracker;
        }

        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                _processedCount++;
                _tracker.IncrementProcessed();
                yield return $"Count{_processedCount}-Item{item}";
            }
        }
    }

    #endregion
}
