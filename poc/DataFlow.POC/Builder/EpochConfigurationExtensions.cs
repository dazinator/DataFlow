namespace DataFlow.POC.Builder;

using DataFlow.POC.Core;

/// <summary>
/// Extension methods for configuring epoch management in a dataflow graph.
/// </summary>
public static class EpochConfigurationExtensions
{
    /// <summary>
    /// Configures epoch management for the dataflow graph.
    /// Epochs provide a mechanism for coordinating lifecycle operations (e.g., transactions)
    /// across multiple data items.
    /// </summary>
    /// <param name="builder">The dataflow graph builder.</param>
    /// <param name="configure">Configuration action.</param>
    /// <param name="coordinator">The epoch coordinator to use. If not provided, must be injected separately.</param>
    /// <returns>The builder for chaining.</returns>
    public static DataFlowGraphBuilder ConfigureEpochs(
        this DataFlowGraphBuilder builder,
        Action<EpochConfiguration> configure,
        IEpochCoordinator? coordinator = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        
        var config = new EpochConfiguration();
        configure(config);
        
        // Validate configuration
        config.Validate();
        
        // Coordinator must be provided (for now we require explicit coordinator)
        // In a real implementation, this might be retrieved from DI
        if (coordinator == null)
        {
            throw new ArgumentNullException(
                nameof(coordinator),
                "Epoch coordinator must be provided. Pass an IEpochCoordinator instance or configure DI.");
        }
        
        // Create epoch source node
        var sourceNode = new EpochSourceNode(coordinator);
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
