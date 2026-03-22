namespace DataFlow.Blazor.Demo.Server.Flows;

using System.Text.Json;
using DataFlow.POC.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Builds and executes the three demo DataFlow topologies on the server.
/// Each run fires-and-forgets in the background and returns a stable invocationId
/// that the Blazor client uses to subscribe to the live event stream.
/// </summary>
public sealed class DemoFlowRunner
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DemoFlowRunner> _logger;
    private readonly ILogger<DataFlowGraph> _graphLogger;

    public DemoFlowRunner(
        IServiceProvider services,
        ILogger<DemoFlowRunner> logger,
        ILogger<DataFlowGraph> graphLogger)
    {
        _services = services;
        _logger = logger;
        _graphLogger = graphLogger;
    }

    /// <summary>
    /// Linear: Producer → Transform → Batch → Processor
    /// </summary>
    public Guid RunLinear()
    {
        var invocationId = Guid.NewGuid();
        var triggerParams = Serialize(new { topology = "linear", itemCount = 50, batchSize = 5, triggeredBy = "demo-ui" });
        _ = Task.Run(() => ExecuteLinearAsync(invocationId, triggerParams));
        return invocationId;
    }

    /// <summary>
    /// Branching (fan-out): Producer → Router → [Processor-High, Processor-Low]
    /// </summary>
    public Guid RunBranching()
    {
        var invocationId = Guid.NewGuid();
        var triggerParams = Serialize(new { topology = "branching", itemCount = 40, routing = "priority", triggeredBy = "demo-ui" });
        _ = Task.Run(() => ExecuteBranchingAsync(invocationId, triggerParams));
        return invocationId;
    }

    /// <summary>
    /// Fan-in: [Producer-A, Producer-B] → Buffer → Batch → Processor
    /// </summary>
    public Guid RunFanIn()
    {
        var invocationId = Guid.NewGuid();
        var triggerParams = Serialize(new { topology = "fan-in", producerCount = 2, itemsPerProducer = 25, triggeredBy = "demo-ui" });
        _ = Task.Run(() => ExecuteFanInAsync(invocationId, triggerParams));
        return invocationId;
    }

    private static string Serialize(object value) =>
        JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = false });

    // -----------------------------------------------------------------------

    private async Task ExecuteLinearAsync(Guid invocationId, string? triggerParamsJson)
    {
        try
        {
            var producer = new DemoProducerBlock("producer", itemCount: 50, delayMs: 200);
            var transform = new DemoTransformBlock("transform");
            var batch = new DemoBatchBlock("batch", batchSize: 5);
            var processor = new DemoProcessorBlock("processor", delayMs: 60);

            var graph = new DataFlowGraph("linear-demo", _graphLogger);
            graph.AddBlock(producer);
            graph.AddBlock(transform);
            graph.AddBlock(batch);
            graph.AddBlock(processor);
            graph.AddEdge(new Edge(producer, transform));
            graph.AddEdge(new Edge(transform, batch));
            graph.AddEdge(new Edge(batch, processor));

            using var scope = _services.CreateScope();
            var ctx = new DataFlow.POC.Core.ExecutionContext(scope.ServiceProvider, CancellationToken.None, invocationId,
                recoveryCheckpoint: null, metrics: null, triggerContext: null, triggerParamsJson: triggerParamsJson);
            await graph.ExecuteAsync(ctx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Linear demo flow {InvocationId} failed", invocationId);
        }
    }

    private async Task ExecuteBranchingAsync(Guid invocationId, string? triggerParamsJson)
    {
        try
        {
            var producer = new DemoProducerBlock("producer", itemCount: 40, delayMs: 200);
            var router = new DemoRouterBlock("router");
            var procHigh = new DemoPriorityProcessorBlock("processor-high", "high", delayMs: 60);
            var procLow = new DemoPriorityProcessorBlock("processor-low", "low", delayMs: 60);

            var graph = new DataFlowGraph("branching-demo", _graphLogger);
            graph.AddBlock(producer);
            graph.AddBlock(router);
            graph.AddBlock(procHigh);
            graph.AddBlock(procLow);
            graph.AddEdge(new Edge(producer, router));
            // Route items to the matching processor based on the Priority tag.
            // Each processor receives only its own items (~20 high, ~20 low).
            var routeStrategy = new SelectiveRoutingEdgeStrategy<(int Value, string Priority)>(
                new Dictionary<string, IBlock> { ["high"] = procHigh, ["low"] = procLow },
                item => item.Priority,
                BufferMode.Bounded,
                100);
            graph.AddEdge(new Edge(router, new[] { procHigh, procLow }, routeStrategy));

            using var scope = _services.CreateScope();
            var ctx = new DataFlow.POC.Core.ExecutionContext(scope.ServiceProvider, CancellationToken.None, invocationId,
                recoveryCheckpoint: null, metrics: null, triggerContext: null, triggerParamsJson: triggerParamsJson);
            await graph.ExecuteAsync(ctx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Branching demo flow {InvocationId} failed", invocationId);
        }
    }

    private async Task ExecuteFanInAsync(Guid invocationId, string? triggerParamsJson)
    {
        try
        {
            var producerA = new DemoProducerBlock("producer-a", itemCount: 25, delayMs: 270);
            var producerB = new DemoProducerBlock("producer-b", itemCount: 25, delayMs: 300);
            var buffer = new DemoBufferBlock("buffer");
            var batch = new DemoBatchBlock("batch", batchSize: 5);
            var processor = new DemoProcessorBlock("processor", delayMs: 60);

            var graph = new DataFlowGraph("fanin-demo", _graphLogger);
            graph.AddBlock(producerA);
            graph.AddBlock(producerB);
            graph.AddBlock(buffer);
            graph.AddBlock(batch);
            graph.AddBlock(processor);
            // Both producers feed the buffer (fan-in via multiple edges)
            graph.AddEdge(new Edge(producerA, buffer));
            graph.AddEdge(new Edge(producerB, buffer));
            graph.AddEdge(new Edge(buffer, batch));
            graph.AddEdge(new Edge(batch, processor));

            using var scope = _services.CreateScope();
            var ctx = new DataFlow.POC.Core.ExecutionContext(scope.ServiceProvider, CancellationToken.None, invocationId,
                recoveryCheckpoint: null, metrics: null, triggerContext: null, triggerParamsJson: triggerParamsJson);
            await graph.ExecuteAsync(ctx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fan-in demo flow {InvocationId} failed", invocationId);
        }
    }
}
