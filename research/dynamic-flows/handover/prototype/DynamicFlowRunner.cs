namespace DataFlow.DynamicFlows.Prototype;

using DataFlow.POC.Core;
using DataFlow.POC.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// ---------------------------------------------------------------------------
// DynamicFlowRunner
// Demonstrates executing a flow from a JSON definition end-to-end.
// This is a reference implementation / demo — the real implementation will
// integrate with the existing DemoFlowRunner pattern.
// ---------------------------------------------------------------------------

/// <summary>
/// Demonstrates how to execute a DataFlow pipeline from a <see cref="FlowDefinition"/>.
///
/// <para>
/// Usage:
/// <code>
/// var runner = new DynamicFlowRunner(serviceProvider, registry, graphLogger);
/// var invocationId = runner.Run(definition);
/// // subscribe to events via existing SignalR / catch-up mechanism
/// </code>
/// </para>
/// </summary>
public sealed class DynamicFlowRunner
{
    private readonly IServiceProvider _services;
    private readonly IBlockTypeRegistry _registry;
    private readonly ILogger<DataFlowGraph> _graphLogger;
    private readonly ILogger<DynamicFlowRunner> _logger;

    public DynamicFlowRunner(
        IServiceProvider services,
        IBlockTypeRegistry registry,
        ILogger<DataFlowGraph> graphLogger,
        ILogger<DynamicFlowRunner> logger)
    {
        _services = services;
        _registry = registry;
        _graphLogger = graphLogger;
        _logger = logger;
    }

    /// <summary>
    /// Runs a flow from the given definition.  Fire-and-forget; returns the invocation ID.
    /// </summary>
    public Guid Run(FlowDefinition definition)
    {
        var invocationId = Guid.NewGuid();
        _ = Task.Run(() => ExecuteAsync(definition, invocationId));
        return invocationId;
    }

    private async Task ExecuteAsync(FlowDefinition definition, Guid invocationId)
    {
        try
        {
            // 1. Validate definition (will throw if invalid)
            var builder = new DynamicFlowBuilder(_registry);
            
            // 2. Build graph using a fresh DI scope
            using var scope = _services.CreateScope();
            var graph = builder.Build(definition, scope.ServiceProvider);

            // 3. Build execution context (matches existing DemoFlowRunner pattern)
            var ctx = new POC.Core.ExecutionContext(
                scope.ServiceProvider,
                CancellationToken.None,
                invocationId,
                recoveryCheckpoint: null,
                metrics: null,
                triggerContext: null,
                triggerParamsJson: System.Text.Json.JsonSerializer.Serialize(new
                {
                    flowId = definition.FlowId,
                    version = definition.Version,
                    source = "dynamic-flow-runner"
                }));

            // 4. Execute — identical to how hardcoded flows run
            await graph.ExecuteAsync(ctx);
        }
        catch (InvalidOperationException ex)
        {
            // Build / validation error — log clearly; visualization will show FlowFailed
            _logger.LogError(ex, "Dynamic flow '{FlowId}' (invocation {InvocationId}) could not be built: {Error}",
                definition.FlowId, invocationId, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dynamic flow '{FlowId}' (invocation {InvocationId}) failed during execution",
                definition.FlowId, invocationId);
        }
    }
}
