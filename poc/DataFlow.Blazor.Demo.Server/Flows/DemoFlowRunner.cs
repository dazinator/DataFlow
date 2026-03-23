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
    /// Backpressure: Producer (fast) → Consumer (slow) with a small bounded buffer.
    /// Producer emits 80 items at 20ms each (~50/sec); consumer processes at 300ms each (~3/sec).
    /// The 10-item buffer fills within seconds and stays near capacity, exercising the amber/red health indicators.
    /// </summary>
    public Guid RunBackpressure()
    {
        var invocationId = Guid.NewGuid();
        var triggerParams = Serialize(new { topology = "backpressure", itemCount = 80, bufferCapacity = 10, consumerDelayMs = 300, triggeredBy = "demo-ui" });
        _ = Task.Run(() => ExecuteBackpressureAsync(invocationId, triggerParams));
        return invocationId;
    }

    /// <summary>
    /// Failure demo: Producer → Validator (throws after 5 items).
    /// Shows how block/flow failure events surface in the visualization.
    /// </summary>
    public Guid RunFailure()
    {
        var invocationId = Guid.NewGuid();
        var triggerParams = Serialize(new { topology = "failure", itemCount = 20, failAfter = 5, triggeredBy = "demo-ui" });
        _ = Task.Run(() => ExecuteFailureAsync(invocationId, triggerParams));
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

    /// <summary>
    /// Queue-message retry demo: Poller → Enricher → Handler
    ///
    /// Simulates a message-queue consumer that encounters a transient fault on the
    /// first attempt and retries.  Both runs share the same CorrelationId so the
    /// visualization collapses them into a single "↻ 2 attempts" group.
    ///
    /// Attempt 1 — fails after processing 3 items (transient downstream fault).
    /// Attempt 2 — succeeds, processing all items normally.
    /// </summary>
    public Guid RunQueueMessage()
    {
        var correlationId = Guid.NewGuid();
        var triggerParams = Serialize(new { topology = "queue-message", message = "order-created", triggeredBy = "demo-ui" });
        _ = Task.Run(() => ExecuteQueueMessageSequenceAsync(correlationId, triggerParams));
        return correlationId;
    }

    /// <summary>
    /// Invoice Processing Pipeline — complex demo exhibiting:
    /// <list type="bullet">
    ///   <item>Routing fan-out (standard / premium / international lanes)</item>
    ///   <item>Fan-in with three bounded buffers converging on a normaliser</item>
    ///   <item>Broadcast fan-out to audit-log, notifications, and analytics sinks</item>
    ///   <item>Sustained backpressure from the slow audit-log sink cascading upstream</item>
    /// </list>
    /// 1 500 invoices at 100 ms intervals ≈ 2.5–3 min end-to-end.
    /// </summary>
    public Guid RunInvoiceProcessing()
    {
        var invocationId = Guid.NewGuid();
        var triggerParams = Serialize(new
        {
            topology = "invoice-processing",
            invoiceCount = 1500,
            lanes = new[] { "standard", "premium", "international" },
            triggeredBy = "demo-ui"
        });
        _ = Task.Run(() => ExecuteInvoiceProcessingAsync(invocationId, triggerParams));
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

    private async Task ExecuteBackpressureAsync(Guid invocationId, string? triggerParamsJson)
    {
        try
        {
            var producer = new DemoProducerBlock("producer", itemCount: 80, delayMs: 20);
            var consumer = new DemoSlowConsumerBlock("consumer", delayMs: 300);

            var graph = new DataFlowGraph("backpressure-demo", _graphLogger);
            graph.AddBlock(producer);
            graph.AddBlock(consumer);
            graph.AddEdge(new Edge(producer, consumer, BufferMode.Bounded, bufferCapacity: 10));

            using var scope = _services.CreateScope();
            var ctx = new DataFlow.POC.Core.ExecutionContext(scope.ServiceProvider, CancellationToken.None, invocationId,
                recoveryCheckpoint: null, metrics: null, triggerContext: null, triggerParamsJson: triggerParamsJson);
            await graph.ExecuteAsync(ctx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backpressure demo flow {InvocationId} failed", invocationId);
        }
    }

    private async Task ExecuteFailureAsync(Guid invocationId, string? triggerParamsJson)
    {
        try
        {
            var producer = new DemoProducerBlock("producer", itemCount: 20, delayMs: 150);
            var validator = new DemoFaultyProcessorBlock("validator", failAfter: 5, delayMs: 200);

            var graph = new DataFlowGraph("failure-demo", _graphLogger);
            graph.AddBlock(producer);
            graph.AddBlock(validator);
            graph.AddEdge(new Edge(producer, validator));

            using var scope = _services.CreateScope();
            var ctx = new DataFlow.POC.Core.ExecutionContext(scope.ServiceProvider, CancellationToken.None, invocationId,
                recoveryCheckpoint: null, metrics: null, triggerContext: null, triggerParamsJson: triggerParamsJson);
            await graph.ExecuteAsync(ctx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failure demo flow {InvocationId} failed", invocationId);
        }
    }

    private async Task ExecuteQueueMessageSequenceAsync(Guid correlationId, string triggerParamsJson)
    {
        // Attempt 1 — transient fault; the handler throws after 3 items
        await ExecuteQueueMessageAsync(Guid.NewGuid(), correlationId, attempt: 1,
            triggerParamsJson, succeeds: false);

        // Brief pause so the UI has time to show the Failed state before the retry
        await Task.Delay(800);

        // Attempt 2 — retry; the handler processes all items successfully
        await ExecuteQueueMessageAsync(Guid.NewGuid(), correlationId, attempt: 2,
            triggerParamsJson, succeeds: true);
    }

    private async Task ExecuteQueueMessageAsync(
        Guid invocationId, Guid correlationId, int attempt, string triggerParamsJson, bool succeeds)
    {
        try
        {
            var poller  = new DemoProducerBlock("poller",   itemCount: 10, delayMs: 120);
            var enricher = new DemoTransformBlock("enricher");

            var graph = new DataFlowGraph("queue-message-demo", _graphLogger);
            graph.AddBlock(poller);
            graph.AddBlock(enricher);
            graph.AddEdge(new Edge(poller, enricher));

            if (succeeds)
            {
                var handler = new DemoMessageHandlerBlock("handler", delayMs: 80);
                graph.AddBlock(handler);
                graph.AddEdge(new Edge(enricher, handler));
            }
            else
            {
                var handler = new DemoFaultyProcessorBlock("handler", failAfter: 3, delayMs: 80);
                graph.AddBlock(handler);
                graph.AddEdge(new Edge(enricher, handler));
            }

            using var scope = _services.CreateScope();
            var ctx = new DataFlow.POC.Core.ExecutionContext(
                scope.ServiceProvider, CancellationToken.None, invocationId,
                recoveryCheckpoint: null, metrics: null, triggerContext: null,
                triggerParamsJson: triggerParamsJson,
                correlationId: correlationId, attemptNumber: attempt);
            await graph.ExecuteAsync(ctx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Queue-message demo attempt {Attempt} (correlation {CorrelationId}) ended with exception",
                attempt, correlationId);
        }
    }

    private async Task ExecuteInvoiceProcessingAsync(Guid invocationId, string? triggerParamsJson)
    {
        try
        {
            // ── Blocks ──────────────────────────────────────────────────────────
            var source       = new InvoiceSourceBlock("invoice-source",       count: 1500, delayMs: 100);
            var validator    = new InvoiceValidatorBlock("invoice-validator");
            var classifier   = new InvoiceClassifierBlock("invoice-classifier");
            var stdProc      = new StandardInvoiceProcessorBlock("standard-processor");
            var premProc     = new PremiumInvoiceProcessorBlock("premium-processor");
            var intlProc     = new InternationalInvoiceProcessorBlock("international-processor");
            var normalizer   = new PaymentNormalizerBlock("payment-normalizer");
            var auditLog     = new AuditLogBlock("audit-log");
            var notifications = new NotificationBlock("notifications");
            var analytics    = new AnalyticsBlock("analytics");

            // ── Graph ───────────────────────────────────────────────────────────
            var graph = new DataFlowGraph("invoice-processing", _graphLogger);
            graph.AddBlock(source);
            graph.AddBlock(validator);
            graph.AddBlock(classifier);
            graph.AddBlock(stdProc);
            graph.AddBlock(premProc);
            graph.AddBlock(intlProc);
            graph.AddBlock(normalizer);
            graph.AddBlock(auditLog);
            graph.AddBlock(notifications);
            graph.AddBlock(analytics);

            // source → validator → classifier (unbounded — no backpressure concern here)
            graph.AddEdge(new Edge(source,     validator));
            graph.AddEdge(new Edge(validator,  classifier));

            // classifier → {standard, premium, international}   routing fan-out
            var routingStrategy = new SelectiveRoutingEdgeStrategy<ValidatedInvoice>(
                new Dictionary<string, IBlock>
                {
                    ["standard"]      = stdProc,
                    ["premium"]       = premProc,
                    ["international"] = intlProc,
                },
                vi => vi.Invoice.Type,
                BufferMode.Bounded, 80);
            graph.AddEdge(new Edge(classifier, new IBlock[] { stdProc, premProc, intlProc }, routingStrategy));

            // {standard, premium, international} → normalizer   fan-in with bounded buffers
            // Each lane has its own buffer so faster lanes (standard) don't block
            // slower ones (international) — backpressure is per-lane.
            graph.AddEdge(new Edge(stdProc,  normalizer, BufferMode.Bounded, 40));
            graph.AddEdge(new Edge(premProc, normalizer, BufferMode.Bounded, 40));
            graph.AddEdge(new Edge(intlProc, normalizer, BufferMode.Bounded, 40));

            // normalizer → {audit-log, notifications, analytics}   broadcast fan-out
            // audit-log at 130 ms is the bottleneck — fills the buffer and cascades
            // backpressure all the way back through the normalizer to the processors.
            var broadcastStrategy = new BroadcastEdgeStrategy(BufferMode.Bounded, 50);
            graph.AddEdge(new Edge(normalizer, new IBlock[] { auditLog, notifications, analytics }, broadcastStrategy));

            // ── Execute ─────────────────────────────────────────────────────────
            using var scope = _services.CreateScope();
            var ctx = new DataFlow.POC.Core.ExecutionContext(
                scope.ServiceProvider, CancellationToken.None, invocationId,
                recoveryCheckpoint: null, metrics: null, triggerContext: null,
                triggerParamsJson: triggerParamsJson);
            await graph.ExecuteAsync(ctx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invoice processing demo flow {InvocationId} failed", invocationId);
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
