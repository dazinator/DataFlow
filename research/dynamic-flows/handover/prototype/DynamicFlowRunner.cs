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
/// Config handling: if a block in the definition has a corresponding entry in
/// <see cref="FlowDefinition.BlockConfigRefs"/>, the runner loads the config blob
/// from <see cref="IBlockConfigBlobRepository"/> (which transparently decrypts secrets)
/// and passes the plain-text JSON to the block via <c>IConfigurableBlock.ApplyConfigJson</c>.
/// </para>
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
    private readonly IBlockConfigBlobRepository? _configBlobRepository;
    private readonly ILogger<DataFlowGraph> _graphLogger;
    private readonly ILogger<DynamicFlowRunner> _logger;

    public DynamicFlowRunner(
        IServiceProvider services,
        IBlockTypeRegistry registry,
        ILogger<DataFlowGraph> graphLogger,
        ILogger<DynamicFlowRunner> logger,
        IBlockConfigBlobRepository? configBlobRepository = null)
    {
        _services = services;
        _registry = registry;
        _graphLogger = graphLogger;
        _logger = logger;
        _configBlobRepository = configBlobRepository;
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
            var flowBuilder = new DynamicFlowBuilder(_registry);
            
            // 2. Build graph using a fresh DI scope
            using var scope = _services.CreateScope();
            var graph = flowBuilder.Build(definition, scope.ServiceProvider);

            // 3. Apply block configuration (if any)
            //    Each block in the graph that implements IConfigurableBlock receives
            //    its plain-text config JSON loaded from the blob repository.
            //    The repository is responsible for decrypting x-secret fields transparently.
            if (_configBlobRepository is not null && definition.BlockConfigRefs is { Count: > 0 } refs)
            {
                foreach (var block in graph.Blocks)
                {
                    if (refs.TryGetValue(block.Name, out var blobId))
                    {
                        var configJson = await _configBlobRepository.LoadAsync(blobId);
                        if (configJson is not null && block is IConfigurableBlock configurableBlock)
                        {
                            configurableBlock.ApplyConfigJson(configJson);
                        }
                    }
                }
            }

            // 4. Build execution context (matches existing DemoFlowRunner pattern)
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

            // 5. Execute — identical to how hardcoded flows run
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

/// <summary>
/// Interface for blocks that accept runtime configuration loaded from a config blob.
/// Implemented by blocks that want their settings to be editable from the designer UI.
/// </summary>
public interface IConfigurableBlock
{
    /// <summary>
    /// The CLR type of this block's options class.
    /// Used by the registry to look up the JSON Schema for the designer editor.
    /// </summary>
    Type OptionsType { get; }

    /// <summary>
    /// Apply plain-text JSON configuration to this block.
    /// Called by <see cref="DynamicFlowRunner"/> before execution, after decryption by the
    /// config blob repository.
    /// </summary>
    void ApplyConfigJson(string optionsJson);
}

