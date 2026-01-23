namespace DataFlow.POC.Builder;

using DataFlow.POC.Core;
using DataFlow.POC.Checkpointing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring epoch management in a dataflow graph.
/// </summary>
public static class EpochConfigurationExtensions
{
    /// <summary>
    /// Configures epoch management for the dataflow graph.
    /// Epochs provide a mechanism for coordinating lifecycle operations (e.g., transactions)
    /// across multiple data items.
    /// The epoch coordinator will be created during Build() when the service provider is available.
    /// </summary>
    /// <param name="builder">The dataflow graph builder.</param>
    /// <param name="configure">Configuration action.</param>
    /// <param name="coordinatorFactory">
    /// Optional factory to create the epoch coordinator. The factory receives the configuration's checkpoint strategy.
    /// If not provided, a default factory will be used that creates an EpochCoordinator with IServiceScopeFactory from the service provider passed to Build().
    /// </param>
    /// <returns>The builder for chaining.</returns>
    public static DataFlowGraphBuilder ConfigureEpochs(
        this DataFlowGraphBuilder builder,
        Action<EpochConfiguration> configure,
        Func<ICheckpointStrategy?, IEpochCoordinator>? coordinatorFactory = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        
        var config = new EpochConfiguration();
        configure(config);
        
        // Validate configuration
        config.Validate();
        
        // Store configuration and factory for deferred coordinator creation in Build()
        builder.SetEpochConfiguration(config, coordinatorFactory);
        
        // Create epoch source node (no longer needs coordinator)
        var sourceNode = new EpochSourceNode();
        builder.SetEpochSource(sourceNode);
        
        // Create epoch processor nodes based on configuration
        foreach (var processorName in config.Processors)
        {
            var processor = new EpochProcessorNode(sourceNode, config.Hooks);
            builder.AddEpochProcessor(processor);
        }
        
        return builder;
    }
}
