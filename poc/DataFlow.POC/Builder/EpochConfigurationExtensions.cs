namespace DataFlow.POC.Builder;

using DataFlow.POC.Core;
using DataFlow.POC.Checkpointing;

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
    /// <param name="coordinatorFactory">Factory to create the epoch coordinator. The factory receives the configuration's checkpoint strategy.</param>
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
        
        // Create coordinator using factory or throw if not provided
        if (coordinatorFactory == null)
        {
            throw new ArgumentNullException(
                nameof(coordinatorFactory),
                "Epoch coordinator factory must be provided. Pass a factory function that creates an IEpochCoordinator.");
        }
        
        var coordinator = coordinatorFactory(config.CheckpointStrategy);
        
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
