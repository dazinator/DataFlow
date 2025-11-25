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
    /// </summary>
    /// <param name="builder">The dataflow graph builder.</param>
    /// <param name="configure">Configuration action.</param>
    /// <param name="coordinatorFactory">
    /// Optional factory to create the epoch coordinator. The factory receives the configuration's checkpoint strategy.
    /// If not provided, a default EpochCoordinator will be created using IServiceScopeFactory from the service provider.
    /// </param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when coordinatorFactory is null and the builder was not constructed with a service provider.
    /// </exception>
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
        
        // Create coordinator using factory or default implementation
        if (coordinatorFactory == null)
        {
            // Try to get service provider for default factory
            var serviceProvider = builder.GetServiceProvider();
            if (serviceProvider == null)
            {
                throw new InvalidOperationException(
                    "ConfigureEpochs requires either a service provider in the builder constructor " +
                    "or an explicit coordinatorFactory parameter.");
            }
            
            // Create default factory using IServiceScopeFactory from DI
            coordinatorFactory = checkpointStrategy =>
            {
                var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
                return new EpochCoordinator(scopeFactory, checkpointStrategy: checkpointStrategy);
            };
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
